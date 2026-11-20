using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SigWeb.Data.Bot;

// Llamadas mínimas a la API de bots de Telegram (https://core.telegram.org/bots/api), con HttpClient
// directo (sin paquete). Todos los mensajes van en HTML (parse_mode=HTML): escapar con Html().
public sealed class ClienteTelegram
{
    public const long MaxArchivo = 50L * 1024 * 1024;   // límite de sendDocument para bots

    private readonly HttpClient _http;
    private readonly string _base;

    public ClienteTelegram(HttpClient http, string token)
    {
        _http = http;
        _base = $"https://api.telegram.org/bot{token}/";
    }

    public static string Html(string? t) => (t ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ---------- Teclado en línea ----------
    // EnLinea: abre "@bot <texto>" en la caja de escribir del mismo chat (modo en línea: lista que se filtra al escribir).
    public sealed record Boton(string Texto, string? Datos = null, string? Url = null, string? EnLinea = null);

    private static JsonArray Teclado(IEnumerable<IEnumerable<Boton>>? filas)
    {
        var arr = new JsonArray();
        if (filas is null) return arr;
        foreach (var f in filas)
        {
            var fila = new JsonArray();
            foreach (var b in f)
            {
                var o = new JsonObject { ["text"] = b.Texto };
                if (b.Url is not null) o["url"] = b.Url;
                else if (b.EnLinea is not null) o["switch_inline_query_current_chat"] = b.EnLinea;
                else o["callback_data"] = b.Datos ?? "-";
                fila.Add(o);
            }
            if (fila.Count > 0) arr.Add(fila);
        }
        return arr;
    }

    // ---------- Llamadas ----------
    private async Task<JsonNode?> Llamar(string metodo, JsonObject cuerpo, CancellationToken ct)
    {
        using var contenido = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");
        using var r = await _http.PostAsync(_base + metodo, contenido, ct);
        var texto = await r.Content.ReadAsStringAsync(ct);
        var nodo = JsonNode.Parse(texto);
        if (nodo?["ok"]?.GetValue<bool>() != true)
            throw new ErrorTelegram(metodo, (int)r.StatusCode, nodo?["description"]?.GetValue<string>() ?? texto);
        return nodo["result"];
    }

    public async Task<(long Id, string Usuario)> GetMeAsync(CancellationToken ct)
    {
        var r = await Llamar("getMe", new JsonObject(), ct);
        return (r!["id"]!.GetValue<long>(), r["username"]?.GetValue<string>() ?? "");
    }

    public Task DeleteWebhookAsync(CancellationToken ct) => Llamar("deleteWebhook", new JsonObject(), ct);

    public async Task<JsonArray> GetUpdatesAsync(long offset, int timeoutSeg, CancellationToken ct)
    {
        var r = await Llamar("getUpdates", new JsonObject
        {
            ["offset"] = offset,
            ["timeout"] = timeoutSeg,
            ["allowed_updates"] = new JsonArray("message", "callback_query", "inline_query")
        }, ct);
        return r as JsonArray ?? new JsonArray();
    }

    public Task SetMyCommandsAsync(IEnumerable<(string Comando, string Descripcion)> comandos, CancellationToken ct)
    {
        var arr = new JsonArray();
        foreach (var (c, d) in comandos) arr.Add(new JsonObject { ["command"] = c, ["description"] = d });
        return Llamar("setMyCommands", new JsonObject { ["commands"] = arr }, ct);
    }

    public Task EnviarAsync(long chat, string html, List<List<Boton>>? teclado, CancellationToken ct)
    {
        var o = new JsonObject { ["chat_id"] = chat, ["text"] = html, ["parse_mode"] = "HTML", ["disable_web_page_preview"] = true };
        if (teclado is not null) o["reply_markup"] = new JsonObject { ["inline_keyboard"] = Teclado(teclado) };
        return Llamar("sendMessage", o, ct);
    }

    // Reescribe un mensaje del bot (navegar sin llenar el chat). "message is not modified" no es error.
    public async Task EditarAsync(long chat, long mensaje, string html, List<List<Boton>>? teclado, CancellationToken ct)
    {
        var o = new JsonObject { ["chat_id"] = chat, ["message_id"] = mensaje, ["text"] = html, ["parse_mode"] = "HTML", ["disable_web_page_preview"] = true };
        if (teclado is not null) o["reply_markup"] = new JsonObject { ["inline_keyboard"] = Teclado(teclado) };
        try { await Llamar("editMessageText", o, ct); }
        catch (ErrorTelegram e) when (e.Descripcion.Contains("not modified", StringComparison.OrdinalIgnoreCase)) { }
    }

    // Resultados del modo en línea (máx. 50). Cada uno: título, descripción y el texto que se envía al elegirlo.
    public sealed record ResultadoEnLinea(string Id, string Titulo, string? Descripcion, string Texto);

    public async Task ResponderEnLineaAsync(string consultaId, IEnumerable<ResultadoEnLinea> resultados, CancellationToken ct)
    {
        var arr = new JsonArray();
        foreach (var r in resultados.Take(50))
        {
            var o = new JsonObject
            {
                ["type"] = "article", ["id"] = r.Id, ["title"] = r.Titulo,
                ["input_message_content"] = new JsonObject { ["message_text"] = r.Texto }
            };
            if (!string.IsNullOrEmpty(r.Descripcion)) o["description"] = r.Descripcion;
            arr.Add(o);
        }
        try { await Llamar("answerInlineQuery", new JsonObject { ["inline_query_id"] = consultaId, ["results"] = arr, ["cache_time"] = 0, ["is_personal"] = true }, ct); }
        catch (ErrorTelegram) { /* consulta vieja (el usuario siguió escribiendo): no importa */ }
    }

    public async Task ResponderBotonAsync(string callbackId, string? aviso, CancellationToken ct)
    {
        var o = new JsonObject { ["callback_query_id"] = callbackId };
        if (!string.IsNullOrEmpty(aviso)) o["text"] = aviso;
        try { await Llamar("answerCallbackQuery", o, ct); } catch (ErrorTelegram) { /* botón viejo: no importa */ }
    }

    public Task EscribiendoAsync(long chat, string accion, CancellationToken ct) =>
        Llamar("sendChatAction", new JsonObject { ["chat_id"] = chat, ["action"] = accion }, ct);

    public Task UbicacionAsync(long chat, double lat, double lon, CancellationToken ct) =>
        Llamar("sendLocation", new JsonObject { ["chat_id"] = chat, ["latitude"] = lat, ["longitude"] = lon }, ct);

    public async Task DocumentoAsync(long chat, string rutaFisica, string nombre, string? leyendaHtml, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chat.ToString()), "chat_id");
        if (!string.IsNullOrEmpty(leyendaHtml))
        {
            form.Add(new StringContent(leyendaHtml), "caption");
            form.Add(new StringContent("HTML"), "parse_mode");
        }
        await using var fs = File.OpenRead(rutaFisica);
        var archivo = new StreamContent(fs);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(archivo, "document", nombre);
        using var r = await _http.PostAsync(_base + "sendDocument", form, ct);
        var texto = await r.Content.ReadAsStringAsync(ct);
        var nodo = JsonNode.Parse(texto);
        if (nodo?["ok"]?.GetValue<bool>() != true)
            throw new ErrorTelegram("sendDocument", (int)r.StatusCode, nodo?["description"]?.GetValue<string>() ?? texto);
    }
}

public sealed class ErrorTelegram(string metodo, int codigo, string descripcion)
    : Exception($"Telegram {metodo} ({codigo}): {descripcion}")
{
    public int Codigo { get; } = codigo;
    public string Descripcion { get; } = descripcion;
}
