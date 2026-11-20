using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.PartesInteresadas;

// Cuadrante de la matriz de stakeholders (hoja "Parámetros" del Excel): influencia o poder × grado de interés.
public enum CuadrantePartes { SinValorar, MinimoEsfuerzo, MantenerInformado, MantenerSatisfecho, GestionarDeCerca }

public enum EstadoSeguimientoParte { SinProgramar, AlDia, PorVencer, Vencido, Inactiva }

public enum TipoCambioParte { Agregada, Eliminada, Campo }

public sealed record CambioParte(TipoCambioParte Tipo, Guid Clave, string Parte, string Campo, string? Antes, string? Despues);

public sealed class DatosPublicacionPartes
{
    public DateTime? VigenteDesde { get; set; } = DateTime.Today;
    public string? Codigo { get; set; }
    public string? Motivo { get; set; }
    public string? Elaboro { get; set; }
    public string? Reviso { get; set; }
    public string? Aprobo { get; set; }
    public DateTime? FechaAprobacion { get; set; } = DateTime.Today;
}

// Situación de seguimiento de una parte (precalculada; nunca se consulta la base desde el render).
public sealed class SituacionParte
{
    public ProgramacionSeguimientoParte? Programacion { get; init; }
    public SeguimientoParteInteresada? Ultimo { get; init; }
    public DateTime? Proximo { get; init; }
    public EstadoSeguimientoParte Estado { get; init; }
    public int Seguimientos { get; init; }
}

public static class ServicioPartesInteresadas
{
    // ---------- Escala (hoja "Parámetros") ----------
    // ALTO = "de 5,1 a 10", BAJO = "de 0 a 4,9". El 5,0 no está en ninguno de los dos rangos; el gráfico del
    // Excel traza la línea en MAX/2 = 5. Supuesto: hasta 5 es BAJO, más de 5 es ALTO.
    public const decimal Umbral = 5m;
    public const decimal Maximo = 10m;
    public const int DiasPorVencer = 7;

    public static bool Alto(decimal v) => v > Umbral;

    // Supuesto: IMP (impacto) hace de "grado de interés" de la hoja Parámetros e INF de "influencia o poder".
    public static CuadrantePartes Cuadrante(decimal? impacto, decimal? influencia)
    {
        if (impacto is not decimal i || influencia is not decimal f) return CuadrantePartes.SinValorar;
        return (Alto(f), Alto(i)) switch
        {
            (true, true) => CuadrantePartes.GestionarDeCerca,
            (true, false) => CuadrantePartes.MantenerSatisfecho,
            (false, true) => CuadrantePartes.MantenerInformado,
            _ => CuadrantePartes.MinimoEsfuerzo
        };
    }

    // Nombres y descripciones tal como vienen en la hoja "Parámetros" (ortografía corregida solo en el nombre corto).
    public static string Nombre(CuadrantePartes c) => c switch
    {
        CuadrantePartes.GestionarDeCerca => "Atender sus requisitos y manejar sus expectativas",
        CuadrantePartes.MantenerSatisfecho => "Mantener satisfecho",
        CuadrantePartes.MantenerInformado => "Mantenerlo informado",
        CuadrantePartes.MinimoEsfuerzo => "Mínimo esfuerzo",
        _ => "Sin valorar"
    };

    public static string Descripcion(CuadrantePartes c) => c switch
    {
        CuadrantePartes.GestionarDeCerca => "Se consideran dominantes, por lo que deben ser considerados al momento de definir las estrategias.",
        CuadrantePartes.MantenerSatisfecho => "En general les interesan poco las estrategias de la empresa, pero tienen un grado de influencia significativo. Por su variabilidad son de cuidado y monitoreo permanente.",
        CuadrantePartes.MantenerInformado => "Su participación puede ser importante al momento de influenciar a otros.",
        CuadrantePartes.MinimoEsfuerzo => "Requieren esfuerzo y supervisión mínima.",
        _ => "Falta el impacto o la influencia."
    };

    public static string Color(CuadrantePartes c) => c switch
    {
        CuadrantePartes.GestionarDeCerca => "#c0392b",
        CuadrantePartes.MantenerSatisfecho => "#d9822b",
        CuadrantePartes.MantenerInformado => "#2f7ec1",
        CuadrantePartes.MinimoEsfuerzo => "#5b8a5b",
        _ => "#8a94a3"
    };

