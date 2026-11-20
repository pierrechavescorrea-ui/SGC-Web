using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Documentos;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoDireccionamiento : IModuloDemo
{
    public string Clave => "direccionamiento";
    public bool EnEmpresa => true;
    public bool EnProyecto => false;
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.DocumentosDireccionamiento.IgnoreQueryFilters().AnyAsync(ct)) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var fecha = new DateTime(2026, 2, 2);
        var vision = await CrearDocumentoAsync(c, "SGI-DEMO-01", "Direccionamiento estratégico",
            "Visión corporativa", ContextoDemo.PdfPolitica, fecha, ct);
        var politica = await CrearDocumentoAsync(c, "SGI-DEMO-02", "Direccionamiento estratégico",
            "Política integrada", ContextoDemo.PdfPolitica, fecha, ct);
        db.DocumentosDireccionamiento.AddRange(
            new DocumentoDireccionamiento { Tipo = "Visión", Titulo = "Visión corporativa 2030", DocumentoMaestroId = vision },
            new DocumentoDireccionamiento { Tipo = "Política integrada", Titulo = "Política integrada de calidad, ambiente y SST", DocumentoMaestroId = politica, Orden = 1 });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    internal static async Task<int> CrearDocumentoAsync(ContextoDemo c, string codigo, string tipo,
        string titulo, string recurso, DateTime fecha, CancellationToken ct)
    {
        var db = c.Db;
        if (!await db.Catalogos.AnyAsync(x => x.Tipo == MigracionDocumentos.CatTipo && x.Nombre == tipo, ct))
            db.Catalogos.Add(new ItemCatalogo { Tipo = MigracionDocumentos.CatTipo, Nombre = tipo });

        var doc = new DocumentoMaestro { Codigo = codigo, TipoDocumental = tipo };
        db.DocumentosMaestro.Add(doc);
        await db.SaveChangesAsync(ct);

        var revision = new RevisionDocumento
        {
            DocumentoMaestroId = doc.Id, Version = "01", Fecha = fecha,
            Cambio = "Emisión inicial para demostración", Responsable = "Coordinación SGI de Ingeniería Demo S.A.S.",
        };
        db.RevisionesDocumento.Add(revision);
        await db.SaveChangesAsync(ct);

        var (ruta, _) = c.CopiarArchivo(recurso, "documentos");
        db.VersionesDocumento.Add(new VersionDocumento
        {
            DocumentoMaestroId = doc.Id, RevisionDocumentoId = revision.Id, Clase = ClaseArchivoDocumento.Vigente,
            Version = revision.Version, Fecha = fecha, NombreOriginal = titulo + " (demo).pdf", RutaArchivo = ruta,
        });
        return doc.Id;
    }
}
