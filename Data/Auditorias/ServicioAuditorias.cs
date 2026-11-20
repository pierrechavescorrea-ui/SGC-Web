using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Riesgos;
using SigWeb.Models;

namespace SigWeb.Data.Auditorias;

// Lógica del módulo Auditoría interna: catálogos, códigos, estado calculado de los hallazgos,
// guardado y envío a la Papelera. Las páginas precargan todo con estos métodos (nunca en el render).
public static class ServicioAuditorias
{
    // ---------- Catálogos ----------
    public const string TipoAuditoria = "Auditoría — tipo";
    public const string Modalidad = "Auditoría — modalidad";
    public const string GradoNc = "Auditoría — grado de no conformidad";
    public const string Norma = "Auditoría — norma";
    // Estados fijos de la app: el catálogo solo da el formato (color, fondo, forma).
    public const string EstadoAuditoriaCat = "Auditoría — estado de la auditoría";
    public const string EstadoHallazgoCat = "Auditoría — estado del hallazgo";

    private static readonly (string Nombre, string Entidad)[] Definiciones =
    {
        (TipoAuditoria, "Auditoría interna — tipo de auditoría (interna, a proveedor, de cliente, de certificador)"),
        (Modalidad, "Auditoría interna — en oficina, en campo, remota, combinada"),
        (GradoNc, "Auditoría interna — grado de una no conformidad (mayor / menor)"),
        (Norma, "Auditoría interna — normas que cubren programa, auditorías y hallazgos"),
        (EstadoAuditoriaCat, "Auditoría interna — formato de los estados de una auditoría"),
        (EstadoHallazgoCat, "Auditoría interna — formato de los estados de un hallazgo"),
    };

    // Valores iniciales: solo al crear la definición (si luego se borran en Catálogos, no vuelven).
    private static readonly Dictionary<string, string[]> Iniciales = new()
    {
        [TipoAuditoria] = new[] { "Interna", "A proveedor o subcontratista", "De cliente o interventoría", "De ente certificador" },
        [Modalidad] = new[] { "En oficina", "En campo", "Remota", "Combinada" },
        [Norma] = new[] { "ISO 9001:2015", "ISO 14001:2015", "ISO 45001:2018" },
    };

    private static readonly Dictionary<string, (string Nombre, string Fondo, string Texto)[]> Formatos = new()
    {
        [GradoNc] = new[] { ("Mayor", "#c0392b", "#ffffff"), ("Menor", "#fd7e14", "#ffffff") },
        [EstadoAuditoriaCat] = new[] { ("Programada", "#6c757d", "#ffffff"), ("En ejecución", "#FFC000", "#1f2a3a"), ("Informe emitido", "#0d6efd", "#ffffff"), ("Cerrada", "#00B050", "#1f2a3a"), ("Cancelada", "#adb5bd", "#1f2a3a") },
        [EstadoHallazgoCat] = new[] { ("Abierto", "#dc3545", "#ffffff"), ("En discusión", "#6f42c1", "#ffffff"), ("Con plan de acción", "#fd7e14", "#ffffff"), ("En verificación", "#0d6efd", "#ffffff"), ("Reabierto (no eficaz)", "#8b0000", "#ffffff"), ("Cerrado", "#198754", "#ffffff") },
    };

