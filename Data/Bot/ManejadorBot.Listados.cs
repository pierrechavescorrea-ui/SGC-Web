using SigWeb.Data.Tablas;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;
using static SigWeb.Data.Bot.ClienteTelegram;

namespace SigWeb.Data.Bot;

// Listados navegables del bot y modo en línea:
// - Planos por grupo (GrupoPlano: Estructural, Civil, Eléctrico…) → "pg:{grupo}:{pág}" (grupo 0 = sin grupo).
// - Documentos por tipología (DocumentoMaestro.TipoDocumental) → "dg:{índice}:{pág}" (índice en el orden alfabético
//   de las tipologías: el texto puede pasar de los 64 bytes del callback).
// - Modo en línea (@bot texto, lista que se filtra al escribir): torres; "p texto" planos, "d texto" documentos,
//   "e texto" equipos. Requiere /setinline en BotFather. Solo para usuarios vinculados.
public sealed partial class ManejadorBot
{
    private const int PorLista = 10;

    // ---------- Equipos: marca, modelo y serie (Campos del equipo) ----------
    private async Task<Dictionary<int, string>> DatosEquiposAsync(List<int> ids)
    {
        // Con campos marcados como Resumen, la línea de cada equipo muestra esos datos (en su orden).
        if (await _db.CamposEquipo.AsNoTracking().AnyAsync(c => c.Activo && c.EnResumen))
        {
            var res = await (from v in _db.ValoresCampoEquipo.AsNoTracking()
                             join c in _db.CamposEquipo.AsNoTracking() on v.CampoEquipoId equals c.Id
                             where ids.Contains(v.EquipoId) && v.Valor != "" && c.Activo && c.EnResumen
                             orderby c.Orden
                             select new { v.EquipoId, v.Valor }).ToListAsync();
            return res.GroupBy(f => f.EquipoId).ToDictionary(g => g.Key, g => string.Join(" · ", g.Select(f => ResumenCampos.Valor(f.Valor ?? ""))));
        }
        var filas = await (from v in _db.ValoresCampoEquipo.AsNoTracking()
                           join c in _db.CamposEquipo.AsNoTracking() on v.CampoEquipoId equals c.Id
                           where ids.Contains(v.EquipoId) && v.Valor != "" && (c.Clave == "Marca" || c.Clave == "Modelo" || c.Clave == "NoSerie")
                           select new { v.EquipoId, c.Clave, v.Valor }).ToListAsync();
        return filas.GroupBy(f => f.EquipoId).ToDictionary(g => g.Key, g =>
        {
            string? V(string k) => g.FirstOrDefault(f => f.Clave == k)?.Valor?.Trim();
            var partes = new[] { V("Marca"), V("Modelo"), V("NoSerie") is { Length: > 0 } s ? $"S/N {s}" : null }.Where(x => !string.IsNullOrWhiteSpace(x));
            return string.Join(" · ", partes);
        });
    }

