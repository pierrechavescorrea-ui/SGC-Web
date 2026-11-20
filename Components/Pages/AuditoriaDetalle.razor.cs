using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Auditorias;
using SigWeb.Data.Impacto;
using SigWeb.Data.Navegacion;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Una auditoría en una sola pantalla: pasos de la auditoría, datos clave (detalle desplegable), cifras
// (incluida la eficacia de sus planes) y los hallazgos en tarjetas, cada una con sus planes de acción
// ("＋ Plan de acción" crea uno ligado). El informe imprimible es la otra vista.
// Los hallazgos que no son NC se cierran a mano; las NC se cierran solas
// cuando todos sus planes están cerrados y verificados eficaces (verificación en Planes de acción).
public partial class AuditoriaDetalle
{
    [Inject] private AppDbContext Db { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AnalizadorImpacto Impacto { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [Parameter] public int Id { get; set; }
    [SupplyParameterFromQuery(Name = "hallazgo")] public int? HallazgoQ { get; set; }
    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }

    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private string mensaje = "";
    private bool error, guardando, cargado;

    private Auditoria? aud;
    private ProgramaAuditoria? prog;
    private List<string> lugaresAud = new();
    private List<CambioAuditoria> cambios = new();
    private List<HallazgoAuditoria> hallazgos = new();
    private Dictionary<int, List<ServicioAuditorias.PlanRef>> planesH = new();
    private Dictionary<int, List<string>> lugaresH = new();
    private Dictionary<int, List<ArchivoHallazgoAuditoria>> anexosH = new();
    private List<(int Id, string Etiqueta)> planesLigables = new();
    // Planes de la auditoría: los creados sobre ella (PlanAccion.AuditoriaId) y los de sus hallazgos.
    private sealed record PlanAuditoria(int Id, string Codigo, DateTime Fecha, string Tipo, string Descripcion, EstadoAccionPlan Estado, bool? Eficaz, DateTime? Programada, int Acciones, int Cerradas, List<string> Hallazgos);
    private List<PlanAuditoria> planesAud = new();
    private OpcionesAuditoria op = new();
    private bool informe, verMas;
    // Informe para entregar: se lee solo al mostrarlo (y se relee con cada Cargar mientras se ve).
    private InformeAuditoria? datosInforme;
    private static readonly EstadoAuditoria[] Pasos = { EstadoAuditoria.Programada, EstadoAuditoria.EnEjecucion, EstadoAuditoria.InformeEmitido, EstadoAuditoria.Cerrada };
    private bool PuedeHallazgos => aud is not null && aud.Estado is not (EstadoAuditoria.Cerrada or EstadoAuditoria.Cancelada);
    // Cifras de la cabecera (calculadas en Cargar).
    private int kAbiertos, kSinPlan, kPorVerificar, kVencidas, kEficaces, kVerificados;
    private bool irAHallazgo;
    private int? selHallazgo;
    private HashSet<int> marcados = new();
    private int? cargadoId;

    protected override async Task OnParametersSetAsync()
    {
        if (cargadoId == Id) return;
        cargadoId = Id;
        informe = VistaQ == "informe";
        selHallazgo = HallazgoQ;
        irAHallazgo = HallazgoQ is not null;
        await Cargar();
        cargado = true;
    }

    private async Task Cargar()
    {
        await _dbLock.WaitAsync();
        try
        {
            aud = await Db.Auditorias.AsNoTracking().FirstOrDefaultAsync(a => a.Id == Id);
            if (aud is null) return;
            prog = await Db.ProgramasAuditoria.AsNoTracking().FirstOrDefaultAsync(p => p.Id == aud.ProgramaAuditoriaId);
            op = OpcionesAuditoria.Cargar(Db);
            lugaresAud = (await Db.LugaresAuditoria.AsNoTracking().Where(l => l.AuditoriaId == Id).ToListAsync())
                .Select(l => op.TextoLugar(new LugarPlan { ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId })).ToList();
            cambios = await Db.CambiosAuditoria.AsNoTracking().Where(c => c.AuditoriaId == Id).OrderByDescending(c => c.Fecha).ToListAsync();
            hallazgos = await Db.HallazgosAuditoria.AsNoTracking().Where(h => h.AuditoriaId == Id).OrderBy(h => h.Codigo).ToListAsync();
            var idsH = hallazgos.Select(h => h.Id).ToList();
            planesH = ServicioAuditorias.PlanesDeHallazgos(Db, idsH);
            lugaresH = (await Db.LugaresHallazgoAuditoria.AsNoTracking().Where(l => idsH.Contains(l.HallazgoAuditoriaId)).ToListAsync())
                .GroupBy(l => l.HallazgoAuditoriaId)
                .ToDictionary(g => g.Key, g => g.Select(l => op.TextoLugar(new LugarPlan { ElementoTrazadoId = l.ElementoTrazadoId, ComponenteId = l.ComponenteId, ItemCatalogoId = l.ItemCatalogoId, TiroId = l.TiroId })).ToList());
            anexosH = (await Db.ArchivosHallazgoAuditoria.AsNoTracking().Where(x => idsH.Contains(x.HallazgoAuditoriaId)).OrderBy(x => x.Id).ToListAsync())
                .GroupBy(x => x.HallazgoAuditoriaId).ToDictionary(g => g.Key, g => g.ToList());
            planesLigables = (await Db.PlanesAccion.AsNoTracking().OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id)
                    .Select(p => new { p.Id, p.Codigo, p.DescripcionHallazgo }).Take(500).ToListAsync())
                .Select(p => (p.Id, $"{p.Codigo} — {(p.DescripcionHallazgo.Length > 70 ? p.DescripcionHallazgo[..70] + "…" : p.DescripcionHallazgo)}")).ToList();
            var codH = hallazgos.ToDictionary(h => h.Id, h => h.Codigo);
            var relH = await Db.HallazgosPlan.AsNoTracking().Where(r => idsH.Contains(r.HallazgoAuditoriaId)).ToListAsync();
            var idsRel = relH.Select(r => r.PlanAccionId).Distinct().ToList();
            var pls = await Db.PlanesAccion.AsNoTracking().Where(p => p.AuditoriaId == Id || idsRel.Contains(p.Id)).ToListAsync();
            var idsP = pls.Select(p => p.Id).ToList();
            var accs = await Db.AccionesPlan.AsNoTracking().Where(a => idsP.Contains(a.PlanAccionId)).ToListAsync();
            planesAud = pls.Select(p =>
            {
                var ac = accs.Where(a => a.PlanAccionId == p.Id).ToList();
                return new PlanAuditoria(p.Id, p.Codigo, p.Fecha, p.TipoHallazgo, p.DescripcionHallazgo, EscalaAccion.EstadoPlan(ac), p.Eficaz, p.FechaProgramadaEficacia,
                    ac.Count, ac.Count(a => a.Estado == EstadoAccionPlan.Cerrada),
                    relH.Where(r => r.PlanAccionId == p.Id && codH.ContainsKey(r.HallazgoAuditoriaId)).Select(r => codH[r.HallazgoAuditoriaId]).OrderBy(c => c).ToList());
            }).OrderBy(p => p.Codigo).ToList();
            var efis = planesAud.Select(p => EscalaEficacia.De(p.Eficaz, p.Programada, p.Estado)).ToList();
            kPorVerificar = efis.Count(e => e is EstadoEficacia.PorProgramar or EstadoEficacia.Programada or EstadoEficacia.Vencida);
            kVencidas = efis.Count(e => e == EstadoEficacia.Vencida);
            kEficaces = efis.Count(e => e == EstadoEficacia.Eficaz);
            kVerificados = kEficaces + efis.Count(e => e == EstadoEficacia.NoEficaz);
            kAbiertos = hallazgos.Count(h => EstadoDe(h) != EstadoHallazgoAuditoria.Cerrado);
            kSinPlan = hallazgos.Count(h => EstadoDe(h) == EstadoHallazgoAuditoria.Abierto && !h.Tipo.Equals("Fortaleza", StringComparison.OrdinalIgnoreCase)
                && (ServicioAuditorias.EsNoConformidad(h.Tipo) || h.Tipo.StartsWith("Observ", StringComparison.OrdinalIgnoreCase)));
            datosInforme = informe ? await InformeAuditoria.CargarAsync(Db, Id, op) : null;
            marcados.RemoveWhere(m => hallazgos.All(h => h.Id != m));
            if (selHallazgo is int s && hallazgos.All(h => h.Id != s)) selHallazgo = null;
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!cargado || aud is null) return;
        if (avisoPlanLeidoId != Id)
        {
            avisoPlanLeidoId = Id;
            var claveAviso = $"sigweb.auditoria.{Id}.planCreado";
            var aviso = await JS.InvokeAsync<string?>("sessionStorage.getItem", claveAviso);
            if (!string.IsNullOrEmpty(aviso))
            {
                await JS.InvokeVoidAsync("sessionStorage.removeItem", claveAviso);
                mensaje = aviso;
                error = false;
                StateHasChanged();
            }
        }
        var ruta = $"/auditoria-interna/{Id}";
        if (irAHallazgo && selHallazgo is int ih)
        {
            irAHallazgo = false;
            try { await JS.InvokeVoidAsync("sigIrA", $"au-h-{ih}"); } catch { }
        }
        var clave = $"{informe}|{selHallazgo}";
        if (clave == recordado) return;
        recordado = clave;
        await JS.RecordarVistaAsync(ruta, new Dictionary<string, object?>
        {
            ["vista"] = informe ? "informe" : null,
            ["hallazgo"] = selHallazgo,
        });
    }
    private string? recordado;
    private int? avisoPlanLeidoId;

