namespace SigWeb.Models;

public interface IGrupoListado
{
    int Id { get; set; }
    string Nombre { get; set; }
    int? IconoCatalogoId { get; set; }
    int Orden { get; set; }
    bool Activo { get; set; }
    bool Eliminado { get; set; }
    DateTime? FechaEliminacion { get; set; }
}
