using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SigWeb.Data.Plantillas;
using SigWeb.Data.Seguridad;
using SigWeb.Models;

namespace SigWeb.Components.Layout;

// Lógica de Plantillas de proyecto (PanelPlantillasProyecto.razor tiene el marcado).
// Los datos se leen en OnInitializedAsync o en los métodos, nunca desde el render.
public partial class PanelPlantillasProyecto
{
    [Parameter] public string? Modulo { get; set; }
    [Parameter] public bool MostrarDescarga { get; set; } = true;
    [Parameter] public bool MostrarCarga { get; set; } = true;
    [Parameter] public bool PuedeDeshacer { get; set; } = true;
    [Parameter] public EventCallback OnCambio { get; set; }

    private bool IncluyeModulo(string clave) =>
        Modulo is null ? !PlantillaCostos.EsDeCostos(clave) : clave == Modulo;

    private const int LimiteFilas = 300;
    private const long TamanoMaximo = 20 * 1024 * 1024;

    private List<ModuloPlantilla> modulos = new();
    private List<CargaProyecto> cargas = new();
    private List<ResumenModulo> resumenModulos = new();
    private List<PlanFila> filasPreview = new();
    private List<ErrorCelda> erroresPreview = new();
    private ServicioPlantillas.ResultadoValidacion? resultado;
    private bool sinFilas;
    private bool conDatos = true;

    private string nombreArchivo = "";
    private string responsable = "";
    private string motivo = "";
    private string mensajeCarga = "";
    private string errorCarga = "";
    private string errorDescarga = "";
    private string errorHistorial = "";

    private bool ocupado, validando, aplicando, cargandoHistorial;
    private int archivoKey;
    private int? confirmarDeshacer;

    private bool PuedeAplicar => resultado is { Valido: true } r && r.Altas + r.Actualizaciones > 0;

    protected override async Task OnInitializedAsync()
    {
        modulos = Servicio.Modulos.Where(m => IncluyeModulo(m.Clave))
            .OrderBy(m => m.Orden).ThenBy(m => m.Nombre)
            .Select(m => new ModuloPlantilla(m.Clave, m.Nombre))
            .ToList();
        if (MostrarCarga) await CargarHistorialAsync();
    }

    private async Task CargarHistorialAsync()
    {
        cargandoHistorial = true;
        try
        {
            cargas = (await Servicio.CargasAsync()).Where(c =>
                c.Modulos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } claves &&
                claves.All(IncluyeModulo)).ToList();
            errorHistorial = "";
        }
        catch (Exception ex)
        {
            cargas = new();
            errorHistorial = "No se pudo cargar el historial: " + (ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            cargandoHistorial = false;
        }
    }

    // ---------------------------------------------------------------- Descargar

