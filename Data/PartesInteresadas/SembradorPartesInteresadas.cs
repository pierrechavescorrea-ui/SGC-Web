using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.PartesInteresadas;

// Primera versión de la matriz, tal como viene en "Mtz Partes Interesadas - Seguimiento.xlsx" (hoja
// "Matriz , gráfico , Seguimiento", filas 5 a 13; la fila 4 sin nombre con IMP = INF = 10 es el ancla de
// escala del gráfico y no se carga). Textos tal cual (solo se quitan espacios al inicio y al final).
// Solo corre si no hay ninguna versión (tampoco en la Papelera).
public static class SembradorPartesInteresadas
{
    public const string Archivo = "Mtz Partes Interesadas - Seguimiento.xlsx";

    // (#, parte, procesos, necesidades, expectativas, cómo afecta al SGC, IMP, INF, clasificación, estrategias, medición, periodicidad, meta, observaciones)
    private sealed record Fila(int N, string Nombre, string? Procesos, string? Necesidades, string? Expectativas, string? AfectaSgc,
        decimal? Imp, decimal? Inf, string? Clasificacion, string? Estrategias, string? Medicion, string? Periodicidad, string? Meta, string? Observaciones);

    private static readonly Fila[] Filas =
    {
        new(1, "Colaboradores", "Realización de la obra.", "Bienestar laboral", "Empleo, Capacitación y entrenamiento, Buen clima laboral, Remuneración adecuda,", "Rotación de personal, baja productividad e incumplimiento de requisitos del proyecto.", 7m, 5m, "MANTENER SATISFECHOS/  TRABAJAR DE CERCA", "Establecer plan de capacitaciones.  \nProgramas motivacionales. \nInvolucramiento en el desarrollo del sistema integrado de Gestión.", "Encuesta de satisfación al personal", "Mensual", "90%", null),
        new(2, "Comunidad", "Realización de la obra. \nMedición de quejas y reclamos.", "Cumplimiento de acuerdos realizados", "Generacion de empleo, servicio y Apoyo en actividades sociales,  cero daños causados por la empresa.", "Reatrasos en el PDT, por inconvemierntes con dueños de predios. \nPeticiones, quejas y reclamos.", 4m, 7m, "MANTENER SATISFECHOS/  TRABAJAR DE CERCA", "Solicitar permisos de acceso\nTramitar Paz y Salvos\nCumplimiento de acuerdos pactados. \nAtención y solución oportuna de PQR.", "(No de compromisos cumplidos / Total de compromisos adquiridos con la comunidad) * 100", "Durante la ejecución del proyecto", "100%", null),
        new(3, "Cliente", "Planeación de la obra. \nRealización de la obra.\nControl de producto no conforme y acciones correctivas. \nMedición de la Satisfacción del cliente.", "Satisfacción sobre el servicio.", "Ejecucion del proyecto en el plazo establecido. \nExcelente calidad en la obra entregada. \nCero accidentes de trabajo.", "No conformidades , baja calificación en evaluación de satisfaccion del cliente.", 8m, 8m, "TRABAJAR DE CERCA", "Establecer plan de audirias. \nReuniones de obra semanales.\nImplementación del Sistema Integrado de Gestión. \nAplicar Evaluación de satisfacción del cliente.", "Encuesta de satisfación del cliente", "50% de proyecto", ">=90%", null),
        new(4, "Proveedores", "Gestión Administrativa.", "Facturación oportuna, cumplimiento del contrato.", "Aliado de negocio, Duración de contrato,\nPedidos claros\nPlaneación  de pedidos", "Incumplimiento de requisitos legales, Incumplimiento en la entrega de productos. \nProductos de baja calidad.", 9m, 5m, "TRABAJAR DE CERCA/ MANTENER INFORMADO", "Aplicación de Procedimientos: \n• COMPRA-01 PROCEDIMIENTO PARA COMPRAS\n• COMPRA-02 CONTRATACIÓN DE SUBCONTRATISTAS\n• COMPRA-04 EVALUACIÓN DE PROVEEDORES, SUBCONTRATISTAS Y TRANSPORTADORES.", "Encuesta de satisfación de los proveedores", "Al finalizar el proyecto", ">=90%", null),
        new(5, "Medio Ambiente", "Planeación de la obra. \nRealización de la obra", "Prevención de impactos ambientales negativos.", "Cumplimiento de la Licencia Ambiental", "Incumplimiento de requisitos legales. \nGeneración de impactos ambientales negativos.", 6m, 8m, "MANTENER INFORMADO", "Elaboración y cumplimiento del Plan de Gestión ambiental.", "Cumplimientos normativa ambiental", "Durante la ejecución del proyecto", "100%", null),
        new(6, "Estado y sus entidades", "Planeación de la obra\nRealización de la obra \nRequisitos legales", "Cumplimiento de requisitos legales.", "Cero desviaciones por incumplimiento de requisitos legales.", "Sanciones, multas, suspensión de proyecto.Mala imagen.", 5m, 7m, "MANTENER SATISFECHOS/  TRABAJAR DE CERCA", "Identificación de requisitos legales aplicables al proyecto. Seguimiento de su cumplimiento.", "Cumplimientos normativa legal", "Durante la ejecución del proyecto", "100%", null),
        new(7, "La empresa", "Todos los procesos", "Entregar proyecto con excente calidad dentro del presupuesto establecido y plazo acordado.", "Buen nombre, adjudicación de nuevos proyectos, clientes satisfechos.", "Mala imagen, baja calificación en evaluación de desempeño, perdida de clientes.", 9m, 9m, "TRABAJAR DE CERCA/ ATENDER SUS REQUISITOS Y MANEJAR ADECUADADMENTE SUS EXPECTATIVAS", "Plan de auditorías internas, reuniones de seguimiento semanal.", "(Auditoría ejecutada /\nAuditoría programada.) *100", "Semestral", "100%", null),
        new(8, "Competencia", "Mercadeo y ofertas\nRealización del proyecto", "Adjudicación de proyectos.", "Competencia sana y honesta", "Mala imagen, perdida de clientes.", 4.5m, 8m, "MANTENER SATISFECHOS/  TRABAJAR DE CERCA", "Identificar oportunidades para crear alianzas estrategicas.", null, null, null, null),
        new(9, "Socios", "Todos los procesos", "Adjudicación de proyectos.", "Competencia sana y honesta", "Mala imagen, perdida de clientes.", 4.5m, 8m, "MANTENER SATISFECHOS/  TRABAJAR DE CERCA", "Identificar oportunidades para crear alianzas estrategicas.", null, null, null, null)
    };

