using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Auditorias;
using SigWeb.Data.Catalogos;
using SigWeb.Data.Impacto;
using SigWeb.Data.Navegacion;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Auditoría interna (ISO 9001 · 9.2): programa anual (matriz proceso × mes), lista de auditorías y
// lista de todos los hallazgos del programa. El detalle de cada auditoría está en AuditoriaDetalle.
public partial class AuditoriaInterna
{
    [Inject] private AppDbContext Db { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AnalizadorImpacto Impacto { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private EstilosCatalogo Estilos { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "programa")] public int? ProgramaQ { get; set; }
    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }

    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private string mensaje = "";
    private bool error, guardando, cargado;

    private List<ProgramaAuditoria> programas = new();
    private ProgramaAuditoria? prog;
    private List<Auditoria> auditorias = new();
    private List<HallazgoAuditoria> hallazgos = new();
    private Dictionary<int, List<ServicioAuditorias.PlanRef>> planesH = new();
    private Dictionary<int, List<string>> lugaresH = new();
    private Dictionary<int, Auditoria> audPorId = new();
    private OpcionesAuditoria op = new();
    private string vista = "programa";

    // Filtros de la lista de hallazgos.
    private EstadoHallazgoAuditoria? filtroEstado;
    private string filtroTipo = "", buscar = "";

    protected override async Task OnParametersSetAsync()
    {
        if (cargado) return;
        if (VistaQ is "auditorias" or "hallazgos") vista = VistaQ;
        await Cargar(ProgramaQ);
        cargado = true;
    }

