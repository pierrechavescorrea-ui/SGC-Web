using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Acciones de cuenta que cambian la cookie (deben ir por HTTP, no por el circuito). Todas por POST con
// antiforgery, salvo canjear "ver como" (token de un solo uso de 1 minuto, atado al administrador que lo pidió).
public static class EndpointsCuenta
{
    public static void MapEndpointsCuenta(this WebApplication app)
    {
        var g = app.MapGroup("/cuenta");

        g.MapPost("/salir", async ([FromForm] string? volver, HttpContext ctx, SignInManager<UsuarioSig> sm, AuditoriaAccesos aud, IWebHostEnvironment env) =>
        {
            aud.Registrar(ctx, "Cierre de sesión", true, null, ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), ctx.User.Identity?.Name);
            await sm.SignOutAsync();
            // En desarrollo, salir apaga el acceso libre: vuelve al login hasta elegir otra vez «entrar sin contraseña».
            if (env.IsDevelopment()) ctx.Response.Cookies.Delete(ArranqueSeguridad.CookieAccesoLibre);
            return Results.LocalRedirect("/cuenta/ingresar");
        });

        g.MapPost("/proyecto", async ([FromForm] int id, [FromForm] string? volver, HttpContext ctx, UserManager<UsuarioSig> um, SignInManager<UsuarioSig> sm,
            ServicioPermisos permisos, ServicioProyectos proyectos, AuditoriaAccesos aud) =>
        {
            if (ctx.User.HasClaim(c => c.Type == ClaimsSig.Impersonador))
                return Results.LocalRedirect("/mi-cuenta");
            var u = await um.GetUserAsync(ctx.User);
            if (u is null) return Results.LocalRedirect("/cuenta/ingresar");
            if (!permisos.ProyectosDe(u.Id).Any(p => p.Id == id))
            {
                aud.Registrar(ctx, "Cambio de proyecto", false, $"Sin acceso al proyecto {id}", u.Id, u.Email);
                return Results.LocalRedirect("/acceso-denegado");
            }
            var error = await proyectos.PrepararAsync(id);
            if (error is not null) return Results.Problem(error, statusCode: 503);
            u.ProyectoActivoId = id;
            await um.UpdateAsync(u);
            await sm.RefreshSignInAsync(u);
            aud.Registrar(ctx, "Cambio de proyecto", true, proyectos.Proyecto(id)?.Codigo, u.Id, u.Email);
            // «volver» solo a una ruta local de la app (nunca //otro-sitio ni una URL absoluta).
            return Results.LocalRedirect(InicioSesionSig.DestinoLocal(volver));
        });

        // Ver como otro usuario (solo administrador global; queda en la auditoría y con franja visible).
        g.MapGet("/ver-como", async (string? t, HttpContext ctx, VerComoTokens tokens, UserManager<UsuarioSig> um,
            SignInManager<UsuarioSig> sm, ServicioPermisos permisos, AuditoriaAccesos aud) =>
        {
            var admin = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var par = tokens.Canjear(t);
            if (par is null || par.Value.Admin != admin || !permisos.EsAdministradorGlobal(admin)) return Results.LocalRedirect("/acceso-denegado");
            var objetivo = await um.FindByIdAsync(par.Value.Objetivo);
            if (objetivo is null || !objetivo.Activo || permisos.EsAdministradorGlobal(objetivo.Id)) return Results.LocalRedirect("/admin/usuarios");
            aud.Registrar(ctx, "Ver como usuario", true, objetivo.Email, admin, ctx.User.Identity?.Name);
            await sm.SignInWithClaimsAsync(objetivo, false, new[] { new Claim(ClaimsSig.Impersonador, admin!) });
            return Results.LocalRedirect("/");
        });

