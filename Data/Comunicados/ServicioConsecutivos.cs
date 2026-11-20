using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Comunicados;

// Consecutivos de comunicados de salida (Comunicados › Consecutivos y /consecutivo en Telegram).
// - Reservar: siguiente número de la plantilla = mayor entre los Comunicados ya listados con ese prefijo y las
//   reservas (todas, también anuladas: un número no se reutiliza) + 1. Año = el de la fecha de envío prevista.
//   Serializado en el proceso (SemaphoreSlim) y protegido por índices únicos (Codigo; plantilla+año+número).
// - Confirmar envío: crea el Comunicado de salida con los datos de la reserva (o liga el que ya exista con ese
//   consecutivo, p. ej. importado del Excel) y, si responde a otro comunicado, le anota la respuesta.
// - Anular: el número queda anulado (trazabilidad), sin Comunicado.
// - Conciliar: reservas sin confirmar cuyo consecutivo ya aparece en el listado → Enviado y ligadas.
// Todos los métodos reciben el DbContext (cada llamador usa el suyo o un scope propio).
public static class ServicioConsecutivos
{
    private static readonly SemaphoreSlim Candado = new(1, 1);

    public sealed record DatosReserva(
        int PlantillaId, DateTime FechaEnvioPrevista, string Asunto, string? DirigidoA, string? Tipo,
        string? RespondeA, bool? RequiereRespuesta, string? ResponsableRespuesta, string? Observaciones,
        string SolicitadoPor, string Origen, long? ChatIdTelegram = null);

    public static int Anio2(DateTime fecha) => fecha.Year % 100;

    public static string Codigo(PlantillaConsecutivo p, int numero, int anio)
    {
        var n = numero.ToString(new string('0', Math.Clamp(p.CifrasNumero, 1, 8)));
        return p.Prefijo.Length == 0 ? $"{n}-{anio:00}" : $"{p.Prefijo}-{n}-{anio:00}";
    }

    private static Regex RxPrefijo(PlantillaConsecutivo p) =>
        new("^" + (p.Prefijo.Length == 0 ? "" : Regex.Escape(Norm(p.Prefijo)) + "-") + @"(\d+)-(\d{2})$", RegexOptions.IgnoreCase);

    // Sin espacios alrededor de los guiones y en mayúsculas (los códigos del Excel a veces traen "CONTRATISTA - INTERVENTORIA").
    public static string Norm(string? s) => Regex.Replace((s ?? "").Trim().ToUpperInvariant(), @"\s*-\s*", "-");

    public static async Task<List<PlantillaConsecutivo>> PlantillasAsync(AppDbContext db, bool soloActivas = true) =>
        await db.PlantillasConsecutivo.AsNoTracking().Where(p => !soloActivas || p.Activa).OrderBy(p => p.Orden).ThenBy(p => p.Id).ToListAsync();

    // Propone una plantilla a partir de los consecutivos de salida ya listados (el prefijo más usado). No guarda.
    public static async Task<PlantillaConsecutivo?> SugerirPlantillaAsync(AppDbContext db)
    {
        var codigos = await db.Comunicados.AsNoTracking().Where(c => c.Sentido == "Salida").Select(c => c.Consecutivo).ToListAsync();
        var rx = new Regex(@"^([A-Z0-9]+)-([A-Z0-9]+)-([A-Z0-9]+)-([A-Z0-9]+)-\d+-\d{2}$");
        var m = codigos.Select(c => rx.Match(Norm(c))).Where(x => x.Success)
            .GroupBy(x => (x.Groups[1].Value, x.Groups[2].Value, x.Groups[3].Value, x.Groups[4].Value))
            .OrderByDescending(g => g.Count()).FirstOrDefault();
        return m is null ? null : new PlantillaConsecutivo { Remite = m.Key.Item1, Destinatario = m.Key.Item2, Proyecto = m.Key.Item3, CodigoProyecto = m.Key.Item4, CifrasNumero = 4 };
    }

