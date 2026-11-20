using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Formularios;
using SigWeb.Data.Vistas;
using SigWeb.Models;

namespace SigWeb.Components.Layout;

public partial class VistaWidgets : IDisposable
{
    [Inject] private IServiceScopeFactory Scopes { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private SigWeb.Data.Navegacion.MenuIcono MenuIco { get; set; } = default!;

    [Parameter, EditorRequired] public string Modulo { get; set; } = "";
    // Valor de ?disenarVista= (id o "nueva"); null = uso normal.
    [Parameter] public string? DisenarVista { get; set; }
    // La pantalla de siempre (vista clásica).
    [Parameter] public RenderFragment? ChildContent { get; set; }
    // Contenido de cada widget (lo pinta la página con sus propios datos).
    [Parameter, EditorRequired] public RenderFragment<Widget> Contenido { get; set; } = default!;
    // Avisa a la página que cambió la vista o un widget (p. ej. para volver a dibujar gráficos).
    [Parameter] public EventCallback OnCambio { get; set; }

    private List<VistaRegistro> vistas = new();
    private int vistaId = VistasModelo.IdClasica;
    private List<Widget> widgets = new();
    private string nombre = "", firmaPanel = "";
    private int editId;
    private string? sel, mensaje, disenoLeido;
    private bool leida;
    private ElementReference refPanel;
    private DotNetObjectReference<VistaWidgets>? yo;

    public bool Disenando => DisenarVista is not null;
    // true = la página muestra widgets (vista guardada o diseño); false = la clásica.
    public bool Personalizada => Disenando || vistaId != VistasModelo.IdClasica;
    // Tipos de widget presentes (la página decide si debe dibujar gráficos).
    public IEnumerable<Widget> Presentes => Personalizada ? Actuales() : Enumerable.Empty<Widget>();
    private string ClaveLocal => "sigweb.vista." + Modulo;

    protected override async Task OnParametersSetAsync()
    {
        if (disenoLeido == (DisenarVista ?? "") && leida) return;
        disenoLeido = DisenarVista ?? "";
        using var scope = Scopes.CreateScope();
        vistas = await VistasModelo.ListarAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), Modulo);
        if (Disenando)
        {
            sel = null; mensaje = null;
            if (int.TryParse(DisenarVista, out var id) && vistas.FirstOrDefault(v => v.Id == id) is { } v)
            { editId = v.Id; nombre = v.Nombre; widgets = VistasModelo.Leer(v.Json); }
            else
            { editId = 0; nombre = "Vista nueva"; widgets = VistasModelo.InicialDe(Modulo); }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            yo = DotNetObjectReference.Create(this);
            if (!Disenando)
            {
                string? g = null;
                try { g = await JS.InvokeAsync<string?>("localStorage.getItem", ClaveLocal); } catch { }
                var id = int.TryParse(g, out var x) && (x == VistasModelo.IdClasica || vistas.Any(v => v.Id == x)) ? x
                    : vistas.FirstOrDefault(v => v.Predeterminada)?.Id ?? VistasModelo.IdClasica;
                leida = true;
                if (id != vistaId) { vistaId = id; RegistrarMenu(); StateHasChanged(); await AvisarAsync(); return; }
                RegistrarMenu();
            }
            leida = true;
            if (Personalizada) await AvisarAsync();
        }
        if (Personalizada && yo is not null)
        {
            try { await JS.InvokeVoidAsync("sigVistaReg.aplicar", refPanel, Json(), Disenando, yo); } catch { }
        }
    }

    private List<Widget> Actuales() =>
        Disenando ? widgets : vistas.FirstOrDefault(v => v.Id == vistaId) is { } v ? VistasModelo.Leer(v.Json) : new();

    private string Json() => System.Text.Json.JsonSerializer.Serialize(Actuales().Select(w => new { id = w.Id, x = w.X, y = w.Y, w = w.W, h = w.H }));

    private async Task AvisarAsync()
    {
        var f = vistaId + "|" + string.Join(",", Actuales().Select(w => w.Id + w.Kpi));
        if (f == firmaPanel) return;
        firmaPanel = f;
        await OnCambio.InvokeAsync();
    }

    // Vistas en el menú del ícono del encabezado (también con una sola: así se ve el enlace para crear más).
    private void RegistrarMenu()
    {
        if (Disenando) return;
        MenuIco.Registrar(new SigWeb.Data.Navegacion.MenuIcono.Menu
        {
            Titulo = "Vista", Dueno = this, Actual = vistaId.ToString(),
            Opciones = vistas.Select(v => new SigWeb.Data.Navegacion.MenuIcono.Opcion(v.Id.ToString(), v.Nombre, null, v.Predeterminada))
                .Prepend(new SigWeb.Data.Navegacion.MenuIcono.Opcion(VistasModelo.IdClasica.ToString(), VistasModelo.NombreClasica, "La pantalla de siempre")).ToList(),
            Elegir = c => InvokeAsync(() => CambiarVistaAsync(int.TryParse(c, out var i) ? i : VistasModelo.IdClasica)),
            EnlaceGestion = "editor?pestana=vistas", TituloGestion = "Crear y diseñar vistas",
        });
    }

    private async Task CambiarVistaAsync(int id)
    {
        vistaId = id;
        try { await JS.InvokeVoidAsync("localStorage.setItem", ClaveLocal, vistaId.ToString()); } catch { }
        RegistrarMenu();
        StateHasChanged();
        await AvisarAsync();
    }

    [JSInvokable]
    public Task MovidaVista(string json)
    {
        if (!Disenando) return Task.CompletedTask;
        foreach (var e in System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateArray())
        {
            var w = widgets.FirstOrDefault(x => x.Id == e.GetProperty("id").GetString());
            if (w is null) continue;
            w.X = e.GetProperty("x").GetInt32(); w.Y = e.GetProperty("y").GetInt32(); w.W = e.GetProperty("w").GetInt32(); w.H = e.GetProperty("h").GetInt32();
        }
        return InvokeAsync(StateHasChanged);
    }

    private void Agregar(TipoWidget t)
    {
        var (x, y) = DisenoModelo.Hueco(widgets.Select(o => (o.X, o.Y, o.W, o.H)).ToList(), t.W, t.H);
        var w = new Widget { Tipo = t.Tipo, X = x, Y = y, W = t.W, H = t.H, Kpi = VistasModelo.OpcionesDe(Modulo, t.Tipo).FirstOrDefault().Clave };
        widgets.Add(w);
        sel = w.Id;
        AlCambiar();
    }

    private void Quitar(string id) { widgets.RemoveAll(w => w.Id == id); if (sel == id) sel = null; AlCambiar(); }
    private void Restablecer() { widgets = VistasModelo.InicialDe(Modulo); sel = null; AlCambiar(); }
    private void AlCambiar() => _ = InvokeAsync(AvisarAsync);

    private async Task Guardar()
    {
        mensaje = null;
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = await VistasModelo.GuardarAsync(db, Modulo, editId, nombre, widgets);
            try { await JS.InvokeVoidAsync("localStorage.setItem", ClaveLocal, id.ToString()); } catch { }
        }
        catch (Exception ex) { mensaje = "No se pudo guardar la vista: " + ex.Message; return; }
        Nav.NavigateTo("editor?pestana=vistas");
    }

    private void Cancelar() => Nav.NavigateTo("editor?pestana=vistas");

    private string TituloBase(Widget w) =>
        VistasModelo.OpcionesDe(Modulo, w.Tipo).FirstOrDefault(o => o.Clave == w.Kpi).Nombre
        ?? VistasModelo.TipoDe(Modulo, w.Tipo)?.Nombre ?? w.Tipo;

    public string Titulo(Widget w) => !string.IsNullOrWhiteSpace(w.Titulo) ? w.Titulo! : TituloBase(w);

    public void Dispose() { yo?.Dispose(); MenuIco.Quitar(this); }
}
