using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Preferencias de interfaz POR USUARIO: tema, menú contraído, paneles del inicio, columnas fijadas y ocultas, densidad, vista elegida por módulo, zoom…
// En el navegador siguen viviendo en localStorage con las mismas claves (sigweb.* / sigwebCols: / sigwebPin: / sigwebDensidad);
// js/preferencias.js las reemplaza al cargar por las del usuario y envía cada cambio a POST /api/preferencias.
// Una preferencia de un módulo (o de una opción) sin permiso Ver en el proyecto activo no se entrega ni se guarda:
// el usuario ve lo predeterminado (p. ej. sin permiso de Temas = tema institucional).
public class PreferenciaUsuario
{
    public int Id { get; set; }
    [MaxLength(450)] public string UsuarioId { get; set; } = "";
    [MaxLength(200)] public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;
}

public sealed partial class ServicioPreferenciasUsuario
{
    public const int MaxClave = 200, MaxValor = 16_000, MaxClaves = 400;

    private readonly SeguridadDbContext _db;
    private readonly ILogger<ServicioPreferenciasUsuario> _log;

    public ServicioPreferenciasUsuario(SeguridadDbContext db, ILogger<ServicioPreferenciasUsuario> log) { _db = db; _log = log; }

    // Claves reales: también llevan rutas, «%» (Uri.EscapeDataString), «:» y espacios. Sin caracteres de control.
    [GeneratedRegex(@"^(sigweb\.[^\x00-\x1f\x7f]{1,190}|sigweb(Cols|Pin):[^\x00-\x1f\x7f]{1,190}|sigwebDensidad)$")]
    private static partial Regex ClaveValida();

    public static bool EsClaveValida(string? clave) => clave is { Length: > 0 and <= MaxClave } && ClaveValida().IsMatch(clave);

    // Módulo (u opción) del que depende la preferencia; null = de uso general (menú, inicio, favoritos, densidad…).
    public static string? ModuloDe(string clave)
    {
        if (clave == "sigweb.tema") return "temas";
        if (clave.StartsWith("sigwebCols:") || clave.StartsWith("sigwebPin:"))
        {
            // Columnas de una tabla: la clave lleva la ruta de la pantalla («/ensayos/3#0»).
            var ruta = clave[(clave.IndexOf(':') + 1)..].Split('#')[0];
            var m = CatalogoModulos.ClaveDeRuta(ruta);
            return m == "inicio" ? null : m;
        }
        if (clave.StartsWith("sigweb.vista.")) return clave["sigweb.vista.".Length..];
        if (clave.StartsWith("sigweb.estadistica.")) return "ensayos";
        if (clave.StartsWith("sigweb.organigrama.")) return "organigrama";
        if (clave == "sigweb.visorifc") return "ifc";
        if (clave == "sigweb.hojavida.panel" || clave.StartsWith("sigweb.elemento.panel.")) return "trazado";
        if (clave.StartsWith("sigweb.disenador.") || clave == "sigweb.vistareg") return "editor";
        if (clave.StartsWith("sigweb.disenadorModulos.") || clave.StartsWith("sigweb.proyectos.")) return Admin;
        return null;
    }

    private const string Admin = "#admin";

    public static bool Permitida(string clave, SesionSig? s)
    {
        if (!EsClaveValida(clave)) return false;
        var m = ModuloDe(clave);
        if (m is null) return true;
        if (s is null) return false;
        if (s.Sistema) return true;
        if (m == Admin) return s.Permisos.AdminGlobal;
        // Módulo que no existe (vista de un módulo creado ya eliminado…) = sin permiso.
        return CatalogoModulos.De(m) is not null && s.Permisos.Puede(m, Accion.Ver);
    }

    // Preferencias que aplican en la sesión actual. null = no se pudieron leer (la página sigue con lo del navegador).
    public async Task<Dictionary<string, string>?> LeerAsync(string usuarioId, SesionSig? s, CancellationToken ct = default)
    {
        try
        {
            var filas = await _db.PreferenciasUsuario.AsNoTracking().Where(p => p.UsuarioId == usuarioId)
                .Select(p => new { p.Clave, p.Valor }).ToListAsync(ct);
            return filas.Where(f => Permitida(f.Clave, s)).ToDictionary(f => f.Clave, f => f.Valor);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudieron leer las preferencias del usuario (¿falta la migración PreferenciasUsuario?).");
            return null;
        }
    }

    public async Task<bool> TieneAlgunaAsync(string usuarioId, CancellationToken ct = default) =>
        await _db.PreferenciasUsuario.AnyAsync(p => p.UsuarioId == usuarioId, ct);

