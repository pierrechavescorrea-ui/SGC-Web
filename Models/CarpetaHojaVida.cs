namespace SigWeb.Models;

// Definición global: los adjuntos pertenecen a una torre concreta.
public class CarpetaHojaVida
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string NombreNormalizado { get; set; } = "";
    // null = hoja de vida; nombre del elemento recortado y en minúsculas = ámbito global de ese elemento.
    public string? ClaveElemento { get; set; }
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

public class ArchivoCarpetaHojaVida
{
    public int Id { get; set; }
    public int CarpetaHojaVidaId { get; set; }
    public CarpetaHojaVida Carpeta { get; set; } = null!;
    public int ElementoTrazadoId { get; set; }
    public int? ComponenteId { get; set; }
    public Componente? Componente { get; set; }
    public string NombreOriginal { get; set; } = "";
    public string Ruta { get; set; } = "";
    public long Tamano { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Descripcion { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.Now;
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

public static class AmbitoCarpetasHojaVida
{
    // Mismo criterio de identidad que Vincular planos en Trazado.
    public static string ClaveElemento(string nombre) => nombre.Trim().ToLowerInvariant();
}
