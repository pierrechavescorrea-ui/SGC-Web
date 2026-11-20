namespace SigWeb.Models;

// Certificado de sistema de gestión de la empresa (ISO 9001, 14001, 45001…) que se exhibe en la
// vitrina de Certificaciones. Sus archivos y versiones son los de un documento del Listado maestro
// (DocumentoMaestro, Tipo documental "Certificados"): Actualizar, obsoletos y "Ver todos" se hacen
// en Documentos (/documentos?doc={id}&abrir=…). Eliminar el certificado no borra el documento.
public class Certificacion
{
    public int Id { get; set; }
    public string Norma { get; set; } = string.Empty;   // ej. "ISO 9001:2015"
    public string? Sistema { get; set; }                // ej. "Calidad", "Ambiental", "SST"
    public string? Alcance { get; set; }
    public string? Organismo { get; set; }              // organismo certificador
    public string? Numero { get; set; }                 // número del certificado
    public DateTime? Emision { get; set; }
    public DateTime? Vencimiento { get; set; }
    public int Orden { get; set; }
    public int? DocumentoMaestroId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
