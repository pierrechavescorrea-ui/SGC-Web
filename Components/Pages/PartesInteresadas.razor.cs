using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Components.Layout;
using SigWeb.Data;
using SigWeb.Data.Impacto;
using SigWeb.Data.Navegacion;
using SigWeb.Data.PartesInteresadas;
using SigWeb.Data.Riesgos;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Matriz de partes interesadas: versiones (solo el borrador se edita), mapa influencia × impacto
// (arrastrar en borrador), programación y rondas de seguimiento, planes de acción desde un seguimiento.
public partial class PartesInteresadas : IAsyncDisposable
{
    [Inject] private AppDbContext Db { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AnalizadorImpacto Impacto { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private SigWeb.Data.Planes.PlanesAccionServicio ServicioPlanes { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "v")] public int? VersionQ { get; set; }
    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }
    [SupplyParameterFromQuery(Name = "parte")] public Guid? ParteQ { get; set; }
    [SupplyParameterFromQuery(Name = "disenarVista")] public string? DisenarQ { get; set; }

    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private DotNetObjectReference<PartesInteresadas>? refNet;
    private ElementReference mapaSvg;
    private VistaWidgets? panelVista;
    private bool TarjetasEnMapa => vista == "mapa" &&
        (panelVista?.Personalizada != true || panelVista.Presentes.Any(w => w.Tipo == "tarjetas"));

    private bool MapaEnVista => vista == "mapa" && ver is not null &&
        (panelVista?.Personalizada != true || panelVista.Presentes.Any(w => w.Tipo == "mapa"));

    private void AlCambiarWidgets() => StateHasChanged();

    private string vista = "mapa";
    private string mensaje = "";
    private bool error, guardando;

    // ---------- Datos (precargados; el render nunca consulta la base) ----------
    private List<VersionPartesInteresadas> versiones = new();
    private VersionPartesInteresadas? ver, vigente, borrador;
    private Dictionary<int, List<ParteInteresada>> partesPorVersion = new();
    private List<ParteInteresada> partes = new();
    private List<SeguimientoParteInteresada> seguimientos = new();
    private Dictionary<Guid, ProgramacionSeguimientoParte> programaciones = new();
    private Dictionary<Guid, SituacionParte> situacion = new();
    private Dictionary<int, List<PlanRef>> planesPorSeguimiento = new();
    private sealed record PlanRef(int Id, string Codigo, EstadoAccionPlan Estado);

    private bool EsBorrador => ver?.Estado == EstadoVersionPartes.Borrador;
    private ParteInteresada? Sel => sel is Guid g ? partes.FirstOrDefault(p => p.Clave == g) : null;
    private Guid? sel;

    // ---------- Filtros ----------
    private CuadrantePartes? filtroCuadrante;
    private EstadoSeguimientoParte? filtroEstado;
    private string buscar = "";
    private bool verInactivas;
    private string ordenMapa = "numero";

    // Inactiva = sin seguimiento (no versionado, va en la programación por Clave).
    private bool EsInactiva(Guid c) => programaciones.TryGetValue(c, out var p) && p.Inactiva;
    private IEnumerable<ParteInteresada> Activas => partes.Where(p => !EsInactiva(p.Clave));

    private IEnumerable<ParteInteresada> Mostradas => partes.Where(p =>
        (verInactivas || !EsInactiva(p.Clave)) &&
        (filtroCuadrante is null || ServicioPartesInteresadas.Cuadrante(p.Impacto, p.Influencia) == filtroCuadrante) &&
        (filtroEstado is null || EstadoDe(p.Clave) == filtroEstado) &&
        (buscar.Length == 0 || (p.Nombre + " " + p.Procesos + " " + p.Necesidades + " " + p.Expectativas + " " + p.Estrategias + " " + p.Medicion)
            .Contains(buscar, StringComparison.CurrentCultureIgnoreCase)));

