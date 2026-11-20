using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Riesgos;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoRiesgos : IModuloDemo
{
    public string Clave => "riesgos";
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.VersionesMatrizRiesgos.IgnoreQueryFilters().AnyAsync(ct)) return;

        var torre = c.Torres().First();
        var sur = c.Indice == 1;
        var sector = sur ? "Sector Sur" : "Sector Norte";
        var servicio = c.Servicios.GetRequiredService<MatrizRiesgosServicio>();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var version = await servicio.CrearVersionAsync(new VersionMatrizRiesgos
        {
            Codigo = "Rev. 01",
            Fecha = c.Hoy.AddDays(-30),
            Etapa = "Construcción",
            ElaboradoPor = "Coordinación de Calidad",
            RevisadoPor = "Residente de obra",
            AprobadoPor = "Dirección de proyecto",
            EstadoDocumento = "Aprobado",
            DescripcionCambios = $"Valoración inicial de los frentes de montaje en {sector}.",
        }, null);
        await servicio.PublicarAsync(version.Id, version);

        var riesgo = new Riesgo
        {
            VersionMatrizRiesgosId = version.Id,
            Codigo = "R-01",
            Orden = 1,
            Descripcion = $"Uso de lotes de pernos sin trazabilidad documental en el frente de {sector}.",
            Causa = "Recepción de pernos sin cotejar el lote con el certificado del proveedor.",
            Consecuencia = "Inmovilización de material y repetición de la inspección de recepción.",
            Disparadores = "Identificación de un lote en bodega sin certificado asociado.",
            PlanRespuesta = "Segregar el lote y cotejar los certificados antes de liberarlo.",
            ComoSeHaceSeguimiento = "Verificar cada mes el registro de recepción y los certificados asociados.",
            EstadoActual = "Activo",
            Categoria = "Interno",
            Area = "Técnica",
            Estrategia = "Mitigar",
            ResponsablePlan = "Coordinación de Calidad",
            EstadoPlan = "En ejecución",
            Probabilidad = ProbabilidadRiesgo.Alta,
            Impacto = ImpactoRiesgo.Mayor,
        };
        var acceso = new Riesgo
        {
            VersionMatrizRiesgosId = version.Id,
            Codigo = "R-02",
            Orden = 2,
            Descripcion = sur ? "Pérdida de acceso por saturación de la vía veredal." : "Interrupción del acarreo por erosión del acceso temporal.",
            Causa = "Precipitación prolongada sobre el acceso sin pavimentar.",
            Consecuencia = "Suspensión del transporte de estructuras hasta recuperar la vía.",
            Disparadores = "Reporte de pérdida de transitabilidad en la inspección del acceso.",
            PlanRespuesta = "Coordinar una ruta alterna y revisar las cunetas antes de movilizar cargas.",
            ComoSeHaceSeguimiento = "Inspeccionar los accesos tras lluvias fuertes y antes de cada despacho.",
            EstadoActual = "Activo",
            Categoria = "Externo",
            Area = "Técnica",
            Estrategia = "Mitigar",
            ResponsablePlan = "Residente de obra",
            EstadoPlan = "En ejecución",
        };
        db.Riesgos.AddRange(riesgo, acceso);
        await db.SaveChangesAsync(ct);
        db.ElementosRiesgo.AddRange(ElementosRiesgo.DesdeTextos(riesgo).Concat(ElementosRiesgo.DesdeTextos(acceso)));
        db.AlcancesRiesgo.Add(new AlcanceRiesgo { RiesgoId = riesgo.Id, ElementoTrazadoId = torre });

        var seguimiento = new SeguimientoRiesgo
        {
            RiesgoId = riesgo.Id,
            Fecha = c.Hoy.AddDays(-10),
            Responsable = "Coordinación de Calidad",
            Resultado = ResultadoSeguimiento.Materializado,
            Observacion = $"En {sector} se detectó un lote sin certificado asociado antes de despacharlo a montaje.",
            Probabilidad = riesgo.Probabilidad,
            Impacto = riesgo.Impacto,
        };
        db.SeguimientosRiesgo.Add(seguimiento);
        await db.SaveChangesAsync(ct);

        var hallazgo = new HallazgoRiesgo
        {
            SeguimientoRiesgoId = seguimiento.Id,
            Descripcion = "El registro de recepción no relaciona el lote de pernos con su certificado de acero.",
            Accion = "Inmovilizar el lote y verificar su trazabilidad antes del montaje.",
            Estado = EstadoHallazgo.EnTratamiento,
        };
        db.HallazgosRiesgo.Add(hallazgo);
        await db.SaveChangesAsync(ct);
        db.LugaresHallazgo.Add(new LugarHallazgo { HallazgoRiesgoId = hallazgo.Id, ElementoTrazadoId = torre });
        var soporte = c.CopiarArchivo(ContextoDemo.PdfCertificado, "riesgos");
        db.ArchivosRiesgo.Add(new ArchivoRiesgo
        {
            HallazgoRiesgoId = hallazgo.Id,
            NombreOriginal = "Certificado de acero para cotejo del lote.pdf",
            RutaArchivo = soporte.RutaWeb,
        });
        await db.SaveChangesAsync(ct);
        SembradorCatalogosRiesgo.SincronizarConRiesgos(db);
        await transaccion.CommitAsync(ct);
    }
}
