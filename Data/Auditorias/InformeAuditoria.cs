using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Auditorias;

// Datos del informe imprimible de una auditoría (ISO 19011:2018 6.5.1): todo lo que alguien ajeno
// necesita para entender qué se auditó, qué se encontró y qué se hizo con cada hallazgo.
// Se lee de una vez (nunca desde el render).
public sealed class InformeAuditoria
{
    public Auditoria Auditoria { get; init; } = new();
    public ProgramaAuditoria? Programa { get; init; }
    public List<string> Lugares { get; init; } = new();
    public List<CambioAuditoria> Cambios { get; init; } = new();
    public List<HallazgoInforme> Hallazgos { get; init; } = new();
    public List<PlanInforme> Planes { get; init; } = new();
    public DateTime Generado { get; init; } = DateTime.Now;

    public sealed class HallazgoInforme
    {
        public HallazgoAuditoria H { get; init; } = new();
        public EstadoHallazgoAuditoria Estado { get; init; }
        public List<string> Lugares { get; init; } = new();
        public List<ArchivoHallazgoAuditoria> Fotos { get; init; } = new();
        public List<ArchivoHallazgoAuditoria> Anexos { get; init; } = new();
        public List<int> Planes { get; init; } = new();
        public bool EsNc => ServicioAuditorias.EsNoConformidad(H.Tipo);
    }

    public sealed class PlanInforme
    {
        public PlanAccion P { get; init; } = new();
        public EstadoAccionPlan Estado { get; init; }
        public EstadoEficacia Eficacia { get; init; }
        public List<AccionPlan> Acciones { get; init; } = new();
        public List<ArchivoEficaciaPlan> Soportes { get; init; } = new();
        public List<string> Hallazgos { get; init; } = new();   // códigos
        public bool SobreAuditoria => Hallazgos.Count == 0;
    }

    public List<string> Equipo => new[] { Auditoria.AuditorLider }.Concat(ServicioAuditorias.Lista(Auditoria.Auditores))
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public string Fechas => Auditoria.FechaInicio is null ? EscalaAuditoria.Mes(Auditoria.Mes) + " (programada)"
        : Auditoria.FechaFin is null || Auditoria.FechaFin == Auditoria.FechaInicio ? $"{Auditoria.FechaInicio:dd/MM/yyyy}"
        : $"{Auditoria.FechaInicio:dd/MM/yyyy} – {Auditoria.FechaFin:dd/MM/yyyy}";

