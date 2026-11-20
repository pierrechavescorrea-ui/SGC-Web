using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Ensayos;

// Campos "Resumen" de los resultados de un tipo de ensayo, tal como el usuario los marca en la tabla del ensayo
// (Ensayos › tipo › Campos: CampoTabla de "ensayos:{tipo}", mismas columnas y nombres que la página EnsayosDatos).
// Columnas propias: Torre/Localización, Elemento, campos de la plantilla (CampoEnsayo.Clave, valores guardados, también las
// fórmulas), Informe, Fecha ejecución, Diseño mezcla. Campos agregados (Excel/manuales): valores de ValoresCampoTabla.
// Respaldo para tipos sin marcas en la tabla: el check EnResumen de la plantilla (CampoEnsayo).
// Null = el tipo no tiene nada marcado (cada vista muestra lo de siempre).
public static class ResumenEnsayos
{
    public sealed record Columna(string Nombre, Func<int, string?> Valor);

    // vista = false: «Información Bot» (EnResumen, con respaldo en la plantilla). vista = true: «Resumen» (EnVistaResumen, sin respaldo).
    public static async Task<Dictionary<int, List<(string Nombre, string Valor)>>?> ParesAsync(AppDbContext db, int tipoEnsayoId, IReadOnlyCollection<ResultadoEnsayo> resultados, bool vista = false)
    {
        var tabla = $"ensayos:{tipoEnsayoId}";
        var marcados = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == tabla && !c.Eliminado && (vista ? c.EnVistaResumen : c.EnResumen)).OrderBy(c => c.Orden).ToListAsync();
        if (vista && marcados.Count == 0) return null;
        if (resultados.Count == 0) return new();
        var plantilla = await db.CamposEnsayo.AsNoTracking().Where(c => c.TipoEnsayoId == tipoEnsayoId && c.Activo).OrderBy(c => c.Orden).ToListAsync();
        var ids = resultados.Select(r => r.Id).ToList();
        var porId = resultados.ToDictionary(r => r.Id);

        // Valores de la plantilla por resultado (clave del campo → valor).
        var claves = plantilla.ToDictionary(c => c.Id, c => c.Clave);
        var vals = (await db.ValoresCampoEnsayo.AsNoTracking().Where(v => ids.Contains(v.ResultadoEnsayoId)).ToListAsync())
            .Where(v => claves.ContainsKey(v.CampoEnsayoId))
            .GroupBy(v => v.ResultadoEnsayoId).ToDictionary(g => g.Key, g => g.GroupBy(v => claves[v.CampoEnsayoId]).ToDictionary(x => x.Key, x => x.First().Valor));
        string? DePlantilla(int id, string clave) => vals.TryGetValue(id, out var d) && d.TryGetValue(clave, out var v) ? v : null;

        var columnas = new List<Columna>();
        if (marcados.Count > 0)
        {
            var torreIds = resultados.Where(r => r.ElementoTrazadoId != null).Select(r => r.ElementoTrazadoId!.Value).Distinct().ToList();
            var torres = await db.ElementosTrazado.AsNoTracking().Where(t => torreIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Codigo);
            var infIds = resultados.Where(r => r.InformeId != null).Select(r => r.InformeId!.Value).Distinct().ToList();
            var informes = await db.Informes.AsNoTracking().Where(i => infIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Numero);
            var propios = marcados.Where(c => c.Origen != OrigenCampoTabla.Nativo).Select(c => c.Id).ToList();
            var valsTabla = propios.Count == 0 ? new Dictionary<(int, int), string?>()
                : (await db.ValoresCampoTabla.AsNoTracking().Where(v => propios.Contains(v.CampoTablaId) && ids.Contains(v.FilaId)).ToListAsync())
                    .GroupBy(v => (v.CampoTablaId, v.FilaId)).ToDictionary(g => g.Key, g => g.First().Valor);
            var clavesPlantilla = plantilla.Select(c => c.Clave).ToHashSet();
            foreach (var c in marcados)
            {
                var campo = c;
                Func<int, string?> valor = campo.Origen != OrigenCampoTabla.Nativo
                    ? id => valsTabla.GetValueOrDefault((campo.Id, id))
                    : campo.Clave switch
                    {
                        "Torre" => id => porId[id].ElementoTrazadoId is int t && torres.TryGetValue(t, out var cod) ? cod : porId[id].Localizacion,
                        "Elemento" => id => porId[id].ElementoTexto,
                        "Informe" => id => porId[id].InformeId is int i && informes.TryGetValue(i, out var n) ? n : null,
                        "FechaEjecucion" => id => porId[id].FechaEjecucion?.ToString("dd/MM/yyyy"),
                        "DisenoMezcla" => id => porId[id].DisenoMezclaCodigo,
                        var k when clavesPlantilla.Contains(k) => id => DePlantilla(id, k),
                        _ => _ => null
                    };
                columnas.Add(new Columna(MotorTabla.EtiquetaCampo(campo), valor));
            }
        }
        else if (!vista && plantilla.Any(c => c.EnResumen))
        {
            foreach (var c in plantilla.Where(c => c.EnResumen))
            {
                var campo = c;
                columnas.Add(new Columna(campo.Etiqueta + (string.IsNullOrWhiteSpace(campo.Unidad) ? "" : $" ({campo.Unidad})"), id => DePlantilla(id, campo.Clave)));
            }
        }
        else return null;

        return ids.ToDictionary(id => id, id => columnas
            .Select(col => (col.Nombre, V: col.Valor(id)))
            .Where(x => !string.IsNullOrWhiteSpace(x.V))
            .Select(x => (x.Nombre, ResumenCampos.Valor(x.V!)))
            .ToList());
    }

    // Columnas marcadas «Resumen» (vista) de un tipo, en orden: los nombres de la cabecera de la vista resumida.
    public static async Task<List<string>> ColumnasVistaAsync(AppDbContext db, int tipoEnsayoId) =>
        (await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == $"ensayos:{tipoEnsayoId}" && !c.Eliminado && c.EnVistaResumen).OrderBy(c => c.Orden).ToListAsync())
            .Select(MotorTabla.EtiquetaCampo).ToList();

    // Arranque (una vez): «Resumen» empieza con lo mismo que «Información Bot» si ninguna tabla de ensayos tiene «Resumen».
    public static void SembrarVistaResumen(AppDbContext db)
    {
        try
        {
            if (db.CamposTabla.Any(c => c.Tabla.StartsWith("ensayos:") && c.EnVistaResumen)) return;
            var l = db.CamposTabla.Where(c => c.Tabla.StartsWith("ensayos:") && c.EnResumen && !c.Eliminado).ToList();
            foreach (var c in l) c.EnVistaResumen = true;
            if (l.Count > 0) db.SaveChanges();
        }
        catch { /* migración ResumenEnsayos sin aplicar */ }
    }

    // "Campo: valor · Campo: valor" por resultado; null = sin marcas (lo de siempre).
    public static async Task<Dictionary<int, string>?> TextoAsync(AppDbContext db, int tipoEnsayoId, IReadOnlyCollection<ResultadoEnsayo> resultados)
    {
        var p = await ParesAsync(db, tipoEnsayoId, resultados);
        return p?.ToDictionary(k => k.Key, k => string.Join(" · ", k.Value.Select(x => $"{x.Nombre}: {x.Valor}")));
    }
}