    // Siguiente número libre (sin reservarlo). Cuenta lo ya listado y todas las reservas.
    public static async Task<int> SiguienteNumeroAsync(AppDbContext db, PlantillaConsecutivo p, int anio)
    {
        var rx = RxPrefijo(p);
        var prefijo = p.Remite.Trim();
        var listados = (await db.Comunicados.IgnoreQueryFilters().AsNoTracking()
                .Where(c => EF.Functions.ILike(c.Consecutivo, prefijo + "%")).Select(c => c.Consecutivo).ToListAsync())
            .Select(c => rx.Match(Norm(c))).Where(m => m.Success)
            .Where(m => !p.ReiniciaCadaAnio || int.Parse(m.Groups[2].Value) == anio)
            .Select(m => int.Parse(m.Groups[1].Value));
        var reservas = await db.ReservasConsecutivo.AsNoTracking()
            .Where(r => r.PlantillaConsecutivoId == p.Id && (!p.ReiniciaCadaAnio || r.Anio == anio)).Select(r => r.Numero).ToListAsync();
        var siguiente = listados.Concat(reservas).DefaultIfEmpty(0).Max() + 1;
        // "Iniciar en" de la plantilla: piso del siguiente número (con reinicio anual, solo el año en que se fijó).
        if (p.NumeroInicial is int ini && ini > 0 && (!p.ReiniciaCadaAnio || p.AnioNumeroInicial == anio))
            siguiente = Math.Max(siguiente, ini);
        return siguiente;
    }

