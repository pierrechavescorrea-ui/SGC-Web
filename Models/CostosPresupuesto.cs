namespace SigWeb.Models;

// ---------- Costos y presupuestos ----------
// Producción (cantidades ejecutadas × precio del ítem de pago) contra costos (órdenes y gastos
// prorrateados por día + nómina), por cuadrilla. Origen: libro "INFORME DIARIO" (hojas
// Formulario de precios, Avance diario, DETALLE DE LAS TORRES / MONTAJE, Costos, Personal,
// Presupuesto, Meta produccion, Seguimiento Meta, C. CONTROL, Informe). Las cuadrillas son el
// catálogo "Cuadrilla" (ItemCatalogo); aquí se guardan por nombre.

// Dónde se registra la cantidad de un ítem de pago.
public enum AmbitoItemPago
{
    Global = 0,   // sin lugar (cuadrillas/mes, helicoportado kg, vías km…)
    Torre = 1,
    Pata = 2,
    Tiro = 3
}

// Ítem de pago del contrato (Formulario de precios). Un capítulo es un ítem sin unidad ni
// precio que agrupa a otros (CapituloId). ActividadObraId liga el ítem con la actividad del
// Avance de obra (visor 3D): varios ítems (2.1.1, 2.2.1, 3.1.1…) pueden ir a "Excavación".
public class ItemPago
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;          // 2.1.1, F2, SA1.1, R5…
    public string Descripcion { get; set; } = string.Empty;
    public string? Unidad { get; set; }
    public int? CapituloId { get; set; }                        // ItemPago padre (capítulo)
    public bool EsCapitulo { get; set; }
    public decimal CantidadContratada { get; set; }
    public decimal? PrecioBase { get; set; }                    // sin AIU, del año base
    public int AnioBase { get; set; } = 2023;
    public decimal AiuPct { get; set; }                         // fracción: 0,33 = 33 %
    public AmbitoItemPago Ambito { get; set; } = AmbitoItemPago.Global;
    public bool AplicaOverhead { get; set; } = true;
    public bool AplicaSostenimiento { get; set; } = true;
    public bool AplicaImpuestos { get; set; } = true;
    public decimal? CostoUnitarioPresupuestado { get; set; }    // para el saldo de costo por actividad (opcional)
    public string? CuadrillaPorDefecto { get; set; }
    public int? ActividadObraId { get; set; }
    public int Orden { get; set; }

    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Ajuste anual de precios (IPC u otro índice compuesto). Factor = Σ peso × (1 + índice).
