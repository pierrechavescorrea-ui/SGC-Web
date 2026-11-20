namespace SigWeb.Models;

// Categoría (pestaña) del listado de Materiales, como las de Documentos y Equipos (2026-11-16).
public class GrupoMaterial : IGrupoListado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? IconoCatalogoId { get; set; }
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;
    public string? Proyecto { get; set; }   // filtro por proyecto futuro

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Categoría (pestaña) del listado de resultados de UN tipo de ensayo: cada ensayo tiene las suyas (2026-11-16).
public class GrupoResultadoEnsayo : IGrupoListado
{
    public int Id { get; set; }
    public int TipoEnsayoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? IconoCatalogoId { get; set; }
    public int Orden { get; set; } = 0;
    public bool Activo { get; set; } = true;
    public string? Proyecto { get; set; }

    // ---------- Papelera de reciclaje ----------
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}
