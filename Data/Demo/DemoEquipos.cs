using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoEquipos : IModuloDemo
{
    public string Clave => "equipos";
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.Equipos.IgnoreQueryFilters().AnyAsync(ct)) return;

        var campos = await db.CamposEquipo.ToDictionaryAsync(x => x.Clave, x => x.Id, ct);
        var ejemplos = new[]
        {
            ("Estación total", "GeoMedición Demo", "ET-5", "ET", 120),
            ("Receptor GPS RTK", "Posicionamiento Demo", "GN-2", "GPS", 15),
        };

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        foreach (var (nombre, marca, modelo, clave, dias) in ejemplos)
        {
            var equipo = new Equipo { Nombre = nombre, PeriodicidadMeses = 12 };
            db.Equipos.Add(equipo);
            await db.SaveChangesAsync(ct);

            var valores = new Dictionary<string, string>
            {
                ["Marca"] = marca,
                ["Modelo"] = modelo,
                ["NoSerie"] = $"{c.Proyecto.Codigo}-DEMO-{clave}-01",
                ["Ubicacion"] = c.Indice == 1 ? "Frente sur de topografía" : "Frente norte de topografía",
            };
            foreach (var (campo, valor) in valores)
                db.ValoresCampoEquipo.Add(new ValorCampoEquipo { EquipoId = equipo.Id, CampoEquipoId = campos[campo], Valor = valor });

            var vence = c.Hoy.AddDays(dias);
            var certificado = $"{c.Proyecto.Codigo}-DEMO-CAL-{clave}";
            var (ruta, _) = c.CopiarArchivo(ContextoDemo.PdfCertificado, "equipos/certificados");
            db.IntervencionesEquipo.Add(new IntervencionEquipo
            {
                EquipoId = equipo.Id, Tipo = TipoIntervencionEquipo.Calibracion,
                Fecha = vence.AddMonths(-12), ProximaFecha = vence,
                Responsable = "Laboratorio Metrológico Demo", NumeroCertificado = certificado,
                Descripcion = clave == "ET" ? "Verificación angular y de distancia" : "Verificación de posicionamiento sobre puntos de control",
                RutaCertificado = ruta, NombreCertificado = $"{certificado}.pdf",
            });
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
