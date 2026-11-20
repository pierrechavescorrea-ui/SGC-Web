using Microsoft.AspNetCore.Components;
using SigWeb.Data;
using SigWeb.Data.Costos;

namespace SigWeb.Components.Costos;

// Base de las vistas editables de Costos y presupuestos: parámetros comunes y el contrato con el
// encabezado de la página (IVistaCostos). Las vistas solo leen la instantánea en el render; los
// cambios se escriben en los manejadores y luego se pide recargar (OnCambio).
public abstract class VistaCostosBase : ComponentBase, IVistaCostos
{
    [Inject] protected AppDbContext Db { get; set; } = default!;
    [Parameter] public InstantaneaCostos S { get; set; } = default!;
    [Parameter] public DateTime Corte { get; set; }
    [Parameter] public EventCallback OnCambio { get; set; }
    [Parameter] public EventCallback OnEstado { get; set; }
    [Parameter] public EventCallback<string> OnError { get; set; }

    public bool Editando { get; protected set; }
    public bool Guardando { get; protected set; }
    public virtual bool PuedeEditar => true;
    public virtual bool PuedeAgregar => true;
    public virtual string? SelectorExportar => null;

    public virtual Task EditarAsync() { Editando = true; return Avisar(); }
    public virtual Task AgregarAsync() => EditarAsync();
    public virtual void Cancelar() { Editando = false; StateHasChanged(); }

    public async Task GuardarAsync()
    {
        Guardando = true;
        await Avisar();
        try
        {
            await GuardarCambiosAsync();
            Editando = false;
            await OnCambio.InvokeAsync();
        }
        catch (Exception ex)
        {
            Db.ChangeTracker.Clear();
            await OnError.InvokeAsync("No se pudo guardar: " + (ex.InnerException?.Message ?? ex.Message));
        }
        finally
        {
            Guardando = false;
            await Avisar();
        }
    }

    protected abstract Task GuardarCambiosAsync();

    // true si cambió la instantánea o el corte desde la última vez. La página se vuelve a pintar a
    // menudo (y los EventCallback siempre llegan como "cambiados"): recalcular solo cuando hace falta.
    private InstantaneaCostos? _sPrevia;
    private DateTime _cortePrevio;
    protected bool DatosNuevos()
    {
        if (ReferenceEquals(S, _sPrevia) && Corte == _cortePrevio) return false;
        _sPrevia = S; _cortePrevio = Corte;
        return true;
    }

    protected async Task Avisar()
    {
        StateHasChanged();
        await OnEstado.InvokeAsync();
    }

    protected static decimal? Dec(object? v) =>
        decimal.TryParse(v?.ToString()?.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

    protected static DateTime? Fec(object? v) => DateTime.TryParse(v?.ToString(), out var f) ? f.Date : null;
}