        g.MapPost("/dejar-ver-como", async (HttpContext ctx, UserManager<UsuarioSig> um, SignInManager<UsuarioSig> sm, ServicioPermisos permisos, AuditoriaAccesos aud, IConfiguration cfg) =>
        {
            var adminId = ctx.User.FindFirstValue(ClaimsSig.Impersonador);
            var admin = adminId is null ? null : await um.FindByIdAsync(adminId);
            if (admin is null || !permisos.EsAdministradorGlobal(adminId) ||
                !await sm.CanSignInAsync(admin) || await um.IsLockedOutAsync(admin))
            {
                await sm.SignOutAsync();
                return Results.LocalRedirect("/cuenta/ingresar");
            }
            aud.Registrar(ctx, "Fin de ver como usuario", true, ctx.User.Identity?.Name, adminId, admin.Email);
            if (cfg.GetValue<bool>("Seguridad:MfaObligatoriaAdministradores"))
            {
                await sm.SignOutAsync();
                return Results.LocalRedirect("/cuenta/ingresar?ReturnUrl=%2Fadmin%2Fusuarios");
            }
            await sm.SignInAsync(admin, false);
            return Results.LocalRedirect("/admin/usuarios");
        });

        // Ley 1581 (derecho de acceso): el usuario descarga sus datos personales y su historial de accesos.
        g.MapGet("/mis-datos", async (HttpContext ctx, UserManager<UsuarioSig> um, SeguridadDbContext db, ServicioProyectos proyectos) =>
        {
            var u = await um.GetUserAsync(ctx.User);
            if (u is null) return Results.Unauthorized();
            var miembros = await db.Miembros.AsNoTracking().Where(m => m.UsuarioId == u.Id).Include(m => m.Rol).ToListAsync();
            var accesos = await db.Auditoria.AsNoTracking().Where(a => a.UsuarioId == u.Id).OrderByDescending(a => a.Fecha).Take(1000)
                .Select(a => new { a.Fecha, a.Evento, a.Exito, a.Ip, a.Navegador, a.Detalle }).ToListAsync();
            var telegram = await db.VinculosTelegram.AsNoTracking().Where(v => v.UsuarioId == u.Id)
                .Select(v => new { v.UsuarioTelegram, v.Activo, v.Vinculado, v.UltimoUso }).ToListAsync();
            var datos = new
            {
                Generado = DateTime.UtcNow,
                Cuenta = new { u.Email, u.NombreCompleto, u.Cargo, u.Empresa, u.Area, u.Profesion, u.MatriculaProfesional, u.PhoneNumber, u.TelefonoFijo, u.CorreoAlterno, u.Ciudad, u.JefeInmediato, TieneFoto = u.FotoVersion != null, u.CreadoEn, u.UltimoIngreso, u.AutorizoDatosEn, u.VersionPolitica, DobleFactor = u.TwoFactorEnabled },
                Proyectos = miembros.Select(m => new { Proyecto = proyectos.Proyecto(m.ProyectoId)?.Nombre, Rol = m.Rol?.Nombre, m.Activo, m.Vence }),
                Telegram = telegram,
                AvisosDesactivados = await db.PreferenciasNotificacion.AsNoTracking().Where(p => p.UsuarioId == u.Id && !p.Recibir).Select(p => p.Tipo).ToListAsync(),
                Accesos = accesos,
            };
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(datos, new JsonSerializerOptions { WriteIndented = true }),
                "application/json", $"mis-datos-sigweb-{DateTime.Now:yyyy-MM-dd}.json");
        });

        // Foto de perfil: se guarda en la base central (no en wwwroot) y se entrega solo con sesión.
        g.MapGet("/foto/{id}", async (string id, HttpContext ctx, SeguridadDbContext db) =>
        {
            var f = await db.FotosUsuario.AsNoTracking().FirstOrDefaultAsync(x => x.UsuarioId == id);
            if (f is null) return Results.NotFound();
            ctx.Response.Headers.CacheControl = "private, max-age=86400";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(f.Datos, f.Tipo);
        }).RequireAuthorization();

        g.MapPost("/foto", async (IFormFile? archivo, [FromForm] string? quitar, HttpContext ctx, UserManager<UsuarioSig> um, SignInManager<UsuarioSig> sm,
            SeguridadDbContext db, AuditoriaAccesos aud) =>
        {
            var u = await um.GetUserAsync(ctx.User);
            if (u is null) return Results.LocalRedirect("/cuenta/ingresar");
            if (ctx.User.FindFirstValue(ClaimsSig.Impersonador) is not null) return Results.LocalRedirect("/mi-cuenta?foto=bloqueado");
            var actual = await db.FotosUsuario.FirstOrDefaultAsync(x => x.UsuarioId == u.Id);
            if (quitar == "1")
            {
                if (actual is not null) db.FotosUsuario.Remove(actual);
                await db.SaveChangesAsync();
                u.FotoVersion = null;
                await um.UpdateAsync(u);
                await Refrescar(ctx, sm, u);
                aud.Registrar(ctx, "Foto de perfil quitada", true, null, u.Id, u.Email);
                return Results.LocalRedirect("/mi-cuenta?foto=quitada");
            }
            if (archivo is null || archivo.Length == 0) return Results.LocalRedirect("/mi-cuenta?foto=vacia");
            if (archivo.Length > MaxFoto) return Results.LocalRedirect("/mi-cuenta?foto=grande");
            using var ms = new MemoryStream();
            await using (var s = archivo.OpenReadStream()) await s.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var tipo = TipoImagen(bytes);   // por el contenido, no por la extensión ni el Content-Type del navegador
            if (tipo is null) return Results.LocalRedirect("/mi-cuenta?foto=formato");
            if (actual is null) { actual = new FotoUsuario { UsuarioId = u.Id }; db.FotosUsuario.Add(actual); }
            actual.Datos = bytes; actual.Tipo = tipo; actual.Actualizado = DateTime.UtcNow;
            await db.SaveChangesAsync();
            u.FotoVersion = DateTime.UtcNow.Ticks;
            await um.UpdateAsync(u);
            await Refrescar(ctx, sm, u);
            aud.Registrar(ctx, "Foto de perfil actualizada", true, $"{bytes.Length / 1024} KB {tipo}", u.Id, u.Email);
            return Results.LocalRedirect("/mi-cuenta?foto=ok");
        });

        // Las URLs de los reportes pueden contener tokens de alta o recuperación. Registrar solo la directiva.
        app.MapPost("/csp-reporte", async (HttpContext ctx, ILoggerFactory lf) =>
        {
            using var lector = new StreamReader(ctx.Request.Body);
            var buffer = new char[4096];
            var n = await lector.ReadBlockAsync(buffer.AsMemory(), ctx.RequestAborted);
            try
            {
                using var json = JsonDocument.Parse(new string(buffer, 0, n));
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("csp-report", out var reporte) &&
                    reporte.ValueKind == JsonValueKind.Object && reporte.TryGetProperty("effective-directive", out var valor) &&
                    valor.ValueKind == JsonValueKind.String && valor.GetString() is { } directiva && DirectivasCsp.Contains(directiva))
                    lf.CreateLogger("SigWeb.Csp").LogInformation("Incidente de CSP en {Directiva}", directiva);
            }
            catch (JsonException) { /* reporte incompleto o mal formado */ }
            return Results.NoContent();
        }).AllowAnonymous().DisableAntiforgery();
    }

    private static readonly HashSet<string> DirectivasCsp = new(StringComparer.Ordinal)
    {
        "script-src", "script-src-elem", "script-src-attr", "style-src", "style-src-elem", "style-src-attr",
        "default-src", "img-src", "connect-src", "font-src", "frame-src", "frame-ancestors", "object-src",
        "base-uri", "form-action", "worker-src", "media-src", "manifest-src", "require-trusted-types-for", "trusted-types"
    };

    // La cookie lleva la versión de la foto (avatar del menú). Con el acceso libre de desarrollo no hay cookie que refrescar.
    private static async Task Refrescar(HttpContext ctx, SignInManager<UsuarioSig> sm, UsuarioSig u)
    {
        if (ctx.User.HasClaim(c => c.Type == ClaimsSig.Desarrollo)) return;
        await sm.RefreshSignInAsync(u);
    }

    public const long MaxFoto = 2 * 1024 * 1024;

    // PNG, JPEG o WebP por sus primeros bytes (SVG no: puede llevar código).
    public static string? TipoImagen(byte[] b)
    {
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }
}
