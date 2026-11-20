using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.AvanceObra;
using SigWeb.Data.Costos;
using SigWeb.Data.Pie;
using SigWeb.Models;
using static SigWeb.Data.Bot.ClienteTelegram;

namespace SigWeb.Data.Bot;

// /estados: el cuadro "Estado de las torres" de Costos y presupuestos (VistaEstadoTorres, hoja C. CONTROL) en el bot,
// con el % de avance de obra de cada torre (ServicioAvanceObra, caché de 3 min del servicio del bot).
// Mismas fuentes que la vista: estado vigente a hoy = último EstadoTorreManual con Desde ≤ hoy; estados y colores =
// catálogo "Estado de torre" en el orden de ConfiguracionCostos.OrdenEstadosTorre (o los de fábrica); etapas para
// "Ejecución de torres" con EstadoTorres.EtapaDe. Cada estado se pinta con un emoji del color más cercano al del
// catálogo (cuadrado = terminado/estable, círculo = en proceso; sin repetir entre estados).
// Callbacks: "es" resumen · "es:c:{pág}" cuadro completo · "es:t:{tiro}:{pág}" torres de un tiro (0 = sin tiro) ·
// "es:e:{índice de la leyenda}:{pág}" torres en un estado (999 = sin estado).
public sealed partial class ManejadorBot
{
    private const int SinEstadoIdx = 999;

    private sealed record LeyEstado(string Nombre, EstadoTorres.Etapa Etapa, string Emoji);
    private sealed record TorreEst(ElementoTrazado E, string Nombre, string? Estado, int Idx, bool Alerta, DateTime? Desde, decimal? Pct);
    private sealed record TiroEst(int Id, int? Numero, string Nombre, List<TorreEst> Torres, decimal? Km);
    private sealed record CuadroEstados(List<LeyEstado> Leyenda, List<TiroEst> Tiros, List<TorreEst> Torres, decimal? PctProyecto);

