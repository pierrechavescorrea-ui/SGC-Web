namespace SigWeb.Models;

// Consecutivos de comunicados de salida (Comunicados › Consecutivos, y /consecutivo en el bot de Telegram).
// Código = {Remite}-{Destinatario}-{Proyecto}-{CodigoProyecto}-{Numero}-{Año 2 dígitos},
// p. ej. CONTRATISTA-INTERVENTORIA-PROYECTO-001-0001-26. Cambian el número (consecutivo) y el año (el de la fecha de envío).

// Plantilla del código (una por destinatario/proyecto). No se borra: se desactiva.
public class PlantillaConsecutivo
{
    public int Id { get; set; }
    public string Remite { get; set; } = string.Empty;          // CONTRATISTA
    public string Destinatario { get; set; } = string.Empty;    // INTERVENTORIA
    public string Proyecto { get; set; } = string.Empty;        // PROYECTO
    public string CodigoProyecto { get; set; } = string.Empty;  // 297
    // false = el número sigue de un año a otro (solo cambia el sufijo del año); true = vuelve a 1 cada año.
    public bool ReiniciaCadaAnio { get; set; }
    // Cifras mínimas del número (4 → 0001, por defecto); 0 = sin ceros a la izquierda.
    public int CifrasNumero { get; set; } = 4;
    // "Iniciar en": el siguiente número nunca es menor que este (p. ej. al empezar a usar SigWeb con un listado ya
    // cargado). Con reinicio anual aplica solo al año en que se fijó (AnioNumeroInicial, dos dígitos). null = sin mínimo.
    public int? NumeroInicial { get; set; }
    public int? AnioNumeroInicial { get; set; }
    public bool Activa { get; set; } = true;
    public int Orden { get; set; }

    // Ninguna parte es obligatoria: las vacías no aparecen en el código.
    public string Prefijo => string.Join("-", new[] { Remite, Destinatario, Proyecto, CodigoProyecto }
        .Select(x => (x ?? "").Trim()).Where(x => x.Length > 0));
}

public enum EstadoConsecutivo
{
    Reservado = 0,   // entregado, aún sin confirmar el envío
    Enviado = 1,     // confirmado: ya es un Comunicado del listado (ComunicadoId)
    Anulado = 2      // no se usó; el número no se reutiliza (trazabilidad)
}

// Un consecutivo entregado a una persona. Al confirmar el envío se crea (o se liga) el Comunicado de salida
// con estos datos; si responde a otro comunicado, a ese se le anota la respuesta.
public class ReservaConsecutivo
{
    public int Id { get; set; }
    public int PlantillaConsecutivoId { get; set; }
    public int Numero { get; set; }
    public int Anio { get; set; }                         // dos dígitos (26)
    public string Codigo { get; set; } = string.Empty;    // único
    public EstadoConsecutivo Estado { get; set; } = EstadoConsecutivo.Reservado;

    // Quién lo pidió y por dónde.
    public string SolicitadoPor { get; set; } = string.Empty;
    public string Origen { get; set; } = "SigWeb";        // SigWeb | Telegram
    public long? ChatIdTelegram { get; set; }
    public DateTime FechaSolicitud { get; set; } = DateTime.Now;

    // Datos del comunicado (los mismos del listado de Comunicados).
    public DateTime FechaEnvioPrevista { get; set; } = DateTime.Today;
    public string Asunto { get; set; } = string.Empty;    // → Comunicado.Descripcion
    public string? DirigidoA { get; set; }                // persona/cargo destinatario (→ Observaciones)
    public string? Tipo { get; set; }                     // Externa, Interna…
    public string? RespondeA { get; set; }                // consecutivo del comunicado que responde
    public bool? RequiereRespuesta { get; set; }
    public string? ResponsableRespuesta { get; set; }     // quién debe responder (Comunicado.ResponsableRespuesta)
    public string? Observaciones { get; set; }

    // Cierre.
    public DateTime? FechaEnvio { get; set; }             // fecha real de envío (→ Comunicado.Fecha)
    public int? ComunicadoId { get; set; }
    public string? MotivoAnulacion { get; set; }
    public string? CerradoPor { get; set; }
    public DateTime? FechaCierre { get; set; }
}
