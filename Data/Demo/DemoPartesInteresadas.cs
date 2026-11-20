using Microsoft.EntityFrameworkCore;
using SigWeb.Data.PartesInteresadas;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoPartesInteresadas : IModuloDemo
{
    public string Clave => "partes-interesadas";
    public int Orden => 12;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.VersionesPartesInteresadas.IgnoreQueryFilters().AnyAsync(ct)) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var inicio = new DateTime(2026, c.Indice == 1 ? 4 : 2, 1);
        var cliente = c.Indice == 1 ? SembradorDemo.Segundo.Cliente : SembradorDemo.Primero.Cliente;
        var version = await ServicioPartesInteresadas.CrearBorradorAsync(db, null, $"Partes interesadas de {c.NombreProyecto}");
        db.VersionesPartesInteresadas.Attach(version);
        version.Alcance = c.NombreProyecto;
        version.Origen = "Demostración";
        await db.SaveChangesAsync(ct);

        var partes = new ParteInteresada[]
        {
            new()
            {
                Nombre = cliente, Procesos = "Dirección de obra y calidad", Necesidades = "Entregables trazables",
                Expectativas = "Recibir dossiers completos en las fechas pactadas", AfectaSgc = "Aceptación de entregables",
                Impacto = 9, Influencia = 9, Estrategias = "Revisión mensual del dossier con interventoría",
                Medicion = "Porcentaje de entregables aprobados en primera revisión", Periodicidad = "Mensual", Meta = "≥ 90%",
            },
            new()
            {
                Nombre = c.Indice == 1 ? "Comunidad del corredor sur (demo)" : "Comunidad del corredor norte (demo)",
                Procesos = "Gestión social y ambiental", Necesidades = "Accesos transitables y avisos de obra",
                Expectativas = "Conocer los cierres temporales antes de su inicio", AfectaSgc = "Continuidad de los frentes de trabajo",
                Impacto = 8, Influencia = 4, Estrategias = "Avisos previos a las intervenciones y atención de solicitudes",
                Medicion = "Porcentaje de solicitudes respondidas en plazo", Periodicidad = "Mensual", Meta = "≥ 95%",
            },
            new()
            {
                Nombre = "Montajes Demo S.A.S.", Procesos = "Montaje y SST", Necesidades = "Planos vigentes y frentes liberados",
                Expectativas = "Contar con la secuencia de montaje aprobada", AfectaSgc = "Conformidad y seguridad del montaje",
                Impacto = 7, Influencia = 6, Estrategias = "Verificar la liberación del frente antes de movilizar la cuadrilla",
                Medicion = "Porcentaje de frentes con liberación documentada", Periodicidad = "Mensual", Meta = "100%",
            },
        };
        string[] responsables = { "Coordinación de calidad", "Gestión social", "Residente de obra" };
        for (var i = 0; i < partes.Length; i++)
        {
            var parte = partes[i];
            parte.VersionPartesInteresadasId = version.Id;
            parte.Clasificacion = ServicioPartesInteresadas.Nombre(ServicioPartesInteresadas.Cuadrante(parte.Impacto, parte.Influencia));
            partes[i] = await ServicioPartesInteresadas.GuardarParteAsync(db, parte);
            await ServicioPartesInteresadas.GuardarProgramacionAsync(db, partes[i].Clave, 1, UnidadPeriodo.Meses, inicio.AddMonths(1), responsables[i]);
        }
        await ServicioPartesInteresadas.PublicarAsync(db, version.Id, new DatosPublicacionPartes
        {
            Codigo = $"{c.Proyecto.Codigo}-SGI-PI-01", VigenteDesde = inicio, FechaAprobacion = inicio,
            Motivo = "Identificación inicial de las partes interesadas de la obra",
            Elaboro = "Coordinación SGI", Reviso = "Residente de obra", Aprobo = "Dirección de proyecto",
        });
        await ServicioPartesInteresadas.GuardarSeguimientoAsync(db, new SeguimientoParteInteresada
        {
            ParteClave = partes[0].Clave, ParteInteresadaId = partes[0].Id, Fecha = inicio.AddMonths(1),
            Responsable = responsables[0], Resultado = c.Indice == 1 ? "95% (19 de 20)" : "90% (9 de 10)",
            Cumplimiento = CumplimientoParte.Cumple, Impacto = partes[0].Impacto, Influencia = partes[0].Influencia,
            Observacion = "Se revisó el registro de entregables con interventoría y se acordó completar los pendientes en el siguiente corte.",
        });
        await tx.CommitAsync(ct);
    }
}