    private async Task<CuadroEstados> CargarEstadosAsync()
    {
        var hoy = DateTime.Today;
        var cfg = await _db.ConfiguracionCostos.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync() ?? new ConfiguracionCostos();
        var catalogo = EstadoTorres.Ordenar(await _db.Catalogos.AsNoTracking()
            .Where(c => c.Tipo == EstadoTorres.Catalogo && c.Activo).ToListAsync(), cfg.OrdenEstadosTorre);
        var actual = (await _db.EstadosTorreManual.AsNoTracking().Where(e => e.Desde <= hoy).ToListAsync())
            .GroupBy(e => e.ElementoTrazadoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Desde).ThenByDescending(e => e.Id).First());
        var torres = (await _db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).ToListAsync())
            .OrderBy(t => NumTorre(t.Nombre ?? t.Codigo)).ThenBy(t => t.Nombre ?? t.Codigo, StringComparer.OrdinalIgnoreCase).ToList();
        var tiros = await _db.Tiros.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Orden).ThenBy(t => t.Nombre).ToListAsync();
        var avance = await _bot.AvanceAsync(_db);
        var pctPorTorre = avance?.Torres.ToDictionary(x => x.Id, x => avance.PctTorre(x, hoy)) ?? new();

        // Leyenda: catálogo (o fábrica) + estados guardados que ya no están en él.
        var nombres = (catalogo.Count > 0 ? catalogo.Select(c => c.Nombre) : EstadoTorres.Fabrica.Select(f => f.Nombre)).ToList();
        foreach (var e in actual.Values.Select(e => e.Estado).Where(s => !string.IsNullOrWhiteSpace(s)))
            if (!nombres.Any(n => EstadoTorres.Clave(n) == EstadoTorres.Clave(e))) nombres.Add(e!);
        var usados = new HashSet<string>();
        var leyenda = new List<LeyEstado>();
        foreach (var n in nombres)
        {
            var etapa = EstadoTorres.EtapaDe(n, catalogo);
            var color = catalogo.FirstOrDefault(c => EstadoTorres.Clave(c.Nombre) == EstadoTorres.Clave(n))?.ColorFondo
                        ?? EstadoTorres.Fabrica.FirstOrDefault(f => EstadoTorres.Clave(f.Nombre) == EstadoTorres.Clave(n))?.Fondo;
            var emoji = EmojiDe(color, EstadoTorres.EnProceso(etapa), usados);
            usados.Add(emoji);
            leyenda.Add(new LeyEstado(n, etapa, emoji));
        }

        var lista = torres.Select(t =>
        {
            var m = actual.GetValueOrDefault(t.Id);
            var est = string.IsNullOrWhiteSpace(m?.Estado) ? null : m!.Estado;
            var idx = est is null ? SinEstadoIdx : leyenda.FindIndex(l => EstadoTorres.Clave(l.Nombre) == EstadoTorres.Clave(est));
            var nombre = ImportadorF054.ClaveTorre(string.IsNullOrWhiteSpace(t.Nombre) ? t.Codigo ?? "" : t.Nombre!);
            return new TorreEst(t, nombre, est, idx < 0 ? SinEstadoIdx : idx, m?.Alerta ?? false, est is null ? null : m!.Desde, pctPorTorre.GetValueOrDefault(t.Id));
        }).ToList();

        var km = EstadoTorres.Longitudes(cfg.LongitudTiros);
        var porId = tiros.ToDictionary(t => t.Id);
        var grupos = lista.GroupBy(t => t.E.TiroId is int id && porId.ContainsKey(id) ? id : 0)
            .Select(g =>
            {
                var tiro = g.Key == 0 ? null : porId[g.Key];
                var num = tiro is null ? null : EstadoTorres.NumeroTiro(tiro.Nombre);
                decimal? k = num is int n && km.TryGetValue(n, out var v) ? v : null;
                return (Orden: tiro?.Orden ?? int.MaxValue, T: new TiroEst(g.Key, num, tiro?.Nombre ?? "Sin tiro", g.ToList(), k));
            })
            .OrderBy(x => x.Orden).ThenBy(x => x.T.Numero ?? int.MaxValue).Select(x => x.T).ToList();
        return new CuadroEstados(leyenda, grupos, grupos.SelectMany(g => g.Torres).ToList(), avance?.PctProyecto(hoy));
    }

    private static int NumTorre(string? n)
    {
        var d = new string((n ?? "").SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(d, out var v) ? v : int.MaxValue;
    }

    // Emoji del color más cercano (distancia RGB) que no use otro estado; en proceso prefiere círculos.
    private static readonly (string Cuadro, string Circulo, int R, int G, int B)[] Colores =
    {
        ("🟥", "🔴", 229, 57, 53), ("🟧", "🟠", 251, 140, 0), ("🟨", "🟡", 253, 216, 53), ("🟩", "🟢", 67, 160, 71),
        ("🟦", "🔵", 30, 136, 229), ("🟪", "🟣", 142, 36, 170), ("🟫", "🟤", 109, 76, 65), ("⬛", "⚫", 40, 40, 40), ("⬜", "⚪", 245, 245, 245),
    };

    private static string EmojiDe(string? hex, bool proceso, HashSet<string> usados)
    {
        int r = 200, g = 200, b = 200;
        var h = (hex ?? "").Trim().TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        if (h.Length >= 6 && int.TryParse(h[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            (r, g, b) = ((v >> 16) & 255, (v >> 8) & 255, v & 255);
        var orden = Colores.OrderBy(c => (c.R - r) * (c.R - r) + (c.G - g) * (c.G - g) + (c.B - b) * (c.B - b)).ToList();
        var candidatos = proceso
            ? orden.Select(c => c.Circulo).Concat(orden.Select(c => c.Cuadro))
            : orden.Select(c => c.Cuadro).Concat(orden.Select(c => c.Circulo));
        return candidatos.FirstOrDefault(e => !usados.Contains(e)) ?? "▪️";
    }

    private static string EmojiTorre(CuadroEstados c, TorreEst t) => t.Idx == SinEstadoIdx ? "▫️" : c.Leyenda[t.Idx].Emoji;
    private static string Bonito(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..].ToLowerInvariant().Replace("mod", "MOD");
    private static string PctE(decimal? v) => CumplimientoPie.Formato(v);
    private static string BarraE(decimal? v)
    {
        if (v is null) return "";
        var n = (int)Math.Round(Math.Clamp(v.Value, 0, 100) / 10m);
        return new string('▰', n) + new string('▱', 10 - n);
    }

    private static decimal? Promedio(IEnumerable<TorreEst> l)
    {
        var v = l.Where(t => t.Pct is not null).Select(t => t.Pct!.Value).ToList();
        return v.Count == 0 ? null : Math.Round(v.Average(), 1);
    }

    private List<Boton> PieEstados(string? volver)
    {
        var f = new List<Boton>();
        if (volver is not null) f.Add(new("⬅️ Volver", volver));
        f.Add(new("📊 Resumen", "es"));
        if (_bot.UrlSigWeb is { } url) f.Add(new("🌐 SGC Web", Url: $"{url}/avance-obra?vista=estado"));
        return f;
    }

    // ---------- Resumen ----------
    private async Task<VistaBot> EstadosResumenAsync()
    {
        var c = await CargarEstadosAsync();
        var total = c.Torres.Count;
        if (total == 0) return new VistaBot("No hay torres en el trazado.", new());
        var etapas = c.Torres.Where(t => t.Idx != SinEstadoIdx).Select(t => c.Leyenda[t.Idx].Etapa).ToList();
        int civil = etapas.Count(EstadoTorres.CivilTerminada), montadas = etapas.Count(EstadoTorres.Montada);
        string P(int n) => PctE(total > 0 ? Math.Round(100m * n / total, 0) : null);
        var kmTotal = c.Tiros.Any(t => t.Km is not null) ? c.Tiros.Sum(t => t.Km ?? 0) : (decimal?)null;

        var sb = new StringBuilder("<b>🗼 Estado de las torres</b> · ").Append(DateTime.Today.ToString("dd/MM/yyyy")).Append('\n');
        sb.Append($"<b>{total}</b> torres · {c.Tiros.Count} tiros{(kmTotal is decimal k ? $" · {k:0.##} km" : "")}\n\n");
        if (c.PctProyecto is not null) sb.Append($"🏗 Avance de obra general: <b>{PctE(c.PctProyecto)}</b>\n{BarraE(c.PctProyecto)}\n");
        sb.Append($"🧱 Obra civil terminada: <b>{P(civil)}</b> ({civil})\n");
        sb.Append($"🔩 Montadas: <b>{P(montadas)}</b> ({montadas})\n\n");

        sb.Append("<b>Ejecución de torres</b>\n");
        foreach (var (nom, n) in new[]
                 {
                     ("Obra civil terminada", civil),
                     ("Obra civil en proceso", etapas.Count(e => e == EstadoTorres.Etapa.CivilProceso)),
                     ("Cambio de cimentación", etapas.Count(e => e == EstadoTorres.Etapa.CambioCimentacion)),
                     ("Montaje terminado", montadas),
                     ("Montaje en proceso", etapas.Count(e => e == EstadoTorres.Etapa.MontajeProceso)),
                     ("Restricción", etapas.Count(e => e == EstadoTorres.Etapa.Restriccion)),
                 })
            sb.Append($"• {nom}: <b>{n}</b>\n");

        sb.Append("\n<b>Estados</b>\n");
        var kb = new List<List<Boton>>();
        var botEst = new List<Boton>();
        for (int i = 0; i < c.Leyenda.Count; i++)
        {
            var n = c.Torres.Count(t => t.Idx == i);
            if (n == 0) continue;
            sb.Append($"{c.Leyenda[i].Emoji} {H(Bonito(c.Leyenda[i].Nombre))}: <b>{n}</b>\n");
            botEst.Add(new($"{c.Leyenda[i].Emoji} {n}", $"es:e:{i}"));
        }
        var sin = c.Torres.Count(t => t.Idx == SinEstadoIdx);
        if (sin > 0) { sb.Append($"▫️ Sin estado: <b>{sin}</b>\n"); botEst.Add(new($"▫️ {sin}", $"es:e:{SinEstadoIdx}")); }
        var nAlerta = c.Torres.Count(t => t.Alerta);
        if (nAlerta > 0) sb.Append($"🔺 Con marca roja: <b>{nAlerta}</b>\n");
        sb.Append("\nToca un tiro para ver sus torres o un estado para filtrarlas.");

        kb.Add(new() { new("🗺 Cuadro completo", "es:c") });
        foreach (var fila in c.Tiros.Select(t =>
                 {
                     var m = t.Torres.Count(x => x.Idx != SinEstadoIdx && EstadoTorres.Montada(c.Leyenda[x.Idx].Etapa));
                     return new Boton($"{(t.Numero is int n ? $"Tiro {n}" : Corto(t.Nombre, 10))} · {m}/{t.Torres.Count}", $"es:t:{t.Id}");
                 }).Chunk(3)) kb.Add(fila.ToList());
        foreach (var fila in botEst.Chunk(4)) kb.Add(fila.ToList());
        if (_bot.UrlSigWeb is { } url) kb.Add(new() { new("🌐 Abrir en SGC Web", Url: $"{url}/avance-obra?vista=estado") });
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Cuadro completo: una línea de emojis por tiro (como las columnas del tablero) ----------
    private async Task<VistaBot> EstadosCuadroAsync(int pag)
    {
        var c = await CargarEstadosAsync();
        var bloques = c.Tiros.Select(t =>
        {
            var m = t.Torres.Count(x => x.Idx != SinEstadoIdx && EstadoTorres.Montada(c.Leyenda[x.Idx].Etapa));
            var cab = $"<b>{(t.Numero is int n ? $"Tiro {n}" : H(t.Nombre))}</b> · {H(t.Torres.First().Nombre)}–{H(t.Torres.Last().Nombre)} · {m}/{t.Torres.Count} montadas";
            if (Promedio(t.Torres) is decimal p) cab += $" · avance {PctE(p)}";
            return cab + "\n" + string.Concat(t.Torres.Select(x => EmojiTorre(c, x))) + "\n";
        }).ToList();

        // Páginas de ≤ 3300 caracteres (límite de Telegram 4096 con la leyenda).
        var paginas = new List<List<string>> { new() };
        int largo = 0;
        foreach (var b in bloques)
        {
            if (largo + b.Length > 3300 && paginas[^1].Count > 0) { paginas.Add(new()); largo = 0; }
            paginas[^1].Add(b); largo += b.Length;
        }
        pag = Math.Clamp(pag, 0, paginas.Count - 1);
        var sb = new StringBuilder("<b>🗺 Cuadro de estados</b>");
        if (paginas.Count > 1) sb.Append($" ({pag + 1}/{paginas.Count})");
        sb.Append('\n');
        sb.Append(string.Join(" ", c.Leyenda.Where((l, i) => c.Torres.Any(t => t.Idx == i)).Select(l => $"{l.Emoji} {H(Bonito(l.Nombre))}")));
        if (c.Torres.Any(t => t.Idx == SinEstadoIdx)) sb.Append(" ▫️ Sin estado");
        sb.Append("\n\n").Append(string.Join("\n", paginas[pag]));

        var kb = new List<List<Boton>>();
        if (paginas.Count > 1)
        {
            var f = new List<Boton>();
            if (pag > 0) f.Add(new("◀️", $"es:c:{pag - 1}"));
            if (pag < paginas.Count - 1) f.Add(new("▶️", $"es:c:{pag + 1}"));
            kb.Add(f);
        }
        kb.Add(PieEstados(null));
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Torres de un tiro o de un estado ----------
    private const int TorresPorPagina = 20;

    private async Task<VistaBot> EstadosListaAsync(bool porTiro, int arg, int pag)
    {
        var c = await CargarEstadosAsync();
        List<TorreEst> lista;
        string titulo, prefijo;
        if (porTiro)
        {
            var t = c.Tiros.FirstOrDefault(x => x.Id == arg);
            if (t is null) return new VistaBot("Ese tiro ya no existe.", new() { PieEstados(null) });
            lista = t.Torres;
            var m = lista.Count(x => x.Idx != SinEstadoIdx && EstadoTorres.Montada(c.Leyenda[x.Idx].Etapa));
            titulo = $"<b>🗼 {(t.Numero is int n ? $"Tiro {n}" : H(t.Nombre))}</b>" + (t.Numero is not null && t.Nombre != $"{t.Numero}" ? $" ({H(t.Nombre)})" : "")
                     + $"\n{lista.Count} torres · {m} montadas{(t.Km is decimal k ? $" · {k:0.##} km" : "")}"
                     + (Promedio(lista) is decimal p ? $"\n🏗 Avance promedio: <b>{PctE(p)}</b> {BarraE(p)}" : "")
                     + "\n" + string.Concat(lista.Select(x => EmojiTorre(c, x))) + "\n";
            prefijo = $"es:t:{arg}";
        }
        else
        {
            lista = c.Torres.Where(x => x.Idx == arg).ToList();
            var nom = arg == SinEstadoIdx ? "Sin estado" : arg < c.Leyenda.Count ? Bonito(c.Leyenda[arg].Nombre) : "?";
            var emo = arg == SinEstadoIdx ? "▫️" : arg < c.Leyenda.Count ? c.Leyenda[arg].Emoji : "";
            titulo = $"<b>{emo} {H(nom)}</b>\n{lista.Count} torres\n";
            prefijo = $"es:e:{arg}";
        }

        var paginas = Math.Max(1, (int)Math.Ceiling(lista.Count / (double)TorresPorPagina));
        pag = Math.Clamp(pag, 0, paginas - 1);
        var vista = lista.Skip(pag * TorresPorPagina).Take(TorresPorPagina).ToList();
        var sb = new StringBuilder(titulo);
        if (paginas > 1) sb.Append($"Página {pag + 1}/{paginas}\n");
        sb.Append('\n');
        foreach (var t in vista)
        {
            sb.Append($"{EmojiTorre(c, t)} <b>{H(t.Nombre)}</b>");
            sb.Append(t.Estado is null ? " · sin estado" : $" · {H(Bonito(t.Estado))}");
            if (t.Alerta) sb.Append(" 🔺");
            if (t.Pct is not null) sb.Append($" · {PctE(t.Pct)}");
            if (t.Desde is DateTime d) sb.Append($" · desde {d:dd/MM/yy}");
            sb.Append('\n');
        }
        sb.Append("\nToca una torre para abrir su hoja de vida.");

        var kb = vista.Select(t => new Boton($"{EmojiTorre(c, t)} {Corto(t.Nombre, 10)}", $"t:{t.E.Id}")).Chunk(4).Select(f => f.ToList()).ToList();
        if (paginas > 1)
        {
            var f = new List<Boton>();
            if (pag > 0) f.Add(new("◀️", $"{prefijo}:{pag - 1}"));
            if (pag < paginas - 1) f.Add(new("▶️", $"{prefijo}:{pag + 1}"));
            kb.Add(f);
        }
        kb.Add(PieEstados(null));
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> EstadosBotonAsync(string[] p)
    {
        int N(int i) => p.Length > i && int.TryParse(p[i], out var v) ? v : 0;
        var sub = p.Length > 1 ? p[1] : "";
        return sub switch
        {
            "c" => await EstadosCuadroAsync(N(2)),
            "t" => await EstadosListaAsync(true, N(2), N(3)),
            "e" => await EstadosListaAsync(false, N(2), N(3)),
            _ => await EstadosResumenAsync(),
        };
    }

    private static string Corto(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        return s.Length > max ? s[..(max - 1)] + "…" : s;
    }
}