    // Resumen.
    public List<(string Clave, int N)> PorTipo() => Hallazgos
        .GroupBy(h => h.H.Tipo + (string.IsNullOrWhiteSpace(h.H.Grado) ? "" : " " + h.H.Grado!.ToLowerInvariant()))
        .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key).ToList();

    public List<(string Clave, int N)> PorProceso() => Hallazgos
        .SelectMany(h => ServicioAuditorias.Lista(h.H.Procesos).DefaultIfEmpty("(sin proceso)"))
        .GroupBy(p => p).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key).ToList();

    public int NoConformidades => Hallazgos.Count(h => h.EsNc);
    public int Abiertos => Hallazgos.Count(h => h.Estado != EstadoHallazgoAuditoria.Cerrado);
    public int Eficaces => Planes.Count(p => p.Eficacia == EstadoEficacia.Eficaz);
    public int Verificados => Planes.Count(p => p.Eficacia is EstadoEficacia.Eficaz or EstadoEficacia.NoEficaz);
    public int PorVerificar => Planes.Count(p => p.Eficacia is EstadoEficacia.PorProgramar or EstadoEficacia.Programada or EstadoEficacia.Vencida);

    public PlanInforme? Plan(int id) => Planes.FirstOrDefault(p => p.P.Id == id);

    public static async Task<InformeAuditoria?> CargarAsync(AppDbContext db, int auditoriaId, OpcionesAuditoria op)
    {
        var aud = await db.Auditorias.AsNoTracking().FirstOrDefaultAsync(a => a.Id == auditoriaId);
        if (aud is null) return null;
        var prog = await db.ProgramasAuditoria.AsNoTracking().FirstOrDefaultAsync(p => p.Id == aud.ProgramaAuditoriaId);
        string Lugar(int? el, int? co, int? it, int? ti) =>
            op.TextoLugar(new LugarPlan { ElementoTrazadoId = el, ComponenteId = co, ItemCatalogoId = it, TiroId = ti });
        var lugares = (await db.LugaresAuditoria.AsNoTracking().Where(l => l.AuditoriaId == auditoriaId).ToListAsync())
            .Select(l => Lugar(l.ElementoTrazadoId, l.ComponenteId, l.ItemCatalogoId, l.TiroId)).ToList();
        var cambios = await db.CambiosAuditoria.AsNoTracking().Where(c => c.AuditoriaId == auditoriaId).OrderBy(c => c.Fecha).ToListAsync();

        var hs = await db.HallazgosAuditoria.AsNoTracking().Where(h => h.AuditoriaId == auditoriaId).OrderBy(h => h.Codigo).ToListAsync();
        var idsH = hs.Select(h => h.Id).ToList();
        var refs = ServicioAuditorias.PlanesDeHallazgos(db, idsH);
        var lugH = (await db.LugaresHallazgoAuditoria.AsNoTracking().Where(l => idsH.Contains(l.HallazgoAuditoriaId)).ToListAsync())
            .GroupBy(l => l.HallazgoAuditoriaId)
            .ToDictionary(g => g.Key, g => g.Select(l => Lugar(l.ElementoTrazadoId, l.ComponenteId, l.ItemCatalogoId, l.TiroId)).ToList());
        var arch = (await db.ArchivosHallazgoAuditoria.AsNoTracking().Where(x => idsH.Contains(x.HallazgoAuditoriaId)).OrderBy(x => x.Id).ToListAsync())
            .GroupBy(x => x.HallazgoAuditoriaId).ToDictionary(g => g.Key, g => g.ToList());

        var relH = await db.HallazgosPlan.AsNoTracking().Where(r => idsH.Contains(r.HallazgoAuditoriaId)).ToListAsync();
        var idsRel = relH.Select(r => r.PlanAccionId).Distinct().ToList();
        var pls = await db.PlanesAccion.AsNoTracking().Where(p => p.AuditoriaId == auditoriaId || idsRel.Contains(p.Id)).OrderBy(p => p.Codigo).ToListAsync();
        var idsP = pls.Select(p => p.Id).ToList();
        var accs = await db.AccionesPlan.AsNoTracking().Where(a => idsP.Contains(a.PlanAccionId)).OrderBy(a => a.Orden).ThenBy(a => a.Id).ToListAsync();
        var sop = (await db.ArchivosEficaciaPlan.AsNoTracking().Where(x => idsP.Contains(x.PlanAccionId)).OrderBy(x => x.Id).ToListAsync())
            .GroupBy(x => x.PlanAccionId).ToDictionary(g => g.Key, g => g.ToList());
        var codH = hs.ToDictionary(h => h.Id, h => h.Codigo);

        return new InformeAuditoria
        {
            Auditoria = aud,
            Programa = prog,
            Lugares = lugares,
            Cambios = cambios,
            Hallazgos = hs.Select(h =>
            {
                var a = arch.TryGetValue(h.Id, out var l) ? l : new();
                return new HallazgoInforme
                {
                    H = h,
                    Estado = ServicioAuditorias.EstadoDe(h, refs.TryGetValue(h.Id, out var r) ? r : null),
                    Lugares = lugH.TryGetValue(h.Id, out var lg) ? lg : new(),
                    Fotos = a.Where(x => SigWeb.Data.Planes.AlmacenAnexos.EsImagen(x.RutaArchivo)).ToList(),
                    Anexos = a.Where(x => !SigWeb.Data.Planes.AlmacenAnexos.EsImagen(x.RutaArchivo)).ToList(),
                    Planes = relH.Where(x => x.HallazgoAuditoriaId == h.Id).Select(x => x.PlanAccionId).Where(idsP.Contains).ToList(),
                };
            }).ToList(),
            Planes = pls.Select(p =>
            {
                var ac = accs.Where(a => a.PlanAccionId == p.Id).ToList();
                var est = EscalaAccion.EstadoPlan(ac);
                return new PlanInforme
                {
                    P = p,
                    Estado = est,
                    Eficacia = EscalaEficacia.De(p.Eficaz, p.FechaProgramadaEficacia, est),
                    Acciones = ac,
                    Soportes = sop.TryGetValue(p.Id, out var s) ? s : new(),
                    Hallazgos = relH.Where(r => r.PlanAccionId == p.Id && codH.ContainsKey(r.HallazgoAuditoriaId)).Select(r => codH[r.HallazgoAuditoriaId]).OrderBy(c => c).ToList(),
                };
            }).ToList(),
        };
    }
}
