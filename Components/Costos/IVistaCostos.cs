namespace SigWeb.Components.Costos;

// Lo que la página Costos y presupuestos le pide a la vista activa para pintar los botones del
// encabezado (Editar, Agregar, Guardar, Cancelar, Exportar). Las acciones van en el encabezado.
public interface IVistaCostos
{
    bool Editando { get; }
    bool PuedeEditar { get; }
    bool PuedeAgregar { get; }
    bool Guardando { get; }
    string? SelectorExportar { get; }
    Task EditarAsync();
    Task AgregarAsync();
    Task GuardarAsync();
    void Cancelar();
}
