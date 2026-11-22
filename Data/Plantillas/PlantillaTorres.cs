using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Trazado;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

// Adaptador de Torres del trazado (ElementoTrazado). Identidad = Código exacto (sin espacios extremos).
// El tiro se referencia por nombre (no por Id) y debe existir o venir en la hoja Tiros del mismo libro.
// Las torres dadas de alta o revividas reciben su ítem de catálogo "Localización" y su componente "Hoja de vida".
public class PlantillaTorres : IPlantillaModulo
{
    private const string Hoja = "Torres";

    public string Clave => "torres";
    public string Nombre => "Torres";
    public int Orden => 30;                                    // después de Catálogos (10) y Tiros (20)

    public IReadOnlyList<HojaPlantilla> Hojas { get; } = new[]
    {
        new HojaPlantilla(Hoja,
            "Torres del trazado. El código identifica la torre; el tiro se referencia por su nombre.",
            new[]
            {
                new ColumnaPlantilla("codigo", "Código", Obligatoria: true, Ayuda: "Código de la torre en el trazado (ej. T-15); identifica la fila."),
                new ColumnaPlantilla("nombre", "Nombre", Ayuda: "Nombre visible de la torre."),
                new ColumnaPlantilla("latitud", "Latitud", TipoColumna.Decimal, Ayuda: "Entre -90 y 90. Obligatoria en una torre nueva."),
                new ColumnaPlantilla("longitud", "Longitud", TipoColumna.Decimal, Ayuda: "Entre -180 y 180. Obligatoria en una torre nueva."),
                new ColumnaPlantilla("tiro", "Tiro", TipoColumna.Texto, Ayuda: "Nombre del tiro; debe existir o venir en la hoja Tiros."),
            }),
    };

    private sealed record DatosTorre(int? Id, string Codigo, string? Nombre, double? Latitud, double? Longitud, string? Tiro, bool Revivida);

