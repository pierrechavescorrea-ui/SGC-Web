using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.AvanceObra;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Bot;

// Bot de Telegram de SGC Web: consulta de hojas de vida de torres, planos, documentos y equipos desde el celular.
// Long polling (getUpdates): no necesita URL pública ni abrir puertos, solo salida a api.telegram.org.
// Token en Telegram:Token (user-secrets / variable de entorno Telegram__Token; NUNCA en appsettings ni Git).
// Telegram:UrlSigWeb (opcional, https pública) = botones "Abrir en SGC Web". Sin token el servicio no hace nada.
// Solo responde a chats autorizados (UsuarioBot activo); se vinculan con /vincular <código> (Administración › Bot de Telegram).
public sealed class ServicioBotTelegram : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ServicioBotTelegram> _log;
    private readonly IWebHostEnvironment _env;
    private readonly SemaphoreSlim[] _turnosChat = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    internal async Task<IDisposable> BloquearChatAsync(long chat, CancellationToken ct = default)
    {
        var turno = _turnosChat[(int)(unchecked((ulong)chat) % (uint)_turnosChat.Length)];
        await turno.WaitAsync(ct);
        return new TurnoChat(turno);
    }

    private sealed class TurnoChat(SemaphoreSlim turno) : IDisposable
    {
        public void Dispose() => turno.Release();
    }

    public ServicioBotTelegram(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<ServicioBotTelegram> log, IWebHostEnvironment env)
    {
        _scopes = scopes; _cfg = cfg; _log = log; _env = env;
    }

    // ---------- Estado (lo muestra Administración › Bot de Telegram) ----------
    public bool TieneToken => !string.IsNullOrWhiteSpace(_cfg["Telegram:Token"]);
    public bool Conectado { get; private set; }
    public string? UsuarioBot { get; private set; }
    public string? UltimoError { get; private set; }
    public DateTime? UltimoMensaje { get; private set; }

    // Base pública de SGC Web para los botones de enlace; Telegram rechaza localhost y direcciones sin http(s).
    public string? UrlSigWeb
    {
        get
        {
            var u = (_cfg["Telegram:UrlSigWeb"] ?? "").Trim().TrimEnd('/');
            if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")) return null;
            if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return null;
            return u;
        }
    }

    // ---------- Códigos de vinculación (en memoria, un solo uso, 15 min) ----------
    // Cada código lleva el proyecto donde se generó y, si se pidió desde "Mi cuenta" o lo asignó el administrador,
    // el usuario de SGC Web cuyos permisos aplicará el chat.
    public sealed record CodigoVinculo(string Nombre, int? ProyectoId, string? UsuarioId);
    private readonly ConcurrentDictionary<string, (CodigoVinculo C, DateTime Vence)> _codigos = new();
    private readonly ConcurrentDictionary<long, (int Fallos, DateTime Desde)> _intentos = new();

    public (string Codigo, DateTime Vence) GenerarCodigo(string nombre, string? usuarioId = null, int? proyectoId = null)
    {
        foreach (var k in _codigos.Where(k => k.Value.Vence < DateTime.Now).Select(k => k.Key).ToList()) _codigos.TryRemove(k, out _);
        var vence = DateTime.Now.AddMinutes(15);
        nombre = nombre.Trim();
        var entrada = new CodigoVinculo(nombre[..Math.Min(nombre.Length, 150)], proyectoId ?? AmbienteSig.ProyectoId, usuarioId);
        string codigo;
        do codigo = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        while (!_codigos.TryAdd(codigo, (entrada, vence)));
        return (codigo, vence);
    }

    // Códigos pendientes del proyecto abierto (o de todos, para la administración).
    public IReadOnlyList<(string Codigo, string Nombre, DateTime Vence, int? ProyectoId, string? UsuarioId)> CodigosPendientes(bool todos = false) =>
        _codigos.Where(k => k.Value.Vence >= DateTime.Now && (todos || k.Value.C.ProyectoId == AmbienteSig.ProyectoId))
            .Select(k => (k.Key, k.Value.C.Nombre, k.Value.Vence, k.Value.C.ProyectoId, k.Value.C.UsuarioId)).OrderBy(x => x.Vence).ToList();

    public bool AnularCodigo(string codigo) => _codigos.TryRemove(codigo, out _);

    // Proyecto de un código sin consumirlo (para abrir la base correcta antes de canjearlo).
    public int? ProyectoDeCodigo(string codigo) =>
        _codigos.TryGetValue(codigo.Trim(), out var c) && c.Vence >= DateTime.Now ? c.C.ProyectoId : null;

    // Devuelve el código si es válido (y lo consume). Máx. 5 fallos por chat y hora.
    public CodigoVinculo? CanjearCodigo(long chat, string codigo, out bool bloqueado)
    {
        bloqueado = false;
        var ahora = DateTime.Now;
        if (_intentos.TryGetValue(chat, out var it) && it.Desde > ahora.AddHours(-1) && it.Fallos >= 5) { bloqueado = true; return null; }
        if (_codigos.TryRemove(codigo.Trim(), out var c) && c.Vence >= ahora) { _intentos.TryRemove(chat, out _); return c.C; }
        _intentos[chat] = it.Desde > ahora.AddHours(-1) ? (it.Fallos + 1, it.Desde) : (1, ahora);
        return null;
    }

    // ---------- Conversaciones en curso (/consecutivo), por chat ----------
    public ConcurrentDictionary<long, ManejadorBot.ConversacionConsecutivo> Conversaciones { get; } = new();

    // ---------- Avance de obra (instantánea de cada proyecto, se reutiliza 3 min) ----------
    private readonly SemaphoreSlim _lockAvance = new(1, 1);
    private readonly Dictionary<int, (InstantaneaAvance Datos, DateTime Hora)> _avance = new();

    public async Task<InstantaneaAvance?> AvanceAsync(AppDbContext db)
    {
        var pid = AmbienteSig.ProyectoId ?? 0;
        await _lockAvance.WaitAsync();
        try
        {
            if (_avance.TryGetValue(pid, out var a) && a.Hora > DateTime.Now.AddMinutes(-3)) return a.Datos;
            var datos = ServicioAvanceObra.Cargar(db, _env.WebRootPath);
            _avance[pid] = (datos, DateTime.Now);
            return datos;
        }
        catch (Exception ex) { _log.LogWarning(ex, "Bot: no se pudo calcular el avance de obra"); return null; }
        finally { _lockAvance.Release(); }
    }

    // ---------- Ciclo principal ----------
    public static readonly (string, string)[] Comandos =
    {
        ("torre", "Hoja de vida de una torre (ej. /torre 45)"),
        ("buscar", "Buscar en SGC Web (torres, planos, documentos, equipos…)"),
        ("planos", "Planos por grupo (estructural, civil, eléctrico…)"),
        ("plano", "Buscar un plano por código o título"),
        ("documentos", "Documentos por tipología"),
        ("doc", "Buscar un documento por código"),
        ("equipo", "Equipo por nombre, marca o serie: calibración"),
        ("estados", "Estado de las torres y avance de obra por tiro"),
        ("consecutivo", "Pedir un consecutivo de comunicado"),
        ("misconsecutivos", "Mis consecutivos: confirmar envío o anular"),
        ("proyecto", "Cambiar de proyecto"),
        ("cancelar", "Cancelar lo que estoy pidiendo"),
        ("vincular", "Vincular este chat con SGC Web"),
        ("ayuda", "Cómo usar el bot"),
    };

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var token = _cfg["Telegram:Token"]?.Trim();
        if (string.IsNullOrEmpty(token)) { _log.LogInformation("Bot Telegram: sin Telegram:Token, no se inicia."); return; }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        var cliente = new ClienteTelegram(http, token);
        long offset = 0;
        var espera = TimeSpan.FromSeconds(5);
        var preparado = false;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!preparado)
                {
                    var (_, usuario) = await cliente.GetMeAsync(ct);
                    UsuarioBot = usuario;
                    await cliente.DeleteWebhookAsync(ct);
                    await cliente.SetMyCommandsAsync(Comandos, ct);
                    preparado = true;
                    _log.LogInformation("Bot Telegram conectado como @{Usuario}", usuario);
                }

                var updates = await cliente.GetUpdatesAsync(offset, 30, ct);
                Conectado = true; UltimoError = null; espera = TimeSpan.FromSeconds(5);
                foreach (var u in updates)
                {
                    offset = Math.Max(offset, (u?["update_id"]?.GetValue<long>() ?? 0) + 1);
                    if (u is JsonObject o) await ProcesarAsync(cliente, o, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Conectado = false;
                UltimoError = ex is ErrorTelegram { Codigo: 409 }
                    ? "Otra instancia de SGC Web está usando el mismo token (solo puede correr una)."
                    : ex is ErrorTelegram { Codigo: 401 } ? "Token inválido o revocado (401). Revisa Telegram:Token." : ex.Message;
                _log.LogWarning("Bot Telegram: {Error}", UltimoError);
                if (ex is ErrorTelegram { Codigo: 401 }) preparado = false;
                try { await Task.Delay(espera, ct); } catch (OperationCanceledException) { break; }
                espera = TimeSpan.FromSeconds(Math.Min(espera.TotalSeconds * 2, 120));
            }
        }
        Conectado = false;
    }

    private async Task ProcesarAsync(ClienteTelegram cliente, JsonObject update, CancellationToken ct)
    {
        UltimoMensaje = DateTime.Now;
        try
        {
            // Cada mensaje se atiende en la base del proyecto del chat (vínculo central) con los permisos de su usuario.
            using var turno = await BloquearChatAsync(ChatDe(update), ct);
            var mensaje = update["message"] as JsonObject;
            var codigo = ManejadorBot.CodigoParaVincular(mensaje);
            var proyectoCodigo = codigo is null ? null : ProyectoDeCodigo(codigo);
            var sesion = await SesionDeAsync(update, proyectoCodigo, ct);
            if (sesion is null) return;
            using var _ = AmbienteSig.Usar(sesion);
            using var scope = _scopes.CreateScope();
            var manejador = new ManejadorBot(cliente, this, scope.ServiceProvider, _env.WebRootPath);
            if (proyectoCodigo == sesion.Proyecto.Id && mensaje is not null && codigo is not null)
                await manejador.VincularCodigoAsync(mensaje, codigo, ct);
            else
                await manejador.ProcesarAsync(update, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _log.LogError(ex, "Bot Telegram: error al procesar un mensaje"); }
    }

    private static long ChatDe(JsonObject u) =>
        (u["message"]?["chat"]?["id"] ?? u["callback_query"]?["message"]?["chat"]?["id"] ?? u["inline_query"]?["from"]?["id"])?.GetValue<long>() ?? 0;

    private bool PermitirChatsSinUsuario =>
        _env.IsDevelopment() && _cfg.GetValue<bool>("Telegram:PermitirChatsSinUsuario");

    private async Task<SesionSig?> SesionDeAsync(JsonObject update, int? proyectoCodigo, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var proyectos = scope.ServiceProvider.GetRequiredService<ServicioProyectos>();
        var permisos = scope.ServiceProvider.GetRequiredService<ServicioPermisos>();
        var central = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
        var chat = ChatDe(update);

        // Vinculación: la base es la del proyecto donde se generó el código.
        if (proyectoCodigo is int pc && proyectos.Proyecto(pc)?.Estado == EstadoProyecto.Activo
            && proyectos.Activo(pc) is { } pcod)
            return new SesionSig { Proyecto = pcod, Sistema = true };

        var v = chat == 0 ? null : await central.VinculosTelegram.AsNoTracking().FirstOrDefaultAsync(x => x.ChatId == chat, ct);
        // Vínculo desactivado por el administrador: el chat no recibe respuesta (no cae al proyecto predeterminado).
        if (v is not null && (!v.Activo || proyectos.Proyecto(v.ProyectoId)?.Estado != EstadoProyecto.Activo)) return null;
        if (v is not null && proyectos.Activo(v.ProyectoId) is { } p)
        {
            if (v.UsuarioId is null)
            {
                if (!PermitirChatsSinUsuario) return null;
                return new SesionSig { Proyecto = p, Sistema = true };
            }
            var perm = permisos.De(v.UsuarioId, p.Id);
            // Sin acceso al proyecto o sin permiso de consultas por Telegram: igual se atiende /proyecto y /ayuda,
            // pero ManejadorBot no muestra datos (permisos vacíos).
            if (!perm.Puede("telegram", Accion.Ver)) perm = PermisosEfectivos.Ninguno;
            return new SesionSig { Proyecto = p, Sistema = true, UsuarioId = v.UsuarioId, Permisos = perm };
        }
        // Chats anteriores al modo multiproyecto: piloto si no se configuró un proyecto explícito.
        var cod = _cfg["Telegram:ProyectoPredeterminado"]?.Trim().ToUpperInvariant();
        ProyectoActivo? pred;
        if (string.IsNullOrEmpty(cod))
        {
            pred = proyectos.Piloto();
        }
        else
        {
            var configurado = proyectos.Todos().FirstOrDefault(p => p.Codigo == cod || p.CodigoConexion == cod);
            pred = configurado is null ? null : proyectos.Activo(configurado.Id);
            if (pred is null)
            {
                _log.LogWarning("Telegram: el proyecto predeterminado {Codigo} no está disponible.", cod);
                return null;
            }
        }
        if (pred is null || proyectos.Proyecto(pred.Id)?.Estado != EstadoProyecto.Activo) return null;
        if (!PermitirChatsSinUsuario)
            return new SesionSig { Proyecto = pred, Sistema = true, Permisos = PermisosEfectivos.Ninguno, UsuarioId = "" };
        return new SesionSig { Proyecto = pred, Sistema = true };
    }
}
