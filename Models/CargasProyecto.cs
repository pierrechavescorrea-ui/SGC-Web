namespace SigWeb.Models;

// Carga de proyecto desde la plantilla Excel (Configuración › Catálogos › Plantillas de proyecto).
// Una carga agrupa todo lo que un libro aplicó en todos los módulos, para poder deshacerlo completo.
// Cada cambio queda en CambioCargaProyecto con el estado anterior (actualizaciones) o como alta.
public class CargaProyecto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public string Modulos { get; set; } = string.Empty;        // claves de módulo separadas por coma
    public int Altas { get; set; }
    public int Actualizaciones { get; set; }
    public string Estado { get; set; } = EstadoCargaProyecto.Aplicada;
    public DateTime? FechaDeshecha { get; set; }
}

public static class EstadoCargaProyecto
{
    public const string Aplicada = "Aplicada";
    public const string Deshecha = "Deshecha";
}

public class CambioCargaProyecto
{
    public int Id { get; set; }
    public int CargaProyectoId { get; set; }
    public int Orden { get; set; }                              // orden de aplicación (se deshace al revés)
    public string Modulo { get; set; } = string.Empty;          // clave del adaptador (IPlantillaModulo.Clave)
    public string Entidad { get; set; } = string.Empty;         // nombre de la clase EF (Tiro, ElementoTrazado…)
    public int EntidadId { get; set; }
    public string Operacion { get; set; } = OperacionCambioCarga.Alta;
    public string? AntesJson { get; set; }                      // valores escalares antes de actualizar (null en altas)
}

public static class OperacionCambioCarga
{
    public const string Alta = "Alta";
    public const string Actualizacion = "Actualizacion";
}
