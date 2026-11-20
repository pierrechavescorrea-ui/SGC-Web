namespace SigWeb.Models;

// Vista configurable de un registro (2026-11-16): p. ej. "Plan por plan" de Planes de acción armado como un dashboard de
// widgets (Data/Vistas). La vista "Clásica" está en el código y no se guarda aquí; estas son las que crea el usuario
// (Configuración › Catálogos › Vistas). Cada una se puede elegir en el módulo; Predeterminada = la que abre si el
// navegador no tiene una elegida.
public class VistaRegistro
{
    public int Id { get; set; }
    public string Modulo { get; set; } = string.Empty;       // p. ej. "planes-accion"
    public string Nombre { get; set; } = string.Empty;
    public string Json { get; set; } = "[]";                // List<Data.Vistas.Widget>
    public bool Predeterminada { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime FechaModificacion { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