    private async Task Cargar(int? programaId = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            programas = await Db.ProgramasAuditoria.AsNoTracking().OrderByDescending(p => p.Anio).ThenByDescending(p => p.Id).ToListAsync();
            var id = programaId ?? prog?.Id;
            prog = programas.FirstOrDefault(p => p.Id == id) ?? programas.FirstOrDefault(p => p.Anio == DateTime.Today.Year) ?? programas.FirstOrDefault();
            auditorias = prog is null ? new() : await Db.Auditorias.AsNoTracking().Where(a => a.ProgramaAuditoriaId == prog.Id).OrderBy(a => a.Mes).ThenBy(a => a.Codigo).ToListAsync();
            audPorId = auditorias.ToDictionary(a => a.Id);
            var idsA = auditorias.Select(a => a.Id).ToList();
            hallazgos = await Db.HallazgosAuditoria.AsNoTracking().Where(h => idsA.Contains(h.AuditoriaId)).OrderBy(h => h.Codigo).ToListAsync();
            var idsH = hallazgos.Select(h => h.Id).ToList();
            planesH = ServicioAuditorias.PlanesDeHallazgos(Db, idsH);
            op = OpcionesAuditoria.Cargar(Db);
            lugaresH = (await Db.LugaresHallazgoAuditoria.AsNoTracking().Where(l => idsH.Contains(l.HallazgoAuditoriaId)).ToListAsync())
                .GroupBy(l => l.HallazgoAuditoriaId)
                .ToDictionary(g => g.Key, g => g.Select(l => op.TextoLugar(new LugarPlan { ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId })).ToList());
        }
        finally { _dbLock.Release(); }
    }

    private async Task Intentar(Func<Task> accion, string? ok = null)
    {
        guardando = true;
        try
        {
            await _dbLock.WaitAsync();
            try { await accion(); }
            finally { _dbLock.Release(); }
            if (ok is not null) { mensaje = ok; error = false; }
        }
        catch (Exception ex) { Db.ChangeTracker.Clear(); mensaje = ex.InnerException?.Message ?? ex.Message; error = true; }
        finally { guardando = false; }
    }

    private async Task Recordar() =>
        await JS.RecordarVistaAsync("/auditoria-interna", new Dictionary<string, object?>
        {
            ["programa"] = prog?.Id,
            ["vista"] = vista == "programa" ? null : vista,
        });

    private async Task CambiarVista(string v) { vista = v; await Recordar(); }

    private async Task VerPrograma(int id) { CerrarFormularios(); await Cargar(id); await Recordar(); }

    private void CerrarFormularios() { formProg = null; formAprobar = null; formAud = null; elim = null; }

    // ---------- Cálculos (precargados: el render no consulta la base) ----------
    private EstadoHallazgoAuditoria EstadoDe(HallazgoAuditoria h) =>
        ServicioAuditorias.EstadoDe(h, planesH.TryGetValue(h.Id, out var l) ? l : null);

    private static bool Ejecutada(Auditoria a) => a.Estado is EstadoAuditoria.InformeEmitido or EstadoAuditoria.Cerrada;

    // % de cumplimiento del programa = ejecutadas / programadas sin las canceladas.
    private int? Cumplimiento
    {
        get
        {
            var base_ = auditorias.Count(a => a.Estado != EstadoAuditoria.Cancelada);
            return base_ == 0 ? null : (int)Math.Round(100.0 * auditorias.Count(Ejecutada) / base_);
        }
    }

    private List<string> FilasProcesos()
    {
        var usados = auditorias.SelectMany(a => ServicioAuditorias.Lista(a.Procesos)).ToList();
        var l = op.Procesos.Concat(usados).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (l.Count == 0 || auditorias.Any(a => ServicioAuditorias.Lista(a.Procesos).Count == 0)) l.Add("");
        return l;
    }

    private IEnumerable<Auditoria> EnCelda(string proceso, int mes) => auditorias.Where(a => a.Mes == mes &&
        (proceso == "" ? ServicioAuditorias.Lista(a.Procesos).Count == 0 : ServicioAuditorias.Lista(a.Procesos).Contains(proceso, StringComparer.OrdinalIgnoreCase)));

    private string EstiloEstado(EstadoAuditoria e)
    {
        var s = Estilos.Estilo(ServicioAuditorias.EstadoAuditoriaCat, EscalaAuditoria.Nombre(e));
        return s is null ? "" : $"background:{s.ColorFondo};color:{s.ColorTexto}";
    }

    private IEnumerable<HallazgoAuditoria> HallazgosMostrados => hallazgos.Where(h =>
        (filtroEstado is null || EstadoDe(h) == filtroEstado) &&
        (filtroTipo == "" || string.Equals(h.Tipo, filtroTipo, StringComparison.OrdinalIgnoreCase)) &&
        (buscar.Length == 0 || (h.Codigo + " " + h.Descripcion + " " + h.Evidencia + " " + h.Requisito + " " + h.Numerales + " " + h.Procesos + " " + h.Auditor + " " +
            string.Join(" ", lugaresH.TryGetValue(h.Id, out var lg) ? lg : new())).Contains(buscar, StringComparison.OrdinalIgnoreCase)));

    private int Conteo(int auditoriaId, bool nc) => hallazgos.Count(h => h.AuditoriaId == auditoriaId && ServicioAuditorias.EsNoConformidad(h.Tipo) == nc);
    private int Abiertos(int auditoriaId) => hallazgos.Count(h => h.AuditoriaId == auditoriaId && EstadoDe(h) != EstadoHallazgoAuditoria.Cerrado);

    private static string Rango(Auditoria a) =>
        a.FechaInicio is null ? "" : a.FechaFin is null || a.FechaFin == a.FechaInicio ? $"{a.FechaInicio:dd/MM/yyyy}" : $"{a.FechaInicio:dd/MM} – {a.FechaFin:dd/MM/yyyy}";

    private void AbrirAuditoria(int id) => Nav.NavigateTo($"auditoria-interna/{id}");

    // ---------- Programa ----------
    private ProgramaAuditoria? formProg;

    private void NuevoPrograma()
    {
        CerrarFormularios();
        var anio = programas.Count == 0 ? DateTime.Today.Year : Math.Max(DateTime.Today.Year, programas.Max(p => p.Anio) + (programas.Any(p => p.Anio == DateTime.Today.Year) ? 1 : 0));
        formProg = new ProgramaAuditoria { Anio = anio, Titulo = $"Programa de auditorías {anio}", Normas = string.Join("\n", op.Normas) };
        normasProg = ServicioAuditorias.Lista(formProg.Normas);
    }

    private List<string> normasProg = new();

    private void EditarPrograma()
    {
        if (prog is null) return;
        CerrarFormularios();
        formProg = new ProgramaAuditoria
        {
            Id = prog.Id, Anio = prog.Anio, Proyecto = prog.Proyecto, Titulo = prog.Titulo, Normas = prog.Normas, Objetivos = prog.Objetivos,
            Responsable = prog.Responsable, Recursos = prog.Recursos, Estado = prog.Estado
        };
        normasProg = ServicioAuditorias.Lista(prog.Normas);
    }

    private async Task GuardarPrograma()
    {
        if (formProg is null) return;
        var f = formProg;
        if (string.IsNullOrWhiteSpace(f.Titulo)) { mensaje = "Escribe el título del programa."; error = true; return; }
        int id = 0;
        await Intentar(async () =>
        {
            ProgramaAuditoria ent;
            if (f.Id == 0) { ent = new ProgramaAuditoria(); Db.ProgramasAuditoria.Add(ent); }
            else ent = await Db.ProgramasAuditoria.FirstAsync(p => p.Id == f.Id);
            ent.Anio = f.Anio;
            ent.Proyecto = string.IsNullOrWhiteSpace(f.Proyecto) ? null : f.Proyecto.Trim();
            ent.Titulo = f.Titulo.Trim();
            ent.Normas = ServicioAuditorias.Unir(normasProg);
            ent.Objetivos = string.IsNullOrWhiteSpace(f.Objetivos) ? null : f.Objetivos.Trim();
            ent.Responsable = string.IsNullOrWhiteSpace(f.Responsable) ? null : f.Responsable.Trim();
            ent.Recursos = string.IsNullOrWhiteSpace(f.Recursos) ? null : f.Recursos.Trim();
            await Db.SaveChangesAsync();
            id = ent.Id;
            Db.ChangeTracker.Clear();
        }, "Programa guardado.");
        if (error) return;
        formProg = null;
        await Cargar();
        await Recordar();
    }

    // Aprobar (Borrador → Aprobado) y cambiar estado del programa.
    private sealed class Aprobacion { public string? Aprobo; public DateTime Fecha = DateTime.Today; }
    private Aprobacion? formAprobar;

    private void AbrirAprobar() { CerrarFormularios(); formAprobar = new Aprobacion { Aprobo = prog?.Responsable }; }

    private async Task Aprobar()
    {
        if (prog is null || formAprobar is null) return;
        if (string.IsNullOrWhiteSpace(formAprobar.Aprobo)) { mensaje = "Escribe quién aprueba el programa."; error = true; return; }
        var id = prog.Id; var a = formAprobar;
        await Intentar(async () =>
        {
            var p = await Db.ProgramasAuditoria.FirstAsync(x => x.Id == id);
            p.Estado = auditorias.Any(x => x.Estado != EstadoAuditoria.Programada && x.Estado != EstadoAuditoria.Cancelada) ? EstadoProgramaAuditoria.EnEjecucion : EstadoProgramaAuditoria.Aprobado;
            p.Aprobo = a.Aprobo!.Trim(); p.FechaAprobacion = a.Fecha;
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }, "Programa aprobado.");
        formAprobar = null;
        await Cargar(id);
    }

    private async Task CambiarEstadoPrograma(string estado)
    {
        if (prog is null) return;
        var id = prog.Id;
        await Intentar(async () =>
        {
            var p = await Db.ProgramasAuditoria.FirstAsync(x => x.Id == id);
            p.Estado = estado;
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }, $"Programa: {estado}.");
        await Cargar(id);
    }

    // ---------- Nueva auditoría ----------
    private ServicioAuditorias.AuditoriaEditable? formAud;

    private void NuevaAuditoria(string? proceso = null, int? mes = null)
    {
        if (prog is null) return;
        CerrarFormularios();
        formAud = new ServicioAuditorias.AuditoriaEditable
        {
            Auditoria = new Auditoria
            {
                ProgramaAuditoriaId = prog.Id,
                Codigo = ServicioAuditorias.SiguienteCodigoAuditoria(Db, prog.Anio),
                Mes = mes ?? (prog.Anio == DateTime.Today.Year ? DateTime.Today.Month : 1),
                Tipo = op.Tipos.FirstOrDefault(t => t.Equals("Interna", StringComparison.OrdinalIgnoreCase)) ?? "",
                Proyecto = prog.Proyecto,
            },
            Normas = ServicioAuditorias.Lista(prog.Normas),
            Procesos = string.IsNullOrEmpty(proceso) ? new() : new() { proceso },
        };
    }

    private async Task GuardarAuditoria()
    {
        if (formAud is null) return;
        int id = 0;
        var f = formAud;
        await Intentar(async () => id = await ServicioAuditorias.GuardarAuditoriaAsync(Db, f));
        if (error) return;
        formAud = null;
        await Cargar();
    }

    // ---------- Eliminar programa (Papelera, con impacto antes) ----------
    private sealed class Eliminacion { public int Id; public string Titulo = ""; public InformeImpacto Informe = new(); public bool Entendido; }
    private Eliminacion? elim;
    private bool BloqueadoPorImpacto => elim is null || (elim.Informe.RequiereEntender && !elim.Entendido);

    private async Task PedirEliminarPrograma()
    {
        if (prog is null) return;
        CerrarFormularios();
        InformeImpacto inf;
        await _dbLock.WaitAsync();
        try { inf = await Impacto.ProgramaAuditoria(prog.Id); }
        finally { _dbLock.Release(); }
        elim = new Eliminacion { Id = prog.Id, Titulo = $"{prog.Anio} · {prog.Titulo}", Informe = inf };
    }

    private async Task ConfirmarEliminar()
    {
        if (elim is null) return;
        var e = elim;
        await Intentar(() => ServicioAuditorias.ProgramaAPapeleraAsync(Db, e.Id), $"«{e.Titulo}» enviado a la Papelera.");
        elim = null;
        prog = null;
        await Cargar();
        await Recordar();
    }
}
