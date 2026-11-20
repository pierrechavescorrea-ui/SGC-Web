using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoMatrizPie : IModuloDemo
{
    public string Clave => "matriz-pie";
    public int Orden => 20;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.RegistrosParametro.IgnoreQueryFilters().AnyAsync(ct)) return;

        var torres = c.Torres().Take(3).ToList();
        var hojas = await db.Componentes.Where(x => torres.Contains(x.ElementoTrazadoId) && x.EsHojaDeVida)
            .ToDictionaryAsync(x => x.ElementoTrazadoId, x => x.Id, ct);
        var tiroId = await db.Tiros.OrderBy(x => x.Orden).ThenBy(x => x.Id).Select(x => x.Id).FirstAsync(ct);
        var controles = await (from p in db.ParametrosActividad
                               join a in db.Actividades on p.ActividadId equals a.Id
                               where p.Activo && a.Activo && a.EnMatrizPie && a.Ambito == AmbitoActividad.Torre
                                   && (a.Nombre == "Replanteo" && p.Codigo == "012"
                                       || a.Nombre == "Obra Civil" && p.Codigo == "049"
                                       || a.Nombre == "Montaje" && p.Codigo == "052")
                               orderby a.Orden, p.Id
                               select p.Id).ToListAsync(ct);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var estados = new[]
        {
            new EstadoParametro { Nombre = "Aprobado", Porcentaje = 100, Color = "#287844", Orden = 3 },
            new EstadoParametro { Nombre = "En revisión", Porcentaje = 60, Color = "#A96812", Orden = 2 },
            new EstadoParametro { Nombre = "Pendiente", Porcentaje = 0, Color = "#666666", Orden = 1 },
        };
        for (var i = 0; i < estados.Length; i++)
        {
            var estado = estados[i];
            var existente = await db.EstadosParametro.FirstOrDefaultAsync(x => x.Nombre == estado.Nombre && x.Activo, ct);
            if (existente is null) db.EstadosParametro.Add(estado);
            else estados[i] = existente;
        }

        var tendido = new Actividad { Nombre = "Control de tendido por tiro", Ambito = AmbitoActividad.Tiro, Orden = 8 };
        db.Actividades.Add(tendido);
        await db.SaveChangesAsync(ct);
        var poleas = new ParametroActividad { ActividadId = tendido.Id, Codigo = "PIE-T01", Nombre = "Inspección de poleas antes del tendido" };
        db.ParametrosActividad.Add(poleas);
        await db.SaveChangesAsync(ct);

        var observaciones = new[]
        {
            "Protocolo de campo firmado por interventoría.",
            c.Indice == 1 ? "Pendiente respuesta de interventoría del tramo sur." : "En revisión por el equipo de calidad del tramo norte.",
            "Pendiente radicación del soporte de campo.",
        };
        RegistroParametro? evidencia = null;
        for (var i = 0; i < torres.Count; i++)
        {
            for (var j = 0; j < controles.Count; j++)
            {
                var estado = (i + j) % estados.Length;
                var registro = new RegistroParametro
                {
                    ComponenteId = hojas[torres[i]],
                    ParametroActividadId = controles[j],
                    Completado = true,
                    EstadoParametroId = estados[estado].Id,
                    Fecha = c.Hoy.AddDays(-7 + i + j),
                    Observaciones = observaciones[estado],
                };
                db.RegistrosParametro.Add(registro);
                // El control de cilindros de la tercera torre queda aprobado.
                if (i == 2 && j == 1) evidencia = registro;
            }
        }

        db.RegistrosParametroTiro.Add(new RegistroParametroTiro
        {
            TiroId = tiroId, ParametroActividadId = poleas.Id, Completado = true,
            EstadoParametroId = estados[1].Id, Fecha = c.Hoy.AddDays(-2),
            Observaciones = "Inspección en campo terminada. Acta en revisión.",
        });
        await db.SaveChangesAsync(ct);
        if (evidencia is not null)
        {
            var (ruta, _) = c.CopiarArchivo(ContextoDemo.PdfEnsayo, "trazado");
            db.ArchivosRegistroParametro.Add(new ArchivoRegistroParametro
            {
                RegistroParametroId = evidencia.Id,
                NombreOriginal = "Informe de resistencia de cilindros (demo).pdf",
                RutaArchivo = ruta,
            });
            await db.SaveChangesAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }
}
