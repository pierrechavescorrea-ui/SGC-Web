using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Dossier;
using SigWeb.Models;

namespace SigWeb.Data.Tablas;

// Campos "Resumen" (2026-11-16): en cada tabla de campos (Campos de las listas, campos de cada tipo de ensayo y campos de
// equipos) el usuario marca con un check qué datos se muestran fuera de la tabla: bot de Telegram, ficha del elemento/pata,
// hoja de vida de la torre. Sin ninguno marcado, cada vista muestra lo de siempre (comportamiento por defecto).
// Cualquier campo se puede marcar, también los propios del módulo (Código, Estado, Fecha…): sus valores se leen con la misma
// tabla que arma el Dossier para ese módulo (columnas propias con las claves de la página, Campos y fórmulas calculadas).
public static partial class ResumenCampos
{
    // Los marcados, o todos si no hay ninguno marcado (la vista decide entonces su criterio de siempre).
    public static List<T> Elegir<T>(List<T> campos, Func<T, bool> marcado)
    {
        var m = campos.Where(marcado).ToList();
        return m.Count > 0 ? m : campos;
    }

    // Valor tal como se lee: fechas guardadas yyyy-MM-dd → dd/MM/yyyy.
    public static string Valor(string v)
    {
        v = v.Trim();
        return RxFecha().IsMatch(v) && DateTime.TryParse(v[..10], out var f) ? f.ToString("dd/MM/yyyy") : v;
    }

    // Texto por fila de toda la tabla, calculado una vez por minuto (o hasta que cambie un check: Invalidar).
    private static readonly ConcurrentDictionary<string, (DateTime Hasta, Dictionary<int, string> Texto)> cache = new();
    public static void Invalidar(string tabla) => cache.TryRemove(SigWeb.Data.Seguridad.AmbienteSig.Clave(tabla), out _);   // caché por proyecto

    // "Campo: valor · Campo: valor" por fila con los campos marcados como Resumen de la tabla. Vacío si no hay marcados.
    public static async Task<Dictionary<int, string>> TextoAsync(AppDbContext db, string tabla, IReadOnlyCollection<int> filas)
    {
        if (filas.Count == 0) return new();
        if (!await db.CamposTabla.AsNoTracking().AnyAsync(c => c.Tabla == tabla && c.EnResumen && !c.Eliminado)) return new();
        var clave = SigWeb.Data.Seguridad.AmbienteSig.Clave(tabla);
        if (!cache.TryGetValue(clave, out var c0) || c0.Hasta < DateTime.UtcNow)
        {
            c0 = (DateTime.UtcNow.AddMinutes(1), await CalcularAsync(db, tabla));
            cache[clave] = c0;
        }
        return filas.Where(c0.Texto.ContainsKey).Distinct().ToDictionary(f => f, f => c0.Texto[f]);
    }

    // Para código síncrono (hoja de vida en Trazado): se calcula en su propio scope, fuera del DbContext del circuito.
    public static Dictionary<int, string> Texto(IServiceScopeFactory scopes, string tabla, IReadOnlyCollection<int> filas) =>
        Task.Run(async () =>
        {
            using var scope = scopes.CreateScope();
            return await TextoAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), tabla, filas);
        }).GetAwaiter().GetResult();

    private static async Task<Dictionary<int, string>> CalcularAsync(AppDbContext db, string tabla)
    {
        // Con fuente del Dossier: tabla completa (propias + Campos + fórmulas), igual que en la pantalla del módulo.
        if (FuentesDossier.Todas.FirstOrDefault(f => f.Clave == tabla) is { } fuente)
        {
            var fc = await fuente.CargarAsync(db, CancellationToken.None);
            if (fc.Motor.Tabla == tabla)
            {
                var marcados = fc.Motor.Ordenados.Where(c => c.EnResumen).ToList();
                return fc.Elementos.Select(e => e.Id).Distinct().ToDictionary(id => id, id => Unir(marcados.Select(c => (c, (string?)fc.Motor.Texto(id, c, out _)))))
                    .Where(k => k.Value.Length > 0).ToDictionary(k => k.Key, k => k.Value);
            }
        }
        // Sin fuente: valores guardados de los Campos marcados.
        var campos = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == tabla && c.EnResumen && !c.Eliminado).OrderBy(c => c.Orden).ToListAsync();
        var ids = campos.Select(c => c.Id).ToList();
        var vals = await db.ValoresCampoTabla.AsNoTracking().Where(v => ids.Contains(v.CampoTablaId) && v.Valor != null && v.Valor != "")
            .Select(v => new { v.CampoTablaId, v.FilaId, v.Valor }).ToListAsync();
        return vals.GroupBy(v => v.FilaId).ToDictionary(g => g.Key, g => Unir(campos.Select(c => (c, g.FirstOrDefault(x => x.CampoTablaId == c.Id)?.Valor))))
            .Where(k => k.Value.Length > 0).ToDictionary(k => k.Key, k => k.Value);
    }

    private static string Unir(IEnumerable<(CampoTabla c, string? v)> pares) =>
        string.Join(" · ", pares.Where(x => !string.IsNullOrWhiteSpace(x.v)).Select(x => $"{MotorTabla.EtiquetaCampo(x.c)}: {Valor(x.v!)}"));

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}")] private static partial Regex RxFecha();
}
