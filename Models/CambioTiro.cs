namespace SigWeb.Models;

// Control de cambios de Tiros (Configuración › Tiros): cada guardado del modo edición deja una fila por cambio real
// (diferencia entre lo que había y lo guardado), agrupadas por Lote. Se capturan solas: torre que cambia de tiro,
// tiro creado, renombrado, reordenado o eliminado. Los nombres quedan como texto (el registro sobrevive aunque el
// tiro o la torre se borren después), por eso no hay llaves foráneas.
public class CambioTiro
{
    public int Id { get; set; }
    public Guid Lote { get; set; }                         // un guardado = un lote
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Tipo { get; set; } = TipoCambioTiro.Asignacion;
    public int? TiroId { get; set; }
    public int? ElementoTrazadoId { get; set; }
    public string Elemento { get; set; } = string.Empty;   // torre o tiro afectado (nombre al momento del cambio)
    public string? Antes { get; set; }
    public string? Despues { get; set; }
    public string? Motivo { get; set; }                    // opcional, el mismo para todo el lote
}

public static class TipoCambioTiro
{
    public const string Asignacion = "Torre";
    public const string Alta = "Tiro nuevo";
    public const string Nombre = "Nombre";
    public const string Orden = "Orden";
    public const string Eliminacion = "Tiro eliminado";
}
