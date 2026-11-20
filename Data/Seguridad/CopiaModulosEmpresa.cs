using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Seguridad;

// Al pasar a multiproyecto, Direccionamiento estratégico y Certificaciones se volvieron módulos de la Empresa (se leen de su base),
// pero lo cargado antes quedó en la base del piloto. Esto copia esos registros del piloto a la Empresa: documentos de
// direccionamiento, certificaciones y sus documentos maestros (revisiones, versiones/archivos y control de cambios).
// No borra nada del piloto. Idempotente: solo actúa si la Empresa aún no tiene registros en ninguno de los dos módulos.
// Los archivos no se mueven: RutaArchivo apunta a wwwroot/uploads, compartido por todos los proyectos.
public static class CopiaModulosEmpresa
{
    public static async Task<string> CopiarSiFaltaAsync(ServicioProyectos proyectos, ILogger log, CancellationToken ct = default)
    {
        var empresa = proyectos.Empresa(); var piloto = proyectos.Piloto();
        if (empresa is null || piloto is null || empresa.Id == piloto.Id) return "Sin Empresa o sin piloto: nada que copiar.";
        if (await proyectos.PrepararAsync(empresa.Id, ct) is { } err) return "La base de la Empresa no está lista: " + err;

        await using var origen = proyectos.CrearContexto(piloto);
        await using var destino = proyectos.CrearContexto(empresa);
        if (!await origen.Database.CanConnectAsync(ct)) return "La base del piloto no responde.";
        if (await destino.DocumentosDireccionamiento.IgnoreQueryFilters().AnyAsync(ct) || await destino.Certificaciones.IgnoreQueryFilters().AnyAsync(ct))
            return "La Empresa ya tiene datos en Direccionamiento o Certificaciones: no se copia nada.";

        var dirs = await origen.DocumentosDireccionamiento.AsNoTracking().Where(d => !d.Eliminado).ToListAsync(ct);
        var certs = await origen.Certificaciones.AsNoTracking().Where(c => !c.Eliminado).ToListAsync(ct);
        if (dirs.Count == 0 && certs.Count == 0) return "El piloto no tiene datos en esos módulos.";

        var docIds = dirs.Select(d => d.DocumentoMaestroId).Concat(certs.Select(c => c.DocumentoMaestroId)).OfType<int>().Distinct().ToList();
        var docs = await origen.DocumentosMaestro.AsNoTracking().Where(d => docIds.Contains(d.Id)).ToListAsync(ct);
        var revs = await origen.RevisionesDocumento.AsNoTracking().Where(r => docIds.Contains(r.DocumentoMaestroId)).ToListAsync(ct);
        var vers = await origen.VersionesDocumento.AsNoTracking().Where(v => docIds.Contains(v.DocumentoMaestroId)).ToListAsync(ct);
        var verIds = vers.Select(v => v.Id).ToList();
        var ccs = await origen.ControlesCambioVersion.AsNoTracking().Where(c => verIds.Contains(c.VersionDocumentoId)).ToListAsync(ct);

        await using var tx = await destino.Database.BeginTransactionAsync(ct);
        // Documentos maestros (sin grupo ni carga del piloto: esas tablas no viajan).
        var mapaDoc = new Dictionary<int, DocumentoMaestro>();
        foreach (var d in docs)
        {
            var viejo = d.Id; d.Id = 0; d.GrupoDocumentoId = null; d.CargaDocumentosId = null;
            destino.DocumentosMaestro.Add(d); mapaDoc[viejo] = d;
        }
        await destino.SaveChangesAsync(ct);
        var mapaRev = new Dictionary<int, RevisionDocumento>();
        foreach (var r in revs)
        {
            var viejo = r.Id; r.Id = 0; r.DocumentoMaestroId = mapaDoc[r.DocumentoMaestroId].Id;
            destino.RevisionesDocumento.Add(r); mapaRev[viejo] = r;
        }
        await destino.SaveChangesAsync(ct);
        var mapaVer = new Dictionary<int, VersionDocumento>();
        foreach (var v in vers)
        {
            var viejo = v.Id; v.Id = 0; v.DocumentoMaestroId = mapaDoc[v.DocumentoMaestroId].Id; v.CargaArchivosDocumentosId = null;
            v.RevisionDocumentoId = v.RevisionDocumentoId is int ri && mapaRev.TryGetValue(ri, out var rn) ? rn.Id : null;
            destino.VersionesDocumento.Add(v); mapaVer[viejo] = v;
        }
        await destino.SaveChangesAsync(ct);
        foreach (var c in ccs) { c.Id = 0; c.VersionDocumentoId = mapaVer[c.VersionDocumentoId].Id; destino.ControlesCambioVersion.Add(c); }
        foreach (var d in dirs) { d.Id = 0; d.DocumentoMaestroId = d.DocumentoMaestroId is int di && mapaDoc.TryGetValue(di, out var dn) ? dn.Id : null; destino.DocumentosDireccionamiento.Add(d); }
        foreach (var c in certs) { c.Id = 0; c.DocumentoMaestroId = c.DocumentoMaestroId is int di && mapaDoc.TryGetValue(di, out var dn) ? dn.Id : null; destino.Certificaciones.Add(c); }
        await destino.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var msg = $"Copiados a la Empresa desde {piloto.Codigo}: {dirs.Count} documento(s) de direccionamiento, {certs.Count} certificación(es), " +
                  $"{docs.Count} documento(s) maestro(s), {revs.Count} revisión(es), {vers.Count} archivo(s).";
        log.LogInformation("{Mensaje}", msg);
        return msg;
    }
}
