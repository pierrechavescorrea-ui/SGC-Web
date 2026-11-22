using SigWeb.Data.Tablas;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Busqueda;
using SigWeb.Data.Dossier.Fuentes;
using SigWeb.Data.Equipos;
using SigWeb.Data.Planos;
using SigWeb.Models;
using SigWeb.Data.Seguridad;
using static SigWeb.Data.Bot.ClienteTelegram;

namespace SigWeb.Data.Bot;

// Atiende UN update de Telegram (mensaje o botón) con su propio scope de servicios (lo crea ServicioBotTelegram).
// Solo chats privados y autorizados (UsuarioBot activo); a los demás solo se les explica cómo vincularse.
// Callbacks: "t:{torre}[:{vista}[:{arg}[:{pág}]]]" hoja de vida (VistasTorreBot) · "f:{tipo}:{id}" enviar archivo ·
// "p:{plano}:{v|a}:{pág}" plano · "d:{doc}:{v|a}:{pág}" documento · "e:{equipo}:{pág}" equipo.
public sealed partial class ManejadorBot
{
    private readonly ClienteTelegram _tg;
    private readonly ServicioBotTelegram _bot;
    private readonly IServiceProvider _sp;
    private readonly AppDbContext _db;
    private readonly string _webRoot;

    public ManejadorBot(ClienteTelegram tg, ServicioBotTelegram bot, IServiceProvider sp, string webRoot)
    {
        _tg = tg; _bot = bot; _sp = sp; _webRoot = webRoot;
        _db = sp.GetRequiredService<AppDbContext>();
    }

    private static string H(string? s) => Html(s);
    private string Arroba => _bot.UsuarioBot is { Length: > 0 } u ? "@" + u : "@bot";

    // ---------- Permisos ----------
    // Chat ligado a un usuario de SGC Web: se aplican sus permisos en el proyecto del chat. Chat vinculado solo con
    // código (como antes del modo multiproyecto): consulta completa, salvo Telegram:PermitirChatsSinUsuario = false.
    private static bool Puede(string modulo, Accion accion = Accion.Ver) =>
        AmbienteSig.Actual is not { UsuarioId: not null } s || s.Permisos.Puede(modulo, accion);

    private static (string Modulo, Accion Accion)? ModuloDeComando(string? cmd) => cmd switch
    {
        "torre" => ("trazado", Accion.Ver),
        "plano" or "planos" => ("planos", Accion.Ver),
        "doc" or "documentos" or "documento" => ("documentos", Accion.Ver),
        "equipo" or "equipos" => ("equipos", Accion.Ver),
        "estados" or "estado" or "avance" => ("costos", Accion.Ver),
        "consecutivo" or "consecutivos" or "misconsecutivos" => ("comunicados", Accion.Crear),
        "buscar" or null => ("inicio", Accion.Ver),
        _ => null,
    };

    // Archivo que se envía al chat → módulo dueño (exige Descargar).
    private static string ModuloDeArchivo(char tipo) => tipo switch
    {
        'c' or 'h' => "trazado", 'p' => "planos", 'i' or 'j' => "ensayos", 'm' => "materiales", 'r' => "matriz-pie",
        'k' => "riesgos", 'a' => "planes-accion", 'v' => "documentos", 'q' or 'x' => "equipos", _ => "inicio",
    };

    private static string? ModuloDeBoton(string prefijo) => prefijo switch
    {
        "t" => "trazado", "p" or "pg" => "planos", "d" or "dc" or "dg" => "documentos", "e" or "eg" => "equipos",
        "es" => "costos", "cs" => "comunicados", _ => null,
    };

    private Task SinPermisoAsync(long chat, string modulo, CancellationToken ct) => _tg.EnviarAsync(chat,
        $"🔒 Su usuario no tiene permiso para <b>{H(CatalogoModulos.De(modulo)?.Nombre ?? modulo)}</b> en el proyecto " +
        $"<b>{H(AmbienteSig.Actual?.Proyecto.Nombre)}</b>. Cambie de proyecto con /proyecto o pida acceso al administrador.", null, ct);

    public async Task ProcesarAsync(JsonObject update, CancellationToken ct)
    {
        if (update["message"] is JsonObject msg) await MensajeAsync(msg, ct);
        else if (update["callback_query"] is JsonObject cb) await BotonAsync(cb, ct);
        else if (update["inline_query"] is JsonObject iq) await EnLineaAsync(iq, ct);
    }

    private async Task<UsuarioBot?> AutorizadoAsync(long chat, CancellationToken ct)
    {
        var u = await _db.UsuariosBot.FirstOrDefaultAsync(x => x.ChatId == chat && x.Activo, ct);
        if (u is not null) { u.UltimoUso = DateTime.Now; await _db.SaveChangesAsync(ct); }
        return u;
    }