    private Task Atras() => JS.VolverAsync(prog is null ? "/auditoria-interna" : $"/auditoria-interna?programa={prog.Id}");

    private void CerrarFormularios() { formAud = null; formHall = null; formCierre = null; elim = null; }

    // ---------- Cálculos ----------
    private EstadoHallazgoAuditoria EstadoDe(HallazgoAuditoria h) =>
        ServicioAuditorias.EstadoDe(h, planesH.TryGetValue(h.Id, out var l) ? l : null);

    private static string TextoPlan(ServicioAuditorias.PlanRef p) => EscalaAccion.Nombre(p.Estado) +
        (p.Eficaz is bool ef ? (ef ? " · eficaz" : " · no eficaz") : p.Estado == EstadoAccionPlan.Cerrada ? " · por verificar" : "");

    private bool TodosCerrados => hallazgos.All(h => EstadoDe(h) == EstadoHallazgoAuditoria.Cerrado);

    private List<string> Equipo => new[] { aud?.AuditorLider }.Concat(ServicioAuditorias.Lista(aud?.Auditores))
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private string Rango => aud?.FechaInicio is null ? "" : aud.FechaFin is null || aud.FechaFin == aud.FechaInicio
        ? $"{aud.FechaInicio:dd/MM/yyyy}" : $"{aud.FechaInicio:dd/MM/yyyy} – {aud.FechaFin:dd/MM/yyyy}";