    // Datos dudosos del Excel (se ven en la ficha y en la lista).
    private static readonly Dictionary<int, string> Supuestos = new()
    {
        [1] = "«Cómo afecta al SGC» trae el nombre del proyecto (posible copia errónea en el Excel).",
        [8] = "En el gráfico del Excel el punto de Competencia toma la INF de «La empresa» (celda H11 = 9) en vez de la suya (H12 = 8); aquí se usa 8.",
        [9] = "Necesidades, expectativas, cómo afecta al SGC, IMP, INF, clasificación y estrategias son idénticos a «Competencia» en el Excel.",
    };

    public static void SembrarSiVacio(AppDbContext db)
    {
        if (db.VersionesPartesInteresadas.IgnoreQueryFilters().Any()) return;
        var v = new VersionPartesInteresadas
        {
            Numero = 1,
            Codigo = "Rev- 12/02/2022",
            Titulo = "Matriz de identificación de partes interesadas",
            Alcance = "Proyecto de línea de transmisión (ejemplo)",
            Estado = EstadoVersionPartes.Vigente,
            VigenteDesde = new DateTime(2022, 2, 12),    // "Rev- 12/02/2022" leído como día/mes/año
            MotivoCambio = "Carga inicial desde el Excel",
            Origen = $"{Archivo} · hoja «Matriz , gráfico , Seguimiento»",
        };
        db.VersionesPartesInteresadas.Add(v);
        db.SaveChanges();
        foreach (var f in Filas)
        {
            var p = new ParteInteresada
            {
                VersionPartesInteresadasId = v.Id, Orden = f.N, Nombre = f.Nombre, Procesos = f.Procesos, Necesidades = f.Necesidades,
                Expectativas = f.Expectativas, AfectaSgc = f.AfectaSgc, Impacto = f.Imp, Influencia = f.Inf, Clasificacion = f.Clasificacion,
                Estrategias = f.Estrategias, Medicion = f.Medicion, Periodicidad = f.Periodicidad, Meta = f.Meta, Observaciones = f.Observaciones,
                Supuesto = Supuestos.TryGetValue(f.N, out var s) ? s : null
            };
            db.PartesInteresadas.Add(p);
            // Programación de seguimiento solo donde la periodicidad del Excel es un periodo fijo.
            int? cada = f.Periodicidad switch { "Mensual" => 1, "Semestral" => 6, _ => null };
            if (cada is not null)
                db.ProgramacionesSeguimientoParte.Add(new ProgramacionSeguimientoParte { ParteClave = p.Clave, Cada = cada, Unidad = UnidadPeriodo.Meses });
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
