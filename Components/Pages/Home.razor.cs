using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using SigWeb.Components.Layout;
using SigWeb.Data;
using SigWeb.Data.Inicio;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Panel de inicio con widgets. Blazor decide QUÉ widgets hay (ids, en el orden guardado); js/inicio.js (sigPanel)
// decide DÓNDE van (x, y, ancho, alto en una rejilla de 12 columnas), resuelve choques y guarda la disposición en
// el navegador (localStorage "sigweb.inicio.panel"). Tras cada render se llama sigPanel.aplicar: los widgets nuevos
// entran en el primer hueco con su tamaño por defecto y los quitados salen de la disposición.
public partial class Home
{
    public enum TipoWidget { Kpi, Lista, Bandeja, Pie, Mapa, Avance, Evm, Indicador, Calendario, Accesos, Resumen }

    public sealed record Widget(string Id, string Titulo, string Icono, string Grupo, TipoWidget Tipo, int W, int H, int MinW, int MinH);

    private sealed record Posicion(string id, int x, int y, int w, int h);

    private TableroInicio.Datos datos = new();
    private List<TableroInicio.Seccion> bandeja = new();
    private Dictionary<string, Widget> catalogo = new();
    private List<string> ids = new();
    private ElementReference panel;
    private bool editando, cargado, verAjustes;
    private string? tab;
    private string mensaje = "", ajResponsable = "";

    // Widgets con datos propios (WidgetsInicio): se leen después del primer render, cada uno con su contexto.
    private WidgetsInicio.ResumenAvance? avance;
    private WidgetsInicio.ResumenEvm? evm;
    private Dictionary<int, WidgetsInicio.SerieIndicador>? series;
    private List<WidgetsInicio.Evento>? eventos;
    private readonly Dictionary<string, ResumenesInicio.Resumen?> resumenes = new();
    private DateTime mesCal = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? diaCal;
    private readonly HashSet<string> leidos = new(), leyendo = new();
    private List<(string Url, string Titulo, string Modulo)> favoritos = new(), recientes = new();
    private List<(int Id, string Nombre)> indicadoresCat = new();

    private TableroInicio.Seccion? Sel => bandeja.FirstOrDefault(s => s.Clave == tab) ?? bandeja.FirstOrDefault();

    // Un ícono por apartado (aunque dos sean del mismo módulo).
    private static readonly Dictionary<string, string> IconosPestana = new()
    {
        ["comunicados"] = "<rect x=\"2.5\" y=\"5\" width=\"14\" height=\"11\" rx=\"1.5\"/><path d=\"m3 6 6.5 5L16 6\"/><circle cx=\"18\" cy=\"16.5\" r=\"3.8\"/><path d=\"M18 14.7v2M18 18.3v.1\"/>",
        ["comunicados-plazo"] = "<rect x=\"2.5\" y=\"5\" width=\"14\" height=\"11\" rx=\"1.5\"/><path d=\"m3 6 6.5 5L16 6\"/><circle cx=\"18\" cy=\"16.5\" r=\"3.8\"/><path d=\"M18 14.6v2l1.3.9\"/>",
        ["acciones"] = "<rect x=\"3.5\" y=\"3.5\" width=\"12\" height=\"16\" rx=\"2\"/><path d=\"M7.5 2.5h4v2.5h-4z\"/><path d=\"M6.5 10h6M6.5 13.5h3.5\"/><circle cx=\"17.5\" cy=\"17\" r=\"4\"/><path d=\"M17.5 15v2l1.3 1\"/>",
        ["seguimiento"] = "<path d=\"M2.5 12s3.5-6.5 9.5-6.5S21.5 12 21.5 12s-3.5 6.5-9.5 6.5S2.5 12 2.5 12z\"/><circle cx=\"12\" cy=\"12\" r=\"2.8\"/><path d=\"M4 20 20 4\"/>",
    };

    private static string IconoSeccion(string clave, string icono) =>
        IconosPestana.TryGetValue(clave, out var t) ? t : EncabezadoPagina.Trazos(icono);

    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private SigWeb.Data.Navegacion.MenuIcono MenuIco { get; set; } = default!;
    [Inject] private SigWeb.Data.Seguridad.ServicioProyectos Proyectos { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }

    // El administrador global entra al Tablero de proyectos (todos); el Inicio de un proyecto se abre con ?vista=proyecto
    // (desde el menú del ícono del encabezado, que lista los proyectos).
    private bool EsAdmin => Actual.EsAdministrador;

    protected override void OnInitialized()
    {
        if (EsAdmin && VistaQ != "proyecto") { Nav.NavigateTo("/proyectos", replace: true); return; }
        Cargar();
    }

