using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoCertificaciones : IModuloDemo
{
    public string Clave => "certificaciones";
    public bool EnEmpresa => true;
    public bool EnProyecto => false;
    public int Orden => 11;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.Certificaciones.IgnoreQueryFilters().AnyAsync(ct)) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var emision = c.Hoy.AddMonths(-6);
        var doc = await DemoDireccionamiento.CrearDocumentoAsync(c, "CERT-DEMO-9001", "Certificados",
            "Certificado ISO 9001", ContextoDemo.PdfCertificado, emision, ct);
        db.Certificaciones.Add(new Certificacion
        {
            Norma = "ISO 9001:2015", Sistema = "Calidad",
            Alcance = "Construcción y montaje de líneas de transmisión eléctrica en Colombia",
            Organismo = "Certificadora Demo S.A.S.", Numero = "QMS-DEMO-2026-001",
            Emision = emision, Vencimiento = emision.AddYears(3), DocumentoMaestroId = doc,
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
