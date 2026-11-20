namespace SigWeb.Models;

// Módulo Auditoría interna (ISO 9001 · 9.2; ISO 19011:2018 como guía). Cuatro niveles:
// programa anual → auditoría (alcance, criterios, equipo) → hallazgos → planes de acción (módulo
// existente, vía HallazgoPlan). La verificación de eficacia vive en el plan (PlanAccion.Eficaz).
// Proyecto es texto por ahora (piloto de un solo proyecto): se convertirá en llave al pasar a varios.

public static class EstadoProgramaAuditoria
{
    public const string Borrador = "Borrador";
    public const string Aprobado = "Aprobado";
    public const string EnEjecucion = "En ejecución";
    public const string Cerrado = "Cerrado";
    public static readonly string[] Todos = { Borrador, Aprobado, EnEjecucion, Cerrado };
}

public class ProgramaAuditoria
{
    public int Id { get; set; }
    public int Anio { get; set; } = DateTime.Today.Year;
    public string? Proyecto { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Normas { get; set; } = string.Empty;          // una por línea (catálogo "Auditoría — norma")
    public string? Objetivos { get; set; }                       // ISO 19011 5.2
    public string? Responsable { get; set; }                     // quien gestiona el programa (19011 5.4.1)
    public string? Recursos { get; set; }                        // 19011 5.4.4
    public string Estado { get; set; } = EstadoProgramaAuditoria.Borrador;
    public string? Aprobo { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

public enum EstadoAuditoria
{
    Programada = 0,
    EnEjecucion = 1,
    InformeEmitido = 2,
    Cerrada = 3,
    Cancelada = 4
}

public class Auditoria
{
    public int Id { get; set; }
    public int ProgramaAuditoriaId { get; set; }
    public string Codigo { get; set; } = string.Empty;          // AUD-2026-01
    public string Tipo { get; set; } = string.Empty;            // catálogo "Auditoría — tipo"
    public string Modalidad { get; set; } = string.Empty;       // catálogo "Auditoría — modalidad"
    public int Mes { get; set; } = DateTime.Today.Month;        // mes programado (1–12)
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? Proyecto { get; set; }                        // proyecto o frente (texto, ver arriba)

    // Alcance (19011 3.5) y criterios (19011 3.7). Lugares en LugarAuditoria.
    public string Procesos { get; set; } = string.Empty;        // uno por línea (catálogo "Plan de acción — proceso")
    public string Normas { get; set; } = string.Empty;          // una por línea
    public string? Objetivo { get; set; }
    public string? Alcance { get; set; }                         // actividades, periodo auditado, exclusiones
    public string? Criterios { get; set; }                       // numerales y documentos internos

    // Equipo (19011 3.15–3.18). Texto por ahora; la ficha de auditores llega en la fase 3.
    public string? AuditorLider { get; set; }
    public string Auditores { get; set; } = string.Empty;       // uno por línea
    public string? ExpertoTecnico { get; set; }
    public string? Observadores { get; set; }
    public string? Auditados { get; set; }                       // cargos o personas entrevistadas

    public EstadoAuditoria Estado { get; set; } = EstadoAuditoria.Programada;

    // Informe (19011 6.5).
    public string? Conclusiones { get; set; }
    public string? Fortalezas { get; set; }
    public DateTime? FechaInforme { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Historial de reprogramaciones y cambios de estado de una auditoría (evidencia del
// seguimiento del programa, 19011 5.6). Se borra con la auditoría.
public class CambioAuditoria
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Campo { get; set; } = string.Empty;           // Mes, Fechas, Estado
    public string? Anterior { get; set; }
    public string? Nuevo { get; set; }
    public string? Motivo { get; set; }
}

// Lugares del alcance: torre, elemento de torre, localización o tiro (igual que LugarPlan).
public class LugarAuditoria
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public int? ElementoTrazadoId { get; set; }
    public int? ComponenteId { get; set; }
    public int? ItemCatalogoId { get; set; }
    public int? TiroId { get; set; }
}

public enum AceptacionHallazgo
{
    Aceptado = 0,
    EnDiscusion = 1
}

// Estado calculado de un hallazgo (no se guarda): ver ServicioAuditorias.EstadoDe.
public enum EstadoHallazgoAuditoria
{
    Abierto = 0,
    EnDiscusion = 1,
    ConPlan = 2,
    EnVerificacion = 3,
    Reabierto = 4,
    Cerrado = 5
}

// Hallazgo: requisito + evidencia + declaración (19011 6.4.8). Tipo del catálogo
// "Plan de acción — tipo de hallazgo" (el mismo de los planes); grado solo para NC
// (catálogo "Auditoría — grado de no conformidad", definiciones de ISO/IEC 17021-1 3.12 y 3.13).
public class HallazgoAuditoria
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public string Codigo { get; set; } = string.Empty;          // AUD-2026-01-H01
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Tipo { get; set; } = string.Empty;
    public string? Grado { get; set; }
    public string Normas { get; set; } = string.Empty;          // una por línea
    public string? Numerales { get; set; }                       // "8.5.1; 7.5.3"
    public string? Documento { get; set; }                       // procedimiento, plan de calidad, especificación
    public string? Requisito { get; set; }
    public string? Evidencia { get; set; }
    public string Descripcion { get; set; } = string.Empty;     // declaración del hallazgo
    public string Procesos { get; set; } = string.Empty;        // uno por línea
    public string? Auditor { get; set; }
    public AceptacionHallazgo Aceptacion { get; set; } = AceptacionHallazgo.Aceptado;
    public string? NotaAceptacion { get; set; }

    // Cierre manual (hallazgos que no llevan plan, o cierre del auditor). Las NC se cierran
    // solas cuando todos sus planes están cerrados y verificados eficaces.
    public DateTime? FechaCierre { get; set; }
    public string? CerradoPor { get; set; }
    public string? ObservacionCierre { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

public class LugarHallazgoAuditoria
{
    public int Id { get; set; }
    public int HallazgoAuditoriaId { get; set; }
    public int? ElementoTrazadoId { get; set; }
    public int? ComponenteId { get; set; }
    public int? ItemCatalogoId { get; set; }
    public int? TiroId { get; set; }
}

// Archivo con descripción (fotos y anexos de hallazgos, soportes de eficacia): lo usa <AdjuntosDescritos>.
public interface IAdjuntoDescrito
{
    int Id { get; set; }
    string NombreOriginal { get; set; }
    string RutaArchivo { get; set; }
    string? Descripcion { get; set; }
    DateTime FechaRegistro { get; set; }
}

// Fotos y anexos tomados al levantar el hallazgo.
public class ArchivoHallazgoAuditoria : IAdjuntoDescrito
{
    public int Id { get; set; }
    public int HallazgoAuditoriaId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    // Qué muestra la foto o el anexo (sale en las tarjetas y en el informe).
    public string? Descripcion { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Soportes de la verificación de eficacia de un plan de acción (ISO 9001 10.2.1 d, 10.2.2).
public class ArchivoEficaciaPlan : IAdjuntoDescrito
{
    public int Id { get; set; }
    public int PlanAccionId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Varios hallazgos pueden tratarse con un mismo plan de acción (misma causa en frentes distintos).
public class HallazgoPlan
{
    public int Id { get; set; }
    public int HallazgoAuditoriaId { get; set; }
    public int PlanAccionId { get; set; }
}

public static class EscalaAuditoria
{
    public static string Nombre(EstadoAuditoria e) => e switch
    {
        EstadoAuditoria.Programada => "Programada",
        EstadoAuditoria.EnEjecucion => "En ejecución",
        EstadoAuditoria.InformeEmitido => "Informe emitido",
        EstadoAuditoria.Cerrada => "Cerrada",
        _ => "Cancelada"
    };

    public static string Nombre(EstadoHallazgoAuditoria e) => e switch
    {
        EstadoHallazgoAuditoria.Abierto => "Abierto",
        EstadoHallazgoAuditoria.EnDiscusion => "En discusión",
        EstadoHallazgoAuditoria.ConPlan => "Con plan de acción",
        EstadoHallazgoAuditoria.EnVerificacion => "En verificación",
        EstadoHallazgoAuditoria.Reabierto => "Reabierto (no eficaz)",
        _ => "Cerrado"
    };

    public static string Nombre(AceptacionHallazgo a) => a == AceptacionHallazgo.Aceptado ? "Aceptado" : "En discusión";

    public static readonly string[] Meses = { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
    public static string Mes(int m) => m is >= 1 and <= 12 ? Meses[m - 1] : "—";
}
