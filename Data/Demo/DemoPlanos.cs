using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoPlanos : IModuloDemo
{
    public string Clave => "planos";
    public int Orden => 10;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.PlanosMaestro.IgnoreQueryFilters().AnyAsync(ct)) return;

        var torre = c.Torres().Select(id => (int?)id).FirstOrDefault();
        var hoja = await db.Componentes.Where(x => x.ElementoTrazadoId == torre && x.EsHojaDeVida)
            .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        var fecha = c.Hoy.AddDays(-20);
        var ejemplos = new[]
        {
            ("CIV-001", "Civil", c.Indice == 1 ? "Detalle de pila para torre BP6" : "Detalle de zapata para torre BP6"),
            ("EST-002", "Estructural", "Montaje de estructura de suspensión BP6"),
        };

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        foreach (var (codigo, disciplina, titulo) in ejemplos)
        {
            var grupo = await db.GruposPlano.SingleAsync(g => g.Nombre == disciplina, ct);
            var plano = new PlanoMaestro
            {
                Codigo = $"{c.Proyecto.Codigo}-DEMO-{codigo}", Titulo = titulo,
                GrupoPlanoId = grupo.Id, RevisionVigente = "01", Fecha = fecha,
                Estado = "APC", Notas = "PDF de muestra para recorrer el listado maestro.",
            };
            var carga = new CargaArchivosPlanos { Origen = "Archivo agregado", Descripcion = plano.Codigo, Archivos = 1 };
            db.PlanosMaestro.Add(plano);
            db.CargasArchivosPlanos.Add(carga);
            await db.SaveChangesAsync(ct);

            var (ruta, _) = c.CopiarArchivo(ContextoDemo.PdfPolitica, "planos");
            var archivo = new ArchivoPlanoMaestro
            {
                PlanoMaestroId = plano.Id, CargaArchivosPlanosId = carga.Id, Clase = ClaseArchivoDocumento.Vigente,
                Version = plano.RevisionVigente, Fecha = fecha,
                NombreOriginal = $"{plano.Codigo}-R01.pdf", RutaArchivo = ruta,
            };
            db.ArchivosPlanoMaestro.Add(archivo);
            await db.SaveChangesAsync(ct);

            if (disciplina == "Estructural" && hoja is int componenteId)
                db.ComponentesPlano.Add(new ComponentePlano { ComponenteId = componenteId, ArchivoPlanoMaestroId = archivo.Id });
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
