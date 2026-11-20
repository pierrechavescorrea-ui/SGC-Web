using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data.Inicio;
using SigWeb.Data.Navegacion;
using SigWeb.Data.Seguridad;

namespace SigWeb.Components.Pages;

// Tablero de proyectos del administrador. Widgets elegibles (Catalogo) con orden, ancho y presencia guardados en el navegador.
public partial class TableroProyectos : IDisposable
{
    [Inject] private ServicioProyectos Servicio { get; set; } = default!;
    [Inject] private IServiceScopeFactory Scopes { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private MenuIcono MenuIco { get; set; } = default!;
    [Inject] private UsuarioActual Actual { get; set; } = default!;

    public sealed record DefWidget(string Clave, string Nombre, string Descripcion, string Icono, bool AnchoFabrica);
    public static readonly IReadOnlyList<DefWidget> CatalogoFabrica = new[]
    {
        new DefWidget("cifras", "Cifras de todos los proyectos", "Totales: proyectos, planes, riesgos, calibraciones, costo de no calidad y PIE", "indicador", true),
        new DefWidget("proyectos", "Proyectos", "Una tarjeta por proyecto con sus indicadores y acceso a su tablero", "tablero", true),
        new DefWidget("comparativo", "Comparativo", "Proyectos × indicadores en una tabla con su semáforo", "tabla", true),
        new DefWidget("alertas", "Alertas consolidadas", "Los pendientes en rojo de todos los proyectos", "riesgos", false),
        new DefWidget("costos", "Costo de no calidad por proyecto", "Barras comparativas del costo registrado en Planes de acción", "costos-no-calidad", false),
        new DefWidget("pie", "Cumplimiento PIE por proyecto", "Barras con el % de la Matriz PIE de cada proyecto", "indicador", false),
        new DefWidget("salud", "Estado de las bases", "Bases listas, sin abrir o con error, y usuarios activos", "mantenimiento", false),
    };
    private static readonly string[] PorDefecto = { "cifras", "proyectos", "alertas", "costos", "comparativo" };
    private static readonly (string Clave, string Titulo)[] Columnas =
        { ("pie", "PIE"), ("planes", "Planes abiertos"), ("acciones", "Acciones vencidas"), ("comunicados", "Comunicados vencidos"),
          ("calibracion", "Calibraciones"), ("riesgos", "Riesgos materializados"), ("indicadores", "Fuera de meta") };
    private const string ClaveLocal = "sigweb.proyectos.widgets";

    private List<ResumenProyecto> resumenes = new();
    private bool cargando = true, editando;
    private List<string> widgets = PorDefecto.ToList();
    private HashSet<string> anchos = CatalogoFabrica.Where(w => w.AnchoFabrica).Select(w => w.Clave).ToHashSet();

    private IEnumerable<DefWidget> Catalogo => CatalogoFabrica.Select(w => w with { AnchoFabrica = anchos.Contains(w.Clave) });
    private IEnumerable<ResumenProyecto> Proyectos => resumenes;
    private string Subtitulo => cargando ? "Leyendo los proyectos…" : $"{resumenes.Count(r => !r.P.EsEmpresa)} proyecto(s) · {DateTime.Now:dd/MM/yyyy HH:mm}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            var g = await JS.InvokeAsync<string?>("localStorage.getItem", ClaveLocal);
            if (!string.IsNullOrWhiteSpace(g) && JsonSerializer.Deserialize<Guardado>(g) is { } d)
            {
                widgets = d.W.Where(w => CatalogoFabrica.Any(c => c.Clave == w)).Distinct().ToList();
                anchos = d.A.ToHashSet();
            }
        }
        catch { }
        await RecargarAsync();
    }

    private sealed record Guardado(List<string> W, List<string> A);

    private async Task RecargarAsync()
    {
        cargando = true; StateHasChanged();
        Dictionary<int, int> miembros;
        using (var scope = Scopes.CreateScope())
        {
            var seg = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
            miembros = await seg.Miembros.AsNoTracking().Where(m => m.Activo).GroupBy(m => m.ProyectoId)
                .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        }
        var sesion = AmbienteSig.Actual;
        resumenes = await TableroProyectosDatos.CargarAsync(Servicio, miembros, sesion);
        AmbienteSig.Actual = sesion;   // la carga fija el ambiente de cada proyecto en sus tareas; el del circuito queda igual
        cargando = false;
        RegistrarMenu();
        StateHasChanged();
    }

    // ---------- Menú del ícono: todos los proyectos / el tablero de cada uno ----------
    private void RegistrarMenu() => MenuIco.Registrar(MenuProyectos(Servicio, "*", this, VerProyectoAsync));

    // Compartido con Inicio: "Todos los proyectos" + cada proyecto activo.
    public static MenuIcono.Menu MenuProyectos(ServicioProyectos servicio, string actual, object dueno, Func<int, Task> verProyecto, Action? todos = null) => new()
    {
        Titulo = "Tablero", Dueno = dueno, Actual = actual,
        Opciones = servicio.Todos().Where(p => p.Estado is EstadoProyecto.Activo or EstadoProyecto.Suspendido)
            .Select(p => new MenuIcono.Opcion(p.Id.ToString(), p.Nombre, p.EsEmpresa ? "Espacio de la empresa" : p.Cliente ?? p.Codigo, false, p.Color, p.Codigo, p.EsEmpresa))
            .Prepend(new MenuIcono.Opcion("*", "Todos los proyectos", "Tablero del administrador")).ToList(),
        Elegir = c =>
        {
            if (c == "*") { todos?.Invoke(); return Task.CompletedTask; }
            return int.TryParse(c, out var id) ? verProyecto(id) : Task.CompletedTask;
        },
        EnlaceGestion = "admin/proyectos", TituloGestion = "Administrar proyectos",
    };

    // Ver el tablero de un proyecto = entrar a él (cambia el proyecto de la sesión) y abrir su Inicio.
    private Task VerProyectoAsync(int id) => JS.InvokeVoidAsync("sigCambiarProyecto", id, "/?vista=proyecto").AsTask();

    // ---------- Widgets ----------
    private async Task GuardarWidgetsAsync()
    {
        try { await JS.InvokeVoidAsync("localStorage.setItem", ClaveLocal, JsonSerializer.Serialize(new Guardado(widgets, anchos.ToList()))); } catch { }
    }
    private async Task Agregar(string c) { if (!widgets.Contains(c)) widgets.Add(c); await GuardarWidgetsAsync(); }
    private async Task Quitar(string c) { widgets.Remove(c); await GuardarWidgetsAsync(); }
    private async Task Mover(string c, int d)
    {
        var i = widgets.IndexOf(c); var j = i + d;
        if (i < 0 || j < 0 || j >= widgets.Count) return;
        (widgets[i], widgets[j]) = (widgets[j], widgets[i]);
        await GuardarWidgetsAsync();
    }
    private async Task AlternarAncho(string c) { if (!anchos.Remove(c)) anchos.Add(c); await GuardarWidgetsAsync(); }
    private async Task RestablecerWidgets()
    {
        widgets = PorDefecto.ToList();
        anchos = CatalogoFabrica.Where(w => w.AnchoFabrica).Select(w => w.Clave).ToHashSet();
        await GuardarWidgetsAsync();
    }

    // ---------- Ayudantes ----------
    public static string Marca(Proyecto p) => p.Codigo.Length > 4 ? p.Codigo[..4] : p.Codigo;
    private static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    private static int CalVencidas(IEnumerable<ResumenProyecto> l) =>
        l.Sum(r => r.Seccion("calibracion")?.Items.Count(i => i.Tono == "rojo") ?? 0);
    private static string PiePromedio(IEnumerable<ResumenProyecto> l)
    {
        var v = l.Where(r => r.Datos?.Pie?.Pct is not null).Select(r => r.Datos!.Pie!.Pct!.Value).ToList();
        return v.Count == 0 ? "—" : SigWeb.Data.Pie.CumplimientoPie.Formato(v.Average());
    }
    private static IEnumerable<(ResumenProyecto, TableroInicio.Seccion, TableroInicio.Item)> Alertas(IEnumerable<ResumenProyecto> l) =>
        l.Where(r => r.Datos is not null)
         .SelectMany(r => r.Datos!.Secciones.Where(s => s.Tono == "rojo").SelectMany(s => s.Items.Where(i => i.Tono == "rojo").Select(i => (r, s, i))));

    public void Dispose() => MenuIco.Quitar(this);
}
