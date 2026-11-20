namespace SigWeb.Models;

// Documento del Direccionamiento estratégico (misión, visión, políticas, valores, código de ética…),
// clasificado por Tipo. Igual que Certificaciones, sus archivos y versiones son los de un documento del
// Listado maestro (DocumentoMaestro, Tipo documental "Direccionamiento estratégico"): Actualizar, obsoletos
// y "Ver todos" se hacen en Documentos (/documentos?doc={id}&abrir=…). Un PDF que trae varios documentos
// unidos se divide por rangos de páginas al cargarlo: cada parte queda como el archivo vigente de su
// documento. Eliminar este registro no borra el documento del listado.
public class DocumentoDireccionamiento
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;    // ej. "Visión", "Misión", "Política integrada"
    public string Titulo { get; set; } = string.Empty;
    public int Orden { get; set; }
    public int? DocumentoMaestroId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
