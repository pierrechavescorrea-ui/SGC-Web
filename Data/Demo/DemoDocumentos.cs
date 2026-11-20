using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoDocumentos : IModuloDemo
{
    public string Clave => "documentos";
    public bool EnEmpresa => true;
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.DocumentosMaestro.IgnoreQueryFilters().AnyAsync(ct)) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var grupo = new GrupoDocumento { Nombre = c.Indice < 0 ? "Gestión integrada Demo" : "Control de calidad Demo" };
        db.GruposDocumento.Add(grupo);
        await db.SaveChangesAsync(ct);

        var prefijo = c.Indice < 0 ? "SGC" : c.Proyecto.Codigo;
        var fecha = c.Hoy.AddDays(-15);
        var ejemplos = new[]
        {
            ("POL-001", "Procedimientos e instructivos", "Política integrada", ContextoDemo.PdfPolitica),
            ("INF-002", "Informes", c.Indice < 0 ? "Informe de seguimiento del SGC" : "Informe de control de cimentaciones", ContextoDemo.PdfEnsayo),
        };

        foreach (var (codigo, tipo, titulo, recurso) in ejemplos)
        {
            var doc = new DocumentoMaestro
            {
                Codigo = $"{prefijo}-DEMO-{codigo}", TipoDocumental = tipo, Estado = "APC", GrupoDocumentoId = grupo.Id,
            };
            db.DocumentosMaestro.Add(doc);
            await db.SaveChangesAsync(ct);

            var revision = new RevisionDocumento
            {
                DocumentoMaestroId = doc.Id, Version = "00", Fecha = fecha,
                Cambio = "Emisión inicial para demostración", Responsable = "Coordinación de calidad Demo",
            };
            var carga = new CargaArchivosDocumentos { Origen = "Nueva versión", Descripcion = doc.Codigo, Archivos = 1 };
            db.RevisionesDocumento.Add(revision);
            db.CargasArchivosDocumentos.Add(carga);
            await db.SaveChangesAsync(ct);

            var (ruta, _) = c.CopiarArchivo(recurso, "documentos");
            db.VersionesDocumento.Add(new VersionDocumento
            {
                DocumentoMaestroId = doc.Id, RevisionDocumentoId = revision.Id, CargaArchivosDocumentosId = carga.Id,
                Clase = ClaseArchivoDocumento.Vigente, Version = revision.Version, Fecha = fecha,
                NombreOriginal = $"{doc.Codigo} - {titulo}.pdf", RutaArchivo = ruta,
            });
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