    // Tarjetas del mapa: inactivas al final.
    private List<ParteInteresada> EnTarjetas() => (ordenMapa switch
    {
        "cuadrante" => Mostradas.OrderByDescending(p => (int)ServicioPartesInteresadas.Cuadrante(p.Impacto, p.Influencia)).ThenByDescending(p => p.Impacto).ThenByDescending(p => p.Influencia).ThenBy(p => p.Orden),
        "seguimiento" => Mostradas.OrderBy(p => EstadoDe(p.Clave) switch
            {
                EstadoSeguimientoParte.Vencido => 0, EstadoSeguimientoParte.PorVencer => 1, EstadoSeguimientoParte.AlDia => 2, _ => 3
            }).ThenBy(p => situacion.GetValueOrDefault(p.Clave)?.Proximo ?? DateTime.MaxValue).ThenBy(p => p.Orden),
        "nombre" => Mostradas.OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase),
        _ => Mostradas.OrderBy(p => p.Orden).ThenBy(p => p.Id)
    }).OrderBy(p => EsInactiva(p.Clave)).ToList();

    private (string Etiqueta, int Cantidad, string Clase) IndicadorWidget(string? clave) => clave switch
    {
        "cerca" => PorCuadrante(CuadrantePartes.GestionarDeCerca),
        "satisfecho" => PorCuadrante(CuadrantePartes.MantenerSatisfecho),
        "informado" => PorCuadrante(CuadrantePartes.MantenerInformado),
        "minimo" => PorCuadrante(CuadrantePartes.MinimoEsfuerzo),
        "vencidos" => ("Seguimientos vencidos", Activas.Count(p => EstadoDe(p.Clave) == EstadoSeguimientoParte.Vencido), "vw-rojo"),
        "por-vencer" => ("Seguimientos por vencer", Activas.Count(p => EstadoDe(p.Clave) == EstadoSeguimientoParte.PorVencer), "vw-ambar"),
        "inactivas" => ("Partes inactivas", partes.Count(p => EsInactiva(p.Clave)), ""),
        "hallazgos" => ("Hallazgos abiertos", HallazgosAbiertos(), "vw-ambar"),
        _ => ("Partes interesadas", partes.Count, ""),
    };

    private (string, int, string) PorCuadrante(CuadrantePartes cuadrante) =>
        (ServicioPartesInteresadas.Nombre(cuadrante),
            Activas.Count(p => ServicioPartesInteresadas.Cuadrante(p.Impacto, p.Influencia) == cuadrante), "");

    private int HallazgosAbiertos()
    {
        var claves = partes.Select(p => p.Clave).ToHashSet();
        return seguimientos.Count(s => claves.Contains(s.ParteClave) && !string.IsNullOrWhiteSpace(s.DescripcionHallazgo)
            && (!planesPorSeguimiento.TryGetValue(s.Id, out var planes) || planes.Any(p => p.Estado != EstadoAccionPlan.Cerrada)));
    }

    private EstadoSeguimientoParte EstadoDe(Guid c) => situacion.TryGetValue(c, out var s) ? s.Estado : EstadoSeguimientoParte.SinProgramar;
    private IEnumerable<SeguimientoParteInteresada> SeguimientosDe(Guid c) => seguimientos.Where(s => s.ParteClave == c).OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id);

    protected override async Task OnInitializedAsync()
    {
        if (DisenarQ is null && VistaQ is ("lista" or "seguimiento" or "versiones" or "resultados")) vista = VistaQ;
        await Cargar(VersionQ);
        if (ParteQ is Guid g && partes.Any(p => p.Clave == g)) sel = g;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!MapaEnVista) return;
        try
        {
            refNet ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("sigPartes.preparar", mapaSvg, refNet, EsBorrador);
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        refNet?.Dispose();
        // Al salir de la página, el borrador que se abrió con "Editar" y quedó sin cambios se descarta.
        try { if (borradorNuevoId is int b) await ServicioPartesInteresadas.DescartarBorradorVacioAsync(Db, b); } catch { }
    }

    private async Task Cargar(int? versionId = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            Db.ChangeTracker.Clear();
            versiones = ServicioPartesInteresadas.Versiones(Db);
            vigente = versiones.FirstOrDefault(v => v.Estado == EstadoVersionPartes.Vigente);
            borrador = versiones.FirstOrDefault(v => v.Estado == EstadoVersionPartes.Borrador);
            var id = versionId ?? ver?.Id;
            ver = versiones.FirstOrDefault(v => v.Id == id) ?? vigente ?? borrador ?? versiones.FirstOrDefault();

            partesPorVersion = (await Db.PartesInteresadas.AsNoTracking().ToListAsync())
                .GroupBy(p => p.VersionPartesInteresadasId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Orden).ThenBy(p => p.Id).ToList());
            partes = ver is not null && partesPorVersion.TryGetValue(ver.Id, out var l) ? l : new();
            var claves = partesPorVersion.Values.SelectMany(x => x).Select(p => p.Clave).Distinct().ToList();

            seguimientos = await Db.SeguimientosParteInteresada.AsNoTracking().Where(s => claves.Contains(s.ParteClave)).ToListAsync();
            programaciones = (await Db.ProgramacionesSeguimientoParte.AsNoTracking().ToListAsync()).ToDictionary(p => p.ParteClave);
            var hoy = DateTime.Today;
            var desde = ver?.VigenteDesde ?? vigente?.VigenteDesde;
            situacion = partes.ToDictionary(p => p.Clave, p => ServicioPartesInteresadas.Situacion(
                programaciones.GetValueOrDefault(p.Clave), seguimientos.Where(s => s.ParteClave == p.Clave).ToList(), desde, hoy));

            var idsSeg = seguimientos.Select(s => s.Id).ToList();
            var planes = await Db.PlanesAccion.AsNoTracking()
                .Where(p => p.SeguimientoParteInteresadaId != null && idsSeg.Contains(p.SeguimientoParteInteresadaId.Value))
                .Select(p => new { p.Id, p.Codigo, Seg = p.SeguimientoParteInteresadaId!.Value }).ToListAsync();
            var idsPlan = planes.Select(p => p.Id).ToList();
            var acciones = await Db.AccionesPlan.AsNoTracking().Where(a => idsPlan.Contains(a.PlanAccionId)).ToListAsync();
            planesPorSeguimiento = planes.GroupBy(p => p.Seg).ToDictionary(g => g.Key,
                g => g.Select(p => new PlanRef(p.Id, p.Codigo, EscalaAccion.EstadoPlan(acciones.Where(a => a.PlanAccionId == p.Id)))).ToList());

            if (sel is Guid s0 && !partes.Any(p => p.Clave == s0)) sel = null;
            if (compA is null || compB is null) PrepararComparacion();
            else Comparar();
        }
        finally { _dbLock.Release(); }
    }

    private void Aviso(string m, bool err = false) { mensaje = m; error = err; }

    private async Task Intentar(Func<Task> accion, string? ok = null)
    {
        guardando = true;
        try
        {
            await _dbLock.WaitAsync();
            try { await accion(); }
            finally { _dbLock.Release(); }
            if (ok is not null) Aviso(ok);
        }
        catch (Exception ex) { Db.ChangeTracker.Clear(); Aviso(ex.InnerException?.Message ?? ex.Message, true); }
        finally { guardando = false; }
    }

    // ---------- Vista y selección ----------
    private async Task CambiarVista(string v) { vista = v; await Recordar(); }

    private async Task Recordar()
    {
        await JS.RecordarVistaAsync("/partes-interesadas", new Dictionary<string, object?>
        {
            ["v"] = ver is not null && ver.Id != vigente?.Id ? ver.Id : null,
            ["vista"] = vista == "mapa" ? null : vista,
            ["parte"] = sel?.ToString(),
            ["disenarVista"] = DisenarQ
        });
    }

    private async Task Seleccionar(Guid c)
    {
        sel = sel == c ? null : c;
        formParte = null; formSeg = null;
        await Recordar();
    }

    // Borrador creado en esta sesión con "Editar": si se sale de él sin cambios, se descarta (no deja versiones vacías).
    private int? borradorNuevoId;

    private async Task DescartarBorradorSiVacio()
    {
        if (borradorNuevoId is not int b) return;
        var descartado = false;
        await Intentar(async () => descartado = await ServicioPartesInteresadas.DescartarBorradorVacioAsync(Db, b));
        borradorNuevoId = null;
        if (descartado) Aviso("El borrador quedó sin cambios y se descartó: no se creó una versión nueva.");
    }

    private async Task VerVersion(int id)
    {
        if (borradorNuevoId is int bn && id != bn) await DescartarBorradorSiVacio();
        CerrarFormularios();
        await Cargar(id);
        await Recordar();
    }

    private void CerrarFormularios() { formParte = null; parteOrigen = null; formSeg = null; ronda = null; pub = null; elim = null; planSeg = null; }

    // ---------- Plan de acción desde el hallazgo (seguimiento individual y ronda) ----------
    // Un hallazgo requiere su plan: el ícono de plan abre el editor completo (EditorPlanAccion) y el plan se
    // guarda ligado al seguimiento (PlanAccion.SeguimientoParteInteresadaId) al guardar el seguimiento o la ronda.
    private bool listasPlan;
    private List<string> planOrigenes = new(), planTipos = new(), planProcesos = new(), planCargos = new();
    private List<ElementoTrazado> planTorres = new();
    private Dictionary<int, Componente> planComponentes = new();
    private List<ItemCatalogo> planLocalizaciones = new();
    private List<Tiro> planTiros = new();
    private SigWeb.Data.Planes.PlanesAccionServicio.PlanEditable? planSeg;
    private bool verPlanSeg;

    private void CargarListasPlan()
    {
        if (listasPlan) return;
        planOrigenes = SembradorCatalogosRiesgo.Valores(Db, SembradorCatalogosRiesgo.OrigenHallazgo);
        planTipos = SembradorCatalogosRiesgo.Valores(Db, SembradorCatalogosRiesgo.TipoHallazgo);
        planProcesos = SembradorCatalogosRiesgo.Valores(Db, SembradorCatalogosRiesgo.ProcesoPlan);
        planCargos = SembradorCatalogosRiesgo.Valores(Db, SembradorCatalogosRiesgo.Cargo);
        planTorres = Db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Codigo).ToList();
        planComponentes = Db.Componentes.IgnoreQueryFilters().AsNoTracking().ToList().ToDictionary(c => c.Id);
        planLocalizaciones = Db.Catalogos.AsNoTracking().Where(c => c.Tipo == "Localización" && c.Activo).OrderBy(c => c.Nombre).ToList();
        planTiros = Db.Tiros.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Nombre).ToList();
        listasPlan = true;
    }

    private IEnumerable<string> CodigosReservados() =>
        (ronda?.Filas.Where(f => f.Plan is not null).Select(f => f.Plan!.Plan.Codigo) ?? Enumerable.Empty<string>())
            .Concat(planSeg is null ? Enumerable.Empty<string>() : new[] { planSeg.Plan.Codigo });

    private SigWeb.Data.Planes.PlanesAccionServicio.PlanEditable NuevoPlanDeHallazgo(string? parte, DateTime fecha, string? hallazgo)
    {
        CargarListasPlan();
        return new()
        {
            Plan = new PlanAccion
            {
                Codigo = ServicioPlanes.SiguienteCodigo(CodigosReservados()),
                Fecha = fecha,
                OrigenHallazgo = SembradorCatalogosRiesgo.OrigenParte(planOrigenes),
                DescripcionHallazgo = string.Join(" — ", new[] { string.IsNullOrWhiteSpace(parte) ? null : $"Parte interesada «{parte}»", hallazgo }
                    .Where(x => !string.IsNullOrWhiteSpace(x)))
            }
        };
    }

    private void AlternarPlanFila(FilaRonda f)
    {
        if (f.Plan is null) { f.Plan = NuevoPlanDeHallazgo(f.Parte.Nombre, ronda?.Fecha ?? DateTime.Today, f.DescripcionHallazgo); f.VerPlan = true; }
        else f.VerPlan = !f.VerPlan;
    }

    private void AlternarPlanSeg()
    {
        if (formSeg is null) return;
        if (planSeg is null) { planSeg = NuevoPlanDeHallazgo(formSegParte, formSeg.Fecha, formSeg.DescripcionHallazgo); verPlanSeg = true; }
        else verPlanSeg = !verPlanSeg;
    }

    // El plan nace con la descripción del momento en que se abrió: si luego se escribió el hallazgo, se completa.
    private static void CompletarPlan(SigWeb.Data.Planes.PlanesAccionServicio.PlanEditable pb, string? parte, string? hallazgo)
    {
        if (string.IsNullOrWhiteSpace(hallazgo)) return;
        var d = pb.Plan.DescripcionHallazgo ?? "";
        if (!d.Contains(hallazgo.Trim())) pb.Plan.DescripcionHallazgo = string.Join(" — ", new[] { d, hallazgo.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private string Subtitulo => ver is null ? "Sin versiones" :
        string.Join(" · ", new[] { ServicioPartesInteresadas.Etiqueta(ver), ver.Estado, RangoVigencia(ver) }.Where(x => !string.IsNullOrWhiteSpace(x)));

    // Para el selector: fecha (vigencia o creación), autor (aprobó, revisó o elaboró) y motivo.
    private IReadOnlyList<SelectorVersiones.Item> ItemsVersiones => versiones
        .Select(v => new SelectorVersiones.Item(v.Id, v.Numero, v.Estado, RangoVigencia(v), v.Aprobo ?? v.Reviso ?? v.Elaboro,
            string.Join(" · ", new[] { v.Codigo, v.MotivoCambio }.Where(x => !string.IsNullOrWhiteSpace(x)))))
        .ToList();

    private static string RangoVigencia(VersionPartesInteresadas v) =>
        v.Estado == EstadoVersionPartes.Borrador ? $"creado {v.FechaCreacion:dd/MM/yyyy}"
        : $"{(v.VigenteDesde is DateTime d ? d.ToString("dd/MM/yyyy") : "sin fecha")} → {(v.VigenteHasta is DateTime h ? h.ToString("dd/MM/yyyy") : v.Estado == EstadoVersionPartes.Vigente ? "hoy" : "—")}";

    // ---------- Mapa (SVG) ----------
    // Plano: X = INF (influencia), Y = IMP (impacto), como el gráfico del Excel.
    private const double MW = 640, MH = 520, ML = 56, MT = 18, MR = 18, MB = 48;
    private static double X(decimal v) => ML + (double)v / (double)ServicioPartesInteresadas.Maximo * (MW - ML - MR);
    private static double Y(decimal v) => MT + (1 - (double)v / (double)ServicioPartesInteresadas.Maximo) * (MH - MT - MB);
    private static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record Punto(ParteInteresada Parte, double X, double Y, double Cx, double Cy, double Lx, double Ly, string Ancla);

    // Partes con la misma valoración se abren en un pequeño círculo para que se vean todas; cada
    // etiqueta busca un lugar libre alrededor de su burbuja (derecha, izquierda, arriba, abajo…).
    private List<Punto> Puntos()
    {
        var bases = new List<(ParteInteresada P, double X, double Y, double Cx, double Cy)>();
        foreach (var g in Mostradas.Where(p => p.Impacto is not null && p.Influencia is not null).GroupBy(p => (p.Influencia, p.Impacto)))
        {
            var l = g.ToList();
            for (var i = 0; i < l.Count; i++)
            {
                double x = X(g.Key.Influencia!.Value), y = Y(g.Key.Impacto!.Value), cx = x, cy = y;
                if (l.Count > 1) { var a = 2 * Math.PI * i / l.Count - Math.PI / 2; cx = x + 15 * Math.Cos(a); cy = y + 15 * Math.Sin(a); }
                bases.Add((l[i], x, y, cx, cy));
            }
        }
        var ocupado = bases.Select(b => (X1: b.Cx - 17, Y1: b.Cy - 17, X2: b.Cx + 17, Y2: b.Cy + 17)).ToList();
        var r = new List<Punto>();
        foreach (var b in bases.OrderBy(b => b.Cy).ThenBy(b => b.Cx))
        {
            var w = b.P.Nombre.Length * 6.4 + 4; const double h = 14;
            (double Lx, double Ly, string An)[] opciones =
            {
                (b.Cx + 18, b.Cy + 4, "start"), (b.Cx - 18, b.Cy + 4, "end"), (b.Cx, b.Cy - 20, "middle"), (b.Cx, b.Cy + 29, "middle"),
                (b.Cx + 16, b.Cy - 14, "start"), (b.Cx + 16, b.Cy + 22, "start"), (b.Cx - 16, b.Cy - 14, "end"), (b.Cx - 16, b.Cy + 22, "end")
            };
            (double Lx, double Ly, string An) elegida = opciones[0];
            var mejor = double.MaxValue;
            foreach (var o in opciones)
            {
                var x1 = o.An == "start" ? o.Lx : o.An == "end" ? o.Lx - w : o.Lx - w / 2;
                var caja = (X1: x1, Y1: o.Ly - h + 3, X2: x1 + w, Y2: o.Ly + 3);
                double choque = ocupado.Sum(c => Math.Max(0, Math.Min(c.X2, caja.X2) - Math.Max(c.X1, caja.X1)) * Math.Max(0, Math.Min(c.Y2, caja.Y2) - Math.Max(c.Y1, caja.Y1)));
                if (caja.X1 < ML || caja.X2 > MW - MR || caja.Y1 < MT || caja.Y2 > MH - MB) choque += 5000;
                if (choque < mejor) { mejor = choque; elegida = o; }
                if (choque == 0) break;
            }
            var ex1 = elegida.An == "start" ? elegida.Lx : elegida.An == "end" ? elegida.Lx - w : elegida.Lx - w / 2;
            ocupado.Add((ex1, elegida.Ly - h + 3, ex1 + w, elegida.Ly + 3));
            r.Add(new Punto(b.P, b.X, b.Y, b.Cx, b.Cy, elegida.Lx, elegida.Ly, elegida.An));
        }
        return r;
    }

    private List<SeguimientoParteInteresada> Trayectoria(Guid c) =>
        seguimientos.Where(s => s.ParteClave == c && s.Impacto is not null && s.Influencia is not null).OrderBy(s => s.Fecha).ThenBy(s => s.Id).ToList();

    private static string ColorEstado(EstadoSeguimientoParte e) => e switch
    {
        EstadoSeguimientoParte.Vencido => "#c0392b",
        EstadoSeguimientoParte.PorVencer => "#d97706",
        EstadoSeguimientoParte.AlDia => "#1f9d55",
        _ => "transparent"
    };

    [JSInvokable]
    public async Task Mover(int id, decimal impacto, decimal influencia)
    {
        await Intentar(() => ServicioPartesInteresadas.MoverAsync(Db, id, impacto, influencia));
        await Cargar();
        StateHasChanged();
    }

    // ---------- Formulario de parte (solo borrador) ----------
    private ParteInteresada? formParte;

    private async Task Editar()
    {
        if (ver is null && versiones.Count == 0)
        {
            await Intentar(async () => { var b = await ServicioPartesInteresadas.CrearBorradorAsync(Db, null); ver = b; }, "Borrador v1 creado.");
            await Cargar(ver?.Id);
            return;
        }
        if (borrador is not null) { await VerVersion(borrador.Id); return; }
        if (ver is null) return;
        VersionPartesInteresadas? nueva = null;
        await Intentar(async () => nueva = await ServicioPartesInteresadas.CrearBorradorAsync(Db, ver.Id));
        if (nueva is not null)
        {
            borradorNuevoId = nueva.Id;
            await Cargar(nueva.Id);
            Aviso($"Borrador v{nueva.Numero} creado a partir de la v{nueva.Numero - 1}: los cambios quedan aquí hasta aprobarlo.");
            await Recordar();
        }
    }

    private void NuevaParte()
    {
        if (!EsBorrador || ver is null) return;
        CerrarFormularios();
        formParte = new ParteInteresada { VersionPartesInteresadasId = ver.Id, Orden = (partes.Count == 0 ? 0 : partes.Max(p => p.Orden)) + 1 };
    }

    private static ParteInteresada CopiaParte(ParteInteresada p) => new()
    {
        Id = p.Id, VersionPartesInteresadasId = p.VersionPartesInteresadasId, Clave = p.Clave, Orden = p.Orden, Nombre = p.Nombre,
        Procesos = p.Procesos, Necesidades = p.Necesidades, Expectativas = p.Expectativas, AfectaSgc = p.AfectaSgc, Impacto = p.Impacto,
        Influencia = p.Influencia, Clasificacion = p.Clasificacion, Estrategias = p.Estrategias, Medicion = p.Medicion,
        Periodicidad = p.Periodicidad, Meta = p.Meta, Observaciones = p.Observaciones, Supuesto = p.Supuesto
    };

    private void EditarParte(ParteInteresada p)
    {
        if (!EsBorrador) return;
        CerrarFormularios();
        formParte = CopiaParte(p);
        parteOrigen = null; destinosParte = new();
    }

    // Edición desde una versión aprobada: el formulario abre sobre la parte tal como está, sin crear nada.
    // "Guardar en" elige el borrador (se crea si no existe) o una corrección directa en una versión que tenga esa
    // parte. Sin cambios no se crea ninguna versión.
    private ParteInteresada? parteOrigen;
    private string firmaParte = "";
    private int destinoParte;
    private List<(int Id, string Nombre)> destinosParte = new();

    private async Task EditarDesdeTarjeta(ParteInteresada p)
    {
        sel = p.Clave;
        if (EsBorrador) { EditarParte(p); await Recordar(); return; }
        CerrarFormularios();
        formParte = CopiaParte(p);
        parteOrigen = p;
        firmaParte = ServicioPartesInteresadas.Firma(formParte);
        destinosParte = new();
        if (borrador is not null)
        {
            if (partesPorVersion.GetValueOrDefault(borrador.Id)?.Any(x => x.Clave == p.Clave) == true) destinosParte.Add((borrador.Id, $"Borrador v{borrador.Numero}"));
        }
        else destinosParte.Add((0, "Nueva versión (borrador)"));
        foreach (var v in versiones.Where(v => v.Estado != EstadoVersionPartes.Borrador && partesPorVersion.GetValueOrDefault(v.Id)?.Any(x => x.Clave == p.Clave) == true))
            destinosParte.Add((v.Id, $"v{v.Numero} · {v.Estado} (corregir sin nueva versión)"));
        destinoParte = destinosParte.Count > 0 ? destinosParte[0].Id : 0;
        await Recordar();
    }

    private async Task GuardarParteConDestino()
    {
        if (formParte is null || parteOrigen is null) return;
        if (ServicioPartesInteresadas.Firma(formParte) == firmaParte)
        { formParte = null; parteOrigen = null; Aviso("Sin cambios: no se modificó ninguna versión."); return; }
        ParteInteresada? g = null;
        string donde = "";
        var propagado = false;
        var origen = parteOrigen;
        await Intentar(async () =>
        {
            var aBorrador = destinoParte == 0 || versiones.Any(v => v.Id == destinoParte && v.Estado == EstadoVersionPartes.Borrador);
            if (aBorrador)
            {
                var b = borrador ?? await ServicioPartesInteresadas.CrearBorradorAsync(Db, vigente?.Id ?? ver?.Id);
                var destino = ServicioPartesInteresadas.Partes(Db, b.Id).FirstOrDefault(x => x.Clave == origen.Clave)
                    ?? throw new InvalidOperationException($"«{origen.Nombre}» no está en el borrador v{b.Numero}.");
                formParte.Id = destino.Id; formParte.VersionPartesInteresadasId = b.Id;
                g = await ServicioPartesInteresadas.GuardarParteAsync(Db, formParte);
                donde = $"el borrador v{b.Numero}";
            }
            else
            {
                var v = versiones.First(x => x.Id == destinoParte);
                var destino = partesPorVersion.GetValueOrDefault(v.Id)?.FirstOrDefault(x => x.Clave == origen.Clave)
                    ?? throw new InvalidOperationException($"«{origen.Nombre}» no está en la v{v.Numero}.");
                g = await ServicioPartesInteresadas.CorregirParteAsync(Db, destino.Id, formParte);
                propagado = await ServicioPartesInteresadas.PropagarAlBorradorAsync(Db, destino, formParte, v.Id);
                donde = $"la v{v.Numero}";
            }
        });
        if (g is null) return;
        formParte = null; parteOrigen = null;
        sel = g.Clave;
        await Cargar();
        Aviso($"Parte guardada en {donde}." + (propagado ? " El borrador recibió la misma corrección." : ""));
    }

    // Eliminar = sacar la parte del borrador (va a la Papelera al confirmar). Desde la vigente abre o crea
    // el borrador y elimina allí la misma Clave: la vigente no se toca hasta aprobar.
    private string TituloEliminarParte => EsBorrador ? "Eliminar parte (va a la Papelera)"
        : borrador is not null ? $"Eliminar del borrador v{borrador.Numero}" : "Eliminar (crea un borrador)";

    private async Task EliminarParteDesde(ParteInteresada p)
    {
        var clave = p.Clave;
        if (!EsBorrador)
        {
            await Editar();
            if (!EsBorrador) return;
        }
        var q = partes.FirstOrDefault(x => x.Clave == clave);
        if (q is null) return;
        await PedirEliminar("parte", q.Id, q.Nombre);
    }

    private async Task EliminarParteRes(string clave)
    {
        if (Guid.TryParse(clave, out var g) && partes.FirstOrDefault(x => x.Clave == g) is { } p) await EliminarParteDesde(p);
    }

    private async Task SeguirDesdeTarjeta(ParteInteresada p)
    {
        NuevoSeguimiento(p);
        await Recordar();
    }

    private async Task Activar(ParteInteresada p, bool activa)
    {
        var clave = p.Clave;
        await Intentar(() => ServicioPartesInteresadas.ActivarAsync(Db, clave, activa),
            activa ? $"«{p.Nombre}» activa de nuevo." : $"«{p.Nombre}» inactiva: sale de las rondas y de los conteos de seguimiento.");
        if (formSeg?.ParteClave == clave) formSeg = null;
        await Cargar();
    }

    private async Task GuardarParte()
    {
        if (formParte is null) return;
        if (parteOrigen is not null) { await GuardarParteConDestino(); return; }
        ParteInteresada? g = null;
        await Intentar(async () => g = await ServicioPartesInteresadas.GuardarParteAsync(Db, formParte), "Parte guardada en el borrador.");
        if (g is null) return;
        formParte = null;
        sel = g.Clave;
        await Cargar();
    }

    // Clasificación sugerida a partir de IMP × INF (el usuario puede escribir otra).
    private void SugerirClasificacion()
    {
        if (formParte is null) return;
        var c = ServicioPartesInteresadas.Cuadrante(formParte.Impacto, formParte.Influencia);
        if (c != CuadrantePartes.SinValorar) formParte.Clasificacion = ServicioPartesInteresadas.Nombre(c).ToUpperInvariant();
    }

    // ---------- Eliminaciones (siempre a la Papelera, con impacto antes) ----------
    private sealed class Eliminacion
    {
        public string Tipo = "";      // parte | seguimiento | version
        public int Id;
        public string Titulo = "";
        public InformeImpacto Informe = new();
        public bool Entendido;
    }
    private Eliminacion? elim;
    private bool BloqueadoPorImpacto => elim is null || (elim.Informe.RequiereEntender && !elim.Entendido);

    private async Task PedirEliminar(string tipo, int id, string titulo)
    {
        CerrarFormularios();
        InformeImpacto inf = new();
        await _dbLock.WaitAsync();
        try
        {
            inf = tipo switch
            {
                "parte" => await Impacto.ParteInteresada(id),
                "seguimiento" => await Impacto.SeguimientoParte(id),
                _ => await Impacto.VersionPartes(id)
            };
        }
        finally { _dbLock.Release(); }
        elim = new Eliminacion { Tipo = tipo, Id = id, Titulo = titulo, Informe = inf };
    }

    private async Task ConfirmarEliminar()
    {
        if (elim is null) return;
        var e = elim;
        await Intentar(async () =>
        {
            switch (e.Tipo)
            {
                case "parte": await ServicioPartesInteresadas.EnviarAPapeleraAsync(Db, e.Id); break;
                case "seguimiento": await ServicioPartesInteresadas.EliminarSeguimientoAsync(Db, e.Id); break;
                default:
                    var v = await Db.VersionesPartesInteresadas.FirstAsync(x => x.Id == e.Id);
                    v.Eliminado = true; v.FechaEliminacion = DateTime.Now;
                    await Db.SaveChangesAsync();
                    Db.ChangeTracker.Clear();
                    break;
            }
        }, $"«{e.Titulo}» enviado a la Papelera.");
        elim = null;
        if (e.Tipo == "version") ver = null;
        await Cargar();
        await Recordar();
    }

    // ---------- Seguimiento individual ----------
    private SeguimientoParteInteresada? formSeg;
    private string formSegParte = "";

    private void NuevoSeguimiento(ParteInteresada p)
    {
        CerrarFormularios();
        sel = p.Clave;
        formSegParte = p.Nombre;
        var ult = SeguimientosDe(p.Clave).FirstOrDefault(s => s.Impacto is not null || s.Influencia is not null);
        formSeg = new SeguimientoParteInteresada
        {
            ParteClave = p.Clave, ParteInteresadaId = p.Id, Fecha = DateTime.Today,
            Responsable = programaciones.GetValueOrDefault(p.Clave)?.Responsable,
            Impacto = ult?.Impacto ?? p.Impacto, Influencia = ult?.Influencia ?? p.Influencia
        };
    }

    private void EditarSeguimiento(SeguimientoParteInteresada s)
    {
        CerrarFormularios();
        formSegParte = partes.FirstOrDefault(p => p.Clave == s.ParteClave)?.Nombre ?? "";
        formSeg = new SeguimientoParteInteresada
        {
            Id = s.Id, ParteClave = s.ParteClave, ParteInteresadaId = s.ParteInteresadaId, Fecha = s.Fecha, Responsable = s.Responsable,
            Resultado = s.Resultado, Cumplimiento = s.Cumplimiento, Impacto = s.Impacto, Influencia = s.Influencia, Observacion = s.Observacion, DescripcionHallazgo = s.DescripcionHallazgo
        };
    }

    private List<SeguimientoParteInteresada> hallazgosGuardados = new();

    // Al guardar se vuelve a la misma vista, con sus filtros. Si una parte guardada sale del filtro (p. ej. pasa de
    // Vencido a Al día), se dice en el mensaje en vez de cambiarle la vista al usuario. Se llama después de Cargar().
    private void MostrarSeguimientosGuardados(List<SeguimientoParteInteresada> guardados)
    {
        var claves = guardados.Select(s => s.ParteClave).Distinct().ToList();
        var visibles = Mostradas.Select(p => p.Clave).ToHashSet();
        if (claves.Count == 1 && visibles.Contains(claves[0])) sel = claves[0];
        var ocultas = claves.Where(c => !visibles.Contains(c)).Select(c => partes.FirstOrDefault(p => p.Clave == c)?.Nombre).Where(n => n is not null).ToList();
        if (ocultas.Count > 0 && !error) mensaje += $" Con el filtro actual no se ve: {string.Join(", ", ocultas)}.";
        // Hallazgos que quedaron sin plan (seguimientos antiguos editados): se ofrece crearlo.
        hallazgosGuardados = guardados.Where(s => s.Id > 0 && !string.IsNullOrWhiteSpace(s.DescripcionHallazgo) && !planesPorSeguimiento.ContainsKey(s.Id)).ToList();
    }

    private async Task GuardarSeguimiento()
    {
        if (formSeg is not { } guardado || guardando) return;
        var plan = planSeg;
        var parte = formSegParte;
        var tienePlan = guardado.Id > 0 && planesPorSeguimiento.ContainsKey(guardado.Id);
        if (!string.IsNullOrWhiteSpace(guardado.DescripcionHallazgo) && plan is null && !tienePlan)
        { Aviso("El hallazgo requiere un plan de acción: ábrelo con el ícono de plan junto al hallazgo.", true); return; }
        if (plan is not null && string.IsNullOrWhiteSpace(guardado.DescripcionHallazgo)) guardado.DescripcionHallazgo = plan.Plan.DescripcionHallazgo;
        var idOriginal = guardado.Id;
        var ok = false;
        string? creado = null;
        await Intentar(async () =>
        {
            if (plan is not null)
            {
                CompletarPlan(plan, parte, guardado.DescripcionHallazgo);
                if (await ServicioPlanes.ValidarAsync(plan) is string problema)
                    throw new InvalidOperationException(problema + " El seguimiento no se guardó.");
            }
            await using var transaccion = await Db.Database.BeginTransactionAsync();
            await ServicioPartesInteresadas.GuardarSeguimientoAsync(Db, guardado);
            if (plan is not null)
            {
                plan.Plan.SeguimientoParteInteresadaId = guardado.Id;
                await ServicioPlanes.GuardarAsync(plan);
                creado = plan.Plan.Codigo;
            }
            await transaccion.CommitAsync();
            ok = true;
        });
        if (!ok) { guardado.Id = idOriginal; return; }
        Aviso("Seguimiento guardado." + (creado is null ? "" : $" Plan de acción {creado} creado."));
        planSeg = null;
        formSeg = null;
        await Cargar();
        MostrarSeguimientosGuardados(new() { guardado });
    }

    private void NuevoPlan(SeguimientoParteInteresada s) => Nav.NavigateTo($"planes-accion/nuevo?seguimientoParte={s.Id}");
    private void IrPlan(int id) => Nav.NavigateTo($"planes-accion/{id}");

    // ---------- Ronda de seguimiento (varias partes, una fecha) ----------
    private sealed class FilaRonda
    {
        public ParteInteresada Parte = default!;
        public bool Incluir;
        public string? Resultado;
        public CumplimientoParte Cumplimiento = CumplimientoParte.SinMedir;
        public decimal? Impacto, Influencia;
        public string? Observacion;
        public string? DescripcionHallazgo;
        public SigWeb.Data.Planes.PlanesAccionServicio.PlanEditable? Plan;
        public bool VerPlan;
    }
    private sealed class Ronda
    {
        public DateTime Fecha = DateTime.Today;
        public string? Responsable;
        public List<FilaRonda> Filas = new();
    }
    private Ronda? ronda;

    private void AbrirRonda()
    {
        CerrarFormularios();
        if (!Activas.Any()) return;
        ronda = new Ronda();
        foreach (var p in Activas)
        {
            var ult = SeguimientosDe(p.Clave).FirstOrDefault(s => s.Impacto is not null || s.Influencia is not null);
            var est = EstadoDe(p.Clave);
            ronda.Filas.Add(new FilaRonda
            {
                Parte = p, Incluir = est is EstadoSeguimientoParte.Vencido or EstadoSeguimientoParte.PorVencer,
                Impacto = ult?.Impacto ?? p.Impacto, Influencia = ult?.Influencia ?? p.Influencia
            });
        }
        if (!ronda.Filas.Any(f => f.Incluir)) ronda.Filas.ForEach(f => f.Incluir = true);
    }

    private async Task GuardarRonda()
    {
        if (ronda is not { } rondaEnviada || guardando) return;
        var filas = ronda.Filas.Where(f => f.Incluir).ToList();
        if (filas.Count == 0) { Aviso("Marque al menos una parte.", true); return; }
        var sinPlan = filas.Where(f => !string.IsNullOrWhiteSpace(f.DescripcionHallazgo) && f.Plan is null).ToList();
        if (sinPlan.Count > 0)
        { Aviso($"Cada hallazgo requiere un plan de acción (ícono en la columna Hallazgo): {string.Join(", ", sinPlan.Select(f => f.Parte.Nombre))}.", true); return; }
        foreach (var f in filas.Where(f => f.Plan is not null && string.IsNullOrWhiteSpace(f.DescripcionHallazgo))) f.DescripcionHallazgo = f.Plan!.Plan.DescripcionHallazgo;
        var segs = filas.Select(f => new SeguimientoParteInteresada
        {
            ParteClave = f.Parte.Clave, ParteInteresadaId = f.Parte.Id, Fecha = ronda.Fecha, Responsable = ServicioPartesInteresadas.Limpio(ronda.Responsable),
            Resultado = ServicioPartesInteresadas.Limpio(f.Resultado), Cumplimiento = f.Cumplimiento, Impacto = f.Impacto, Influencia = f.Influencia,
            Observacion = ServicioPartesInteresadas.Limpio(f.Observacion), DescripcionHallazgo = ServicioPartesInteresadas.Limpio(f.DescripcionHallazgo)
        }).ToList();
        var n = 0;
        var creados = new List<string>();
        await Intentar(async () =>
        {
            var codigosEnvio = new List<string>();
            foreach (var f in filas.Where(f => f.Plan is not null))
            {
                CompletarPlan(f.Plan!, f.Parte.Nombre, f.DescripcionHallazgo);
                if (await ServicioPlanes.ValidarAsync(f.Plan!, codigosEnvio) is string problema)
                    throw new InvalidOperationException($"{f.Parte.Nombre}: {problema} La ronda no se guardó.");
                codigosEnvio.Add(f.Plan!.Plan.Codigo);
            }
            await using var transaccion = await Db.Database.BeginTransactionAsync();
            var guardados = await ServicioPartesInteresadas.GuardarRondaAsync(Db, segs);
            // segs conserva el orden de filas: cada plan queda ligado al seguimiento de su fila.
            for (var i = 0; i < filas.Count; i++)
            {
                if (filas[i].Plan is not { } pb) continue;
                CompletarPlan(pb, filas[i].Parte.Nombre, segs[i].DescripcionHallazgo);
                pb.Plan.SeguimientoParteInteresadaId = segs[i].Id;
                await ServicioPlanes.GuardarAsync(pb);
                creados.Add(pb.Plan.Codigo);
            }
            await transaccion.CommitAsync();
            n = guardados;
        });
        if (n == 0) return;
        Aviso($"Ronda del {rondaEnviada.Fecha:dd/MM/yyyy}: {n} seguimiento(s) registrados." + (creados.Count > 0 ? $" Planes de acción creados: {string.Join(", ", creados)}." : ""));
        ronda = null;
        await Cargar();
        MostrarSeguimientosGuardados(segs);
    }

    // ---------- Programación (no versionada) ----------
    private sealed class EdicionProgramacion
    {
        public Guid Clave;
        public int? Cada;
        public string Unidad = UnidadPeriodo.Meses;
        public DateTime? Desde;
        public string? Responsable;
    }
    private EdicionProgramacion? prog;

    private void EditarProgramacion(ParteInteresada p)
    {
        var x = programaciones.GetValueOrDefault(p.Clave);
        prog = new EdicionProgramacion { Clave = p.Clave, Cada = x?.Cada, Unidad = x?.Unidad ?? UnidadPeriodo.Meses, Desde = x?.Desde, Responsable = x?.Responsable };
    }

    private async Task GuardarProgramacion()
    {
        if (prog is null) return;
        var e = prog;
        var ok = false;
        await Intentar(async () => { await ServicioPartesInteresadas.GuardarProgramacionAsync(Db, e.Clave, e.Cada, e.Unidad, e.Desde, e.Responsable); ok = true; }, "Programación guardada.");
        if (!ok) return;
        prog = null;
        await Cargar();
    }

    // Línea de tiempo de la vista Seguimiento: 12 meses atrás y 3 adelante.
    private static readonly int MesesAtras = 11, MesesAdelante = 3;
    private DateTime InicioLinea => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-MesesAtras);
    private DateTime FinLinea => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(MesesAdelante + 1);
    private static readonly System.Globalization.CultureInfo Es = new("es-CO");
    private static string Mes(DateTime d) => d.Month == 1 || d == new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-MesesAtras)
        ? d.ToString("MMM yy", Es).Replace(".", "") : d.ToString("MMM", Es).Replace(".", "");
    private double PosLinea(DateTime d) => Math.Clamp((d - InicioLinea).TotalDays / (FinLinea - InicioLinea).TotalDays * 100, 0, 100);

    // ---------- Aprobar (publicar el borrador) ----------
    private DatosPublicacionPartes? pub;

    private void AbrirPublicar()
    {
        if (!EsBorrador || ver is null) return;
        CerrarFormularios();
        pub = new DatosPublicacionPartes { Codigo = ver.Codigo };
        PrepararComparacion();
    }

    private async Task Publicar()
    {
        if (pub is null || ver is null) return;
        var id = ver.Id; var d = pub;
        var ok = false;
        await Intentar(async () => { await ServicioPartesInteresadas.PublicarAsync(Db, id, d); ok = true; });
        if (!ok) return;
        pub = null;
        await Cargar(id);
        Aviso($"v{ver?.Numero} aprobada y vigente desde {ver?.VigenteDesde:dd/MM/yyyy}.");
        await Recordar();
    }

    private async Task AplicarValoracion()
    {
        if (!EsBorrador || ver is null) return;
        var n = 0; var id = ver.Id;
        await Intentar(async () => n = await ServicioPartesInteresadas.AplicarValoracionSeguimientosAsync(Db, id));
        if (!error) Aviso(n == 0 ? "El borrador ya tiene la última valoración de los seguimientos." : $"{n} parte(s) toman la valoración de su último seguimiento.");
        await Cargar();
    }

    // ---------- Comparación de versiones ----------
    private int? compA, compB;
    private List<CambioParte> cambios = new();

    private void PrepararComparacion()
    {
        if (ver is null) { cambios = new(); return; }
        compB = ver.Id;
        compA = ver.VersionBaseId is int b && versiones.Any(x => x.Id == b) ? b
            : versiones.Where(x => x.Numero < ver.Numero).OrderByDescending(x => x.Numero).Select(x => (int?)x.Id).FirstOrDefault();
        Comparar();
    }

    private void Comparar()
    {
        cambios = compA is int a && compB is int b
            ? ServicioPartesInteresadas.Comparar(partesPorVersion.GetValueOrDefault(a) ?? new(), partesPorVersion.GetValueOrDefault(b) ?? new())
            : new();
    }

    // Historia de la parte seleccionada: cambios entre cada versión y la anterior.
    private List<(VersionPartesInteresadas Version, List<CambioParte> Cambios)> HistoriaDe(Guid c)
    {
        var r = new List<(VersionPartesInteresadas, List<CambioParte>)>();
        var orden = versiones.OrderBy(v => v.Numero).ToList();
        List<ParteInteresada> previa = new();
        foreach (var v in orden)
        {
            var actual = (partesPorVersion.GetValueOrDefault(v.Id) ?? new()).Where(p => p.Clave == c).ToList();
            var cs = ServicioPartesInteresadas.Comparar(previa, actual);
            if (cs.Count > 0) r.Add((v, cs));
            previa = actual;
        }
        r.Reverse();
        return r;
    }

    // ---------- Exportar ----------
    private async Task Exportar(string formato)
    {
        if (ver is null) return;
        if (formato == "pdf")
        {
            if (vista == "mapa" && !MapaEnVista)
            {
                Aviso("Para exportar el mapa a PDF, seleccione una vista que lo muestre.", true);
                return;
            }
            try { await JS.InvokeVoidAsync("sigPartes.imprimir", ".pi-imprimible", $"Partes interesadas {ServicioPartesInteresadas.Etiqueta(ver)}"); } catch { }
            return;
        }
        var bytes = ServicioPartesInteresadas.Exportar(ver, partes, situacion, seguimientos.Where(s => partes.Any(p => p.Clave == s.ParteClave)).ToList(), versiones);
        await JS.InvokeVoidAsync("descargarArchivo", $"Partes_interesadas_v{ver.Numero}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Convert.ToBase64String(bytes));
    }
}
