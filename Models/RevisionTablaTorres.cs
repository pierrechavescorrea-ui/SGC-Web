namespace SigWeb.Models;

// Control de revisiones de la tabla de torres (Trazado › Bases de datos › Tabla de torres). Cada carga de una versión
// nueva del Excel (p. ej. "A29-10") es una revisión con su copia del archivo y la lista de cambios por torre (valor
// anterior → nuevo, con la observación del comunicado). El historial es permanente (no depende de la Papelera) y la
// revisión vigente se puede deshacer devolviendo los valores anteriores.
public enum EstadoRevisionTabla
{
    Vigente = 0,    // la última aplicada
    Anterior = 1,   // reemplazada por una posterior
    Deshecha = 2    // revertida: sus cambios se devolvieron
}

public class RevisionTablaTorres
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;        // A29-10
    // Qué se cargó: "Tabla de torres", "KMZ" o "Coordenadas". La vigente y el deshacer son por origen.
    public string Origen { get; set; } = OrigenTabla;
    public const string OrigenTabla = "Tabla de torres", OrigenKmz = "KMZ", OrigenCoordenadas = "Coordenadas";
    public DateTime? Fecha { get; set; }                        // fecha de la revisión / del comunicado
    public string? Descripcion { get; set; }                    // texto del comunicado o resumen de los cambios
    public string NombreArchivo { get; set; } = string.Empty;
    public string? RutaArchivo { get; set; }                    // copia del Excel (/uploads/tabla-torres/…)
    public DateTime FechaCarga { get; set; } = DateTime.Now;
    public EstadoRevisionTabla Estado { get; set; } = EstadoRevisionTabla.Vigente;
    public int Torres { get; set; }                             // torres con algún cambio
    public int Cambios { get; set; }
    public DateTime? FechaDeshecha { get; set; }
    public string? MotivoDeshacer { get; set; }
}

public enum TipoCambioTabla
{
    Nuevo = 0,        // la torre no tenía ese dato
    Cambiado = 1,     // valor distinto al anterior
    Coordenada = 2    // ubicación de una pata (ValorAnterior/ValorNuevo = "lat;lon")
}

public class CambioTablaTorres
{
    public int Id { get; set; }
    public int RevisionTablaTorresId { get; set; }
    public int ElementoTrazadoId { get; set; }
    public string CodigoTorre { get; set; } = string.Empty;     // copia para leer el historial aunque la torre cambie
    public int? ComponenteId { get; set; }                      // pata (null = dato de la torre)
    public string Ambito { get; set; } = "Torre";               // Torre | Pata A…
    public string Clave { get; set; } = string.Empty;           // columna (Cimentación — Tipo…)
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public TipoCambioTabla Tipo { get; set; }
    public string? Observacion { get; set; }                    // de la torre en esta revisión (p. ej. del comunicado)
    // Para deshacer: el campo que dejó esta revisión y el que reemplazó (CampoExtraTorre o CampoExtraComponente).
    public int? CampoNuevoId { get; set; }
    public int? CampoAnteriorId { get; set; }
}