    // Seguro de llamar en cada arranque.
    public static void SembrarCatalogos(AppDbContext db)
    {
        SembradorCatalogosRiesgo.SembrarDefiniciones(db);
        var ya = db.DefinicionesCatalogo.IgnoreQueryFilters().Select(d => d.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orden = 40;
        var creados = false;
        foreach (var (nombre, entidad) in Definiciones)
        {
            orden++;
            if (ya.Contains(nombre)) continue;
            db.DefinicionesCatalogo.Add(new DefinicionCatalogo { Nombre = nombre, EntidadAsociada = entidad, Orden = orden });
            if (Iniciales.TryGetValue(nombre, out var vals))
                foreach (var v in vals) db.Catalogos.Add(new ItemCatalogo { Tipo = nombre, Nombre = v });
            if (Formatos.TryGetValue(nombre, out var est))
                foreach (var (n, f, t) in est)
                    db.Catalogos.Add(new ItemCatalogo { Tipo = nombre, Nombre = n, Estilo = "personalizado", ColorFondo = f, ColorTexto = t, Forma = "pildora", Negrita = true });
            // Primera vez del módulo: "Fortaleza" se suma a los tipos de hallazgo de los planes.
            if (nombre == TipoAuditoria &&
                !db.Catalogos.IgnoreQueryFilters().Any(c => c.Tipo == SembradorCatalogosRiesgo.TipoHallazgo && c.Nombre == "Fortaleza"))
                db.Catalogos.Add(new ItemCatalogo { Tipo = SembradorCatalogosRiesgo.TipoHallazgo, Nombre = "Fortaleza" });
            creados = true;
        }
        if (creados) { db.SaveChanges(); db.ChangeTracker.Clear(); }
    }

    // ---------- Utilidades ----------
    public static List<string> Lista(string? t) =>
        (t ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static string Unir(IEnumerable<string> l) =>
        string.Join("\n", l.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));

    // Un tipo de hallazgo "No conformidad…" exige plan de acción.
    public static bool EsNoConformidad(string? tipo) =>
        (tipo ?? "").Trim().StartsWith("No conformidad", StringComparison.OrdinalIgnoreCase);

    // AUD-2026-01, AUD-2026-02… por año, sin repetir los de la Papelera.
    public static string SiguienteCodigoAuditoria(AppDbContext db, int anio)
    {
        var pref = $"AUD-{anio}-";
        var usados = db.Auditorias.IgnoreQueryFilters().AsNoTracking().Where(a => a.Codigo.StartsWith(pref)).Select(a => a.Codigo).ToList();
        var max = usados.Select(c => int.TryParse(c[pref.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return pref + (max + 1).ToString("00");
    }

    // AUD-2026-01-H01… dentro de la auditoría.
    public static string SiguienteCodigoHallazgo(AppDbContext db, Auditoria a)
    {
        var pref = a.Codigo + "-H";
        var usados = db.HallazgosAuditoria.IgnoreQueryFilters().AsNoTracking().Where(h => h.AuditoriaId == a.Id).Select(h => h.Codigo).ToList();
        var max = usados.Where(c => c.StartsWith(pref)).Select(c => int.TryParse(c[pref.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return pref + (max + 1).ToString("00");
    }

    // ---------- Estado de los hallazgos ----------
    public sealed record PlanRef(int Id, string Codigo, EstadoAccionPlan Estado, bool? Eficaz, DateTime? Programada = null)
    {
        public EstadoEficacia Eficacia => EscalaEficacia.De(Eficaz, Programada, Estado);
    }

    // Planes (no eliminados) de cada hallazgo, con su estado calculado de las acciones.
    public static Dictionary<int, List<PlanRef>> PlanesDeHallazgos(AppDbContext db, IEnumerable<int> idsHallazgo)
    {
        var ids = idsHallazgo.ToList();
        var rel = db.HallazgosPlan.AsNoTracking().Where(r => ids.Contains(r.HallazgoAuditoriaId)).ToList();
        var idsPlan = rel.Select(r => r.PlanAccionId).Distinct().ToList();
        var planes = db.PlanesAccion.AsNoTracking().Where(p => idsPlan.Contains(p.Id)).ToDictionary(p => p.Id);
        var acciones = db.AccionesPlan.AsNoTracking().Where(a => idsPlan.Contains(a.PlanAccionId)).ToList();
        var d = new Dictionary<int, List<PlanRef>>();
        foreach (var g in rel.GroupBy(r => r.HallazgoAuditoriaId))
            d[g.Key] = g.Where(r => planes.ContainsKey(r.PlanAccionId)).Select(r =>
            {
                var p = planes[r.PlanAccionId];
                return new PlanRef(p.Id, p.Codigo, EscalaAccion.EstadoPlan(acciones.Where(a => a.PlanAccionId == p.Id)), p.Eficaz, p.FechaProgramadaEficacia);
            }).OrderBy(p => p.Codigo).ToList();
        return d;
    }

    // Cierre manual manda; luego la discusión; sin planes = abierto; con planes: alguno verificado
    // no eficaz = reabierto, todos cerrados y eficaces = cerrado, todos cerrados = en verificación.
    public static EstadoHallazgoAuditoria EstadoDe(HallazgoAuditoria h, IReadOnlyList<PlanRef>? planes)
    {
        if (h.FechaCierre is not null) return EstadoHallazgoAuditoria.Cerrado;
        if (h.Aceptacion == AceptacionHallazgo.EnDiscusion) return EstadoHallazgoAuditoria.EnDiscusion;
        if (planes is null || planes.Count == 0) return EstadoHallazgoAuditoria.Abierto;
        if (planes.Any(p => p.Eficaz == false)) return EstadoHallazgoAuditoria.Reabierto;
        if (planes.All(p => p.Estado == EstadoAccionPlan.Cerrada))
            return planes.All(p => p.Eficaz == true) ? EstadoHallazgoAuditoria.Cerrado : EstadoHallazgoAuditoria.EnVerificacion;
        return EstadoHallazgoAuditoria.ConPlan;
    }

    // ---------- Auditoría ----------
    public sealed class AuditoriaEditable
    {
        public Auditoria Auditoria { get; set; } = new();
        public List<string> Procesos { get; set; } = new();
        public List<string> Normas { get; set; } = new();
        public List<string> Auditores { get; set; } = new();
        public List<LugarPlan> Lugares { get; set; } = new();
        public string? Motivo { get; set; }           // obligatorio al reprogramar o cancelar
    }

    public static AuditoriaEditable CargarAuditoria(AppDbContext db, int id)
    {
        var a = db.Auditorias.AsNoTracking().First(x => x.Id == id);
        return new AuditoriaEditable
        {
            Auditoria = a,
            Procesos = Lista(a.Procesos),
            Normas = Lista(a.Normas),
            Auditores = Lista(a.Auditores),
            Lugares = db.LugaresAuditoria.AsNoTracking().Where(l => l.AuditoriaId == id).ToList()
                .Select(l => new LugarPlan { ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId }).ToList()
        };
    }

    // Programa ya aprobado: cambiar mes o fechas, o cancelar, pide motivo y queda en el historial.
    public static async Task<int> GuardarAuditoriaAsync(AppDbContext db, AuditoriaEditable e)
    {
        var f = e.Auditoria;
        var codigo = (f.Codigo ?? "").Trim();
        if (codigo.Length == 0) throw new InvalidOperationException("La auditoría necesita un código.");
        if (f.Mes is < 1 or > 12) throw new InvalidOperationException("Elige el mes programado.");
        if (f.FechaInicio is DateTime fi && f.FechaFin is DateTime ff && ff < fi) throw new InvalidOperationException("La fecha final es anterior a la inicial.");
        if (await db.Auditorias.IgnoreQueryFilters().AnyAsync(a => a.Id != f.Id && a.Codigo == codigo))
            throw new InvalidOperationException($"Ya existe una auditoría con el código \"{codigo}\" (puede estar en la Papelera).");
        var prog = await db.ProgramasAuditoria.AsNoTracking().FirstOrDefaultAsync(p => p.Id == f.ProgramaAuditoriaId)
            ?? throw new InvalidOperationException("El programa ya no existe.");

        Auditoria ent;
        var cambios = new List<CambioAuditoria>();
        if (f.Id == 0) { ent = new Auditoria { ProgramaAuditoriaId = f.ProgramaAuditoriaId }; db.Auditorias.Add(ent); }
        else
        {
            ent = await db.Auditorias.FirstAsync(a => a.Id == f.Id);
            if (ent.Mes != f.Mes) cambios.Add(new CambioAuditoria { Campo = "Mes", Anterior = EscalaAuditoria.Mes(ent.Mes), Nuevo = EscalaAuditoria.Mes(f.Mes) });
            if (ent.FechaInicio != f.FechaInicio || ent.FechaFin != f.FechaFin)
                cambios.Add(new CambioAuditoria { Campo = "Fechas", Anterior = Rango(ent.FechaInicio, ent.FechaFin), Nuevo = Rango(f.FechaInicio, f.FechaFin) });
            if (ent.Estado != f.Estado) cambios.Add(new CambioAuditoria { Campo = "Estado", Anterior = EscalaAuditoria.Nombre(ent.Estado), Nuevo = EscalaAuditoria.Nombre(f.Estado) });
            var pideMotivo = (prog.Estado != EstadoProgramaAuditoria.Borrador && cambios.Any(c => c.Campo == "Mes"))
                             || (f.Estado == EstadoAuditoria.Cancelada && ent.Estado != EstadoAuditoria.Cancelada);
            if (pideMotivo && string.IsNullOrWhiteSpace(e.Motivo))
                throw new InvalidOperationException("Escribe el motivo de la reprogramación o cancelación.");
        }
        ent.Codigo = codigo;
        ent.Tipo = (f.Tipo ?? "").Trim();
        ent.Modalidad = (f.Modalidad ?? "").Trim();
        ent.Mes = f.Mes;
        ent.FechaInicio = f.FechaInicio;
        ent.FechaFin = f.FechaFin;
        ent.Proyecto = Limpio(f.Proyecto);
        ent.Procesos = Unir(e.Procesos);
        ent.Normas = Unir(e.Normas);
        ent.Objetivo = Limpio(f.Objetivo);
        ent.Alcance = Limpio(f.Alcance);
        ent.Criterios = Limpio(f.Criterios);
        ent.AuditorLider = Limpio(f.AuditorLider);
        ent.Auditores = Unir(e.Auditores);
        ent.ExpertoTecnico = Limpio(f.ExpertoTecnico);
        ent.Observadores = Limpio(f.Observadores);
        ent.Auditados = Limpio(f.Auditados);
        ent.Estado = f.Estado;
        ent.Conclusiones = Limpio(f.Conclusiones);
        ent.Fortalezas = Limpio(f.Fortalezas);
        ent.FechaInforme = f.FechaInforme;
        await db.SaveChangesAsync();

        foreach (var c in cambios) { c.AuditoriaId = ent.Id; c.Motivo = Limpio(e.Motivo); db.CambiosAuditoria.Add(c); }
        db.LugaresAuditoria.RemoveRange(await db.LugaresAuditoria.Where(l => l.AuditoriaId == ent.Id).ToListAsync());
        foreach (var l in e.Lugares)
            db.LugaresAuditoria.Add(new LugarAuditoria { AuditoriaId = ent.Id, ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId });
        // El programa aprobado pasa a "En ejecución" con la primera auditoría que arranca.
        if (ent.Estado != EstadoAuditoria.Programada && ent.Estado != EstadoAuditoria.Cancelada && prog.Estado == EstadoProgramaAuditoria.Aprobado)
        {
            var p = await db.ProgramasAuditoria.FirstAsync(x => x.Id == prog.Id);
            p.Estado = EstadoProgramaAuditoria.EnEjecucion;
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return ent.Id;
    }

    // Cambio de estado rápido (botones de la auditoría: iniciar, emitir informe, cerrar).
    public static async Task CambiarEstadoAsync(AppDbContext db, int auditoriaId, EstadoAuditoria nuevo, string? motivo = null)
    {
        var e = CargarAuditoria(db, auditoriaId);
        e.Auditoria.Estado = nuevo;
        e.Motivo = motivo;
        if (nuevo == EstadoAuditoria.InformeEmitido && e.Auditoria.FechaInforme is null) e.Auditoria.FechaInforme = DateTime.Today;
        await GuardarAuditoriaAsync(db, e);
    }

    private static string Rango(DateTime? a, DateTime? b) =>
        a is null && b is null ? "—" : $"{a:dd/MM/yyyy} – {b:dd/MM/yyyy}";

    private static string? Limpio(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    // ---------- Hallazgo ----------
    public sealed class HallazgoEditable
    {
        public HallazgoAuditoria Hallazgo { get; set; } = new();
        public List<string> Normas { get; set; } = new();
        public List<string> Procesos { get; set; } = new();
        public List<LugarPlan> Lugares { get; set; } = new();
        public List<ArchivoHallazgoAuditoria> Anexos { get; set; } = new();      // Id 0 = subido, sin registrar
        public List<ArchivoHallazgoAuditoria> AnexosQuitados { get; set; } = new();
    }

    public static HallazgoEditable CargarHallazgo(AppDbContext db, int id)
    {
        var h = db.HallazgosAuditoria.AsNoTracking().First(x => x.Id == id);
        return new HallazgoEditable
        {
            Hallazgo = h,
            Normas = Lista(h.Normas),
            Procesos = Lista(h.Procesos),
            Lugares = db.LugaresHallazgoAuditoria.AsNoTracking().Where(l => l.HallazgoAuditoriaId == id).ToList()
                .Select(l => new LugarPlan { ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId }).ToList(),
            Anexos = db.ArchivosHallazgoAuditoria.AsNoTracking().Where(x => x.HallazgoAuditoriaId == id).OrderBy(x => x.Id).ToList()
        };
    }

    public static async Task<int> GuardarHallazgoAsync(AppDbContext db, HallazgoEditable e)
    {
        var f = e.Hallazgo;
        if (string.IsNullOrWhiteSpace(f.Tipo)) throw new InvalidOperationException("Elige el tipo de hallazgo.");
        if (string.IsNullOrWhiteSpace(f.Descripcion)) throw new InvalidOperationException("Escribe la declaración del hallazgo.");
        if (EsNoConformidad(f.Tipo) && string.IsNullOrWhiteSpace(f.Evidencia))
            throw new InvalidOperationException("Una no conformidad necesita su evidencia objetiva.");
        var aud = await db.Auditorias.AsNoTracking().FirstOrDefaultAsync(a => a.Id == f.AuditoriaId)
            ?? throw new InvalidOperationException("La auditoría ya no existe.");

        HallazgoAuditoria ent;
        if (f.Id == 0)
        {
            ent = new HallazgoAuditoria { AuditoriaId = aud.Id, Codigo = SiguienteCodigoHallazgo(db, aud) };
            db.HallazgosAuditoria.Add(ent);
        }
        else ent = await db.HallazgosAuditoria.FirstAsync(h => h.Id == f.Id);
        ent.Fecha = f.Fecha;
        ent.Tipo = f.Tipo.Trim();
        ent.Grado = EsNoConformidad(f.Tipo) ? Limpio(f.Grado) : null;
        ent.Normas = Unir(e.Normas);
        ent.Numerales = Limpio(f.Numerales);
        ent.Documento = Limpio(f.Documento);
        ent.Requisito = Limpio(f.Requisito);
        ent.Evidencia = Limpio(f.Evidencia);
        ent.Descripcion = f.Descripcion.Trim();
        ent.Procesos = Unir(e.Procesos);
        ent.Auditor = Limpio(f.Auditor);
        ent.Aceptacion = f.Aceptacion;
        ent.NotaAceptacion = Limpio(f.NotaAceptacion);
        await db.SaveChangesAsync();

        db.LugaresHallazgoAuditoria.RemoveRange(await db.LugaresHallazgoAuditoria.Where(l => l.HallazgoAuditoriaId == ent.Id).ToListAsync());
        foreach (var l in e.Lugares)
            db.LugaresHallazgoAuditoria.Add(new LugarHallazgoAuditoria { HallazgoAuditoriaId = ent.Id, ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId });
        foreach (var x in e.Anexos.Where(x => x.Id == 0))
            db.ArchivosHallazgoAuditoria.Add(new ArchivoHallazgoAuditoria { HallazgoAuditoriaId = ent.Id, NombreOriginal = x.NombreOriginal, RutaArchivo = x.RutaArchivo, Descripcion = Limpio(x.Descripcion) });
        var desc = e.Anexos.Where(x => x.Id > 0).ToDictionary(x => x.Id, x => Limpio(x.Descripcion));
        foreach (var x in await db.ArchivosHallazgoAuditoria.Where(x => desc.Keys.Contains(x.Id)).ToListAsync())
            x.Descripcion = desc[x.Id];
        var ahora = DateTime.Now;
        var idsQuitar = e.AnexosQuitados.Where(x => x.Id > 0).Select(x => x.Id).ToList();
        foreach (var x in await db.ArchivosHallazgoAuditoria.Where(x => idsQuitar.Contains(x.Id)).ToListAsync())
        { x.Eliminado = true; x.FechaEliminacion = ahora; }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return ent.Id;
    }

    public static async Task CerrarHallazgoAsync(AppDbContext db, int id, string? por, string? observacion, DateTime fecha)
    {
        var h = await db.HallazgosAuditoria.FirstAsync(x => x.Id == id);
        h.FechaCierre = fecha; h.CerradoPor = Limpio(por); h.ObservacionCierre = Limpio(observacion);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public static async Task ReabrirHallazgoAsync(AppDbContext db, int id)
    {
        var h = await db.HallazgosAuditoria.FirstAsync(x => x.Id == id);
        h.FechaCierre = null; h.CerradoPor = null; h.ObservacionCierre = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // ---------- Vínculo con planes de acción ----------
    public static async Task LigarPlanAsync(AppDbContext db, IEnumerable<int> idsHallazgo, int planId)
    {
        var ya = await db.HallazgosPlan.Where(r => r.PlanAccionId == planId).Select(r => r.HallazgoAuditoriaId).ToListAsync();
        foreach (var h in idsHallazgo.Distinct().Where(h => !ya.Contains(h)))
            db.HallazgosPlan.Add(new HallazgoPlan { HallazgoAuditoriaId = h, PlanAccionId = planId });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // Quitar el vínculo es configuración (borrado directo): el plan y el hallazgo quedan.
    public static async Task DesligarPlanAsync(AppDbContext db, int hallazgoId, int planId)
    {
        db.HallazgosPlan.RemoveRange(await db.HallazgosPlan.Where(r => r.HallazgoAuditoriaId == hallazgoId && r.PlanAccionId == planId).ToListAsync());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // ---------- Papelera (misma marca de tiempo para lo que se va junto) ----------
    public static async Task ProgramaAPapeleraAsync(AppDbContext db, int id)
    {
        var ahora = DateTime.Now;
        var p = await db.ProgramasAuditoria.FirstAsync(x => x.Id == id);
        p.Eliminado = true; p.FechaEliminacion = ahora;
        var auds = await db.Auditorias.Where(a => a.ProgramaAuditoriaId == id).ToListAsync();
        foreach (var a in auds) { a.Eliminado = true; a.FechaEliminacion = ahora; }
        var idsA = auds.Select(a => a.Id).ToList();
        foreach (var h in await db.HallazgosAuditoria.Where(h => idsA.Contains(h.AuditoriaId)).ToListAsync()) { h.Eliminado = true; h.FechaEliminacion = ahora; }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public static async Task AuditoriaAPapeleraAsync(AppDbContext db, int id)
    {
        var ahora = DateTime.Now;
        var a = await db.Auditorias.FirstAsync(x => x.Id == id);
        a.Eliminado = true; a.FechaEliminacion = ahora;
        foreach (var h in await db.HallazgosAuditoria.Where(h => h.AuditoriaId == id).ToListAsync()) { h.Eliminado = true; h.FechaEliminacion = ahora; }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public static async Task HallazgoAPapeleraAsync(AppDbContext db, int id)
    {
        var h = await db.HallazgosAuditoria.FirstAsync(x => x.Id == id);
        h.Eliminado = true; h.FechaEliminacion = DateTime.Now;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}

// Listas que usan los formularios del módulo (se precargan en la página, nunca en el render).
public sealed class OpcionesAuditoria
{
    public List<string> Procesos { get; set; } = new();
    public List<string> Tipos { get; set; } = new();
    public List<string> Modalidades { get; set; } = new();
    public List<string> Normas { get; set; } = new();
    public List<string> TiposHallazgo { get; set; } = new();
    public List<string> Grados { get; set; } = new();
    public List<string> Personas { get; set; } = new();          // auditores y auditados ya usados
    public List<ElementoTrazado> Torres { get; set; } = new();
    public Dictionary<int, Componente> Componentes { get; set; } = new();
    public List<ItemCatalogo> Localizaciones { get; set; } = new();
    public List<Tiro> Tiros { get; set; } = new();

    public static OpcionesAuditoria Cargar(AppDbContext db)
    {
        List<string> V(string t) => SembradorCatalogosRiesgo.Valores(db, t);
        var auds = db.Auditorias.AsNoTracking().Select(a => new { a.AuditorLider, a.Auditores }).ToList();
        var hall = db.HallazgosAuditoria.AsNoTracking().Select(h => h.Auditor).ToList();
        return new OpcionesAuditoria
        {
            Procesos = V(SembradorCatalogosRiesgo.ProcesoPlan),
            Tipos = V(ServicioAuditorias.TipoAuditoria),
            Modalidades = V(ServicioAuditorias.Modalidad),
            Normas = V(ServicioAuditorias.Norma),
            TiposHallazgo = V(SembradorCatalogosRiesgo.TipoHallazgo),
            Grados = V(ServicioAuditorias.GradoNc),
            Personas = auds.Select(a => a.AuditorLider).Concat(auds.SelectMany(a => ServicioAuditorias.Lista(a.Auditores))).Concat(hall)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
            Torres = db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Codigo).ToList(),
            Componentes = db.Componentes.IgnoreQueryFilters().AsNoTracking().ToList().ToDictionary(c => c.Id),
            Localizaciones = db.Catalogos.AsNoTracking().Where(c => c.Tipo == "Localización" && c.Activo).OrderBy(c => c.Nombre).ToList(),
            Tiros = db.Tiros.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Nombre).ToList(),
        };
    }

    public string TextoLugar(LugarPlan l) => SigWeb.Components.Layout.SelectorLugares.Texto(l, Torres, Componentes, Localizaciones, Tiros);

    // Opciones de una lista más el valor actual si ya no está en ella.
    public static List<string> ConActual(List<string> l, string? actual)
    {
        var r = l.ToList();
        if (!string.IsNullOrWhiteSpace(actual) && !r.Contains(actual, StringComparer.OrdinalIgnoreCase)) r.Insert(0, actual);
        return r;
    }
}
