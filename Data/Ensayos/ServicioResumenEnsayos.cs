using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Ensayos;

// Resumen de ensayos (Ensayos › Resumen): todos los resultados de todas las torres, con SOLO las columnas que el usuario marcó
// «Resumen» en la tabla de cada ensayo (CampoTabla.EnVistaResumen). La app no decide qué datos mostrar.
// Se lee con su propio DbContext; nada se escribe.
public sealed class FilaResumenEnsayo
{
    public int Id { get; init; }
    public int? TorreId { get; init; }          // ElementoTrazado (para agrupar por torre); null = localización libre
    public string Lugar { get; init; } = "";    // código de torre o localización (agrupación «Por torre»)
    public DateTime Fecha { get; init; }        // ejecución o creación: orden
    public Dictionary<string, string> Valores { get; init; } = new();   // columna → valor
}

public sealed class TipoResumenEnsayo
{
    public int Id { get; init; }
    public string Nombre { get; init; } = "";
    public string Grupo { get; init; } = "";
    public bool UsaTorre { get; init; }
    public bool SinMarcas { get; init; }        // ninguna columna marcada «Resumen»
    public List<string> Columnas { get; init; } = new();
    public List<FilaResumenEnsayo> Filas { get; init; } = new();
}

public static class ServicioResumenEnsayos
{
    public static async Task<List<TipoResumenEnsayo>> CargarAsync(AppDbContext db, CancellationToken ct = default)
    {
        var grupos = await db.GruposEnsayo.AsNoTracking().ToDictionaryAsync(g => g.Id, g => (g.Nombre, g.Orden), ct);
        var tipos = (await db.TiposEnsayo.AsNoTracking().ToListAsync(ct))
            .OrderBy(t => grupos.TryGetValue(t.GrupoEnsayoId, out var g) ? g.Orden : int.MaxValue)
            .ThenBy(t => grupos.TryGetValue(t.GrupoEnsayoId, out var g) ? g.Nombre : "").ThenBy(t => t.Orden).ThenBy(t => t.Nombre).ToList();
        var r = new List<TipoResumenEnsayo>();
        foreach (var t in tipos)
        {
            ct.ThrowIfCancellationRequested();
            var columnas = await ResumenEnsayos.ColumnasVistaAsync(db, t.Id);
            var resultados = columnas.Count == 0
                ? await db.ResultadosEnsayo.AsNoTracking().Where(x => x.TipoEnsayoId == t.Id && !x.Eliminado).Select(x => new ResultadoEnsayo { Id = x.Id }).ToListAsync(ct)
                : await db.ResultadosEnsayo.AsNoTracking().Where(x => x.TipoEnsayoId == t.Id && !x.Eliminado).ToListAsync(ct);
            var filas = new List<FilaResumenEnsayo>();
            if (columnas.Count > 0 && resultados.Count > 0)
            {
                var pares = await ResumenEnsayos.ParesAsync(db, t.Id, resultados, vista: true) ?? new();
                var torreIds = resultados.Where(x => x.ElementoTrazadoId != null).Select(x => x.ElementoTrazadoId!.Value).Distinct().ToList();
                var torres = await db.ElementosTrazado.AsNoTracking().Where(e => torreIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Codigo, ct);
                foreach (var x in resultados)
                    filas.Add(new FilaResumenEnsayo
                    {
                        Id = x.Id, TorreId = x.ElementoTrazadoId,
                        Lugar = x.ElementoTrazadoId is int tid && torres.TryGetValue(tid, out var cod) ? cod : (x.Localizacion ?? "").Trim(),
                        Fecha = x.FechaEjecucion ?? x.FechaCreacion,
                        Valores = (pares.GetValueOrDefault(x.Id) ?? new()).GroupBy(p => p.Nombre).ToDictionary(g => g.Key, g => g.First().Valor),
                    });
            }
            r.Add(new TipoResumenEnsayo
            {
                Id = t.Id, Nombre = t.Nombre, UsaTorre = t.UsaTorre,
                Grupo = grupos.TryGetValue(t.GrupoEnsayoId, out var gg) ? gg.Nombre : "",
                SinMarcas = columnas.Count == 0, Columnas = columnas,
                Filas = columnas.Count == 0 ? resultados.Select(x => new FilaResumenEnsayo { Id = x.Id }).ToList() : filas,
            });
        }
        return r;
    }
}