    // ---------- Vista y selección ----------
    private async Task AlternarInforme()
    {
        informe = !informe; CerrarFormularios(); editandoInforme = false;
        if (informe) await Cargar(); else datosInforme = null;
    }
    private void Seleccionar(int id) => selHallazgo = selHallazgo == id ? null : id;
    private void Marcar(int id, bool si) { if (si) marcados.Add(id); else marcados.Remove(id); }

    // ---------- Estado de la auditoría ----------
    private async Task Avanzar(EstadoAuditoria nuevo)
    {
        if (aud is null) return;
        await Intentar(() => ServicioAuditorias.CambiarEstadoAsync(Db, aud.Id, nuevo), $"Auditoría: {EscalaAuditoria.Nombre(nuevo)}.");
        await Cargar();
    }

    // ---------- Editar datos de la auditoría ----------
    private ServicioAuditorias.AuditoriaEditable? formAud;

    private void EditarAuditoria()
    {
        if (aud is null) return;
        CerrarFormularios();
        informe = false;
        formAud = ServicioAuditorias.CargarAuditoria(Db, aud.Id);
    }

    private async Task GuardarAuditoria()
    {
        if (formAud is null) return;
        var f = formAud;
        await Intentar(() => ServicioAuditorias.GuardarAuditoriaAsync(Db, f), "Auditoría guardada.");
        if (error) return;
        formAud = null;
        await Cargar();
    }

