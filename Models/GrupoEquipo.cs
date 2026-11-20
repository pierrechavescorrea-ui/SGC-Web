namespace SigWeb.Models;

public class GrupoEquipo : IGrupoListado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? IconoCatalogoId { get; set; }
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
