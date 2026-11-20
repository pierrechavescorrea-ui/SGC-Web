using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Modelos3D;

namespace SigWeb.Components.Pages;

// Piloto: modelo de detalle total (piezas, perforaciones y tornillería) generado por
// tools/planos-dwg/detalle-bp6/armar-detalle.mjs. No reemplaza ni se vincula al modelo BP6 de Línea 3D.
public partial class ModeloDetalle
{
    [Inject] private IServiceScopeFactory Scopes { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "modelo")] public string? Modelo { get; set; }
    [SupplyParameterFromQuery(Name = "escena")] public string? Escena { get; set; }

    private ElementReference contenedor, marco;
    private IJSObjectReference? modulo, visor;
    private string? error;

    // Solo archivos de wwwroot/modelos3d/detalle (sin rutas).
    private string Url => "modelos3d/detalle/" + (string.IsNullOrWhiteSpace(Modelo) || Modelo.IndexOfAny(['/', (char)92]) >= 0 || Modelo.Contains("..")
        ? "bp6-torre.json" : Modelo);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            modulo = await JS.InvokeAsync<IJSObjectReference>("import", "./js/visorDetalle.js");
            visor = await modulo.InvokeAsync<IJSObjectReference>("crear", TimeSpan.FromMinutes(1), contenedor, Url, marco, Escena);
            await EnlazarPlanosAsync();
        }
        catch (Exception ex) when (ex is JSException or TaskCanceledException)
        {
            error = "No se pudo abrir el modelo de detalle: " + ex.Message;
            StateHasChanged();
        }
    }

    private async Task EnlazarPlanosAsync()
    {
        var actual = visor;
        if (actual is null) return;
        try
        {
            var referencias = await actual.InvokeAsync<EnlacePlanos.Referencia[]>("referenciasPlanos");
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var enlaces = await EnlacePlanos.ResolverAsync(db, referencias);
            await actual.InvokeVoidAsync("setPlanos", enlaces.Select(e => new { clave = e.Clave, texto = e.Texto, url = e.Url }).ToArray(), "listo");
        }
        catch (JSDisconnectedException) { }
        catch (Exception)
        {
            // Una consulta fallida no debe impedir seleccionar piezas ni presentarse como ausencia del plano.
            try { await actual.InvokeVoidAsync("setPlanos", Array.Empty<object>(), "error"); }
            catch (JSException) { }
            catch (TaskCanceledException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (visor is not null) { await visor.InvokeVoidAsync("dispose"); await visor.DisposeAsync(); }
            if (modulo is not null) await modulo.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