    // ---------- Mensajes ----------
    private async Task MensajeAsync(JsonObject msg, CancellationToken ct)
    {
        if (msg["chat"]?["type"]?.GetValue<string>() != "private") return;   // grupos: aún no
        var chat = msg["chat"]!["id"]!.GetValue<long>();
        var texto = (msg["text"]?.GetValue<string>() ?? "").Trim();
        if (texto.Length == 0) return;

        var (cmd, arg) = Partir(texto);
        if (cmd is "vincular" || (cmd is "start" && arg.Length > 0)) { await VincularAsync(chat, arg, msg, ct); return; }
        if (cmd is "proyecto" or "proyectos") { await ProyectosAsync(chat, null, ct); return; }

        var usuario = await AutorizadoAsync(chat, ct);
        if (usuario is null)
        {
            // Código enviado solo (p. ej. tras tocar /vincular en el menú, que lo envía sin número).
            if (cmd is null && RxCodigo().IsMatch(texto)) { await VincularAsync(chat, texto, msg, ct); return; }
            if (cmd is "start" or "ayuda" or "help")
                await _tg.EnviarAsync(chat, "🔒 Este bot es de uso interno de SGC Web.\n\nPide a un administrador un código de vinculación " +
                    "(SGC Web › Administración › Bot de Telegram, o Mi cuenta › Bot de Telegram) y envíalo así:\n<code>/vincular CÓDIGO</code>", null, ct);
            return;
        }

        if (ModuloDeComando(cmd) is { } req && !Puede(req.Modulo, req.Accion)) { await SinPermisoAsync(chat, req.Modulo, ct); return; }

        switch (cmd)
        {
            case "start": case "ayuda": case "help": await AyudaAsync(chat, usuario.Nombre, ct); break;
            case "torre": await TorreAsync(chat, arg, ct); break;
            case "buscar": await BuscarAsync(chat, arg, ct); break;
            case "plano": case "planos":
                if (arg.Length == 0) await EnviarVistaAsync(chat, null, await GruposPlanosAsync(), ct); else await BuscarPlanosAsync(chat, arg, ct);
                break;
            case "doc": case "documentos": case "documento":
                if (arg.Length == 0) await EnviarVistaAsync(chat, null, await TiposDocumentosAsync(), ct); else await BuscarDocumentosAsync(chat, arg, ct);
                break;
            case "equipo": case "equipos":
                if (arg.Length == 0) await EnviarVistaAsync(chat, null, await CategoriasEquiposAsync(), ct); else await BuscarEquiposAsync(chat, arg, ct);
                break;
            case "estados": case "estado": case "avance": await EnviarVistaAsync(chat, null, await EstadosResumenAsync(), ct); break;
            case "consecutivo": case "consecutivos": Terminar(chat); await ConsecutivoInicioAsync(chat, ct); break;
            case "misconsecutivos": await MisConsecutivosAsync(chat, ct); break;
            case "cancelar": Terminar(chat); await _tg.EnviarAsync(chat, "Cancelado.", null, ct); break;
            case null:
                // Conversación en curso (p. ej. /consecutivo): el texto es la respuesta a la pregunta.
                if (await ConsecutivoTextoAsync(chat, usuario, texto, ct)) break;
                // Texto suelto: si es una torre, su hoja de vida; si no, búsqueda general.
                var torres = Puede("trazado") ? await ResolverTorreAsync(texto) : new Resolucion(new(), new());
                if (torres.Exactas.Count == 1) await EnviarVistaAsync(chat, null, await Torres().MostrarAsync(torres.Exactas[0].Id, "", 0, 0), ct);
                else await BuscarAsync(chat, texto, ct);
                break;
            default: await _tg.EnviarAsync(chat, "No conozco ese comando. Envía /ayuda.", null, ct); break;
        }
    }

    private static (string? Cmd, string Arg) Partir(string texto)
    {
        if (!texto.StartsWith('/')) return (null, texto);
        var esp = texto.IndexOf(' ');
        var cmd = (esp < 0 ? texto[1..] : texto[1..esp]).ToLowerInvariant();
        var arroba = cmd.IndexOf('@');
        if (arroba >= 0) cmd = cmd[..arroba];
        return (cmd, esp < 0 ? "" : texto[(esp + 1)..].Trim());
    }

    [GeneratedRegex(@"\b[0-9A-Fa-f]{32}\b")] private static partial Regex RxCodigo();

    internal static string? CodigoParaVincular(JsonObject? msg)
    {
        if (msg?["chat"]?["type"]?.GetValue<string>() != "private") return null;
        var texto = (msg["text"]?.GetValue<string>() ?? "").Trim();
        var (cmd, arg) = Partir(texto);
        if (cmd is "vincular" or "start")
            return RxCodigo().Match(arg) is { Success: true } m ? m.Value.ToUpperInvariant() : null;
        return cmd is null && texto.Length == 32 && RxCodigo().IsMatch(texto) ? texto.ToUpperInvariant() : null;
    }

    internal Task VincularCodigoAsync(JsonObject msg, string codigo, CancellationToken ct) =>
        VincularAsync(msg["chat"]!["id"]!.GetValue<long>(), codigo, msg, ct);