    public async Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct)
    {
        if (!string.Equals(hoja, Hoja, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<Dictionary<string, object?>>();

        var tiros = (await db.Tiros.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct))
            .GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().Nombre);
        return (await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Codigo).ToListAsync(ct))
            .Select(t => new Dictionary<string, object?>
            {
                ["codigo"] = t.Codigo,
                ["nombre"] = t.Nombre,
                ["latitud"] = t.Latitud,
                ["longitud"] = t.Longitud,
                ["tiro"] = t.TiroId is int id && tiros.TryGetValue(id, out var n) ? n : null,
            }).ToList();
    }

    public async Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct)
    {
        var plan = new PlanModulo { Clave = Clave, Nombre = Nombre };
        if (!hojas.TryGetValue(Hoja, out var filas)) return plan;

        // IgnoreQueryFilters: una torre en la Papelera con el mismo código se revive, no se duplica.
        var existentes = new Dictionary<string, ElementoTrazado>(StringComparer.Ordinal);
        foreach (var t in (await ctx.Db.ElementosTrazado.AsNoTracking().IgnoreQueryFilters().ToListAsync(ct)).OrderBy(t => t.Eliminado))
            existentes.TryAdd(t.Codigo.Trim(), t);

        // Tiro no tiene filtro de Papelera: uno eliminado solo cuenta si la hoja Tiros de este libro lo revive.
        var listaTiros = await ctx.Db.Tiros.AsNoTracking().ToListAsync(ct);
        var tirosPorNombre = listaTiros
            .GroupBy(t => t.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Eliminado).First(), StringComparer.OrdinalIgnoreCase);
        var tirosPorId = listaTiros.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().Nombre);

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in filas)
        {
            var codigo = f.Texto("codigo")!;
            if (!vistos.Add(codigo))
            {
                plan.Errores.Add(new(Hoja, f.NumeroFila, "Código", $"«{codigo}» está repetido en la hoja."));
                continue;
            }

            var nombre = f.Texto("nombre");
            var latitud = f.Decimal("latitud");
            var longitud = f.Decimal("longitud");
            var tiro = f.Texto("tiro");

            var ok = true;
            if (latitud is double la && (la < -90 || la > 90))
            {
                plan.Errores.Add(new(Hoja, f.NumeroFila, "Latitud", "Debe estar entre -90 y 90."));
                ok = false;
            }
            if (longitud is double lo && (lo < -180 || lo > 180))
            {
                plan.Errores.Add(new(Hoja, f.NumeroFila, "Longitud", "Debe estar entre -180 y 180."));
                ok = false;
            }
            if (tiro is not null && (!tirosPorNombre.TryGetValue(tiro, out var tiroBd) || tiroBd.Eliminado) && !ctx.SeCrea("Tiro", tiro) && !ctx.SeCrea("TiroRevivido", tiro))
            {
                plan.Errores.Add(new(Hoja, f.NumeroFila, "Tiro", $"«{tiro}» no existe; agréguelo en la hoja Tiros."));
                ok = false;
            }
            if (!ok) continue;

            if (existentes.TryGetValue(codigo, out var t))
            {
                var revivida = t.Eliminado;
                var cambios = new List<string>();
                if (revivida) cambios.Add("Estado: En papelera → Activa");
                if (nombre is not null && !string.Equals(nombre, t.Nombre, StringComparison.Ordinal))
                    cambios.Add($"Nombre: {t.Nombre} → {nombre}");
                if (latitud is double nla && Math.Abs(nla - t.Latitud) > 1e-9)
                    cambios.Add($"Latitud: {t.Latitud} → {nla}");
                if (longitud is double nlo && Math.Abs(nlo - t.Longitud) > 1e-9)
                    cambios.Add($"Longitud: {t.Longitud} → {nlo}");
                if (tiro is not null)
                {
                    if (!tirosPorNombre.TryGetValue(tiro, out var destino) || t.TiroId != destino.Id)
                    {
                        var tiroActual = t.TiroId is int actual && tirosPorId.TryGetValue(actual, out var tn) ? tn : null;
                        cambios.Add($"Tiro: {tiroActual ?? "—"} → {tiro}");
                    }
                }

                var pf = new PlanFila
                {
                    Hoja = Hoja, NumeroFila = f.NumeroFila, Codigo = codigo,
                    Operacion = cambios.Count > 0 ? OperacionFila.Actualizacion : OperacionFila.SinCambios,
                    Datos = new DatosTorre(t.Id, codigo, nombre, latitud, longitud, tiro, revivida),
                };
                pf.Cambios.AddRange(cambios);
                plan.Filas.Add(pf);
            }
            else
            {
                if (latitud is null)
                {
                    plan.Errores.Add(new(Hoja, f.NumeroFila, "Latitud", "Obligatoria en una torre nueva."));
                    ok = false;
                }
                if (longitud is null)
                {
                    plan.Errores.Add(new(Hoja, f.NumeroFila, "Longitud", "Obligatoria en una torre nueva."));
                    ok = false;
                }
                if (!ok) continue;

                plan.Filas.Add(new PlanFila
                {
                    Hoja = Hoja, NumeroFila = f.NumeroFila, Codigo = codigo, Operacion = OperacionFila.Alta,
                    Datos = new DatosTorre(null, codigo, nombre, latitud, longitud, tiro, false),
                });
            }
        }

        return plan;
    }

    public async Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var tirosPorNombre = (await ctx.Db.Tiros.AsNoTracking().ToListAsync(ct))
            .GroupBy(t => t.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Eliminado).First().Id, StringComparer.OrdinalIgnoreCase);

        // Una sola capa por carga, solo si hay altas: deja identificable de dónde salieron las torres.
        CapaTrazado? capa = null;
        if (plan.Filas.Any(f => f.Operacion == OperacionFila.Alta))
        {
            var ahora = DateTime.Now;
            capa = new CapaTrazado
            {
                Nombre = "Plantilla de proyecto " + ahora.ToString("dd/MM/yyyy HH:mm"),
                Origen = "Plantilla",
                FechaCarga = ahora,
            };
            ctx.Db.Capas.Add(capa);
            ctx.Alta(capa);
            // Las torres necesitan el Id real de la capa para su clave foránea.
            await ctx.Db.SaveChangesAsync(ct);
        }

        int TiroId(string? nombre)
        {
            if (nombre is null) return 0;
            if (tirosPorNombre.TryGetValue(nombre, out var id)) return id;
            throw new InvalidOperationException($"No se encontró el tiro «{nombre}» al aplicar las torres.");
        }

        foreach (var f in plan.Filas)
        {
            var d = (DatosTorre)f.Datos!;
            if (f.Operacion == OperacionFila.Alta)
            {
                var t = new ElementoTrazado
                {
                    Codigo = d.Codigo,
                    Nombre = d.Nombre,
                    Latitud = d.Latitud!.Value,
                    Longitud = d.Longitud!.Value,
                    CapaTrazadoId = capa!.Id,
                    TiroId = d.Tiro is null ? null : TiroId(d.Tiro),
                };
                ctx.Db.ElementosTrazado.Add(t);
                ctx.Alta(t);
            }
            else if (f.Operacion == OperacionFila.Actualizacion)
            {
                var t = await ctx.Db.ElementosTrazado.IgnoreQueryFilters().FirstAsync(x => x.Id == d.Id, ct);
                ctx.AntesDeActualizar(t);
                if (d.Revivida)
                {
                    t.Eliminado = false;
                    t.FechaEliminacion = null;
                }
                if (d.Nombre is not null) t.Nombre = d.Nombre;
                if (d.Latitud is double la) t.Latitud = la;
                if (d.Longitud is double lo) t.Longitud = lo;
                if (d.Tiro is not null) t.TiroId = TiroId(d.Tiro);
            }
        }
    }

    // Efectos secundarios de una torre nueva o revivida: ítem de catálogo "Localización" y componente "Hoja de vida".
    // Sincronizar guarda y limpia el ChangeTracker: la torre se vuelve a leer por Id antes de cada uso.
    public async Task DespuesDeGuardarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var ids = new List<int>();
        foreach (var f in plan.Filas)
        {
            var d = (DatosTorre)f.Datos!;
            if (f.Operacion == OperacionFila.Alta)
            {
                var creada = await ctx.Db.ElementosTrazado.AsNoTracking().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Codigo == d.Codigo, ct);
                if (creada is not null) ids.Add(creada.Id);
            }
            else if (f.Operacion == OperacionFila.Actualizacion && d.Revivida && d.Id is int id)
            {
                ids.Add(id);
            }
        }

        foreach (var id in ids)
        {
            var torre = await ctx.Db.ElementosTrazado.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id, ct);
            if (torre is null) continue;

            var nombre = string.IsNullOrWhiteSpace(torre.Nombre) ? torre.Codigo : torre.Nombre!;

            // ¿Sincronizar va a crear un ítem nuevo o a adoptar uno manual existente?
            var yaExisteItem = await ctx.Db.Catalogos.AsNoTracking().AnyAsync(c => c.ElementoTrazadoId == id, ct)
                || await ctx.Db.Catalogos.AsNoTracking().AnyAsync(c =>
                    c.Tipo == "Localización" && c.ElementoTrazadoId == null && c.Nombre.ToLower() == nombre.ToLower(), ct);

            SincronizadorCatalogoLocalizacion.Sincronizar(ctx.Db, torre);

            if (!yaExisteItem)
            {
                var item = await ctx.Db.Catalogos.AsNoTracking().FirstOrDefaultAsync(c => c.ElementoTrazadoId == id, ct);
                if (item is not null) ctx.Alta(item);
            }

            // Componente no tiene filtro de Papelera: una hoja de vida eliminada (p. ej. al deshacer una carga) se revive.
            var hojas = await ctx.Db.Componentes.Where(c => c.ElementoTrazadoId == id && c.EsHojaDeVida).ToListAsync(ct);
            var eliminada = hojas.FirstOrDefault(c => c.Eliminado);
            if (hojas.Any(c => !c.Eliminado)) { }
            else if (eliminada is not null)
            {
                ctx.AntesDeActualizar(eliminada);
                eliminada.Eliminado = false;
                eliminada.FechaEliminacion = null;
            }
            else
            {
                var componente = new Componente
                {
                    ElementoTrazadoId = id,
                    Nombre = "Hoja de vida",
                    EsPredeterminado = true,
                    EsHojaDeVida = true,
                    Orden = -1,
                };
                ctx.Db.Componentes.Add(componente);
                ctx.Alta(componente);
            }
        }
    }

    // Deshacer una torre nueva: su ítem de Localización se desactiva (no se borra, como en el Trazado).
    public Task AlDeshacerAsync(IReadOnlyList<CambioCargaProyecto> cambios, AppDbContext db, CancellationToken ct)
    {
        foreach (var c in cambios.Where(c => c.Entidad == nameof(ElementoTrazado) && c.Operacion == OperacionCambioCarga.Alta))
            SincronizadorCatalogoLocalizacion.MarcarInactivo(db, c.EntidadId);
        return Task.CompletedTask;
    }
}
