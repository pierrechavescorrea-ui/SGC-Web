using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Auditorias;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoAuditoriaInterna : IModuloDemo
{
    public string Clave => "auditoria-interna";
    public int Orden => 30;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.ProgramasAuditoria.IgnoreQueryFilters().AnyAsync(ct)) return;

        var planId = await db.PlanesAccion.Where(p => p.Codigo == DemoPlanesAccion.CodigoDemo).Select(p => p.Id).SingleAsync(ct);
        var torre = c.Torres().First();
        var sector = c.Indice == 1 ? "Sector Sur" : "Sector Norte";
        var fecha = new DateTime(2026, 11, c.Indice == 1 ? 15 : 12);
        const string norma = "ISO 9001:2015";
        const string proceso = "Construcción";

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var programa = new ProgramaAuditoria
        {
            Anio = 2026,
            Proyecto = c.NombreProyecto,
            Titulo = $"Programa de auditoría interna de obra — {sector}",
            Normas = norma,
            Objetivos = "Verificar la trazabilidad de los materiales y los controles de liberación para montaje.",
            Responsable = "Coordinación SGI",
            Recursos = "Equipo auditor interno, registros de recepción y visita al frente de montaje.",
            Estado = EstadoProgramaAuditoria.Aprobado,
            Aprobo = "Dirección de proyecto",
            FechaAprobacion = new DateTime(2026, 5, 12),
        };
        db.ProgramasAuditoria.Add(programa);
        await db.SaveChangesAsync(ct);

        var auditoria = new Auditoria
        {
            ProgramaAuditoriaId = programa.Id,
            Codigo = ServicioAuditorias.SiguienteCodigoAuditoria(db, programa.Anio),
            Tipo = "Interna",
            Modalidad = "En campo",
            Mes = fecha.Month,
            FechaInicio = fecha,
            FechaFin = fecha,
            Proyecto = c.NombreProyecto,
            Objetivo = "Evaluar el control de recepción y liberación de pernos para montaje.",
            Alcance = $"Almacén y frente de montaje de {sector}.",
            Criterios = "Procedimiento interno de recepción y liberación de materiales.",
            AuditorLider = "Auditoría interna SGI",
            Auditados = "Almacén de obra y Coordinación de Calidad",
            Estado = EstadoAuditoria.InformeEmitido,
            FechaInforme = fecha.AddDays(2),
            Conclusiones = "La identificación física del lote está disponible. Se debe asegurar el vínculo documental en la recepción.",
            Fortalezas = "El personal aplica la segregación física de los lotes pendientes de liberación.",
        };
        var auditoriaId = await ServicioAuditorias.GuardarAuditoriaAsync(db, new ServicioAuditorias.AuditoriaEditable
        {
            Auditoria = auditoria,
            Procesos = [proceso],
            Normas = [norma],
            Lugares = [new LugarPlan { ElementoTrazadoId = torre }],
        });

        auditoria.Id = auditoriaId;
        var hallazgoId = await ServicioAuditorias.GuardarHallazgoAsync(db, new ServicioAuditorias.HallazgoEditable
        {
            Hallazgo = new HallazgoAuditoria
            {
                AuditoriaId = auditoriaId,
                Codigo = ServicioAuditorias.SiguienteCodigoHallazgo(db, auditoria),
                Fecha = fecha,
                Tipo = "No conformidad",
                Grado = "Menor",
                Documento = "Procedimiento interno de recepción y liberación de materiales",
                Requisito = "Relacionar el certificado del fabricante con el lote recibido antes de liberar el material.",
                Evidencia = $"El registro del lote de pernos de {sector} no contiene la referencia al certificado del fabricante.",
                Descripcion = "No se demostró la trazabilidad documental del lote de pernos seleccionado.",
                Auditor = "Auditoría interna SGI",
            },
            Procesos = [proceso],
            Normas = [norma],
            Lugares = [new LugarPlan { ElementoTrazadoId = torre }],
            Anexos =
            [
                new ArchivoHallazgoAuditoria
                {
                    NombreOriginal = "Certificado de acero revisado.pdf",
                    RutaArchivo = c.CopiarArchivo(ContextoDemo.PdfCertificado, "auditorias").RutaWeb,
                    Descripcion = "Certificado examinado durante el cotejo documental del lote.",
                },
            ],
        });
        await ServicioAuditorias.LigarPlanAsync(db, [hallazgoId], planId);

        // La demostración conserva el estado solicitado del programa aunque ya tenga un informe.
        var programaGuardado = await db.ProgramasAuditoria.SingleAsync(p => p.Id == programa.Id, ct);
        programaGuardado.Estado = EstadoProgramaAuditoria.Aprobado;
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