    private async Task DescargarAsync()
    {
        if (ocupado) return;
        var claves = modulos.Where(m => m.Marcado).Select(m => m.Clave).ToList();
        if (claves.Count == 0) return;
        if (conDatos && claves.Any(c => !Servicio.PuedeUsar(c, Accion.Descargar)))
        {
            errorDescarga = "Quite de la selección los módulos sin permiso de descarga o descargue la plantilla sin datos.";
            return;
        }
        ocupado = true;
        errorDescarga = "";
        StateHasChanged();
        try
        {
            var datos = conDatos ? 1 : 0;
            var url = Modulo is null
                ? $"/api/plantillas/proyecto.xlsx?modulos={Uri.EscapeDataString(string.Join(",", claves))}&datos={datos}"
                : $"/api/plantillas/costos.xlsx?modulo={Uri.EscapeDataString(Modulo)}&datos={datos}";
            try
            {
                await JS.InvokeVoidAsync("sigDescargarUrl", url);
            }
            catch (JSException)
            {
                Nav.NavigateTo(url, forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            errorDescarga = "No se pudo descargar: " + (ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            ocupado = false;
            StateHasChanged();
        }
    }

    // ---------------------------------------------------------------- Cargar

    private async Task ElegirArchivoAsync(InputFileChangeEventArgs e)
    {
        if (ocupado) return;
        var archivo = e.File;
        if (archivo is null) return;
        mensajeCarga = "";
        errorCarga = "";
        if (!archivo.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errorCarga = "El archivo debe ser .xlsx.";
            return;
        }
        if (archivo.Size > TamanoMaximo)
        {
            errorCarga = "El archivo supera 20 MB.";
            return;
        }

        ocupado = true;
        validando = true;
        resultado = null;
        sinFilas = false;
        resumenModulos = new();
        filasPreview = new();
        erroresPreview = new();
        nombreArchivo = "";
        StateHasChanged();
        try
        {
            using var ms = new MemoryStream();
            await using (var s = archivo.OpenReadStream(TamanoMaximo)) await s.CopyToAsync(ms);
            ms.Position = 0;
            resultado = await Servicio.ValidarAsync(ms, archivo.Name, modulo: Modulo);
            nombreArchivo = archivo.Name;
            responsable = "";
            motivo = "";
            PrepararVista(resultado);
        }
        catch (Exception ex)
        {
            resultado = null;
            errorCarga = "No se pudo leer el archivo: " + (ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            validando = false;
            ocupado = false;
            StateHasChanged();
        }
    }

    private void PrepararVista(ServicioPlantillas.ResultadoValidacion v)
    {
        sinFilas = v.Planes.Count == 0;
        resumenModulos = v.Planes
            .Select(p => new ResumenModulo(p.Nombre, p.Altas, p.Actualizaciones,
                p.Filas.Count(f => f.Operacion == OperacionFila.SinCambios)))
            .ToList();
        filasPreview = v.Planes.SelectMany(p => p.Filas).ToList();
        erroresPreview = v.TodosLosErrores.ToList();
    }

    private async Task AplicarAsync()
    {
        if (ocupado || resultado is null || !PuedeAplicar) return;
        if (string.IsNullOrWhiteSpace(motivo))
        {
            errorCarga = "Indique el motivo de la carga.";
            return;
        }

        ocupado = true;
        aplicando = true;
        errorCarga = "";
        mensajeCarga = "";
        StateHasChanged();
        try
        {
            var carga = await Servicio.AplicarAsync(resultado, responsable, motivo);
            mensajeCarga = $"Carga aplicada: {carga.Altas} altas, {carga.Actualizaciones} actualizaciones.";
            LimpiarVista();
            await CargarHistorialAsync();
            await OnCambio.InvokeAsync();
        }
        catch (Exception ex)
        {
            errorCarga = "No se pudo aplicar la carga: " + (ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            aplicando = false;
            ocupado = false;
            StateHasChanged();
        }
    }

    private void CancelarVista()
    {
        LimpiarVista();
        errorCarga = "";
    }

    private void LimpiarVista()
    {
        resultado = null;
        sinFilas = false;
        resumenModulos = new();
        filasPreview = new();
        erroresPreview = new();
        nombreArchivo = "";
        responsable = "";
        motivo = "";
        archivoKey++;
    }

    // ---------------------------------------------------------------- Historial

    private async Task DeshacerAsync(CargaProyecto carga)
    {
        if (ocupado || !PuedeDeshacer || !cargas.Any(c => c.Id == carga.Id)) return;
        ocupado = true;
        errorHistorial = "";
        StateHasChanged();
        try
        {
            await Servicio.DeshacerAsync(carga.Id);
            confirmarDeshacer = null;
            await CargarHistorialAsync();
            await OnCambio.InvokeAsync();
        }
        catch (Exception ex)
        {
            errorHistorial = "No se pudo deshacer: " + (ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            ocupado = false;
            StateHasChanged();
        }
    }

    private string NombresModulos(string claves) =>
        string.Join(", ", (claves ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => modulos.FirstOrDefault(m => string.Equals(m.Clave, k, StringComparison.OrdinalIgnoreCase))?.Nombre ?? k));

    private sealed class ModuloPlantilla
    {
        public string Clave { get; }
        public string Nombre { get; }
        public bool Marcado { get; set; } = true;

        public ModuloPlantilla(string clave, string nombre)
        {
            Clave = clave;
            Nombre = nombre;
        }
    }

    private sealed record ResumenModulo(string Nombre, int Altas, int Actualizaciones, int SinCambios);
}