    // ¿La clasificación escrita en la matriz menciona el cuadrante que dan IMP e INF? (solo para avisar)
    public static bool ClasificacionCoincide(string? escrita, CuadrantePartes c)
    {
        if (string.IsNullOrWhiteSpace(escrita) || c == CuadrantePartes.SinValorar) return true;
        var t = escrita.ToUpperInvariant();
        return c switch
        {
            CuadrantePartes.GestionarDeCerca => t.Contains("ATENDER") || t.Contains("CERCA"),
            CuadrantePartes.MantenerSatisfecho => t.Contains("SATISFECH"),
            CuadrantePartes.MantenerInformado => t.Contains("INFORMAD"),
            CuadrantePartes.MinimoEsfuerzo => t.Contains("MINIMO") || t.Contains("MÍNIMO"),
            _ => true
        };
    }

    public static string Nombre(CumplimientoParte c) => c switch
    {
        CumplimientoParte.Cumple => "Cumple",
        CumplimientoParte.Parcial => "Cumple parcialmente",
        CumplimientoParte.NoCumple => "No cumple",
        _ => "Sin medir"
    };

    public static string Color(CumplimientoParte c) => c switch
    {
        CumplimientoParte.Cumple => "#1f9d55",
        CumplimientoParte.Parcial => "#d97706",
        CumplimientoParte.NoCumple => "#c0392b",
        _ => "#8a94a3"
    };

    public static string Nombre(EstadoSeguimientoParte e) => e switch
    {
        EstadoSeguimientoParte.AlDia => "Al día",
        EstadoSeguimientoParte.PorVencer => "Por vencer",
        EstadoSeguimientoParte.Vencido => "Vencido",
        EstadoSeguimientoParte.Inactiva => "Inactiva",
        _ => "Sin programar"
    };

    public static string NombreUnidad(string u, int n) => u switch
    {
        UnidadPeriodo.Dias => n == 1 ? "día" : "días",
        UnidadPeriodo.Semanas => n == 1 ? "semana" : "semanas",
        _ => n == 1 ? "mes" : "meses"
    };

    public static string TextoPeriodo(ProgramacionSeguimientoParte? p) =>
        p?.Cada is int n && n > 0 ? (n == 1 ? (p.Unidad == UnidadPeriodo.Meses ? "Mensual" : p.Unidad == UnidadPeriodo.Semanas ? "Semanal" : "Diario") : $"Cada {n} {NombreUnidad(p.Unidad, n)}") : "";

    public static DateTime Sumar(DateTime d, int n, string unidad) => unidad switch
    {
        UnidadPeriodo.Dias => d.AddDays(n),
        UnidadPeriodo.Semanas => d.AddDays(7 * n),
        _ => d.AddMonths(n)
    };

