using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoEnsayos : IModuloDemo
{
    public string Clave => "ensayos";
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.ResultadosEnsayo.IgnoreQueryFilters().AnyAsync(ct)) return;

        var tipo = await db.TiposEnsayo.SingleAsync(x => x.Nombre == "Resistencia a la compresión", ct);
        var campos = await db.CamposEnsayo.Where(x => x.TipoEnsayoId == tipo.Id).ToDictionaryAsync(x => x.Clave, ct);
        var torres = c.Torres().Take(3).ToList();
        var fecha = c.Hoy.AddDays(-12);
        var numero = $"{c.Proyecto.Codigo}-DEMO-LAB-01";

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var informe = new Informe { TipoEnsayoId = tipo.Id, Numero = numero, Fecha = fecha };
        db.Informes.Add(informe);
        await db.SaveChangesAsync(ct);

        var (ruta, _) = c.CopiarArchivo(ContextoDemo.PdfEnsayo, "ensayos");
        var contenido = await File.ReadAllBytesAsync(Path.Combine(ContextoDemo.CarpetaRecursos, ContextoDemo.PdfEnsayo), ct);
        db.ArchivosInforme.Add(new ArchivoInforme
        {
            InformeId = informe.Id, NombreOriginal = $"{numero}.pdf", RutaArchivo = ruta,
            Hash = Convert.ToHexString(SHA256.HashData(contenido)),
        });
        await db.SaveChangesAsync(ct);

        var fc = c.Indice == 1 ? 4060 : 3050;
        for (var i = 0; i < torres.Count; i++)
        {
            var resultado = new ResultadoEnsayo
            {
                TipoEnsayoId = tipo.Id, InformeId = informe.Id, ElementoTrazadoId = torres[i],
                ElementoTexto = "Cimentación", Origen = "Manual",
            };
            db.ResultadosEnsayo.Add(resultado);
            await db.SaveChangesAsync(ct);

            var psi = fc + 140 + i * 85;
            var carga = psi * 0.00689476 * Math.PI * 75 * 75 / 1000;
            var valores = new Dictionary<string, string>
            {
                ["NoMuestra"] = (i + 1).ToString(CultureInfo.InvariantCulture),
                ["NoCilindro"] = "1",
                ["FechaMuestra"] = fecha.AddDays(-28).ToString("yyyy-MM-dd"),
                ["FechaEnsayo"] = fecha.ToString("yyyy-MM-dd"),
                ["Edad"] = "28",
                ["Diametro"] = "150",
                ["CargaKn"] = carga.ToString("F1", CultureInfo.InvariantCulture),
                ["CargaPsi"] = psi.ToString(CultureInfo.InvariantCulture),
                ["FcDiseno"] = fc.ToString(CultureInfo.InvariantCulture),
                ["Observaciones"] = "Cilindro curado en laboratorio. Resultado de demostración.",
            };
            foreach (var (clave, valor) in valores)
                db.ValoresCampoEnsayo.Add(new ValorCampoEnsayo { ResultadoEnsayoId = resultado.Id, CampoEnsayoId = campos[clave].Id, Valor = valor });
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