    private async Task VincularAsync(long chat, string texto, JsonObject msg, CancellationToken ct)
    {
        // Se toman los 32 caracteres hexadecimales aunque venga texto alrededor.
        var codigo = RxCodigo().Match(texto) is { Success: true } m ? m.Value.ToUpperInvariant() : "";
        if (codigo.Length == 0)
        {
            await _tg.EnviarAsync(chat, texto.Length == 0 ? "Envía el código así: <code>/vincular CÓDIGO</code>"
                : "Copie el código completo que muestra SGC Web. Envíalo así: <code>/vincular CÓDIGO</code>", null, ct);
            return;
        }
        var canje = _bot.CanjearCodigo(chat, codigo, out var bloqueado);
        if (canje?.ProyectoId is int proyecto && proyecto != AmbienteSig.ProyectoId)
            canje = null;
        var nombre = canje?.Nombre;
        if (canje is null || nombre is null)
        {
            Console.Error.WriteLine($"Bot Telegram: vinculación rechazada (chat {chat}, pendientes {_bot.CodigosPendientes().Count}{(bloqueado ? ", bloqueado" : "")}).");
            await _tg.EnviarAsync(chat, bloqueado ? "Demasiados intentos. Espera una hora y pide un código nuevo."
                : "Código inválido o vencido (duran 15 minutos y sirven una sola vez). Pide uno nuevo en SGC Web.", null, ct);
            return;
        }
        var from = msg["from"];
        var visible = string.Join(" ", new[] { from?["first_name"]?.GetValue<string>(), from?["last_name"]?.GetValue<string>() }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var usuarioTg = from?["username"]?.GetValue<string>() is { Length: > 0 } un ? $"@{un} ({visible})" : visible;
        var u = await _db.UsuariosBot.FirstOrDefaultAsync(x => x.ChatId == chat, ct);
        if (u is null) { u = new UsuarioBot { ChatId = chat }; _db.UsuariosBot.Add(u); }
        usuarioTg = usuarioTg[..Math.Min(usuarioTg.Length, 150)];
        u.Nombre = nombre; u.UsuarioTelegram = usuarioTg; u.Activo = true; u.FechaVinculo = DateTime.Now; u.UltimoUso = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        // Vínculo central: chat → proyecto (y usuario de SGC Web, si el código se pidió para un usuario).
        if (canje.ProyectoId is int pid)
        {
            var central = _sp.GetRequiredService<SeguridadDbContext>();
            var v = await central.VinculosTelegram.FirstOrDefaultAsync(x => x.ChatId == chat, ct);
            if (v is null) { v = new VinculoTelegram { ChatId = chat }; central.VinculosTelegram.Add(v); }
            v.ProyectoId = pid; v.UsuarioId = canje.UsuarioId; v.Nombre = nombre; v.UsuarioTelegram = usuarioTg;
            v.Activo = true; v.Vinculado = DateTime.UtcNow; v.UltimoUso = DateTime.UtcNow;
            await central.SaveChangesAsync(ct);
            _sp.GetRequiredService<AuditoriaAccesos>().Registrar("Telegram vinculado", true, $"chat {chat} → proyecto {pid}", usuarioId: canje.UsuarioId, proyectoId: pid);
        }
        await _tg.EnviarAsync(chat, $"✅ Listo, <b>{H(nombre)}</b>: este chat quedó vinculado con SGC Web.", null, ct);
        await AyudaAsync(chat, nombre, ct);
    }

    private Task AyudaAsync(long chat, string nombre, CancellationToken ct) => _tg.EnviarAsync(chat,
        $"Hola, {H(nombre)} 👋\n\n" +
        "<b>/torre 45</b> — hoja de vida de una torre (también sirve escribir solo <code>45</code>)\n" +
        "<b>/buscar texto</b> — busca en todo SGC Web\n" +
        "<b>/planos</b> — planos por grupo (estructural, civil, eléctrico…); <b>/plano código</b> — uno\n" +
        "<b>/documentos</b> — documentos por tipología; <b>/doc código</b> — uno\n" +
        "<b>/equipo nombre, marca o serie</b> — estado de calibración\n" +
        "<b>/estados</b> — estado de las torres y avance de obra, tiro por tiro\n" +
        "<b>/consecutivo</b> — pedir un consecutivo de comunicado; <b>/misconsecutivos</b> — confirmar envío o anular\n\n" +
        $"🔎 <b>Lista mientras escribes:</b> en la caja de texto escribe <code>{Arroba} 57</code> (torres), <code>{Arroba} p cim</code> (planos), " +
        $"<code>{Arroba} d PR</code> (documentos) o <code>{Arroba} e estación</code> (equipos).\n\n" +
        "En la hoja de vida navega con los botones; 📥 envía el archivo al chat.", null, ct);

    // ---------- Vistas ----------
    private VistasTorreBot Torres() => new(_db, _sp, _bot);

    private async Task EnviarVistaAsync(long chat, long? mensaje, VistaBot v, CancellationToken ct)
    {
        var html = v.Html.Length > 4000 ? v.Html[..3990] + "…" : v.Html;
        if (mensaje is long m) await _tg.EditarAsync(chat, m, html, v.Teclado, ct);
        else await _tg.EnviarAsync(chat, html, v.Teclado, ct);
    }

    // ---------- Botones ----------
    private async Task BotonAsync(JsonObject cb, CancellationToken ct)
    {
        var id = cb["id"]!.GetValue<string>();
        var datos = cb["data"]?.GetValue<string>() ?? "";
        var chat = cb["message"]?["chat"]?["id"]?.GetValue<long>() ?? 0;
        var mensaje = cb["message"]?["message_id"]?.GetValue<long>();
        var usuarioBot = chat == 0 ? null : await AutorizadoAsync(chat, ct);
        if (usuarioBot is null) { await _tg.ResponderBotonAsync(id, "Chat no autorizado.", ct); return; }

        var p = datos.Split(':');
        int N(int i) => p.Length > i && int.TryParse(p[i], out var v) ? v : 0;
        if (p[0] == "px") { await _tg.ResponderBotonAsync(id, null, ct); await ProyectosAsync(chat, N(1), ct); return; }
        if (p[0] == "f" && !Puede(ModuloDeArchivo(p.Length > 1 && p[1].Length == 1 ? p[1][0] : '?'), Accion.Ver | Accion.Descargar))
        { await _tg.ResponderBotonAsync(id, "Sin permiso para descargar este archivo.", ct); return; }
        if (ModuloDeBoton(p[0]) is { } mb && !Puede(mb, p[0] == "cs" ? Accion.Crear : Accion.Ver))
        { await _tg.ResponderBotonAsync(id, "Sin permiso en este módulo.", ct); return; }
        try
        {
            switch (p[0])
            {
                case "cs":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await ConsecutivoBotonAsync(chat, p, usuarioBot, ct);
                    return;
                case "f":
                    await _tg.ResponderBotonAsync(id, "Enviando archivo…", ct);
                    await ArchivoAsync(chat, p.Length > 1 && p[1].Length == 1 ? p[1][0] : '?', N(2), ct);
                    return;
                case "t" when p.Length >= 3 && p[2] == "loc":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    var torreId = N(1);
                    var e = await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(x => x.Id == torreId, ct);
                    if (e is not null) await _tg.UbicacionAsync(chat, e.Latitud, e.Longitud, ct);
                    return;
                case "t":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    var vista = p.Length > 2 ? p[2] : "";
                    // Vistas con argumento (pl, ac, el): t:torre:vista:arg[:pág]; las demás: t:torre:vista[:pág].
                    var conArg = vista is "pl" or "ac" or "el" or "plc" or "hv";
                    var vb = vista == "hc"
                        ? await Torres().MostrarAsync(N(1), vista, N(3), N(5), N(4))
                        : await Torres().MostrarAsync(N(1), vista, conArg ? N(3) : 0, conArg ? N(4) : N(3));
                    await EnviarVistaAsync(chat, mensaje, vb, ct);
                    return;
                case "p":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, await PlanoAsync(N(1), p.Length > 2 && p[2] == "a", N(3)), ct);
                    return;
                case "d":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, await DocumentoAsync(N(1), p.Length > 2 && p[2] == "a", N(3), ct), ct);
                    return;
                case "pg":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, p.Length > 1 ? await PlanosDeGrupoAsync(N(1), N(2)) : await GruposPlanosAsync(), ct);
                    return;
                case "dc":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, p.Length > 1 ? await DocumentosDeCategoriaAsync(N(1), N(2)) : await TiposDocumentosAsync(), ct);
                    return;
                case "eg":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, p.Length > 1 ? await EquiposDeCategoriaAsync(N(1), N(2)) : await CategoriasEquiposAsync(), ct);
                    return;
                case "dg":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, p.Length > 1 ? await DocumentosDeTipoAsync(N(1), N(2)) : await TiposDocumentosAsync(), ct);
                    return;
                case "e":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, await EquipoAsync(N(1), N(2)), ct);
                    return;
                case "es":
                    await _tg.ResponderBotonAsync(id, null, ct);
                    await EnviarVistaAsync(chat, mensaje, await EstadosBotonAsync(p), ct);
                    return;
                default:
                    await _tg.ResponderBotonAsync(id, null, ct);
                    return;
            }
        }
        catch (ErrorTelegram ex) when (ex.Descripcion.Contains("message to edit not found", StringComparison.OrdinalIgnoreCase)) { }
    }

    // ---------- Torres ----------
    private sealed record TorreCand(int Id, string Codigo, string? Nombre);
    private sealed record Resolucion(List<TorreCand> Exactas, List<TorreCand> Parecidas);

    private static string Norm(string? s) => new((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    // Clave de comparación: sin espacios ni guiones y sin prefijos T / TORRE / SN delante del número.
    private static string Clave(string? s)
    {
        var n = Norm(s);
        foreach (var pre in new[] { "TORRE", "SN", "T" })
            if (n.Length > pre.Length && n.StartsWith(pre) && char.IsDigit(n[pre.Length])) return n[pre.Length..];
        return n;
    }

    private async Task<Resolucion> ResolverTorreAsync(string texto)
    {
        var q = Clave(texto);
        if (q.Length == 0) return new(new(), new());
        var todas = await _db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado)
            .Select(t => new TorreCand(t.Id, t.Codigo, t.Nombre)).ToListAsync();
        var exactas = todas.Where(t => Clave(t.Codigo) == q || Clave(t.Nombre) == q).ToList();
        if (exactas.Count == 0)
        {
            var alias = (await _db.AliasTorres.AsNoTracking().ToListAsync()).Where(a => Clave(a.Texto) == q).Select(a => a.ElementoTrazadoId).ToHashSet();
            exactas = todas.Where(t => alias.Contains(t.Id)).ToList();
        }
        if (exactas.Count > 0) return new(exactas, new());
        // "143" → 143N, 143NN…; si no, las que contienen el texto.
        var bas = q.TrimEnd('N');
        var parecidas = todas.Where(t => bas.Length > 0 && Clave(t.Codigo).TrimEnd('N') == bas).ToList();
        if (parecidas.Count == 0) parecidas = todas.Where(t => Clave(t.Codigo).Contains(q) || Norm(t.Nombre).Contains(q)).ToList();
        return new(new(), parecidas.OrderBy(t => t.Codigo.Length).ThenBy(t => t.Codigo).Take(12).ToList());
    }

    private async Task TorreAsync(long chat, string arg, CancellationToken ct)
    {
        var buscarEnLinea = new List<Boton> { new("🔎 Buscar torre mientras escribo", EnLinea: "") };
        if (arg.Length == 0)
        {
            await _tg.EnviarAsync(chat, "Toca el botón y escribe el número: la lista se va filtrando (57 → 57, 57N, 57AN…).\n" +
                "También: <code>/torre 57AN</code>", new List<List<Boton>> { buscarEnLinea }, ct);
            return;
        }
        var r = await ResolverTorreAsync(arg);
        if (r.Exactas.Count == 1) { await EnviarVistaAsync(chat, null, await Torres().MostrarAsync(r.Exactas[0].Id, "", 0, 0), ct); return; }
        var cands = r.Exactas.Count > 1 ? r.Exactas : r.Parecidas;
        if (cands.Count == 0) { await _tg.EnviarAsync(chat, $"No encontré la torre «{H(arg)}».", new List<List<Boton>> { buscarEnLinea }, ct); return; }
        var kb = cands.Select(t => new Boton($"🗼 {t.Codigo}", $"t:{t.Id}")).Chunk(4).Select(f => f.ToList()).ToList();
        kb.Add(new() { new("🔎 Buscar mientras escribo", EnLinea: arg) });
        await _tg.EnviarAsync(chat, $"¿Cuál torre? (para «{H(arg)}»)", kb, ct);
    }

    // ---------- Búsqueda general (el mismo buscador del Ctrl + K) ----------
    [GeneratedRegex(@"^trazado\?torre=(\d+)")] private static partial Regex RxTorre();
    [GeneratedRegex(@"^documentos\?doc=(\d+)")] private static partial Regex RxDoc();
    [GeneratedRegex(@"^equipos/(\d+)/hoja-de-vida")] private static partial Regex RxEquipo();
    [GeneratedRegex(@"^planos\?buscar=([^&]+)")] private static partial Regex RxPlano();

    private async Task BuscarAsync(long chat, string q, CancellationToken ct)
    {
        if (q.Trim().Length < 2) { await _tg.EnviarAsync(chat, "Escribe al menos 2 letras: <code>/buscar cimentación</code>", null, ct); return; }
        await _tg.EscribiendoAsync(chat, "typing", ct);
        var res = (await BuscadorGlobal.BuscarAsync(_db, q, ct)).Where(r => Puede(CatalogoModulos.ClaveDeRuta(r.Url))).ToList();
        if (res.Count == 0) { await _tg.EnviarAsync(chat, $"Sin resultados para «{H(q)}».", null, ct); return; }

        var sb = new StringBuilder($"🔎 <b>{H(q)}</b> — {res.Count} resultado(s)\n\n");
        var botones = new List<Boton>();
        var n = 0;
        foreach (var r in res.Take(15))
        {
            n++;
            string? datos = null;
            if (RxTorre().Match(r.Url) is { Success: true } mt) datos = $"t:{mt.Groups[1].Value}";
            else if (RxDoc().Match(r.Url) is { Success: true } md) datos = $"d:{md.Groups[1].Value}";
            else if (RxEquipo().Match(r.Url) is { Success: true } me) datos = $"e:{me.Groups[1].Value}";
            else if (RxPlano().Match(r.Url) is { Success: true } mp)
            {
                var cod = Uri.UnescapeDataString(mp.Groups[1].Value);
                var pid = await _db.PlanosMaestro.AsNoTracking().Where(x => x.Codigo == cod && !x.Eliminado).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
                if (pid is int pi) datos = $"p:{pi}";
            }
            sb.Append($"{n}. <i>{H(r.Tipo)}</i> · <b>{H(r.Titulo)}</b>" + (string.IsNullOrWhiteSpace(r.Detalle) ? "" : $" — {H(Recortar(r.Detalle, 60))}") + "\n");
            if (datos is not null) botones.Add(new Boton($"{n}. {Recortar(r.Titulo, 16)}", datos));
            else if (_bot.UrlSigWeb is { } url && botones.Count < 12) botones.Add(new Boton($"{n}. 🌐", Url: $"{url}/{r.Url}"));
        }
        if (res.Count > 15) sb.Append($"\n… y {res.Count - 15} más. Afina la búsqueda.");
        await _tg.EnviarAsync(chat, sb.ToString(), botones.Take(12).Chunk(3).Select(f => f.ToList()).ToList(), ct);
    }

    private static string Recortar(string? s, int max)
    {
        s = (s ?? "").Replace('\n', ' ').Trim();
        return s.Length > max ? s[..(max - 1)] + "…" : s;
    }

    private static string Patron(string q) => $"%{q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    private async Task BuscarPlanosAsync(long chat, string q, CancellationToken ct)
    {
        if (q.Length < 2) { await _tg.EnviarAsync(chat, "Escribe el código o parte del título: <code>/plano cimentación</code>", null, ct); return; }
        var p = Patron(q);
        var l = await _db.PlanosMaestro.AsNoTracking().Where(x => !x.Eliminado && (EF.Functions.ILike(x.Codigo, p) || EF.Functions.ILike(x.Titulo, p)))
            .OrderBy(x => x.Codigo).Take(13).ToListAsync(ct);
        if (l.FirstOrDefault(x => string.Equals(x.Codigo.Trim(), q.Trim(), StringComparison.OrdinalIgnoreCase)) is { } exacto) l = new() { exacto };
        if (l.Count == 1) { await EnviarVistaAsync(chat, null, await PlanoAsync(l[0].Id, false, 0), ct); return; }
        if (l.Count == 0) { await _tg.EnviarAsync(chat, $"No encontré planos con «{H(q)}».", null, ct); return; }
        var sb = new StringBuilder($"📐 Planos con «{H(q)}»:\n\n");
        for (int i = 0; i < Math.Min(12, l.Count); i++) sb.Append($"{i + 1}. <b>{H(l[i].Codigo)}</b> {H(Recortar(l[i].Titulo, 60))}\n");
        if (l.Count > 12) sb.Append("… hay más: afina la búsqueda.");
        var kb = l.Take(12).Select((x, i) => new Boton($"{i + 1}. {Recortar(x.Codigo, 14)}", $"p:{x.Id}")).Chunk(3).Select(f => f.ToList()).ToList();
        await _tg.EnviarAsync(chat, sb.ToString(), kb, ct);
    }

    private async Task BuscarDocumentosAsync(long chat, string q, CancellationToken ct)
    {
        if (q.Length < 2) { await _tg.EnviarAsync(chat, "Escribe el código: <code>/doc PR-SGI-01</code>", null, ct); return; }
        var p = Patron(q);
        var l = await _db.DocumentosMaestro.AsNoTracking().Where(x => x.Codigo != null && EF.Functions.ILike(x.Codigo, p))
            .OrderBy(x => x.Codigo).Take(13).ToListAsync(ct);
        if (l.Count == 0) { await BuscarAsync(chat, q, ct); return; }   // el título vive en Campos: lo encuentra el buscador general
        if (l.FirstOrDefault(x => string.Equals(x.Codigo?.Trim(), q.Trim(), StringComparison.OrdinalIgnoreCase)) is { } exacto) l = new() { exacto };
        if (l.Count == 1) { await EnviarVistaAsync(chat, null, await DocumentoAsync(l[0].Id, false, 0, ct), ct); return; }
        var sb = new StringBuilder($"📄 Documentos con «{H(q)}»:\n\n");
        var nombresDoc = await NombresDocumentosAsync(l.Select(x => x.Id).ToList());
        for (int i = 0; i < Math.Min(12, l.Count); i++) sb.Append($"{i + 1}. <b>{H(l[i].Codigo)}</b> {H(Recortar(nombresDoc.GetValueOrDefault(l[i].Id), 50))} · <i>{H(l[i].TipoDocumental)}</i>\n");
        var kb = l.Take(12).Select((x, i) => new Boton($"{i + 1}. {Recortar(x.Codigo, 14)}", $"d:{x.Id}")).Chunk(3).Select(f => f.ToList()).ToList();
        await _tg.EnviarAsync(chat, sb.ToString(), kb, ct);
    }

    private async Task BuscarEquiposAsync(long chat, string q, CancellationToken ct)
    {
        if (q.Length < 2) { await _tg.EnviarAsync(chat, "Escribe el nombre: <code>/equipo estación total</code>", null, ct); return; }
        if (q.StartsWith('#') && int.TryParse(q[1..], out var idEq)) { await EnviarVistaAsync(chat, null, await EquipoAsync(idEq, 0), ct); return; }
        var p = Patron(q);
        // Por nombre o por marca / modelo / serie (Campos del equipo).
        var porCampo = _db.ValoresCampoEquipo.AsNoTracking().Where(v => EF.Functions.ILike(v.Valor, p)).Select(v => v.EquipoId);
        var l = await _db.Equipos.AsNoTracking().Where(x => !x.Eliminado && (EF.Functions.ILike(x.Nombre, p) || porCampo.Contains(x.Id)))
            .OrderBy(x => x.Nombre).Take(13).ToListAsync(ct);
        if (l.Count == 0) { await _tg.EnviarAsync(chat, $"No encontré equipos con «{H(q)}».", null, ct); return; }
        if (l.Count == 1) { await EnviarVistaAsync(chat, null, await EquipoAsync(l[0].Id, 0), ct); return; }
        var datosEq = await DatosEquiposAsync(l.Select(x => x.Id).ToList());
        var sb = new StringBuilder($"🧰 Equipos con «{H(q)}»:\n\n");
        for (int i = 0; i < Math.Min(12, l.Count); i++)
            sb.Append($"{i + 1}. <b>{H(l[i].Nombre)}</b>" + (datosEq.TryGetValue(l[i].Id, out var de) && de.Length > 0 ? $"\n    {H(de)}" : "") + "\n");
        var kb = l.Take(12).Select((x, i) => new Boton($"{i + 1}. {Recortar(x.Nombre, 16)}", $"e:{x.Id}")).Chunk(3).Select(f => f.ToList()).ToList();
        await _tg.EnviarAsync(chat, sb.ToString(), kb, ct);
    }

    // ---------- Fichas: plano, documento, equipo ----------
    private async Task<VistaBot> PlanoAsync(int id, bool anteriores, int pag)
    {
        var pl = await _db.PlanosMaestro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado);
        if (pl is null) return new VistaBot("Ese plano ya no existe.", new());
        var archivos = await _db.ArchivosPlanoMaestro.AsNoTracking().Where(a => a.PlanoMaestroId == id && !a.Eliminado).ToListAsync();
        var act = ArchivosPlanos.Actuales(archivos);
        var ant = ArchivosPlanos.Anteriores(archivos);
        var sb = new StringBuilder($"<b>📐 {H(pl.Codigo)}</b>\n{H(pl.Titulo)}\n");
        var datos = new List<string>();
        if (!string.IsNullOrWhiteSpace(pl.RevisionVigente)) datos.Add($"Rev. vigente {H(pl.RevisionVigente)}");
        if (pl.Fecha is DateTime f) datos.Add(f.ToString("dd/MM/yyyy"));
        if (!string.IsNullOrWhiteSpace(pl.Estado)) datos.Add(H(pl.Estado));
        if (datos.Count > 0) sb.Append(string.Join(" · ", datos) + "\n");
        if ((await ResumenCampos.TextoAsync(_db, "planos", new[] { id })).TryGetValue(id, out var resPl)) sb.Append(H(resPl) + "\n");
        var kb = new List<List<Boton>>();
        var lista = (anteriores ? ant : act).Select(a => new ArchivoBot('p', a.Id, a.NombreOriginal,
            string.Join(" · ", new[] { string.IsNullOrWhiteSpace(a.Version) ? null : "v" + a.Version.Trim(),
                anteriores ? (a.Clase == ClaseArchivoDocumento.Obsoleta ? "OBSOLETO" : "versión anterior") : null }.Where(s => s != null)))).ToList();
        if (lista.Count == 0) sb.Append(anteriores ? "\nNo hay versiones anteriores." : "\n<i>Sin archivo vigente cargado.</i>");
        VistasTorreBot.Archivos(sb, kb, lista, pag, $"p:{id}:{(anteriores ? "a" : "v")}", anteriores ? "Versiones anteriores" : "Vigente");
        var fin = new List<Boton>();
        if (anteriores) fin.Add(new("⬅️ Vigente", $"p:{id}:v"));
        else if (ant.Count > 0) fin.Add(new($"🗂 Anteriores ({ant.Count})", $"p:{id}:a"));
        if (_bot.UrlSigWeb is { } url) fin.Add(new("🌐 SGC Web", Url: $"{url}/planos?buscar={Uri.EscapeDataString(pl.Codigo)}"));
        if (fin.Count > 0) kb.Add(fin);
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> DocumentoAsync(int id, bool anteriores, int pag, CancellationToken ct)
    {
        var d = await _db.DocumentosMaestro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return new VistaBot("Ese documento ya no existe.", new());
        var sb = new StringBuilder($"<b>📄 {H(d.Codigo ?? $"Documento #{d.Id}")}</b>\n");
        var datos = new[] { d.TipoDocumental, d.Estado }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(H).ToList();
        if (datos.Count > 0) sb.Append(string.Join(" · ", datos) + "\n");
        if ((await ResumenCampos.TextoAsync(_db, "documentos", new[] { id })).TryGetValue(id, out var resDoc)) sb.Append(H(resDoc) + "\n");
        var rev = await _db.RevisionesDocumento.AsNoTracking().Where(r => r.DocumentoMaestroId == id)
            .OrderByDescending(r => r.Fecha ?? DateTime.MinValue).ThenByDescending(r => r.Creada).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        if (rev is not null)
            sb.Append($"Versión {H(rev.Version)}" + (rev.Fecha is DateTime f ? $" · {f:dd/MM/yyyy}" : "") + (string.IsNullOrWhiteSpace(rev.Cambio) ? "" : $"\n<i>{H(Recortar(rev.Cambio, 150))}</i>") + "\n");

        // Mismo criterio de "actuales" que Documentos y el Dossier (vigente/editable de la revisión más reciente).
        var marcados = (await FuenteDocumentos.ArchivosDeAsync(_db, new() { id }, ct)).GetValueOrDefault(id) ?? new();
        var versiones = await _db.VersionesDocumento.AsNoTracking().Where(v => v.DocumentoMaestroId == id && !v.Eliminado).ToListAsync(ct);
        var porRuta = versiones.GroupBy(v => v.RutaArchivo).ToDictionary(g => g.Key, g => g.First());
        var lista = marcados.Where(m => m.Actual != anteriores && porRuta.ContainsKey(m.Ruta))
            .Select(m => { var v = porRuta[m.Ruta]; return new ArchivoBot('v', v.Id, v.NombreOriginal,
                string.Join(" · ", new[] { m.Clase, string.IsNullOrWhiteSpace(v.Version) ? null : "v" + v.Version.Trim() }.Where(s => s != null))); })
            .ToList();
        var nAnt = marcados.Count(m => !m.Actual);
        var kb = new List<List<Boton>>();
        if (lista.Count == 0) sb.Append(anteriores ? "\nNo hay versiones anteriores." : "\n<i>Sin archivo vigente cargado.</i>");
        VistasTorreBot.Archivos(sb, kb, lista, pag, $"d:{id}:{(anteriores ? "a" : "v")}", anteriores ? "Versiones anteriores" : "Actual");
        var fin = new List<Boton>();
        if (anteriores) fin.Add(new("⬅️ Actual", $"d:{id}:v"));
        else if (nAnt > 0) fin.Add(new($"🗂 Anteriores ({nAnt})", $"d:{id}:a"));
        if (_bot.UrlSigWeb is { } url) fin.Add(new("🌐 SGC Web", Url: $"{url}/documentos?doc={id}&abrir=todos"));
        if (fin.Count > 0) kb.Add(fin);
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> EquipoAsync(int id, int pag)
    {
        var e = await _db.Equipos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado);
        if (e is null) return new VistaBot("Ese equipo ya no existe.", new());
        var sb = new StringBuilder($"<b>🧰 {H(e.Nombre)}</b>\n");
        var todos = await (from v in _db.ValoresCampoEquipo.AsNoTracking()
                           join c in _db.CamposEquipo.AsNoTracking() on v.CampoEquipoId equals c.Id
                           where v.EquipoId == id && c.Activo && v.Valor != ""
                           orderby c.Orden
                           select new { c.Etiqueta, v.Valor, c.EnResumen }).ToListAsync();
        // Campos marcados como Resumen; sin marcados, los 8 primeros con dato (lo de siempre).
        var hayResumen = await _db.CamposEquipo.AsNoTracking().AnyAsync(c => c.Activo && c.EnResumen);
        var campos = hayResumen ? todos.Where(c => c.EnResumen).ToList() : todos.Take(8).ToList();
        foreach (var c in campos) sb.Append($"{H(c.Etiqueta)}: <b>{H(Recortar(c.Valor, 60))}</b>\n");

        var inter = await _db.IntervencionesEquipo.AsNoTracking().Where(i => i.EquipoId == id).ToListAsync();
        var vig = EstadoEquipo.Vigente(inter);
        var (estado, clase) = EstadoEquipo.Estado(vig);
        var ic = clase.Contains("danger") ? "🔴" : clase.Contains("warning") ? "🟠" : clase.Contains("success") ? "🟢" : "▫️";
        sb.Append($"\n{ic} Calibración: <b>{H(estado)}</b>\n");
        if (vig is not null)
            sb.Append($"Última {vig.Fecha:dd/MM/yyyy}" + (vig.ProximaFecha is DateTime pf ? $" · próxima {pf:dd/MM/yyyy}" : "") +
                      (string.IsNullOrWhiteSpace(vig.NumeroCertificado) ? "" : $" · cert. {H(vig.NumeroCertificado)}") + "\n");

        var archivos = new List<ArchivoBot>();
        if (vig is not null && !string.IsNullOrWhiteSpace(vig.RutaCertificado)) archivos.Add(new ArchivoBot('x', vig.Id, "Certificado de calibración vigente", vig.Fecha.ToString("dd/MM/yyyy")));
        archivos.AddRange((await _db.ArchivosEquipo.AsNoTracking().Where(a => a.EquipoId == id && !a.Eliminado).OrderByDescending(a => a.FechaCarga).ToListAsync())
            .Select(a => new ArchivoBot('q', a.Id, a.NombreOriginal, "ficha técnica")));
        var kb = new List<List<Boton>>();
        VistasTorreBot.Archivos(sb, kb, archivos, pag, $"e:{id}", "Archivos");
        if (_bot.UrlSigWeb is { } url) kb.Add(new() { new("🌐 Hoja de vida en SGC Web", Url: $"{url}/equipos/{id}/hoja-de-vida") });
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Envío de archivos ----------
    private async Task<(string Ruta, string Nombre, string? Nota)?> BuscarArchivoAsync(char tipo, int id)
    {
        switch (tipo)
        {
            case 'h': { var a = await _db.ArchivosCarpetaHojaVida.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); return a is null ? null : (a.Ruta, a.NombreOriginal, a.Descripcion); }
            case 'c': { var a = await _db.ArchivosComponente.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, null); }
            case 'p': { var a = await _db.ArchivosPlanoMaestro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, a.Clase == ClaseArchivoDocumento.Obsoleta ? "⚠️ OBSOLETO" : null); }
            case 'i': { var a = await _db.ArchivosInforme.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, null); }
            case 'm': { var a = await _db.AdjuntosMaterial.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, a.Clase); }
            case 'r': { var a = await _db.ArchivosRegistroParametro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, null); }
            case 'k': { var a = await _db.ArchivosRiesgo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, null); }
            case 'a': { var a = await _db.ArchivosAccionPlan.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, null); }
            case 'v': { var a = await _db.VersionesDocumento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, a.Clase == ClaseArchivoDocumento.Obsoleta ? "⚠️ OBSOLETO" : a.Clase == ClaseArchivoDocumento.Editable ? "Editable" : null); }
            case 'j': { var a = await _db.AdjuntosEnsayo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id); return a is null ? null : (a.Ruta, a.NombreOriginal, null); }
            case 'q': { var a = await _db.ArchivosEquipo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado); return a is null ? null : (a.RutaArchivo, a.NombreOriginal, null); }
            case 'x':
            {
                var a = await _db.IntervencionesEquipo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (a is null || string.IsNullOrWhiteSpace(a.RutaCertificado)) return null;
                var nombre = Path.GetFileName(a.RutaCertificado);
                return (a.RutaCertificado, string.IsNullOrWhiteSpace(a.NumeroCertificado) ? nombre : $"Certificado {a.NumeroCertificado}{Path.GetExtension(nombre)}", null);
            }
            default: return null;
        }
    }

    private async Task ArchivoAsync(long chat, char tipo, int id, CancellationToken ct)
    {
        var a = await BuscarArchivoAsync(tipo, id);
        if (a is null) { await _tg.EnviarAsync(chat, "Ese archivo ya no existe (¿se envió a la Papelera?).", null, ct); return; }
        var (ruta, nombre, nota) = a.Value;

        var fisica = ArchivosSeguros.RutaFisica(_webRoot, ruta);
        if (fisica is null || !File.Exists(fisica))
        {
            await _tg.EnviarAsync(chat, $"No encuentro «{H(nombre)}» en el servidor.", null, ct);
            return;
        }
        var tam = new FileInfo(fisica).Length;
        if (tam > MaxArchivo)
        {
            var enlace = _bot.UrlSigWeb is { } url ? $"\n<a href=\"{H(url + "/" + ruta.TrimStart('/'))}\">Descargar desde SGC Web</a>" : "\nÁbrelo desde SGC Web.";
            await _tg.EnviarAsync(chat, $"«{H(nombre)}» pesa {tam / 1048576.0:0.#} MB y Telegram solo permite 50 MB a los bots.{enlace}", null, ct);
            return;
        }
        if (Path.GetExtension(nombre).Length == 0) nombre += Path.GetExtension(fisica);
        await _tg.EscribiendoAsync(chat, "upload_document", ct);
        await _tg.DocumentoAsync(chat, fisica, nombre, nota is null ? null : $"<b>{H(nota)}</b>", ct);
    }
}