    public static async Task<ReservaConsecutivo> ReservarAsync(AppDbContext db, DatosReserva d)
    {
        if (string.IsNullOrWhiteSpace(d.Asunto)) throw new InvalidOperationException("Falta el asunto.");
        var p = await db.PlantillasConsecutivo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == d.PlantillaId && x.Activa)
            ?? throw new InvalidOperationException("La plantilla de consecutivo no existe o está inactiva.");
        await Candado.WaitAsync();
        try
        {
            for (var intento = 0; ; intento++)
            {
                var anio = Anio2(d.FechaEnvioPrevista);
                var numero = await SiguienteNumeroAsync(db, p, anio);
                var r = new ReservaConsecutivo
                {
                    PlantillaConsecutivoId = p.Id, Numero = numero, Anio = anio, Codigo = Codigo(p, numero, anio),
                    SolicitadoPor = d.SolicitadoPor.Trim(), Origen = d.Origen, ChatIdTelegram = d.ChatIdTelegram, FechaSolicitud = DateTime.Now,
                    FechaEnvioPrevista = d.FechaEnvioPrevista.Date, Asunto = d.Asunto.Trim(), DirigidoA = Limpio(d.DirigidoA), Tipo = Limpio(d.Tipo) ?? "Externa",
                    RespondeA = Limpio(d.RespondeA) is { } ra ? Norm(ra) : null, RequiereRespuesta = d.RequiereRespuesta,
                    ResponsableRespuesta = Limpio(d.ResponsableRespuesta), Observaciones = Limpio(d.Observaciones),
                };
                db.ReservasConsecutivo.Add(r);
                try { await db.SaveChangesAsync(); return r; }
                catch (DbUpdateException) when (intento < 4)
                {
                    // Otro proceso tomó el mismo número (índice único): se recalcula.
                    db.Entry(r).State = EntityState.Detached;
                }
            }
        }
        finally { Candado.Release(); }
    }

    public static async Task<Comunicado> ConfirmarEnvioAsync(AppDbContext db, int reservaId, DateTime fechaEnvio, string quien)
    {
        var r = await db.ReservasConsecutivo.FirstOrDefaultAsync(x => x.Id == reservaId)
            ?? throw new InvalidOperationException("La reserva no existe.");
        if (r.Estado == EstadoConsecutivo.Anulado) throw new InvalidOperationException($"{r.Codigo} está anulado.");
        var p = await db.PlantillasConsecutivo.AsNoTracking().FirstAsync(x => x.Id == r.PlantillaConsecutivoId);

        var clave = Norm(r.Codigo);
        var com = r.ComunicadoId is int cid ? await db.Comunicados.FirstOrDefaultAsync(c => c.Id == cid) : null;
        com ??= (await db.Comunicados.Where(c => EF.Functions.ILike(c.Consecutivo, p.Remite.Trim() + "%")).ToListAsync())
            .FirstOrDefault(c => Norm(c.Consecutivo) == clave);
        if (com is null)
        {
            com = new Comunicado
            {
                Tipo = r.Tipo ?? "Externa", Remitente = p.Remite, Sentido = "Salida", Consecutivo = r.Codigo, Fecha = fechaEnvio.Date,
                Descripcion = r.Asunto, RequiereRespuesta = r.RequiereRespuesta, ResponsableRespuesta = r.ResponsableRespuesta,
                Observaciones = string.Join(" · ", new[] { r.DirigidoA is { } da ? $"Dirigido a: {da}" : null, r.Observaciones,
                    $"Consecutivo entregado a {r.SolicitadoPor} ({r.Origen}, {r.FechaSolicitud:dd/MM/yyyy})" }.Where(x => !string.IsNullOrWhiteSpace(x))),
                FechaRegistro = DateTime.Now,
            };
            db.Comunicados.Add(com);
        }
        else
        {
            // Ya estaba en el listado (importado o escrito a mano): se completa sin pisar lo que tenga.
            com.Fecha ??= fechaEnvio.Date;
            if (string.IsNullOrWhiteSpace(com.Descripcion)) com.Descripcion = r.Asunto;
            com.RequiereRespuesta ??= r.RequiereRespuesta;
            if (string.IsNullOrWhiteSpace(com.ResponsableRespuesta)) com.ResponsableRespuesta = r.ResponsableRespuesta;
        }

        // Responde a otro comunicado: a ese se le anota esta respuesta (si no tenía otra).
        if (r.RespondeA is { Length: > 0 } ra)
        {
            var claveRa = Norm(ra);
            var original = (await db.Comunicados.ToListAsync()).FirstOrDefault(c => Norm(c.Consecutivo) == claveRa);
            if (original is not null && string.IsNullOrWhiteSpace(original.ConsecutivoRespuesta))
            {
                original.ConsecutivoRespuesta = r.Codigo;
                original.FechaRespuesta ??= fechaEnvio.Date;
            }
        }

        await db.SaveChangesAsync();
        r.Estado = EstadoConsecutivo.Enviado; r.FechaEnvio = fechaEnvio.Date; r.ComunicadoId = com.Id;
        r.CerradoPor = quien; r.FechaCierre = DateTime.Now;
        await db.SaveChangesAsync();
        return com;
    }

    public static async Task AnularAsync(AppDbContext db, int reservaId, string motivo, string quien)
    {
        var r = await db.ReservasConsecutivo.FirstOrDefaultAsync(x => x.Id == reservaId)
            ?? throw new InvalidOperationException("La reserva no existe.");
        if (r.Estado == EstadoConsecutivo.Enviado) throw new InvalidOperationException($"{r.Codigo} ya se envió: no se puede anular aquí (está en el listado de Comunicados).");
        if (string.IsNullOrWhiteSpace(motivo)) throw new InvalidOperationException("Escribe el motivo de la anulación.");
        r.Estado = EstadoConsecutivo.Anulado; r.MotivoAnulacion = motivo.Trim(); r.CerradoPor = quien; r.FechaCierre = DateTime.Now;
        await db.SaveChangesAsync();
    }

    // Reservas sin confirmar cuyo consecutivo ya está en el listado (p. ej. tras importar el Excel): pasan a Enviado.
    public static async Task<int> ConciliarAsync(AppDbContext db)
    {
        var pendientes = await db.ReservasConsecutivo.Where(r => r.Estado == EstadoConsecutivo.Reservado).ToListAsync();
        if (pendientes.Count == 0) return 0;
        var codigos = pendientes.Select(r => Norm(r.Codigo)).ToHashSet();
        var remites = await db.PlantillasConsecutivo.AsNoTracking().Select(p => p.Remite).Distinct().ToListAsync();
        var listados = new List<Comunicado>();
        foreach (var rem in remites)
            listados.AddRange(await db.Comunicados.AsNoTracking().Where(c => EF.Functions.ILike(c.Consecutivo, rem.Trim() + "%")).ToListAsync());
        var porCodigo = listados.GroupBy(c => Norm(c.Consecutivo)).Where(g => codigos.Contains(g.Key)).ToDictionary(g => g.Key, g => g.First());
        var n = 0;
        foreach (var r in pendientes)
            if (porCodigo.TryGetValue(Norm(r.Codigo), out var c))
            {
                r.Estado = EstadoConsecutivo.Enviado; r.ComunicadoId = c.Id; r.FechaEnvio = c.Fecha ?? r.FechaEnvioPrevista;
                r.CerradoPor = "Conciliación con el listado"; r.FechaCierre = DateTime.Now; n++;
            }
        if (n > 0) await db.SaveChangesAsync();
        return n;
    }

    // Comunicados de entrada pendientes de respuesta que coinciden con el texto (para "¿responde a…?").
    public static async Task<List<Comunicado>> BuscarPorResponderAsync(AppDbContext db, string texto, int max = 8)
    {
        var t = (texto ?? "").Trim();
        if (t.Length < 2) return new();
        var pat = $"%{t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        return await db.Comunicados.AsNoTracking()
            .Where(c => EF.Functions.ILike(c.Consecutivo, pat) || (c.Descripcion != null && EF.Functions.ILike(c.Descripcion, pat)))
            .OrderBy(c => c.ConsecutivoRespuesta != null && c.ConsecutivoRespuesta != "")   // primero las sin respuesta
            .ThenByDescending(c => c.Fecha).Take(max).ToListAsync();
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
