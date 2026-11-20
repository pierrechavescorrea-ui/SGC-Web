namespace SigWeb.Models;

// Modelo 3D de torre cargado desde la app (Modelos 3D › Cargar): el IFC que se genera desde los planos
// (DWG→DXF) y, opcional, el JSON del mismo modelo (lista de materiales, Línea 3D y Avance de obra), su miniatura y
// el informe de validación. Cada carga con el mismo código es una versión nueva; la vigente es la de mayor número
// que no esté en la Papelera, y reemplaza al modelo del repositorio (wwwroot/modelos-ifc) que tenga ese código.
// Archivos en wwwroot/uploads/modelos-ifc (IFC, miniatura, informes/) y wwwroot/uploads/modelos3d (JSON),
// con nombre c-{código}-v{versión} para que el .ifc y el .json se emparejen como los del repositorio.
public class ModeloIfcCargado
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;        // BP7, AAE6… (se compara sin espacios ni guiones)
    public int Version { get; set; } = 1;
    public string Nombre { get; set; } = string.Empty;        // tipo en la tabla de torres (por defecto el código)
    public string? Familia { get; set; }
    public string? Descripcion { get; set; }
    public string? Cambios { get; set; }                      // qué cambia en esta versión

    public string ArchivoIfc { get; set; } = string.Empty;    // ruta web /uploads/modelos-ifc/c-bp7-v1.ifc
    public string NombreOriginalIfc { get; set; } = string.Empty;
    public string? ArchivoJson { get; set; }                  // /uploads/modelos3d/c-bp7-v1.json
    public string? ArchivoMiniatura { get; set; }
    public string? ArchivoInforme { get; set; }               // informe de validación (PDF, Excel, MD…)
    public string? NombreOriginalInforme { get; set; }

    // Leídos del Pset SigWeb_Torre del IFC al cargarlo.
    public int Validaciones { get; set; }
    public int Supuestos { get; set; }

    public DateTime FechaCarga { get; set; } = DateTime.Now;
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