    // valor null = quitar. Claves inválidas, sin permiso o demasiado grandes se ignoran. Devuelve cuántas se aplicaron.
    public async Task<int> GuardarAsync(string usuarioId, IReadOnlyDictionary<string, string?> cambios, SesionSig? s, CancellationToken ct = default)
    {
        var validos = cambios.Where(c => Permitida(c.Key, s) && (c.Value is null || c.Value.Length <= MaxValor))
            .Take(MaxClaves).ToDictionary(c => c.Key, c => c.Value);
        if (validos.Count == 0) return 0;
        var claves = validos.Keys.ToList();
        var existentes = await _db.PreferenciasUsuario.Where(p => p.UsuarioId == usuarioId && claves.Contains(p.Clave)).ToListAsync(ct);
        var total = await _db.PreferenciasUsuario.CountAsync(p => p.UsuarioId == usuarioId, ct);
        var ahora = DateTime.UtcNow;
        foreach (var (clave, valor) in validos)
        {
            var e = existentes.FirstOrDefault(x => x.Clave == clave);
            if (valor is null) { if (e is not null) { _db.PreferenciasUsuario.Remove(e); total--; } continue; }
            if (e is not null) { e.Valor = valor; e.ModificadoEn = ahora; continue; }
            if (total >= MaxClaves) continue;
            _db.PreferenciasUsuario.Add(new PreferenciaUsuario { UsuarioId = usuarioId, Clave = clave, Valor = valor, ModificadoEn = ahora });
            total++;
        }
        await _db.SaveChangesAsync(ct);
        return validos.Count;
    }

    // Bloque para <meta name="sig-preferencias">: u = usuario (para limpiar lo de otro usuario en un equipo compartido),
    // inicial = el usuario aún no tiene nada guardado (el navegador sube lo que ya tenía, una sola vez),
    // lectura = «ver como» (se aplican las del usuario observado, sin guardar cambios).
    public async Task<string?> BloquePaginaAsync(string usuarioId, SesionSig? s, CancellationToken ct = default)
    {
        var p = await LeerAsync(usuarioId, s, ct);
        if (p is null) return null;
        bool inicial;
        try { inicial = p.Count == 0 && !await TieneAlgunaAsync(usuarioId, ct); } catch { return null; }
        return JsonSerializer.Serialize(new { u = usuarioId, inicial, lectura = s?.Impersonador is not null, p });
    }
}

public static class EndpointsPreferencias
{
    public static readonly object ClaveItem = new();

    // Páginas HTML con sesión: deja el bloque de preferencias en HttpContext.Items para App.razor.
    public static IApplicationBuilder UsePreferenciasUsuario(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var path = ctx.Request.Path;
        if (HttpMethods.IsGet(ctx.Request.Method) && !Path.HasExtension(path.Value) && !path.StartsWithSegments("/_blazor")
            && !path.StartsWithSegments("/api") && !path.StartsWithSegments("/cuenta") && !path.StartsWithSegments("/uploads")
            && AmbienteSig.Actual is { UsuarioId: { Length: > 0 } uid } s)
            ctx.Items[ClaveItem] = await ctx.RequestServices.GetRequiredService<ServicioPreferenciasUsuario>()
                .BloquePaginaAsync(uid, s, ctx.RequestAborted);
        await next();
    });

    public static void MapEndpointsPreferencias(this WebApplication app)
    {
        // Solo desde la propia app: JSON + cabecera propia (un formulario de otro sitio no puede enviarlas sin CORS) y la
        // cookie de sesión es SameSite=Lax. Autenticación por la política general.
        app.MapPost("/api/preferencias", async (HttpContext ctx, ServicioPreferenciasUsuario prefs, CancellationToken ct) =>
        {
            if (ctx.Request.ContentType?.StartsWith("application/json") != true) return Results.BadRequest();
            var s = AmbienteSig.Actual;
            var uid = s?.UsuarioId;
            if (string.IsNullOrEmpty(uid)) return Results.Unauthorized();
            // La cabecera trae el usuario con el que se cargó la página: una pestaña vieja (de otra cuenta, tras cambiar de
            // sesión en el mismo navegador) no escribe en las preferencias de la cuenta actual.
            if (ctx.Request.Headers["X-Sig-Preferencias"].ToString() != uid) return Results.Conflict();
            if (s!.Impersonador is not null) return Results.NoContent();   // «ver como»: no se tocan las del usuario observado
            // Tope real del cuerpo (también sin Content-Length).
            if (ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } tope)
                tope.MaxRequestBodySize = 512_000;
            if (ctx.Request.ContentLength is > 512_000) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            Dictionary<string, string?>? cambios;
            try { cambios = await JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(ctx.Request.Body, cancellationToken: ct); }
            catch (JsonException) { return Results.BadRequest(); }
            catch (BadHttpRequestException) { return Results.StatusCode(StatusCodes.Status413PayloadTooLarge); }
            if (cambios is null || cambios.Count == 0) return Results.NoContent();
            try { await prefs.GuardarAsync(uid, cambios, s, ct); }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or Npgsql.PostgresException)
            {
                ctx.RequestServices.GetRequiredService<ILogger<ServicioPreferenciasUsuario>>()
                    .LogWarning(ex, "No se pudieron guardar las preferencias del usuario.");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            return Results.NoContent();
        }).DisableAntiforgery();
    }
}
