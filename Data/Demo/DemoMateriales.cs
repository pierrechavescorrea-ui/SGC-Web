using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoMateriales : IModuloDemo
{
    public string Clave => "materiales";
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.Materiales.IgnoreQueryFilters().AnyAsync(ct)) return;

        var torreId = c.Torres().First();
        var ejemplos = new[]
        {
            ("Acero corrugado de refuerzo", "Aceros del Oriente Demo", "AC", ContextoDemo.PdfCertificado),
            (c.Indice == 1 ? "Concreto premezclado de 28 MPa" : "Concreto premezclado de 21 MPa", "Mezclas Andinas Demo", "CO", ContextoDemo.PdfEnsayo),
        };

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var motor = await MotorTabla.CargarAsync(db, Clave, [], []);
        var columnas = new[]
        {
            ("NombreMaterial", "Material", TipoCampoTabla.Texto),
            ("Proveedor", "Proveedor / Fabricante", TipoCampoTabla.Texto),
            ("Lote", "Lote", TipoCampoTabla.Texto),
            ("NumeroCertificado", "Cert. Calidad", TipoCampoTabla.Texto),
            ("Fecha", "Fecha", TipoCampoTabla.Fecha),
        };
        var campos = new List<CampoTabla>();
        foreach (var (clave, titulo, tipo) in columnas)
        {
            var campo = motor.Campos.FirstOrDefault(x => x.Clave == clave && x.Origen != OrigenCampoTabla.Nativo);
            if (campo is null)
            {
                campo = new CampoTabla { Tabla = Clave, Clave = clave, Nombre = titulo, Tipo = tipo, Visible = true, Orden = campos.Count };
                db.CamposTabla.Add(campo);
            }
            campos.Add(campo);
        }
        await db.SaveChangesAsync(ct);

        var fecha = c.Hoy.AddDays(-40);
        foreach (var (nombre, proveedor, clave, recurso) in ejemplos)
        {
            var material = new Material { EnCampos = true, Fecha = fecha };
            db.Materiales.Add(material);
            await db.SaveChangesAsync(ct);

            var lote = $"{c.Proyecto.Codigo}-DEMO-{clave}-01";
            var certificado = $"CC-{lote}";
            string[] valores = [nombre, proveedor, lote, certificado, fecha.ToString("yyyy-MM-dd")];
            for (var i = 0; i < campos.Count; i++)
                await motor.GuardarValorAsync(db, material.Id, campos[i], valores[i]);

            var (ruta, _) = c.CopiarArchivo(recurso, "materiales");
            db.AdjuntosMaterial.Add(new AdjuntoMaterial
            {
                MaterialId = material.Id, Clase = AdjuntoMaterial.Certificado,
                NombreOriginal = $"{certificado}.pdf", RutaArchivo = ruta,
            });
            db.UbicacionesMaterial.Add(new UbicacionMaterial { MaterialId = material.Id, ElementoTrazadoId = torreId, Fecha = fecha });
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