    // Próximo seguimiento = último + periodo; sin seguimientos: la fecha "desde" (o la vigencia de la matriz).
    // Una parte inactiva no tiene próximo seguimiento.
    public static SituacionParte Situacion(ProgramacionSeguimientoParte? prog, IReadOnlyList<SeguimientoParteInteresada> segs, DateTime? vigenteDesde, DateTime hoy)
    {
        var ultimo = segs.OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).FirstOrDefault();
        DateTime? proximo = null;
        var estado = EstadoSeguimientoParte.SinProgramar;
        if (prog?.Inactiva == true) estado = EstadoSeguimientoParte.Inactiva;
        else if (prog?.Cada is int n && n > 0)
        {
            proximo = ultimo is not null ? Sumar(ultimo.Fecha.Date, n, prog.Unidad) : (prog.Desde ?? vigenteDesde ?? hoy).Date;
            estado = proximo < hoy.Date ? EstadoSeguimientoParte.Vencido
                : proximo <= hoy.Date.AddDays(DiasPorVencer) ? EstadoSeguimientoParte.PorVencer
                : EstadoSeguimientoParte.AlDia;
        }
        return new SituacionParte { Programacion = prog, Ultimo = ultimo, Proximo = proximo, Estado = estado, Seguimientos = segs.Count };
    }

    // ---------- Consultas ----------
    public static List<VersionPartesInteresadas> Versiones(AppDbContext db) =>
        db.VersionesPartesInteresadas.AsNoTracking().OrderByDescending(v => v.Numero).ToList();

    public static List<ParteInteresada> Partes(AppDbContext db, int versionId) =>
        db.PartesInteresadas.AsNoTracking().Where(p => p.VersionPartesInteresadasId == versionId)
            .OrderBy(p => p.Orden).ThenBy(p => p.Id).ToList();

    public static string Etiqueta(VersionPartesInteresadas v) => $"v{v.Numero}" + (string.IsNullOrWhiteSpace(v.Codigo) ? "" : $" · {v.Codigo}");

    // ---------- Edición (solo el borrador) ----------
    private static async Task<VersionPartesInteresadas> BorradorDe(AppDbContext db, int versionId)
    {
        var v = await db.VersionesPartesInteresadas.FirstAsync(x => x.Id == versionId);
        if (v.Estado != EstadoVersionPartes.Borrador) throw new InvalidOperationException($"La v{v.Numero} está {v.Estado.ToLowerInvariant()}: solo se edita el borrador.");
        return v;
    }

    public static async Task<ParteInteresada> GuardarParteAsync(AppDbContext db, ParteInteresada f)
    {
        await BorradorDe(db, f.VersionPartesInteresadasId);
        if (string.IsNullOrWhiteSpace(f.Nombre)) throw new InvalidOperationException("Falta el nombre de la parte interesada.");
        Validar(f.Impacto, "El impacto"); Validar(f.Influencia, "La influencia");
        ParteInteresada ent;
        if (f.Id == 0)
        {
            ent = new ParteInteresada { VersionPartesInteresadasId = f.VersionPartesInteresadasId, Clave = f.Clave == Guid.Empty ? Guid.NewGuid() : f.Clave };
            ent.Orden = f.Orden > 0 ? f.Orden
                : (await db.PartesInteresadas.Where(p => p.VersionPartesInteresadasId == f.VersionPartesInteresadasId).MaxAsync(p => (int?)p.Orden) ?? 0) + 1;
            db.PartesInteresadas.Add(ent);
        }
        else
        {
            ent = await db.PartesInteresadas.FirstAsync(p => p.Id == f.Id);
            ent.Orden = f.Orden;
        }
        Copiar(ent, f);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return ent;
    }

    private static void Copiar(ParteInteresada ent, ParteInteresada f)
    {
        ent.Nombre = f.Nombre.Trim();
        ent.Procesos = Limpio(f.Procesos); ent.Necesidades = Limpio(f.Necesidades); ent.Expectativas = Limpio(f.Expectativas);
        ent.AfectaSgc = Limpio(f.AfectaSgc); ent.Impacto = f.Impacto; ent.Influencia = f.Influencia;
        ent.Clasificacion = Limpio(f.Clasificacion); ent.Estrategias = Limpio(f.Estrategias); ent.Medicion = Limpio(f.Medicion);
        ent.Periodicidad = Limpio(f.Periodicidad); ent.Meta = Limpio(f.Meta); ent.Observaciones = Limpio(f.Observaciones);
        ent.Supuesto = Limpio(f.Supuesto);
    }

    // Huella de lo editable de una parte: si no cambia, guardar no crea ni modifica ninguna versión.
    public static string Firma(ParteInteresada p) =>
        string.Join("\u001f", Campos.Select(c => Limpio(c.Valor(p)) ?? "").Append(p.Orden.ToString()));

    // Corrección directa en una versión ya aprobada (vigente u obsoleta), sin crear versión nueva: para lo que
    // faltó registrar en esa versión. Solo partes existentes.
    public static async Task<ParteInteresada> CorregirParteAsync(AppDbContext db, int parteId, ParteInteresada f)
    {
        if (string.IsNullOrWhiteSpace(f.Nombre)) throw new InvalidOperationException("Falta el nombre de la parte interesada.");
        Validar(f.Impacto, "El impacto"); Validar(f.Influencia, "La influencia");
        var ent = await db.PartesInteresadas.FirstAsync(p => p.Id == parteId);
        ent.Orden = f.Orden;
        Copiar(ent, f);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return ent;
    }

    // Tras corregir una versión aprobada, el borrador (si existe) recibe la misma corrección en los campos que allí
    // siguen iguales al valor anterior: lo que ya se cambió en el borrador no se pisa.
    public static async Task<bool> PropagarAlBorradorAsync(AppDbContext db, ParteInteresada antes, ParteInteresada nuevo, int versionCorregida)
    {
        var b = await db.VersionesPartesInteresadas.AsNoTracking().FirstOrDefaultAsync(v => v.Estado == EstadoVersionPartes.Borrador);
        if (b is null || b.Id == versionCorregida) return false;
        var p = await db.PartesInteresadas.FirstOrDefaultAsync(x => x.VersionPartesInteresadasId == b.Id && x.Clave == antes.Clave);
        if (p is null) return false;
        static bool Igual(string? x, string? y) => string.Equals(Limpio(x), Limpio(y), StringComparison.Ordinal);
        if (Igual(p.Nombre, antes.Nombre)) p.Nombre = nuevo.Nombre.Trim();
        if (Igual(p.Procesos, antes.Procesos)) p.Procesos = Limpio(nuevo.Procesos);
        if (Igual(p.Necesidades, antes.Necesidades)) p.Necesidades = Limpio(nuevo.Necesidades);
        if (Igual(p.Expectativas, antes.Expectativas)) p.Expectativas = Limpio(nuevo.Expectativas);
        if (Igual(p.AfectaSgc, antes.AfectaSgc)) p.AfectaSgc = Limpio(nuevo.AfectaSgc);
        if (p.Impacto == antes.Impacto) p.Impacto = nuevo.Impacto;
        if (p.Influencia == antes.Influencia) p.Influencia = nuevo.Influencia;
        if (Igual(p.Clasificacion, antes.Clasificacion)) p.Clasificacion = Limpio(nuevo.Clasificacion);
        if (Igual(p.Estrategias, antes.Estrategias)) p.Estrategias = Limpio(nuevo.Estrategias);
        if (Igual(p.Medicion, antes.Medicion)) p.Medicion = Limpio(nuevo.Medicion);
        if (Igual(p.Periodicidad, antes.Periodicidad)) p.Periodicidad = Limpio(nuevo.Periodicidad);
        if (Igual(p.Meta, antes.Meta)) p.Meta = Limpio(nuevo.Meta);
        if (Igual(p.Observaciones, antes.Observaciones)) p.Observaciones = Limpio(nuevo.Observaciones);
        if (Igual(p.Supuesto, antes.Supuesto)) p.Supuesto = Limpio(nuevo.Supuesto);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return true;
    }

    // Un borrador copiado de otra versión que se abrió y se dejó sin cambios (y sin seguimientos registrados
    // sobre sus filas) se descarta: abrir "Editar" y salir sin tocar nada no deja versiones vacías.
    public static async Task<bool> DescartarBorradorVacioAsync(AppDbContext db, int borradorId)
    {
        var v = await db.VersionesPartesInteresadas.FirstOrDefaultAsync(x => x.Id == borradorId);
        if (v is null || v.Estado != EstadoVersionPartes.Borrador || v.VersionBaseId is not int baseId) return false;
        var antes = Partes(db, baseId);
        var despues = Partes(db, borradorId);
        if (Comparar(antes, despues).Count > 0) return false;
        var ordenA = antes.ToDictionary(p => p.Clave, p => p.Orden);
        if (despues.Any(p => !ordenA.TryGetValue(p.Clave, out var o) || o != p.Orden)) return false;
        var ids = despues.Select(p => (int?)p.Id).ToList();
        if (await db.SeguimientosParteInteresada.IgnoreQueryFilters().AnyAsync(s => ids.Contains(s.ParteInteresadaId))) return false;
        db.PartesInteresadas.RemoveRange(await db.PartesInteresadas.IgnoreQueryFilters().Where(p => p.VersionPartesInteresadasId == borradorId).ToListAsync());
        db.VersionesPartesInteresadas.Remove(v);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return true;
    }

    // Arrastrar la parte en el mapa (borrador): nueva valoración, redondeada a 0,5.
    public static async Task MoverAsync(AppDbContext db, int parteId, decimal impacto, decimal influencia)
    {
        var p = await db.PartesInteresadas.FirstAsync(x => x.Id == parteId);
        await BorradorDe(db, p.VersionPartesInteresadasId);
        p.Impacto = Redondear(impacto); p.Influencia = Redondear(influencia);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public static decimal Redondear(decimal v) => Math.Clamp(Math.Round(v * 2, MidpointRounding.AwayFromZero) / 2, 0, Maximo);

    public static async Task EnviarAPapeleraAsync(AppDbContext db, int parteId)
    {
        var p = await db.PartesInteresadas.FirstAsync(x => x.Id == parteId);
        await BorradorDe(db, p.VersionPartesInteresadasId);
        p.Eliminado = true; p.FechaEliminacion = DateTime.Now;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // Lleva al borrador la valoración (IMP/INF) del último seguimiento de cada parte, si es distinta.
    public static async Task<int> AplicarValoracionSeguimientosAsync(AppDbContext db, int borradorId)
    {
        await BorradorDe(db, borradorId);
        var partes = await db.PartesInteresadas.Where(p => p.VersionPartesInteresadasId == borradorId).ToListAsync();
        var claves = partes.Select(p => p.Clave).ToList();
        var ultimos = (await db.SeguimientosParteInteresada.AsNoTracking()
                .Where(s => claves.Contains(s.ParteClave) && (s.Impacto != null || s.Influencia != null)).ToListAsync())
            .GroupBy(s => s.ParteClave).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).First());
        var n = 0;
        foreach (var p in partes)
        {
            if (!ultimos.TryGetValue(p.Clave, out var s)) continue;
            var imp = s.Impacto ?? p.Impacto; var inf = s.Influencia ?? p.Influencia;
            if (imp == p.Impacto && inf == p.Influencia) continue;
            p.Impacto = imp; p.Influencia = inf; n++;
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return n;
    }

    // ---------- Versiones ----------
    public static async Task<VersionPartesInteresadas> CrearBorradorAsync(AppDbContext db, int? baseId, string? titulo = null)
    {
        if (await db.VersionesPartesInteresadas.AnyAsync(v => v.Estado == EstadoVersionPartes.Borrador))
            throw new InvalidOperationException("Ya hay un borrador de la matriz.");
        var b = baseId is int id ? await db.VersionesPartesInteresadas.AsNoTracking().FirstAsync(v => v.Id == id) : null;
        var numero = (await db.VersionesPartesInteresadas.IgnoreQueryFilters().MaxAsync(v => (int?)v.Numero) ?? 0) + 1;
        var nueva = new VersionPartesInteresadas
        {
            Numero = numero, Titulo = b?.Titulo ?? titulo ?? "Matriz de identificación de partes interesadas", Alcance = b?.Alcance,
            Estado = EstadoVersionPartes.Borrador, VersionBaseId = b?.Id, Origen = b is null ? "Creada en blanco" : $"Copia de la v{b.Numero}"
        };
        db.VersionesPartesInteresadas.Add(nueva);
        await db.SaveChangesAsync();
        if (b is not null)
        {
            var copias = new List<(int Origen, ParteInteresada Copia)>();
            foreach (var p in Partes(db, b.Id))
            {
                copias.Add((p.Id, p));
                p.Id = 0; p.VersionPartesInteresadasId = nueva.Id;
                db.PartesInteresadas.Add(p);
            }
            await db.SaveChangesAsync();
            // Campos nuevos del formulario (diseñador de formularios): se copian con cada parte.
            var campos = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == "form:partes-interesadas").Select(c => c.Id).ToListAsync();
            if (campos.Count > 0)
            {
                var mapa = copias.ToDictionary(x => x.Origen, x => x.Copia.Id);
                var origenes = mapa.Keys.ToList();
                foreach (var v in await db.ValoresCampoTabla.AsNoTracking().Where(v => campos.Contains(v.CampoTablaId) && origenes.Contains(v.FilaId)).ToListAsync())
                    db.ValoresCampoTabla.Add(new ValorCampoTabla { CampoTablaId = v.CampoTablaId, FilaId = mapa[v.FilaId], Valor = v.Valor, Formula = v.Formula });
                await db.SaveChangesAsync();
            }
        }
        db.ChangeTracker.Clear();
        return nueva;
    }

    public static async Task PublicarAsync(AppDbContext db, int versionId, DatosPublicacionPartes d)
    {
        var v = await db.VersionesPartesInteresadas.FirstAsync(x => x.Id == versionId);
        if (v.Estado != EstadoVersionPartes.Borrador) throw new InvalidOperationException("Solo se publica un borrador.");
        if (string.IsNullOrWhiteSpace(d.Motivo)) throw new InvalidOperationException("Falta el motivo del cambio.");
        if (!await db.PartesInteresadas.AnyAsync(p => p.VersionPartesInteresadasId == versionId))
            throw new InvalidOperationException("El borrador no tiene partes interesadas.");
        var desde = (d.VigenteDesde ?? DateTime.Today).Date;
        foreach (var anterior in await db.VersionesPartesInteresadas.Where(x => x.Estado == EstadoVersionPartes.Vigente && x.Id != versionId).ToListAsync())
        {
            if (anterior.VigenteDesde is DateTime ad && ad.Date > desde)
                throw new InvalidOperationException($"La v{anterior.Numero} rige desde {ad:dd/MM/yyyy}: la nueva no puede empezar antes.");
            anterior.Estado = EstadoVersionPartes.Obsoleta;
            anterior.VigenteHasta = desde.AddDays(-1) < (anterior.VigenteDesde ?? DateTime.MinValue) ? desde : desde.AddDays(-1);
        }
        v.Estado = EstadoVersionPartes.Vigente;
        v.VigenteDesde = desde; v.VigenteHasta = null;
        v.Codigo = Limpio(d.Codigo) ?? v.Codigo;
        v.MotivoCambio = d.Motivo.Trim(); v.Elaboro = Limpio(d.Elaboro); v.Reviso = Limpio(d.Reviso); v.Aprobo = Limpio(d.Aprobo);
        v.FechaAprobacion = d.FechaAprobacion;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // ---------- Comparación (por Clave) ----------
    private static readonly (string Campo, Func<ParteInteresada, string?> Valor)[] Campos =
    {
        ("#", p => p.Orden.ToString()),
        ("Parte interesada", p => p.Nombre),
        ("Procesos que intervienen", p => p.Procesos),
        ("Necesidades", p => p.Necesidades),
        ("Expectativas", p => p.Expectativas),
        ("Cómo afecta al SGC", p => p.AfectaSgc),
        ("IMP", p => Num(p.Impacto)),
        ("INF", p => Num(p.Influencia)),
        ("Clasificación", p => p.Clasificacion),
        ("Estrategias", p => p.Estrategias),
        ("Medición", p => p.Medicion),
        ("Periodicidad", p => p.Periodicidad),
        ("Meta", p => p.Meta),
        ("Observaciones", p => p.Observaciones),
    };

    public static List<CambioParte> Comparar(IReadOnlyList<ParteInteresada> antes, IReadOnlyList<ParteInteresada> despues)
    {
        var r = new List<CambioParte>();
        var a = antes.GroupBy(p => p.Clave).ToDictionary(g => g.Key, g => g.First());
        var b = despues.GroupBy(p => p.Clave).ToDictionary(g => g.Key, g => g.First());
        foreach (var p in despues.Where(p => !a.ContainsKey(p.Clave)))
            r.Add(new(TipoCambioParte.Agregada, p.Clave, p.Nombre, "Parte nueva", null, p.Nombre));
        foreach (var p in antes.Where(p => !b.ContainsKey(p.Clave)))
            r.Add(new(TipoCambioParte.Eliminada, p.Clave, p.Nombre, "Parte retirada", p.Nombre, null));
        foreach (var p in despues.Where(p => a.ContainsKey(p.Clave)))
        {
            var o = a[p.Clave];
            foreach (var (campo, valor) in Campos)
            {
                var x = Limpio(valor(o)); var y = Limpio(valor(p));
                if (!string.Equals(x, y, StringComparison.Ordinal)) r.Add(new(TipoCambioParte.Campo, p.Clave, p.Nombre, campo, x, y));
            }
            var ca = Cuadrante(o.Impacto, o.Influencia); var cb = Cuadrante(p.Impacto, p.Influencia);
            if (ca != cb) r.Add(new(TipoCambioParte.Campo, p.Clave, p.Nombre, "Cuadrante (IMP × INF)", Nombre(ca), Nombre(cb)));
        }
        return r.OrderBy(c => c.Tipo).ThenBy(c => c.Parte).ToList();
    }

    // ---------- Seguimientos ----------
    public static async Task GuardarSeguimientoAsync(AppDbContext db, SeguimientoParteInteresada f)
    {
        if (f.ParteClave == Guid.Empty) throw new InvalidOperationException("Falta la parte interesada.");
        Validar(f.Impacto, "El impacto"); Validar(f.Influencia, "La influencia");
        SeguimientoParteInteresada ent;
        if (f.Id == 0) { ent = new SeguimientoParteInteresada { ParteClave = f.ParteClave, ParteInteresadaId = f.ParteInteresadaId }; db.SeguimientosParteInteresada.Add(ent); }
        else ent = await db.SeguimientosParteInteresada.FirstAsync(s => s.Id == f.Id);
        ent.Fecha = f.Fecha.Date; ent.Responsable = Limpio(f.Responsable); ent.Resultado = Limpio(f.Resultado);
        ent.Cumplimiento = f.Cumplimiento; ent.Impacto = f.Impacto; ent.Influencia = f.Influencia; ent.Observacion = Limpio(f.Observacion);
        ent.DescripcionHallazgo = Limpio(f.DescripcionHallazgo);
        await db.SaveChangesAsync();
        f.Id = ent.Id;
        db.ChangeTracker.Clear();
    }

    public static async Task<int> GuardarRondaAsync(AppDbContext db, IEnumerable<SeguimientoParteInteresada> segs)
    {
        var l = segs.ToList();
        foreach (var s in l) { Validar(s.Impacto, "El impacto"); Validar(s.Influencia, "La influencia"); s.Id = 0; s.Fecha = s.Fecha.Date; s.FechaRegistro = DateTime.Now; }
        db.SeguimientosParteInteresada.AddRange(l);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return l.Count;
    }

    public static async Task EliminarSeguimientoAsync(AppDbContext db, int id)
    {
        var s = await db.SeguimientosParteInteresada.FirstAsync(x => x.Id == id);
        s.Eliminado = true; s.FechaEliminacion = DateTime.Now;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public static async Task GuardarProgramacionAsync(AppDbContext db, Guid clave, int? cada, string unidad, DateTime? desde, string? responsable)
    {
        if (cada is int n && (n < 1 || n > 999)) throw new InvalidOperationException("El periodo debe estar entre 1 y 999.");
        var p = await db.ProgramacionesSeguimientoParte.FirstOrDefaultAsync(x => x.ParteClave == clave);
        if (p is null) { p = new ProgramacionSeguimientoParte { ParteClave = clave }; db.ProgramacionesSeguimientoParte.Add(p); }
        p.Cada = cada; p.Unidad = unidad is UnidadPeriodo.Dias or UnidadPeriodo.Semanas ? unidad : UnidadPeriodo.Meses;
        p.Desde = desde?.Date; p.Responsable = Limpio(responsable);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // Activar / desactivar una parte (por Clave, en todas las versiones; no cambia la matriz).
    public static async Task ActivarAsync(AppDbContext db, Guid clave, bool activa)
    {
        var p = await db.ProgramacionesSeguimientoParte.FirstOrDefaultAsync(x => x.ParteClave == clave);
        if (p is null) { p = new ProgramacionSeguimientoParte { ParteClave = clave }; db.ProgramacionesSeguimientoParte.Add(p); }
        p.Inactiva = !activa;
        p.InactivaDesde = activa ? null : DateTime.Today;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // ---------- Exportar ----------
    public static readonly string[] Columnas = { "#", "Parte interesada", "Procesos que intervienen", "Necesidades", "Expectativas", "Cómo afecta al SGC",
        "IMP", "INF", "Clasificación", "Cuadrante (IMP × INF)", "Estrategias", "Medición", "Periodicidad", "Meta", "Observaciones",
        "Seguimiento cada", "Último seguimiento", "Próximo seguimiento", "Estado del seguimiento", "Supuesto" };

    public static byte[] Exportar(VersionPartesInteresadas v, IReadOnlyList<ParteInteresada> partes, IReadOnlyDictionary<Guid, SituacionParte> situacion,
        IReadOnlyList<SeguimientoParteInteresada> seguimientos, IReadOnlyList<VersionPartesInteresadas> versiones)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Matriz");
        ws.Cell(1, 1).Value = v.Titulo; ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = string.Join(" · ", new[] { v.Alcance, Etiqueta(v), v.Estado, v.VigenteDesde is DateTime d ? $"desde {d:dd/MM/yyyy}" : null }.Where(x => !string.IsNullOrWhiteSpace(x)));
        Cabecera(ws, 4, Columnas);
        var fila = 5;
        foreach (var p in partes)
        {
            situacion.TryGetValue(p.Clave, out var s);
            var c = 1;
            ws.Cell(fila, c++).Value = p.Orden;
            ws.Cell(fila, c++).Value = p.Nombre;
            ws.Cell(fila, c++).Value = p.Procesos ?? "";
            ws.Cell(fila, c++).Value = p.Necesidades ?? "";
            ws.Cell(fila, c++).Value = p.Expectativas ?? "";
            ws.Cell(fila, c++).Value = p.AfectaSgc ?? "";
            if (p.Impacto is decimal i) ws.Cell(fila, c).Value = i; c++;
            if (p.Influencia is decimal f) ws.Cell(fila, c).Value = f; c++;
            ws.Cell(fila, c++).Value = p.Clasificacion ?? "";
            ws.Cell(fila, c++).Value = Nombre(Cuadrante(p.Impacto, p.Influencia));
            ws.Cell(fila, c++).Value = p.Estrategias ?? "";
            ws.Cell(fila, c++).Value = p.Medicion ?? "";
            ws.Cell(fila, c++).Value = p.Periodicidad ?? "";
            ws.Cell(fila, c++).Value = p.Meta ?? "";
            ws.Cell(fila, c++).Value = p.Observaciones ?? "";
            ws.Cell(fila, c++).Value = TextoPeriodo(s?.Programacion);
            if (s?.Ultimo is { } u) { ws.Cell(fila, c).Value = u.Fecha; ws.Cell(fila, c).Style.DateFormat.Format = "dd/mm/yyyy"; } c++;
            if (s?.Proximo is DateTime px) { ws.Cell(fila, c).Value = px; ws.Cell(fila, c).Style.DateFormat.Format = "dd/mm/yyyy"; } c++;
            ws.Cell(fila, c++).Value = s is null ? "" : Nombre(s.Estado);
            ws.Cell(fila, c++).Value = p.Supuesto ?? "";
            fila++;
        }
        Ajustar(ws, 4, fila, Columnas.Length);

        var nombres = partes.ToDictionary(p => p.Clave, p => p.Nombre);
        var wsS = wb.Worksheets.Add("Seguimientos");
        string[] cs = { "Fecha", "Parte interesada", "Responsable", "Resultado", "Cumplimiento", "IMP", "INF", "Observación" };
        Cabecera(wsS, 1, cs);
        var r = 2;
        foreach (var s in seguimientos.OrderByDescending(s => s.Fecha))
        {
            wsS.Cell(r, 1).Value = s.Fecha; wsS.Cell(r, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            wsS.Cell(r, 2).Value = nombres.TryGetValue(s.ParteClave, out var n) ? n : "(no está en esta versión)";
            wsS.Cell(r, 3).Value = s.Responsable ?? "";
            wsS.Cell(r, 4).Value = s.Resultado ?? "";
            wsS.Cell(r, 5).Value = Nombre(s.Cumplimiento);
            if (s.Impacto is decimal i) wsS.Cell(r, 6).Value = i;
            if (s.Influencia is decimal f) wsS.Cell(r, 7).Value = f;
            wsS.Cell(r, 8).Value = s.Observacion ?? "";
            r++;
        }
        Ajustar(wsS, 1, r, cs.Length);

        var wv = wb.Worksheets.Add("Control de cambios");
        string[] cv = { "Versión", "Código", "Estado", "Vigente desde", "Vigente hasta", "Motivo del cambio", "Elaboró", "Revisó", "Aprobó", "Fecha de aprobación", "Origen" };
        Cabecera(wv, 1, cv);
        r = 2;
        foreach (var x in versiones.OrderBy(x => x.Numero))
        {
            wv.Cell(r, 1).Value = $"v{x.Numero}";
            wv.Cell(r, 2).Value = x.Codigo ?? "";
            wv.Cell(r, 3).Value = x.Estado;
            if (x.VigenteDesde is DateTime a) { wv.Cell(r, 4).Value = a; wv.Cell(r, 4).Style.DateFormat.Format = "dd/mm/yyyy"; }
            if (x.VigenteHasta is DateTime h) { wv.Cell(r, 5).Value = h; wv.Cell(r, 5).Style.DateFormat.Format = "dd/mm/yyyy"; }
            wv.Cell(r, 6).Value = x.MotivoCambio ?? "";
            wv.Cell(r, 7).Value = x.Elaboro ?? "";
            wv.Cell(r, 8).Value = x.Reviso ?? "";
            wv.Cell(r, 9).Value = x.Aprobo ?? "";
            if (x.FechaAprobacion is DateTime fa) { wv.Cell(r, 10).Value = fa; wv.Cell(r, 10).Style.DateFormat.Format = "dd/mm/yyyy"; }
            wv.Cell(r, 11).Value = x.Origen ?? "";
            r++;
        }
        Ajustar(wv, 1, r, cv.Length);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Cabecera(IXLWorksheet ws, int fila, string[] cols)
    {
        for (var c = 0; c < cols.Length; c++) ws.Cell(fila, c + 1).Value = cols[c];
        var cab = ws.Range(fila, 1, fila, cols.Length);
        cab.Style.Font.Bold = true; cab.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2A3A"); cab.Style.Font.FontColor = XLColor.White;
    }

    private static void Ajustar(IXLWorksheet ws, int filaCab, int fila, int ncols)
    {
        ws.Range(filaCab, 1, Math.Max(filaCab, fila - 1), ncols).SetAutoFilter();
        ws.SheetView.FreezeRows(filaCab);
        ws.Columns().AdjustToContents(filaCab, Math.Min(fila, 400));
        foreach (var col in ws.ColumnsUsed()) if (col.Width > 50) col.Width = 50;
        ws.Range(filaCab + 1, 1, Math.Max(filaCab + 1, fila - 1), ncols).Style.Alignment.WrapText = true;
        ws.Range(filaCab + 1, 1, Math.Max(filaCab + 1, fila - 1), ncols).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    // ---------- Utilidades ----------
    private static void Validar(decimal? v, string que)
    {
        if (v is decimal d && (d < 0 || d > Maximo)) throw new InvalidOperationException($"{que} debe estar entre 0 y {Maximo:0}.");
    }

    public static string? Num(decimal? v) => v?.ToString("0.##");
    public static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