// Se aplica en cadena desde el año base del ítem: precio(año) = precio(año−1) × factor(año).
// Alcance: todos los ítems, salvo los que tengan PrecioManual de ese año.
public class AjustePrecio
{
    public int Id { get; set; }
    public int Anio { get; set; }
    public string Nombre { get; set; } = string.Empty;          // "IPC 2024", "Compuesto 2026"
    public string? Soporte { get; set; }                        // documento o fuente del índice
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Componente de un ajuste: peso × índice (ej. 0,6 × 5,1 % + 0,4 × 23 %).
public class ComponenteAjustePrecio
{
    public int Id { get; set; }
    public int AjustePrecioId { get; set; }
    public string Nombre { get; set; } = string.Empty;          // "IPC", "Componente 2"
    public decimal Peso { get; set; } = 1m;                     // fracción
    public decimal Indice { get; set; }                         // fracción: 0,0928 = 9,28 %
}

// Precio fijado a mano para un ítem y un año (sin AIU). Manda sobre el ajuste.
public class PrecioManual
{
    public int Id { get; set; }
    public int ItemPagoId { get; set; }
    public int Anio { get; set; }
    public decimal Precio { get; set; }
    public string? Motivo { get; set; }
}

public enum TipoModificacionContrato
{
    Otrosi = 0,
    CambioCantidad = 1,
    ItemNuevo = 2,
    PrecioPactado = 3,
    Otro = 4
}

// Modificación del contrato con su soporte (trazabilidad de cantidades y precios).
public class ModificacionContrato
{
    public int Id { get; set; }
    public TipoModificacionContrato Tipo { get; set; }
    public DateTime Fecha { get; set; }
    public string? Numero { get; set; }                         // otrosí n.º, comunicado…
    public string Descripcion { get; set; } = string.Empty;
    public string? RutaSoporte { get; set; }
    public string? NombreSoporte { get; set; }
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

public class LineaModificacionContrato
{
    public int Id { get; set; }
    public int ModificacionContratoId { get; set; }
    public int ItemPagoId { get; set; }
    public decimal? CantidadAntes { get; set; }
    public decimal? CantidadDespues { get; set; }
    public decimal? PrecioAntes { get; set; }
    public decimal? PrecioDespues { get; set; }
    public int? AnioPrecio { get; set; }
}

// Cantidad prevista de un ítem en una torre (o pata), hojas DETALLE DE LAS TORRES / MONTAJE.
public class CantidadTorre
{
    public int Id { get; set; }
    public int ElementoTrazadoId { get; set; }
    public int? ComponenteId { get; set; }
    public int ItemPagoId { get; set; }
    public decimal CantidadPrevista { get; set; }
    public string? Cimentacion { get; set; }                    // ZAPATA, PARRILLA L.…
    public string? TipoTorre { get; set; }                      // AP H49.5…
    public string? Cuerpo { get; set; }                         // AA7…
    public string? Nota { get; set; }
}

// Avance diario: cantidad ejecutada de un ítem en un día, en una torre/pata/tiro o global.
// Es la ÚNICA captura de avance: de aquí salen producción, detalles por torre, metas y curva S.
public class RegistroCantidad
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int ItemPagoId { get; set; }
    public int? ElementoTrazadoId { get; set; }
    public int? ComponenteId { get; set; }
    public int? TiroId { get; set; }
    public decimal Cantidad { get; set; }
    public string? Cuadrilla { get; set; }
    public string? Descripcion { get; set; }
    public int? ActividadProgramaId { get; set; }               // PDT (trazabilidad)
    public string? Origen { get; set; }                         // "Manual", "Libro: Avance diario", "Libro: DETALLE…"
    public DateTime CreadoEn { get; set; } = DateTime.Now;

    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Acta de facturación (columna Acta del Avance diario).
public class ActaFacturacion
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public DateTime? PeriodoDesde { get; set; }
    public DateTime? PeriodoHasta { get; set; }
    public string? Observaciones { get; set; }
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

public class LineaActaFacturacion
{
    public int Id { get; set; }
    public int ActaFacturacionId { get; set; }
    public int ItemPagoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Valor { get; set; }                          // con AIU
}

// ---------- Presupuesto de costos ----------
public enum GrupoPresupuesto
{
    General = 0,
    Materiales = 1,
    ManoDeObra = 2,
    EquipoYHerramientas = 3,
    Otros = 4,
    Indirectos = 5
}

public class CodigoPresupuesto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;          // INST, VIA, MOD, MOI…
    public string Nombre { get; set; } = string.Empty;
    public GrupoPresupuesto Grupo { get; set; }
    public decimal PresupuestoInicial { get; set; }
    public int Orden { get; set; }
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Presupuesto actual (EAC) con historial: el vigente es el de fecha más reciente.
public class ValorPresupuesto
{
    public int Id { get; set; }
    public int CodigoPresupuestoId { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Valor { get; set; }
    public string? Motivo { get; set; }
}

public enum ConceptoIndirecto
{
    Overhead = 0,
    Sostenimiento = 1,
    ImpuestosFinanciacion = 2
}

// % sobre la producción, vigente desde una fecha (el más reciente ≤ día manda).
public class PorcentajeIndirecto
{
    public int Id { get; set; }
    public ConceptoIndirecto Concepto { get; set; }
    public DateTime Desde { get; set; }
    public decimal Porcentaje { get; set; }                     // fracción
    public string? Nota { get; set; }
}

// Orden de compra o gasto (hoja Costos). Se prorratea por igual entre Desde y Hasta.
public class CostoOrden
{
    public int Id { get; set; }
    public string? NumeroOrden { get; set; }
    public int? CodigoPresupuestoId { get; set; }
    public string? Proveedor { get; set; }
    public string? Concepto { get; set; }
    public decimal Valor { get; set; }
    public DateTime? Desde { get; set; }                        // sin fechas = comprometido pero no se carga (como el Excel)
    public DateTime? Hasta { get; set; }
    public string? Cuadrilla { get; set; }
    public bool Pf1 { get; set; }
    public int? ItemPagoId { get; set; }                        // costo directo de un ítem (opcional)
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// Personal (hoja Personal). Valor día con ParametrosNomina; su costo entra a su código
// presupuestal y a su cuadrilla, y siempre al PF1.
public class Persona
{
    public int Id { get; set; }
    public int? CodigoPresupuestoId { get; set; }               // MOD / MOI
    public string? Cargo { get; set; }
    public string? Cedula { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateTime? Ingreso { get; set; }
    public DateTime? Retiro { get; set; }
    public decimal Salario { get; set; }
    public decimal Auxilio { get; set; }                        // auxilios sin prestaciones (mensual)
    public string? Cuadrilla { get; set; }
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

// ---------- Metas ----------
// Periodo de seguimiento (normalmente un mes) con su meta de facturación y la versión vigente
// de la meta de producción. Las metas se digitan; nunca se calculan.
public class MetaPeriodo
{
    public int Id { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public DateTime InicioSemana1 { get; set; }                 // primer día de la semana 1 del seguimiento
    public decimal MetaFacturacion { get; set; }
    public decimal? MetaProduccionTotal { get; set; }           // si no hay detalle por ítem/día
    public int Version { get; set; } = 1;
    public string? Motivo { get; set; }
    public bool Vigente { get; set; } = true;
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

public class MetaProduccionDia
{
    public int Id { get; set; }
    public int MetaPeriodoId { get; set; }
    public string Cuadrilla { get; set; } = string.Empty;
    public int ItemPagoId { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Cantidad { get; set; }
}

// Ajuste manual del costo asignado a un ítem en un día (sobre el reparto automático).
public class AsignacionCostoActividad
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Cuadrilla { get; set; } = string.Empty;
    public int ItemPagoId { get; set; }
    public decimal Valor { get; set; }
    public string? Motivo { get; set; }
}

// ---------- PDT: Programa Detallado de Trabajo ----------
// Cada reprogramación es una versión nueva; la línea base no se edita.
public class ProgramaTrabajo
{
    public int Id { get; set; }
    public int Version { get; set; }
    public DateTime Fecha { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool LineaBase { get; set; }
    public bool Vigente { get; set; }
    public string? Motivo { get; set; }
    public string? AprobadoPor { get; set; }
    public string DiasLaborables { get; set; } = "1111111";      // lunes…domingo, "1" = laboral (calendario del programa)
    public string? Festivos { get; set; }                         // días no laborables "yyyy-MM-dd;…"
    public bool Eliminado { get; set; } = false;
    public DateTime? FechaEliminacion { get; set; }
}

public class ActividadPrograma
{
    public int Id { get; set; }
    public int ProgramaTrabajoId { get; set; }
    public int? Uid { get; set; }                               // UID de la tarea en MS Project (traza entre versiones)
    public string Nombre { get; set; } = string.Empty;
    public string? Wbs { get; set; }                            // EDT / WBS
    public int Nivel { get; set; }                              // OutlineLevel
    public bool EsResumen { get; set; }
    public int Orden { get; set; }
    public decimal? ValorPlaneado { get; set; }                 // costo de la tarea en Project o cantidad × precio
    public int? ItemPagoId { get; set; }                        // null = tarea sin ítem de pago (resumen, hito…)
    public int? ActividadObraId { get; set; }                   // actividad de Avance de obra deducida del nombre
    public decimal? AvanceProject { get; set; }                 // % completado según Project (0–1), referencia
    public int? ElementoTrazadoId { get; set; }
    public int? TiroId { get; set; }
    public string? Cuadrilla { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public decimal Cantidad { get; set; }
    public int Duracion { get; set; }                           // días laborables (0 = hito)
    public string? Predecesoras { get; set; }                   // por UID: "45;46CC;47FC+3" (MotorPrograma.Vinculo)
    public string? Nota { get; set; }
}

// Bitácora de cambios del PDT (qué cambió, antes, después, motivo y quién).
public class CambioPrograma
{
    public int Id { get; set; }
    public int ProgramaTrabajoId { get; set; }
    public int? ActividadProgramaId { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string Campo { get; set; } = string.Empty;
    public string? Antes { get; set; }
    public string? Despues { get; set; }
    public string? Motivo { get; set; }
    public string? Usuario { get; set; }
}

// Estados de torre que no se pueden calcular del avance (C. CONTROL): restricción, inicio 2027…
public class EstadoTorreManual
{
    public int Id { get; set; }
    public int ElementoTrazadoId { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime Desde { get; set; }
    public string? Nota { get; set; }
    public bool Alerta { get; set; }        // marca roja (texto rojo en la hoja C. CONTROL)
}

public enum CriterioRepartoCosto
{
    ValorProducido = 0,
    Cantidad = 1,
    PartesIguales = 2
}

// Configuración del módulo (una fila).
public class ConfiguracionCostos
{
    public int Id { get; set; }
    public string? Cliente { get; set; }
    public string? Contrato { get; set; }
    public string? Interventor { get; set; }
    public string? Objeto { get; set; }
    public DateTime? Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public decimal? ValorContrato { get; set; }                 // null = Σ formulario de precios
    public bool ValorizarPrecioActual { get; set; }             // true = como el Excel (todo al precio del año en curso)
    public CriterioRepartoCosto CriterioReparto { get; set; } = CriterioRepartoCosto.ValorProducido;
    public string CuadrillaAdministracion { get; set; } = "ADMINISTRACION";
    public string? CuadrillasSinRepartoAdmin { get; set; } = "VARIOS";   // separadas por coma
    // Valor día del personal = Salario/30 × FactorPrestacional × (1 + RecargoA × RecargoB) + Auxilio/30 + BaseAdicional/DivisorAdicional/30
    public decimal FactorPrestacional { get; set; } = 1.4476m;
    public decimal RecargoA { get; set; } = 0.2m;
    public decimal RecargoB { get; set; } = 1.25m;
    public decimal BaseAdicional { get; set; } = 1750905m;
    public decimal DivisorAdicional { get; set; } = 40m;
    public string? OrdenEstadosTorre { get; set; }              // ids del catálogo "Estado de torre" en el orden de la leyenda
    public string? LongitudTiros { get; set; }                  // km por tiro: "1=6.5;2=9" (n.º del tiro); sin dato = distancia entre torres
}
