using Microsoft.JSInterop;

namespace SigWeb.Data.Navegacion;

// Puente con wwwroot/js/volver.js: botón «volver» y memoria de la vista (lo seleccionado viaja en la
// URL para que, al regresar con Atrás, la página se abra otra vez sobre la misma torre o elemento).
// Si el circuito ya se cerró (la página se está yendo), las llamadas se ignoran.
public static class VistaJs
{
    // Vuelve a la pantalla anterior de la app; si no hay ninguna en la pestaña, a «respaldo» (o al Inicio).
    public static Task VolverAsync(this IJSRuntime js, string? respaldo = null) => Llamar(js, "sigVolver", respaldo ?? "/");

    // Anota valores en la consulta de la URL actual sin navegar. null, "" o false quitan el parámetro.
    // «ruta» es la ruta de la página que anota: si la pantalla ya cambió, no se toca nada.
    public static Task RecordarVistaAsync(this IJSRuntime js, string ruta, IDictionary<string, object?> valores) => Llamar(js, "sigRecordarVista", valores, ruta);

    // Cambia la ruta de la URL actual sin navegar (p. ej. /riesgos → /riesgos/12), si sigue en «desde».
    public static Task RecordarRutaAsync(this IJSRuntime js, string ruta, string desde) => Llamar(js, "sigRecordarRuta", ruta, desde);

    private static async Task Llamar(IJSRuntime js, string funcion, params object?[] args)
    {
        try { await js.InvokeVoidAsync(funcion, args); }
        catch (Exception e) when (e is JSDisconnectedException or TaskCanceledException or JSException or InvalidOperationException) { }
    }
}

// Vistas internas que el usuario ya vio dentro de una misma página (pestañas, detalle abierto desde la
// lista…), de la más vieja a la más reciente y sin repetir. «Volver» saca la última distinta de la
// actual; si no queda ninguna, la página vuelve a la pantalla anterior. Se guarda en la entrada del
// historial (sigGuardarPila), así que sobrevive a ir a otra pantalla y regresar con Atrás.
// Cada entrada es "vista" o "vista:detalle" (p. ej. "torre:12"): se compara por la vista, y el detalle
// es lo que había seleccionado en ella para restaurarlo.
public sealed class PilaVistas
{
    private const int Max = 15;
    public static string Vista(string e) => e.Split(':', 2)[0];
    private List<string> pila = new();
    private bool sucia;

    public void Cambio(string desde, string hacia)
    {
        if (Vista(desde) == Vista(hacia)) return;
        pila.RemoveAll(x => Vista(x) == Vista(desde) || Vista(x) == Vista(hacia));
        pila.Add(desde);
        if (pila.Count > Max) pila.RemoveAt(0);
        sucia = true;
    }

    public string? Sacar(string actual)
    {
        while (pila.Count > 0)
        {
            var x = pila[^1];
            pila.RemoveAt(pila.Count - 1);
            sucia = true;
            if (Vista(x) != Vista(actual)) return x;
        }
        return null;
    }

    // Uso típico: Observar(estado) después de cada render (anota cada cambio de vista) y, en el botón
    // volver, Volver(estado): devuelve la vista a restaurar, o null para ir a la pantalla anterior.
    private string? previo;
    private bool sacando;

    public void Observar(string actual)
    {
        if (previo is not null && !sacando) Cambio(previo, actual);
        sacando = false;
        previo = actual;
    }

    public string? Volver(string actual)
    {
        var x = Sacar(actual);
        if (x is not null) sacando = true;
        return x;
    }

    public async Task CargarAsync(IJSRuntime js, string clave)
    {
        try
        {
            var guardada = await js.InvokeAsync<string[]>("sigLeerPila", clave);
            pila = guardada.Where(x => !pila.Any(y => Vista(y) == Vista(x))).Concat(pila).ToList();
        }
        catch (Exception e) when (e is JSDisconnectedException or TaskCanceledException or JSException or InvalidOperationException) { }
    }

    public async Task GuardarAsync(IJSRuntime js, string clave, string ruta)
    {
        if (!sucia) return;
        sucia = false;
        try { await js.InvokeVoidAsync("sigGuardarPila", clave, pila, ruta); }
        catch (Exception e) when (e is JSDisconnectedException or TaskCanceledException or JSException or InvalidOperationException) { }
    }
}
