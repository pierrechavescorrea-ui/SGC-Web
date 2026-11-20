namespace SigWeb.Models;

// Matriz de partes interesadas (ISO 9001 · 4.2) con control de cambios por versiones, igual que el
// Organigrama: cada versión es una foto completa de la matriz; solo el borrador se edita y al
// aprobarlo pasa a Vigente (la anterior queda Obsoleta). Cada parte conserva su Clave de versión
// en versión: así se comparan versiones y los seguimientos no se pierden al publicar una nueva.
public static class EstadoVersionPartes
{
    public const string Borrador = "Borrador";
    public const string Vigente = "Vigente";
    public const string Obsoleta = "Obsoleta";
}

public class VersionPartesInteresadas
{
    public int Id { get; set; }
    public int Numero { get; set; }                       // consecutivo: v1, v2…
    public string? Codigo { get; set; }                   // identificador del documento ("Rev- 12/02/2022")
    public string Titulo { get; set; } = string.Empty;
    public string? Alcance { get; set; }                  // proyecto / tramo / contrato
    public string Estado { get; set; } = EstadoVersionPartes.Borrador;
    public DateTime? VigenteDesde { get; set; }
    public DateTime? VigenteHasta { get; set; }           // se llena al publicar la siguiente
    public string? MotivoCambio { get; set; }
    public string? Elaboro { get; set; }
    public string? Reviso { get; set; }
    public string? Aprobo { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public string? Origen { get; set; }                   // ej. archivo Excel del que se cargó
    public int? VersionBaseId { get; set; }               // versión de la que se copió
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Una fila de la matriz dentro de una versión (columnas del Excel "Matriz , gráfico , Seguimiento").
public class ParteInteresada
{
    public int Id { get; set; }
    public int VersionPartesInteresadasId { get; set; }
    public Guid Clave { get; set; } = Guid.NewGuid();     // identidad de la parte entre versiones
    public int Orden { get; set; }                        // columna "#"
    public string Nombre { get; set; } = string.Empty;    // Parte interesada
    public string? Procesos { get; set; }                 // Procesos que intervienen
    public string? Necesidades { get; set; }
    public string? Expectativas { get; set; }
    public string? AfectaSgc { get; set; }                // Cómo afecta al SGC
    public decimal? Impacto { get; set; }                 // IMP (0 a 10)
    public decimal? Influencia { get; set; }              // INF (0 a 10)
    public string? Clasificacion { get; set; }            // CLASIFICACIÓN tal como la escribió el usuario
    public string? Estrategias { get; set; }
    public string? Medicion { get; set; }
    public string? Periodicidad { get; set; }             // texto del Excel ("Mensual", "Al finalizar el proyecto"…)
    public string? Meta { get; set; }                     // texto ("90%", ">=90%")
    public string? Observaciones { get; set; }
    public string? Supuesto { get; set; }                 // dato dudoso del origen

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Cada cuánto se hace seguimiento a una parte y si está activa: lo elige el usuario y NO forma parte
// del control de cambios de la matriz (se cambia en cualquier momento). Una por parte (Clave).
// Inactiva = la parte sigue en la matriz pero no se le hace seguimiento (sale de rondas y conteos).
public class ProgramacionSeguimientoParte
{
    public int Id { get; set; }
    public Guid ParteClave { get; set; }
    public int? Cada { get; set; }                        // null = sin programar
    public string Unidad { get; set; } = UnidadPeriodo.Meses;
    public DateTime? Desde { get; set; }                  // primera fecha (si aún no hay seguimientos)
    public string? Responsable { get; set; }
    public bool Inactiva { get; set; }
    public DateTime? InactivaDesde { get; set; }
}

public static class UnidadPeriodo
{
    public const string Dias = "dias";
    public const string Semanas = "semanas";
    public const string Meses = "meses";
}

public enum CumplimientoParte
{
    SinMedir = 0,
    Cumple = 1,
    Parcial = 2,
    NoCumple = 3
}

// Un seguimiento a una parte interesada: fecha elegida por el usuario, resultado de la medición
// frente a la meta y, si cambió, la nueva valoración IMP/INF (queda la trayectoria en el mapa).
// Se liga por Clave (sobrevive a las versiones) y guarda la fila de la versión con que se hizo.
public class SeguimientoParteInteresada
{
    public int Id { get; set; }
    public Guid ParteClave { get; set; }
    public int? ParteInteresadaId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string? Responsable { get; set; }
    public string? Resultado { get; set; }                // valor medido (texto: "92%", "8 de 10"…)
    public CumplimientoParte Cumplimiento { get; set; } = CumplimientoParte.SinMedir;
    public decimal? Impacto { get; set; }                 // valoración al momento del seguimiento
    public decimal? Influencia { get; set; }
    public string? Observacion { get; set; }
    public string? DescripcionHallazgo { get; set; }   // si el seguimiento arroja hallazgo: base del plan de acción
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
