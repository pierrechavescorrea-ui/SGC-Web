using Microsoft.EntityFrameworkCore;
using SigWeb.Data.CostosNoCalidad;
using SigWeb.Data.Planes;
using SigWeb.Data.Riesgos;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoPlanesAccion : IModuloDemo
{
    public string Clave => "planes-accion";
    public int Orden => 20;
    internal const string CodigoDemo = "PA-DEMO-01";

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.PlanesAccion.IgnoreQueryFilters().AnyAsync(ct)) return;

        var seguimiento = await db.SeguimientosRiesgo
            .Where(s => s.Resultado == ResultadoSeguimiento.Materializado)
            .OrderBy(s => s.Id).FirstAsync(ct);
        var torre = c.Torres().First();
        var sector = c.Indice == 1 ? "Sector Sur" : "Sector Norte";
        var servicio = c.Servicios.GetRequiredService<PlanesAccionServicio>();

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var plan = new PlanesAccionServicio.PlanEditable
        {
            Plan = new PlanAccion
            {
                Codigo = CodigoDemo,
                Fecha = seguimiento.Fecha,
                OrigenHallazgo = SembradorCatalogosRiesgo.OrigenSeguimientoRiesgos,
                TipoHallazgo = "No conformidad",
                DescripcionHallazgo = $"El lote de pernos destinado al frente de {sector} quedó sin certificado asociado al registro de recepción.",
                AnalisisCausa = "El formato de recepción permite cerrar el registro sin consignar el lote ni verificar el certificado.",
                ReportoNombre = "Equipo QA/QC",
                ReportoCargo = "Coordinación de Calidad",
                ReportoEmpresa = "Ingeniería Demo S.A.S.",
                SeguimientoRiesgoId = seguimiento.Id,
                CriterioEficacia = "Los próximos tres lotes cuentan con certificado asociado antes de su liberación.",
                FechaProgramadaEficacia = c.Hoy.AddDays(30),
            },
            Procesos = ["Construcción"],
            Riesgos = [seguimiento.RiesgoId],
            Lugares = [new LugarPlan { ElementoTrazadoId = torre }],
            Acciones =
            [
                new()
                {
                    Accion = new AccionPlan
                    {
                        Descripcion = "Segregar el lote y verificar su identificación contra el certificado del proveedor.",
                        Tipo = TipoAccionPlan.Correccion,
                        Fecha = c.Hoy.AddDays(-3),
                        Estado = EstadoAccionPlan.Cerrada,
                    },
                    Responsables = ["Almacén de obra"],
                    Productos =
                    [
                        new()
                        {
                            Producto = new ProductoAccion { Descripcion = "Certificado de acero cotejado con el lote" },
                            Anexos =
                            [
                                new ArchivoAccionPlan
                                {
                                    NombreOriginal = "Certificado de acero.pdf",
                                    RutaArchivo = c.CopiarArchivo(ContextoDemo.PdfCertificado, "planes-accion").RutaWeb,
                                },
                            ],
                        },
                    ],
                },
                new()
                {
                    Accion = new AccionPlan
                    {
                        Descripcion = "Actualizar el formato de recepción para exigir el número de lote y el certificado antes de liberar material.",
                        Fecha = c.Hoy.AddDays(12),
                        Estado = EstadoAccionPlan.Abierta,
                    },
                    Responsables = ["Coordinación de Calidad"],
                    Productos = [new() { Producto = new ProductoAccion { Descripcion = "Formato de recepción actualizado y socializado" } }],
                },
            ],
        };
        var planId = await servicio.GuardarAsync(plan);

        var carpeta = new CarpetaPlan { PlanAccionId = planId, Nombre = "Soportes de verificación" };
        db.CarpetasPlan.Add(carpeta);
        await db.SaveChangesAsync(ct);
        db.ArchivosCarpetaPlan.Add(new ArchivoCarpetaPlan
        {
            CarpetaPlanId = carpeta.Id,
            NombreOriginal = "Informe de ensayo de referencia.pdf",
            RutaArchivo = c.CopiarArchivo(ContextoDemo.PdfEnsayo, "planes-accion/carpetas").RutaWeb,
        });

        var costos = await ServicioCostosNoCalidad.CargarAsync(db);
        if (costos.Campos.FirstOrDefault() is { } campo)
        {
            db.ValoresCampoTabla.Add(new ValorCampoTabla
            {
                CampoTablaId = campo.Id,
                FilaId = planId,
                Valor = c.Indice == 1 ? "920000" : "680000",
            });
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