    private void RegistrarMenuProyectos()
    {
        if (!EsAdmin) return;
        var actualId = SigWeb.Data.Seguridad.AmbienteSig.Actual?.Proyecto.Id.ToString() ?? "";
        MenuIco.Registrar(TableroProyectos.MenuProyectos(Proyectos, actualId, this,
            id => JS.InvokeVoidAsync("sigCambiarProyecto", id, "/?vista=proyecto").AsTask(),
            () => Nav.NavigateTo("/proyectos")));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (firstRender)
            {
                RegistrarMenuProyectos();
                var guardado = await JS.InvokeAsync<string?>("sigPanel.leer");
                var lista = Leer(guardado);
                if (lista is null)
                {
                    lista = Predeterminado();
                    await JS.InvokeVoidAsync("sigPanel.cargar", JsonSerializer.Serialize(lista));
                }
                ids = lista.Select(p => p.id).Where(catalogo.ContainsKey).Distinct().ToList();
                cargado = true;
                StateHasChanged();
                _ = LeerDatosAsync();
                return;
            }
            if (cargado) await JS.InvokeVoidAsync("sigPanel.aplicar", panel, editando);
        }
        catch (JSDisconnectedException) { }
        catch (TaskCanceledException) { }
    }

    private static List<Posicion>? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<Posicion>>(json); }
        catch { return null; }
    }

    private void Cargar()
    {
        Db.ChangeTracker.Clear();
        try
        {
            datos = TableroInicio.Cargar(Db);
        }
        catch (Exception ex)
        {
            datos = new();
            mensaje = "No se pudo leer el tablero (¿falta aplicar la migración?): " + ex.Message;
        }
        bandeja = datos.Bandeja();
        try { indicadoresCat = Db.Indicadores.AsNoTracking().Where(i => !i.Inactivo).OrderBy(i => i.Orden).ThenBy(i => i.Id).Select(i => new { i.Id, i.Nombre }).ToList().Select(i => (i.Id, i.Nombre)).ToList(); }
        catch { indicadoresCat = new(); }
        catalogo = Catalogo().Where(w => Ve(ModuloDe(w))).ToDictionary(w => w.Id);
    }

    // Módulo de permisos de cada widget: un widget con datos de un módulo que el usuario no puede ver no se ofrece.
    private string? ModuloDe(Widget w) => w.Tipo switch
    {
        TipoWidget.Avance => "avance-obra",
        TipoWidget.Evm => "costos",
        TipoWidget.Pie => "matriz-pie",
        TipoWidget.Mapa => "riesgos",
        TipoWidget.Indicador => "indicadores",
        TipoWidget.Resumen => ResumenesInicio.Lista.FirstOrDefault(d => d.Id == w.Id)?.Ruta,
        TipoWidget.Kpi or TipoWidget.Lista when SigWeb.Data.Seguridad.CatalogoModulos.De(w.Icono) is not null => w.Icono,
        _ => null,
    };

    private bool Ve(string? modulo) => modulo is null || Actual.Puede(SigWeb.Data.Seguridad.CatalogoModulos.ClaveDeRuta(modulo), SigWeb.Data.Seguridad.Accion.Ver);

    // Widgets disponibles: un indicador por KPI, la bandeja, una lista por apartado y los gráficos.
    private IEnumerable<Widget> Catalogo()
    {
        foreach (var k in datos.Kpis)
            yield return new Widget("kpi:" + k.Clave, k.Titulo, k.Icono, "Indicadores", TipoWidget.Kpi, 2, 2, 2, 2);
        yield return new Widget("bandeja", "Bandeja de pendientes", "inicio", "Listas", TipoWidget.Bandeja, 6, 7, 3, 3);
        foreach (var s in datos.Secciones.Where(s => s.Clave != "pie"))
            yield return new Widget("lista:" + s.Clave, s.Titulo, s.Icono, "Listas", TipoWidget.Lista, 4, 5, 3, 3);
        yield return new Widget("calendario", "Calendario de vencimientos", "calendario", "Listas", TipoWidget.Calendario, 4, 8, 3, 5);
        yield return new Widget("accesos", "Accesos rápidos", "favorito", "Listas", TipoWidget.Accesos, 3, 5, 2, 3);
        yield return new Widget("avance", "Avance de obra", "avance-obra", "Gráficos", TipoWidget.Avance, 3, 7, 3, 4);
        yield return new Widget("evm", "Valor ganado", "costos", "Gráficos", TipoWidget.Evm, 4, 5, 3, 4);
        yield return new Widget("pie", "Matriz PIE", "matriz", "Gráficos", TipoWidget.Pie, 3, 7, 3, 3);
        yield return new Widget("mapa", "Riesgos activos", "riesgos", "Gráficos", TipoWidget.Mapa, 3, 4, 2, 3);
        foreach (var d in ResumenesInicio.Lista)
            yield return new Widget(d.Id, d.Titulo, d.Icono, "Resúmenes", TipoWidget.Resumen, d.W, d.H, 3, 4);
        foreach (var (id, nombre) in indicadoresCat)
            yield return new Widget("ind:" + id, nombre, "indicadores", "Indicadores en el tiempo", TipoWidget.Indicador, 4, 4, 3, 3);
    }

    // Disposición por defecto: los 6 indicadores arriba; bandeja, Matriz PIE, mapa de riesgos y certificaciones.
    private List<Posicion> Predeterminado()
    {
        var r = new List<Posicion>();
        var x = 0;
        foreach (var k in datos.Kpis.Take(6)) { r.Add(new Posicion("kpi:" + k.Clave, x, 0, 2, 2)); x += 2; }
        r.Add(new Posicion("bandeja", 0, 2, 5, 8));
        r.Add(new Posicion("avance", 5, 2, 3, 8));
        r.Add(new Posicion("calendario", 8, 2, 4, 8));
        r.Add(new Posicion("evm", 0, 10, 4, 5));
        r.Add(new Posicion("pie", 4, 10, 3, 5));
        r.Add(new Posicion("mapa", 7, 10, 3, 5));
        r.Add(new Posicion("accesos", 10, 10, 2, 5));
        if (indicadoresCat.Count > 0) r.Add(new Posicion("ind:" + indicadoresCat[0].Id, 0, 15, 4, 4));
        return r.Where(p => catalogo.ContainsKey(p.id)).ToList();
    }

    private IEnumerable<Widget> Disponibles() => catalogo.Values.Where(w => !ids.Contains(w.Id));

    private void Editar(bool si) => editando = si;

    private void Agregar(string id)
    {
        if (!ids.Contains(id)) ids.Add(id);
        _ = LeerDatosAsync();
    }

    // Lee (una vez) los datos de los widgets presentes que los necesitan, cada uno con su propio contexto y fuera del render.
    private async Task LeerDatosAsync()
    {
        var tareas = new List<Task>();
        void Leer(string clave, Func<AppDbContext, Task> leer)
        {
            if (leidos.Contains(clave) || !leyendo.Add(clave)) return;
            tareas.Add(Task.Run(async () =>
            {
                try
                {
                    using var scope = Scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await leer(db);
                }
                catch { /* el widget queda vacío */ }
                finally
                {
                    try { await InvokeAsync(() => { leyendo.Remove(clave); leidos.Add(clave); StateHasChanged(); }); } catch { }
                }
            }));
        }
        if (ids.Contains("avance")) Leer("avance", db => { avance = WidgetsInicio.Avance(db, Env.WebRootPath); return Task.CompletedTask; });
        if (ids.Contains("evm")) Leer("evm", async db => evm = await WidgetsInicio.ValorGanado(db));
        if (ids.Any(i => i.StartsWith("ind:"))) Leer("ind", db => { series = WidgetsInicio.Indicadores(db).ToDictionary(x => x.Id); return Task.CompletedTask; });
        foreach (var d in ResumenesInicio.Lista.Where(d => ids.Contains(d.Id)))
            Leer("r:" + d.Id, async db => resumenes[d.Id] = await ResumenesInicio.CargarAsync(d.Id, db));
        if (ids.Contains("calendario")) Leer("calendario", db => { eventos = WidgetsInicio.Calendario(db, mesCal.AddDays(-7), mesCal.AddMonths(1).AddDays(7)); return Task.CompletedTask; });
        if (ids.Contains("accesos") && !leidos.Contains("accesos"))
        {
            leidos.Add("accesos");
            try
            {
                var json = await JS.InvokeAsync<string?>("sigPanel.accesos");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    List<(string, string, string)> L(string k) => doc.RootElement.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array
                        ? a.EnumerateArray().Select(x => (Txt(x, "url"), Txt(x, "titulo"), Txt(x, "modulo"))).Where(x => x.Item1 != "").ToList() : new();
                    favoritos = L("fav"); recientes = L("rec");
                }
            }
            catch { }
            StateHasChanged();
        }
        await Task.WhenAll(tareas);
    }

    private static string Txt(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private void MoverMes(int delta)
    {
        mesCal = mesCal.AddMonths(delta);
        diaCal = null;
        eventos = null;
        leidos.Remove("calendario");
        _ = LeerDatosAsync();
    }

    private void Quitar(string id) => ids.Remove(id);

    private async Task Restaurar()
    {
        var lista = Predeterminado();
        try { await JS.InvokeVoidAsync("sigPanel.cargar", JsonSerializer.Serialize(lista)); } catch (JSDisconnectedException) { }
        ids = lista.Select(p => p.id).ToList();
        _ = LeerDatosAsync();
    }

    private void AbrirAjustes()
    {
        verAjustes = !verAjustes;
        ajResponsable = datos.Responsable == "" ? TableroInicio.Todos : datos.Responsable;
    }

    private async Task GuardarAjustes()
    {
        try
        {
            var cfg = await Db.Configuracion.FirstOrDefaultAsync(c => c.Id == 1);
            if (cfg is null) { cfg = new ConfiguracionEmpresa { Id = 1 }; Db.Configuracion.Add(cfg); }
            cfg.TableroResponsableComunicados = string.IsNullOrWhiteSpace(ajResponsable) ? null : ajResponsable.Trim();
            await Db.SaveChangesAsync();
            verAjustes = false;
            Cargar();
        }
        catch (Exception ex) { mensaje = "No se pudo guardar: " + ex.Message; }
    }

    public void Dispose() => MenuIco.Quitar(this);
}