    // ---------- Informe ----------
    private string? informeConclusiones, informeFortalezas;
    private DateTime? informeFecha;
    private bool editandoInforme;

    private void EditarInforme()
    {
        if (aud is null) return;
        informeConclusiones = aud.Conclusiones; informeFortalezas = aud.Fortalezas; informeFecha = aud.FechaInforme ?? DateTime.Today;
        editandoInforme = true;
    }

    private async Task GuardarInforme()
    {
        if (aud is null) return;
        var e = ServicioAuditorias.CargarAuditoria(Db, aud.Id);
        e.Auditoria.Conclusiones = informeConclusiones; e.Auditoria.Fortalezas = informeFortalezas; e.Auditoria.FechaInforme = informeFecha;
        await Intentar(() => ServicioAuditorias.GuardarAuditoriaAsync(Db, e), "Informe guardado.");
        if (error) return;
        editandoInforme = false;
        await Cargar();
    }

    private Task Imprimir() => JS.InvokeVoidAsync("print").AsTask();

    // ---------- Hallazgos ----------
    private ServicioAuditorias.HallazgoEditable? formHall;

    private void NuevoHallazgo()
    {
        if (aud is null) return;
        CerrarFormularios();
        informe = false;
        formHall = new ServicioAuditorias.HallazgoEditable
        {
            Hallazgo = new HallazgoAuditoria
            {
                AuditoriaId = aud.Id,
                Fecha = aud.FechaInicio is DateTime fi && fi <= DateTime.Today ? DateTime.Today : aud.FechaInicio ?? DateTime.Today,
                Auditor = aud.AuditorLider,
                Tipo = op.TiposHallazgo.FirstOrDefault(t => ServicioAuditorias.EsNoConformidad(t)) ?? "",
            },
            Normas = ServicioAuditorias.Lista(aud.Normas).Take(1).ToList(),
            Procesos = ServicioAuditorias.Lista(aud.Procesos).Count == 1 ? ServicioAuditorias.Lista(aud.Procesos) : new(),
        };
    }

    private void EditarHallazgo(int id)
    {
        CerrarFormularios();
        formHall = ServicioAuditorias.CargarHallazgo(Db, id);
    }

    private async Task GuardarHallazgo()
    {
        if (formHall is null) return;
        var f = formHall;
        int id = 0;
        await Intentar(async () => id = await ServicioAuditorias.GuardarHallazgoAsync(Db, f), "Hallazgo guardado.");
        if (error) return;
        formHall = null;
        // Primer hallazgo con la auditoría aún programada: pasa a "En ejecución".
        if (aud?.Estado == EstadoAuditoria.Programada)
            await Intentar(() => ServicioAuditorias.CambiarEstadoAsync(Db, aud.Id, EstadoAuditoria.EnEjecucion));
        await Cargar();
    }

    // Plan de acción directo sobre la auditoría (sin hallazgo): queda ligado a ella con su código.
    private void NuevoPlanAuditoria() => Nav.NavigateTo($"planes-accion/nuevo?auditoria={Id}");

