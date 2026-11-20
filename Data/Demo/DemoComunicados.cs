using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Comunicados;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoComunicados : IModuloDemo
{
    public string Clave => "comunicados";
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.Comunicados.IgnoreQueryFilters().AnyAsync(ct)) return;

        var sur = c.Indice == 1;
        var sector = sur ? "Sector Sur" : "Sector Norte";
        var cliente = sur ? SembradorDemo.Segundo.Cliente : SembradorDemo.Primero.Cliente;
        var entrada = c.Hoy.AddDays(-12);
        var respuesta = entrada.AddDays(3);
        var plantilla = new PlantillaConsecutivo
        {
            Remite = "INGDEMO",
            Destinatario = sur ? "EADEMO" : "TRDEMO",
            Proyecto = c.Proyecto.Codigo,
            ReiniciaCadaAnio = true,
        };
        var codigoRespuesta = ServicioConsecutivos.Codigo(plantilla, 1, ServicioConsecutivos.Anio2(respuesta));
        var comunicados = new[]
        {
            new Comunicado
            {
                Consecutivo = $"CLI-{c.Proyecto.Codigo}-041",
                Sentido = "Entrada",
                Tipo = "Externa",
                Remitente = cliente,
                Fecha = entrada,
                Descripcion = $"Solicitud de certificados de acero y soportes de recepción del frente de {sector}.",
                RequiereRespuesta = true,
                ConsecutivoRespuesta = codigoRespuesta,
                FechaRespuesta = respuesta,
                ResponsableRespuesta = "Calidad",
            },
            new Comunicado
            {
                Consecutivo = codigoRespuesta,
                Sentido = "Salida",
                Tipo = "Externa",
                Remitente = "Ingeniería Demo S.A.S.",
                Fecha = respuesta,
                Descripcion = $"Entrega de certificados de acero del frente de {sector} en respuesta a la solicitud del cliente.",
                RequiereRespuesta = false,
            },
            new Comunicado
            {
                Consecutivo = $"CLI-{c.Proyecto.Codigo}-046",
                Sentido = "Entrada",
                Tipo = "Externa",
                Remitente = cliente,
                Fecha = c.Hoy.AddDays(-2),
                Descripcion = $"Solicitud de confirmar la próxima visita de inspección de montaje en {sector}.",
                RequiereRespuesta = true,
                ResponsableRespuesta = "Construcción",
            },
        };

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        db.PlantillasConsecutivo.Add(plantilla);
        db.Comunicados.AddRange(comunicados);
        await db.SaveChangesAsync(ct);

        var nombres = new[] { "Solicitud de soportes de recepción.pdf", "Certificado de acero.pdf", "Solicitud de visita de inspección.pdf" };
        for (var i = 0; i < comunicados.Length; i++)
        {
            var recurso = i == 1 ? ContextoDemo.PdfCertificado : ContextoDemo.PdfEnsayo;
            db.AdjuntosComunicado.Add(new AdjuntoComunicado
            {
                ComunicadoId = comunicados[i].Id,
                Nombre = nombres[i],
                Ruta = c.CopiarArchivo(recurso, "comunicados").RutaWeb,
            });
        }
        await db.SaveChangesAsync(ct);
        await ServicioConsecutivos.ReservarAsync(db, new ServicioConsecutivos.DatosReserva(
            plantilla.Id, c.Hoy.AddDays(2), $"Confirmación de visita de inspección en {sector}",
            "Interventoría de obra", "Externa", comunicados[2].Consecutivo, false, null, null,
            "Coordinación de Calidad", "SigWeb"));

        await transaccion.CommitAsync(ct);
    }
}