    // ---------- Planos por grupo ----------
    private async Task<VistaBot> GruposPlanosAsync()
    {
        var grupos = await _db.GruposPlano.AsNoTracking().OrderBy(g => g.Nombre).ToListAsync();
        var conteo = await _db.PlanosMaestro.AsNoTracking().Where(p => !p.Eliminado)
            .GroupBy(p => p.GrupoPlanoId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        int N(int? id) => conteo.FirstOrDefault(c => c.Key == id)?.N ?? 0;
        var sb = new StringBuilder("<b>📐 Planos por grupo</b>\n\n");
        var botones = new List<Boton>();
        foreach (var g in grupos.Where(g => N(g.Id) > 0))
        {
            sb.Append($"• {H(g.Nombre)}: <b>{N(g.Id)}</b>\n");
            botones.Add(new Boton($"{Recortar(g.Nombre, 18)} ({N(g.Id)})", $"pg:{g.Id}:0"));
        }
        if (N(null) > 0) { sb.Append($"• Sin grupo: <b>{N(null)}</b>\n"); botones.Add(new Boton($"Sin grupo ({N(null)})", "pg:0:0")); }
        if (botones.Count == 0) sb.Append("<i>No hay planos cargados.</i>");
        sb.Append("\nTambién: <code>/plano código</code> o escribe <code>@bot p texto</code>.");
        var kb = botones.Chunk(2).Select(f => f.ToList()).ToList();
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> PlanosDeGrupoAsync(int grupoId, int pag)
    {
        var nombre = grupoId == 0 ? "Sin grupo" : (await _db.GruposPlano.AsNoTracking().Where(g => g.Id == grupoId).Select(g => g.Nombre).FirstOrDefaultAsync()) ?? "Grupo";
        var q = _db.PlanosMaestro.AsNoTracking().Where(p => !p.Eliminado && (grupoId == 0 ? p.GrupoPlanoId == null : p.GrupoPlanoId == grupoId));
        var total = await q.CountAsync();
        var paginas = Math.Max(1, (total + PorLista - 1) / PorLista);
        pag = Math.Clamp(pag, 0, paginas - 1);
        var lista = await q.OrderBy(p => p.Codigo).Skip(pag * PorLista).Take(PorLista).ToListAsync();
        var sb = new StringBuilder($"<b>📐 {H(nombre)}</b> — {total} plano(s)" + (paginas > 1 ? $" · pág. {pag + 1}/{paginas}" : "") + "\n\n");
        for (int i = 0; i < lista.Count; i++)
            sb.Append($"{pag * PorLista + i + 1}. <b>{H(lista[i].Codigo)}</b> {H(Recortar(lista[i].Titulo, 55))}" +
                      (string.IsNullOrWhiteSpace(lista[i].RevisionVigente) ? "" : $" · <i>Rev. {H(lista[i].RevisionVigente)}</i>") + "\n");
        var kb = lista.Select((p, i) => new Boton($"{pag * PorLista + i + 1}. {Recortar(p.Codigo, 14)}", $"p:{p.Id}")).Chunk(2).Select(f => f.ToList()).ToList();
        kb.Add(Paginacion($"pg:{grupoId}", pag, paginas, "pg"));
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Documentos por tipología ----------
    private async Task<List<(string Tipo, int N)>> TiposDocumentalesAsync() =>
        (await _db.DocumentosMaestro.AsNoTracking().GroupBy(d => d.TipoDocumental ?? "").Select(g => new { g.Key, N = g.Count() }).ToListAsync())
            .Select(x => (x.Key, x.N)).OrderBy(x => x.Key == "" ? "~" : x.Key, StringComparer.OrdinalIgnoreCase).ToList();

    private async Task<VistaBot> TiposDocumentosAsync()
    {
        if (await _db.GruposDocumento.AnyAsync(g => g.Activo)) return await CategoriasDocumentosAsync();
        var tipos = await TiposDocumentalesAsync();
        var sb = new StringBuilder("<b>📄 Documentos por tipología</b>\n\n");
        var botones = new List<Boton>();
        for (int i = 0; i < tipos.Count; i++)
        {
            var nombre = tipos[i].Tipo == "" ? "Sin tipología" : tipos[i].Tipo;
            sb.Append($"• {H(nombre)}: <b>{tipos[i].N}</b>\n");
            botones.Add(new Boton($"{Recortar(nombre, 20)} ({tipos[i].N})", $"dg:{i}:0"));
        }
        if (tipos.Count == 0) sb.Append("<i>No hay documentos cargados.</i>");
        sb.Append("\nTambién: <code>/doc código</code> o escribe <code>@bot d texto</code>.");
        return new VistaBot(sb.ToString(), botones.Chunk(2).Select(f => f.ToList()).ToList());
    }

    // Nombre de cada documento: el Campo de la tabla "documentos" llamado Nombre/Título/Descripción.
    private async Task<Dictionary<int, string>> NombresDocumentosAsync(List<int> ids)
    {
        var campo = await _db.CamposTabla.AsNoTracking()
            .Where(c => c.Tabla == "documentos" && !c.Eliminado && (c.Nombre == "Nombre" || c.Nombre == "Título" || c.Nombre == "Titulo" || c.Nombre == "Descripción"))
            .OrderBy(c => c.Nombre == "Nombre" ? 0 : c.Nombre.StartsWith("T") ? 1 : 2).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        if (campo is null) return new();
        return await _db.ValoresCampoTabla.AsNoTracking().Where(v => v.CampoTablaId == campo && ids.Contains(v.FilaId) && v.Valor != null && v.Valor != "")
            .ToDictionaryAsync(v => v.FilaId, v => v.Valor!);
    }

    private async Task<VistaBot> DocumentosDeTipoAsync(int indice, int pag)
    {
        var tipos = await TiposDocumentalesAsync();
        if (indice < 0 || indice >= tipos.Count) return await TiposDocumentosAsync();
        var tipo = tipos[indice].Tipo;
        var q = _db.DocumentosMaestro.AsNoTracking().Where(d => tipo == "" ? (d.TipoDocumental == null || d.TipoDocumental == "") : d.TipoDocumental == tipo);
        var total = await q.CountAsync();
        var paginas = Math.Max(1, (total + PorLista - 1) / PorLista);
        pag = Math.Clamp(pag, 0, paginas - 1);
        var lista = await q.OrderBy(d => d.Codigo).Skip(pag * PorLista).Take(PorLista).ToListAsync();
        var nombres = await NombresDocumentosAsync(lista.Select(d => d.Id).ToList());
        var sb = new StringBuilder($"<b>📄 {H(tipo == "" ? "Sin tipología" : tipo)}</b> — {total} documento(s)" + (paginas > 1 ? $" · pág. {pag + 1}/{paginas}" : "") + "\n\n");
        for (int i = 0; i < lista.Count; i++)
            sb.Append($"{pag * PorLista + i + 1}. <b>{H(lista[i].Codigo ?? $"#{lista[i].Id}")}</b> {H(Recortar(nombres.GetValueOrDefault(lista[i].Id), 55))}" +
                      (string.IsNullOrWhiteSpace(lista[i].Estado) ? "" : $" · <i>{H(lista[i].Estado)}</i>") + "\n");
        var kb = lista.Select((d, i) => new Boton($"{pag * PorLista + i + 1}. {Recortar(d.Codigo ?? $"#{d.Id}", 14)}", $"d:{d.Id}")).Chunk(2).Select(f => f.ToList()).ToList();
        kb.Add(Paginacion($"dg:{indice}", pag, paginas, "dg"));
        return new VistaBot(sb.ToString(), kb);
    }

    private static List<Boton> Paginacion(string baseDatos, int pag, int paginas, string volver)
    {
        var f = new List<Boton>();
        if (pag > 0) f.Add(new("◀️", $"{baseDatos}:{pag - 1}"));
        f.Add(new("⬆️ Grupos", volver));
        if (pag < paginas - 1) f.Add(new("▶️", $"{baseDatos}:{pag + 1}"));
        return f;
    }

    // ---------- Modo en línea ----------
    private async Task EnLineaAsync(JsonObject q, CancellationToken ct)
    {
        var id = q["id"]!.GetValue<string>();
        var desde = q["from"]?["id"]?.GetValue<long>() ?? 0;
        // En un chat privado, el id del chat es el del usuario.
        if (desde == 0 || !Puede("telegram") || !await _db.UsuariosBot.AnyAsync(u => u.ChatId == desde && u.Activo, ct)) { await _tg.ResponderEnLineaAsync(id, Array.Empty<ResultadoEnLinea>(), ct); return; }
        var texto = (q["query"]?.GetValue<string>() ?? "").Trim();
        var res = new List<ResultadoEnLinea>();

        string? Resto(params string[] prefijos)
        {
            foreach (var p in prefijos)
                if (texto.StartsWith(p + " ", StringComparison.OrdinalIgnoreCase) || texto.Equals(p, StringComparison.OrdinalIgnoreCase))
                    return texto.Length > p.Length ? texto[(p.Length + 1)..].Trim() : "";
            return null;
        }

        if (Resto("p", "plano", "planos") is { } tp)
        {
            var pat = Patron(tp);
            var l = await _db.PlanosMaestro.AsNoTracking().Where(x => !x.Eliminado && (tp == "" || EF.Functions.ILike(x.Codigo, pat) || EF.Functions.ILike(x.Titulo, pat)))
                .OrderBy(x => x.Codigo).Take(50).ToListAsync(ct);
            res.AddRange(l.Select(x => new ResultadoEnLinea($"p{x.Id}", $"📐 {x.Codigo}", x.Titulo, $"/plano {x.Codigo}")));
        }
        else if (Resto("d", "doc", "documento") is { } td)
        {
            var pat = Patron(td);
            var l = await _db.DocumentosMaestro.AsNoTracking().Where(x => x.Codigo != null && (td == "" || EF.Functions.ILike(x.Codigo, pat)))
                .OrderBy(x => x.Codigo).Take(50).ToListAsync(ct);
            var nombres = await NombresDocumentosAsync(l.Select(x => x.Id).ToList());
            res.AddRange(l.Select(x => new ResultadoEnLinea($"d{x.Id}", $"📄 {x.Codigo}",
                string.Join(" · ", new[] { nombres.GetValueOrDefault(x.Id), x.TipoDocumental }.Where(s => !string.IsNullOrWhiteSpace(s))), $"/doc {x.Codigo}")));
        }
        else if (Resto("e", "equipo", "equipos") is { } te)
        {
            var pat = Patron(te);
            var l = await _db.Equipos.AsNoTracking().Where(x => !x.Eliminado && (te == "" || EF.Functions.ILike(x.Nombre, pat)))
                .OrderBy(x => x.Nombre).Take(50).ToListAsync(ct);
            var datos = await DatosEquiposAsync(l.Select(x => x.Id).ToList());
            res.AddRange(l.Select(x => new ResultadoEnLinea($"e{x.Id}", $"🧰 {x.Nombre}", datos.GetValueOrDefault(x.Id), $"/equipo #{x.Id}")));
        }
        else
        {
            // Torres: las que empiezan por lo escrito primero (57 → 57, 57N, 57AN…), luego las que lo contienen.
            var clave = Clave(texto);
            var todas = await _db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).Select(t => new { t.Id, t.Codigo, t.Nombre, t.TiroId }).ToListAsync(ct);
            var tiros = await _db.Tiros.AsNoTracking().Where(t => !t.Eliminado).ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);
            var l = todas.Select(t => new { t, k = Clave(t.Codigo) })
                .Where(x => clave.Length == 0 || x.k.StartsWith(clave) || x.k.Contains(clave))
                .OrderBy(x => x.k == clave ? 0 : x.k.StartsWith(clave) ? 1 : 2)
                .ThenBy(x => NumeroInicial(x.t.Codigo)).ThenBy(x => x.t.Codigo.Length).ThenBy(x => x.t.Codigo)
                .Take(50).ToList();
            res.AddRange(l.Select(x => new ResultadoEnLinea($"t{x.t.Id}", $"🗼 {x.t.Codigo}",
                string.Join(" · ", new[] { !string.IsNullOrWhiteSpace(x.t.Nombre) && x.t.Nombre != x.t.Codigo ? x.t.Nombre : null,
                    x.t.TiroId is int ti && tiros.TryGetValue(ti, out var tn) ? $"Tiro {tn}" : null }.Where(s => s != null)),
                $"/torre {x.t.Codigo}")));
        }
        await _tg.ResponderEnLineaAsync(id, res, ct);
    }

    private static int NumeroInicial(string s)
    {
        var d = new string(s.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(d, out var n) ? n : int.MaxValue;
    }

    private async Task<VistaBot> CategoriasDocumentosAsync()
    {
        var grupos = await _db.GruposDocumento.AsNoTracking().Where(g => g.Activo).OrderBy(g => g.Orden).ThenBy(g => g.Nombre).ToListAsync();
        var conteos = await _db.DocumentosMaestro.AsNoTracking().Where(e => !e.Eliminado).GroupBy(e => e.GrupoDocumentoId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var botones = grupos.Select(g => new Boton($"{Recortar(g.Nombre, 22)} ({conteos.FirstOrDefault(c => c.Key == g.Id)?.N ?? 0})", $"dc:{g.Id}:0")).ToList();
        var sin = conteos.FirstOrDefault(c => c.Key == null)?.N ?? 0;
        if (sin > 0) botones.Add(new Boton($"Sin categoría ({sin})", "dc:0:0"));
        return new VistaBot("<b>Documentos por categoría</b>\n\nSelecciona una categoría.", botones.Chunk(2).Select(f => f.ToList()).ToList());
    }

    private async Task<VistaBot> DocumentosDeCategoriaAsync(int grupoId, int pag)
    {
        var nombre = grupoId < 0 ? "Todos" : grupoId == 0 ? "Sin categoría" : await _db.GruposDocumento.Where(g => g.Id == grupoId).Select(g => g.Nombre).FirstOrDefaultAsync() ?? "Categoría";
        var q = _db.DocumentosMaestro.AsNoTracking().Where(e => !e.Eliminado && (grupoId < 0 || (grupoId == 0 ? e.GrupoDocumentoId == null : e.GrupoDocumentoId == grupoId)));
        var total = await q.CountAsync();
        var paginas = Math.Max(1, (total + PorLista - 1) / PorLista);
        pag = Math.Clamp(pag, 0, paginas - 1);
        var lista = await q.OrderBy(e => e.Codigo).Skip(pag * PorLista).Take(PorLista).ToListAsync();
        var sb = new StringBuilder($"<b>{H(nombre)}</b> - {total} registro(s), pág. {pag + 1}/{paginas}\n\n");
        var nombres = await NombresDocumentosAsync(lista.Select(e => e.Id).ToList());
        foreach (var e in lista) sb.Append($"<b>{H(e.Codigo ?? $"#{e.Id}")}</b> {H(Recortar(nombres.GetValueOrDefault(e.Id), 55))}\n");
        var kb = lista.Select(e => new Boton(Recortar(e.Codigo ?? $"#{e.Id}", 25), $"d:{e.Id}")).Chunk(2).Select(f => f.ToList()).ToList();
        kb.Add(Paginacion($"dc:{grupoId}", pag, paginas, "dc"));
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> CategoriasEquiposAsync()
    {
        var grupos = await _db.GruposEquipo.AsNoTracking().Where(g => g.Activo).OrderBy(g => g.Orden).ThenBy(g => g.Nombre).ToListAsync();
        if (grupos.Count == 0) return await EquiposDeCategoriaAsync(-1, 0);
        var conteos = await _db.Equipos.AsNoTracking().Where(e => !e.Eliminado).GroupBy(e => e.GrupoEquipoId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var botones = grupos.Select(g => new Boton($"{Recortar(g.Nombre, 22)} ({conteos.FirstOrDefault(c => c.Key == g.Id)?.N ?? 0})", $"eg:{g.Id}:0")).ToList();
        var sin = conteos.FirstOrDefault(c => c.Key == null)?.N ?? 0;
        if (sin > 0) botones.Add(new Boton($"Sin categoría ({sin})", "eg:0:0"));
        return new VistaBot("<b>Equipos por categoría</b>\n\nSelecciona una categoría.", botones.Chunk(2).Select(f => f.ToList()).ToList());
    }

    private async Task<VistaBot> EquiposDeCategoriaAsync(int grupoId, int pag)
    {
        var nombre = grupoId < 0 ? "Todos" : grupoId == 0 ? "Sin categoría" : await _db.GruposEquipo.Where(g => g.Id == grupoId).Select(g => g.Nombre).FirstOrDefaultAsync() ?? "Categoría";
        var q = _db.Equipos.AsNoTracking().Where(e => !e.Eliminado && (grupoId < 0 || (grupoId == 0 ? e.GrupoEquipoId == null : e.GrupoEquipoId == grupoId)));
        var total = await q.CountAsync();
        var paginas = Math.Max(1, (total + PorLista - 1) / PorLista);
        pag = Math.Clamp(pag, 0, paginas - 1);
        var lista = await q.OrderBy(e => e.Nombre).Skip(pag * PorLista).Take(PorLista).ToListAsync();
        var sb = new StringBuilder($"<b>{H(nombre)}</b> - {total} registro(s), pág. {pag + 1}/{paginas}\n\n");
        var datos = await DatosEquiposAsync(lista.Select(e => e.Id).ToList());
        foreach (var e in lista) sb.Append($"<b>{H(e.Nombre)}</b> {H(datos.GetValueOrDefault(e.Id))}\n");
        var kb = lista.Select(e => new Boton(Recortar(e.Nombre ?? $"#{e.Id}", 25), $"e:{e.Id}")).Chunk(2).Select(f => f.ToList()).ToList();
        kb.Add(Paginacion($"eg:{grupoId}", pag, paginas, "eg"));
        return new VistaBot(sb.ToString(), kb);
    }

}