    // Abre Planes de acción con el plan prellenado desde uno o varios hallazgos.
    private void AbrirPlan(IEnumerable<int> ids)
    {
        var l = ids.Distinct().ToList();
        if (l.Count == 0) return;
        Nav.NavigateTo($"planes-accion/nuevo?hallazgos={string.Join(",", l)}");
    }

    private int vueltaLigar;

    private async Task Ligar(int hallazgoId, ChangeEventArgs e)
    {
        vueltaLigar++;
        if (!int.TryParse(e.Value?.ToString(), out var planId)) return;
        var ids = marcados.Count > 0 && marcados.Contains(hallazgoId) ? marcados.ToList() : new List<int> { hallazgoId };
        await Intentar(() => ServicioAuditorias.LigarPlanAsync(Db, ids, planId), "Plan de acción ligado.");
        await Cargar();
    }

    private async Task Desligar(int hallazgoId, int planId)
    {
        await Intentar(() => ServicioAuditorias.DesligarPlanAsync(Db, hallazgoId, planId), "Vínculo quitado (el plan se conserva).");
        await Cargar();
    }

    // Cierre manual (hallazgos que no son NC).
    private sealed class Cierre { public int HallazgoId; public DateTime Fecha = DateTime.Today; public string? Por; public string? Observacion; }
    private Cierre? formCierre;

    private void AbrirCierre(int id) { CerrarFormularios(); formCierre = new Cierre { HallazgoId = id, Por = aud?.AuditorLider }; }

    private async Task Cerrar()
    {
        if (formCierre is null) return;
        var c = formCierre;
        await Intentar(() => ServicioAuditorias.CerrarHallazgoAsync(Db, c.HallazgoId, c.Por, c.Observacion, c.Fecha), "Hallazgo cerrado.");
        formCierre = null;
        await Cargar();
    }

    private async Task Reabrir(int id)
    {
        await Intentar(() => ServicioAuditorias.ReabrirHallazgoAsync(Db, id), "Hallazgo reabierto.");
        await Cargar();
    }

    // ---------- Eliminar (Papelera, con impacto antes) ----------
    private sealed class Eliminacion { public string Tipo = ""; public int Id; public string Titulo = ""; public InformeImpacto Informe = new(); public bool Entendido; }
    private Eliminacion? elim;
    private bool BloqueadoPorImpacto => elim is null || (elim.Informe.RequiereEntender && !elim.Entendido);

    private async Task PedirEliminar(string tipo, int id, string titulo)
    {
        CerrarFormularios();
        InformeImpacto inf;
        await _dbLock.WaitAsync();
        try { inf = tipo == "auditoria" ? await Impacto.Auditoria(id) : await Impacto.HallazgoAuditoria(id); }
        finally { _dbLock.Release(); }
        elim = new Eliminacion { Tipo = tipo, Id = id, Titulo = titulo, Informe = inf };
    }

    private async Task ConfirmarEliminar()
    {
        if (elim is null) return;
        var e = elim;
        if (e.Tipo == "auditoria")
        {
            await Intentar(() => ServicioAuditorias.AuditoriaAPapeleraAsync(Db, e.Id), $"«{e.Titulo}» enviada a la Papelera.");
            elim = null;
            if (!error) await Atras();
            return;
        }
        await Intentar(() => ServicioAuditorias.HallazgoAPapeleraAsync(Db, e.Id), $"«{e.Titulo}» enviado a la Papelera.");
        elim = null;
        await Cargar();
    }

    // Resumen del informe: hallazgos por tipo (y grado) y por proceso.
    private List<(string Clave, int N)> PorTipo() => hallazgos
        .GroupBy(h => string.IsNullOrWhiteSpace(h.Grado) ? h.Tipo : $"{h.Tipo} {h.Grado!.ToLowerInvariant()}")
        .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList();

    private List<(string Clave, int N)> PorProceso() => hallazgos
        .SelectMany(h => ServicioAuditorias.Lista(h.Procesos).DefaultIfEmpty("—"))
        .GroupBy(p => p, StringComparer.OrdinalIgnoreCase).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ToList();
}
