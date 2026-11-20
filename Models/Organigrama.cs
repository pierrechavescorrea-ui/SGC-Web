namespace SigWeb.Models;

// Organigrama con control de cambios: cada versión es una foto completa de la estructura
// (cargos y jefes). Solo el borrador se edita; al publicarlo pasa a Vigente y la vigente
// anterior a Obsoleta (con su fecha de fin). Un cargo conserva su Clave de versión en versión,
// así se sigue su historia (nombre, ocupante, jefe) y se comparan versiones.
public static class EstadoOrganigrama
{
    public const string Borrador = "Borrador";
    public const string Vigente = "Vigente";
    public const string Obsoleta = "Obsoleta";
}

public class VersionOrganigrama
{
    public int Id { get; set; }
    public int Numero { get; set; }                       // consecutivo: v1, v2…
    public string Titulo { get; set; } = string.Empty;
    public string? Alcance { get; set; }                  // proyecto / tramo / contrato
    public string Estado { get; set; } = EstadoOrganigrama.Borrador;
    public DateTime? VigenteDesde { get; set; }
    public DateTime? VigenteHasta { get; set; }           // se llena al publicar la siguiente
    public string? MotivoCambio { get; set; }
    public string? Elaboro { get; set; }
    public string? Reviso { get; set; }
    public string? Aprobo { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public string? Origen { get; set; }                   // ej. archivo Excel del que se cargó
    public string? Notas { get; set; }
    public int? VersionBaseId { get; set; }               // versión de la que se copió
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

public class CargoOrganigrama
{
    public int Id { get; set; }
    public int VersionOrganigramaId { get; set; }
    public Guid Clave { get; set; } = Guid.NewGuid();     // identidad del cargo entre versiones
    public int? JefeId { get; set; }                      // cargo de la misma versión; null = raíz
    public string Nombre { get; set; } = string.Empty;
    public string? Ocupante { get; set; }                 // una o varias personas (separadas por coma o salto de línea)
    public string? Area { get; set; }
    public int? Plazas { get; set; }                      // null = no definida
    public bool Staff { get; set; }                       // apoyo/asesoría del jefe (se dibuja al lado)
    public int Orden { get; set; }
    public string? Notas { get; set; }
    public string? Supuesto { get; set; }                 // relación o dato no deducible del origen

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
