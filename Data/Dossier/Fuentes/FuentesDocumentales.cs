using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Direccionamiento estratégico: cada documento es un DocumentoMaestro; archivos = los de Documentos (vigente actual,
// anteriores en "Versiones anteriores"). Grupo = tipo (Visión, Misión, Política…).
public sealed class FuenteDireccionamiento : IFuenteDossier
{
    public string Clave => "direccionamiento";
    public string Nombre => "Direccionamiento estratégico";
    public string Icono => "direccionamiento";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var docs = await db.DocumentosDireccionamiento.AsNoTracking().Where(d => !d.Eliminado)
            .OrderBy(d => d.Tipo).ThenBy(d => d.Orden).ThenBy(d => d.Id).ToListAsync(ct);
        var idsDm = docs.Where(d => d.DocumentoMaestroId != null).Select(d => d.DocumentoMaestroId!.Value).ToList();
        var maestros = await db.DocumentosMaestro.AsNoTracking().Where(m => idsDm.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var archivos = await FuenteDocumentos.ArchivosDeAsync(db, idsDm, ct);
        var porId = docs.ToDictionary(d => d.Id);
        DocumentoMaestro? M(int id) => porId.TryGetValue(id, out var d) && d.DocumentoMaestroId is int m ? maestros.GetValueOrDefault(m) : null;
        var nativas = new[]
        {
            new ColumnaNativa("Titulo", "Título", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Titulo),
            new ColumnaNativa("Tipo", "Tipo", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Tipo),
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => M(id)?.Codigo),
            new ColumnaNativa("Estado", "Estado", TipoCampoTabla.Texto, id => M(id)?.Estado),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:direccionamiento", nativas, docs.Select(d => (d.Id, d.Titulo)));
        var elementos = docs.Select(d =>
        {
            var el = new ElementoDossier { Id = d.Id, Grupo = d.Tipo };
            if (d.DocumentoMaestroId is int m && archivos.TryGetValue(m, out var l)) el.Archivos.AddRange(l);
            return el;
        }).ToList();
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Titulo", NombreGrupo = "Tipo",
            Clases = new() { "Vigente", "Editable", "Obsoleta" }, TieneVersiones = true
        };
    }
}

// Certificaciones (vitrina ISO de la empresa): cada certificado se liga a un DocumentoMaestro.
public sealed class FuenteCertificaciones : IFuenteDossier
{
    public string Clave => "certificaciones";
    public string Nombre => "Certificaciones";
    public string Icono => "certificaciones";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var certs = await db.Certificaciones.AsNoTracking().Where(c => !c.Eliminado).OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync(ct);
        var idsDm = certs.Where(c => c.DocumentoMaestroId != null).Select(c => c.DocumentoMaestroId!.Value).ToList();
        var archivos = await FuenteDocumentos.ArchivosDeAsync(db, idsDm, ct);
        var porId = certs.ToDictionary(c => c.Id);
        Certificacion? C(int id) => porId.GetValueOrDefault(id);
        var nativas = new[]
        {
            new ColumnaNativa("Norma", "Norma", TipoCampoTabla.Texto, id => C(id)?.Norma),
            new ColumnaNativa("Sistema", "Sistema", TipoCampoTabla.Texto, id => C(id)?.Sistema),
            new ColumnaNativa("Alcance", "Alcance", TipoCampoTabla.Texto, id => C(id)?.Alcance),
            new ColumnaNativa("Organismo", "Organismo", TipoCampoTabla.Texto, id => C(id)?.Organismo),
            new ColumnaNativa("Numero", "Número", TipoCampoTabla.Texto, id => C(id)?.Numero),
            new ColumnaNativa("Emision", "Emisión", TipoCampoTabla.Fecha, id => C(id)?.Emision),
            new ColumnaNativa("Vencimiento", "Vencimiento", TipoCampoTabla.Fecha, id => C(id)?.Vencimiento),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:certificaciones", nativas, certs.Select(c => (c.Id, c.Norma)));
        var elementos = certs.Select(c =>
        {
            var el = new ElementoDossier { Id = c.Id };
            if (c.DocumentoMaestroId is int m && archivos.TryGetValue(m, out var l)) el.Archivos.AddRange(l);
            return el;
        }).ToList();
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Norma",
            Clases = new() { "Vigente", "Editable", "Obsoleta" }, TieneVersiones = true
        };
    }
}
