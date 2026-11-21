using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;

namespace SigWeb.Data.Seguridad;

public static class MiddlewareSeguridad
{
    // Rutas que no son pantallas de módulo (no pasan por el control de permisos por módulo).
    private static bool EsInfraestructura(PathString p) =>
        p.StartsWithSegments("/_blazor") || p.StartsWithSegments("/_framework") || p.StartsWithSegments("/_content") ||
        p.StartsWithSegments("/cuenta") || p.StartsWithSegments("/salud") || p.StartsWithSegments("/privacidad") ||
        p.StartsWithSegments("/acceso-denegado") || p.StartsWithSegments("/mi-cuenta") || p.StartsWithSegments("/not-found") || p.StartsWithSegments("/Error") ||
        p.StartsWithSegments("/csp-reporte") || p.StartsWithSegments("/admin") || p.StartsWithSegments("/api") ||
        Path.HasExtension(p.Value);

    // ---------- Acceso libre en desarrollo ----------
    // SOLO con ASPNETCORE_ENVIRONMENT=Development, Autenticacion:AccesoLibreDesarrollo = true y petición desde el
    // mismo equipo (loopback). Entra como el usuario de desarrollo (administrador global). En otro entorno no existe.
    private static readonly object OrigenLocal = new();

    public static IApplicationBuilder UseRegistrarOrigenDesarrollo(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return app;
        return app.Use(async (ctx, next) =>
        {
            ctx.Items[OrigenLocal] = EsLoopback(ctx.Connection.RemoteIpAddress);
            await next();
        });
    }

    private static bool EsLoopback(System.Net.IPAddress? ip) =>
        ip is not null && System.Net.IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);

    public static IApplicationBuilder UseAccesoLibreDesarrollo(this WebApplication app)
    {
        var habilitado = app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Autenticacion:AccesoLibreDesarrollo");
        if (habilitado) app.Logger.LogWarning("Acceso libre de desarrollo activo para peticiones locales.");
        return app.Use(async (ctx, next) =>
        {
            var host = ctx.Request.Host.Host;
            var local = habilitado && ctx.Items.TryGetValue(OrigenLocal, out var origen) && origen is true &&
                EsLoopback(ctx.Connection.RemoteIpAddress) &&
                (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var ipHost) && EsLoopback(ipHost));
            if (!local && ctx.User.HasClaim(c => c.Type == ClaimsSig.Desarrollo))
            {
                await ctx.SignOutAsync(IdentityConstants.ApplicationScheme);
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            if (local && ctx.User.Identity?.IsAuthenticated != true && ctx.Request.Cookies.ContainsKey(ArranqueSeguridad.CookieAccesoLibre)
                && !ctx.Request.Path.StartsWithSegments("/cuenta") && !ctx.Request.Path.StartsWithSegments("/_blazor")
                && !Path.HasExtension(ctx.Request.Path.Value))
            {
                var um = ctx.RequestServices.GetRequiredService<UserManager<UsuarioSig>>();
                var sm = ctx.RequestServices.GetRequiredService<SignInManager<UsuarioSig>>();
                var dev = await um.FindByEmailAsync(ArranqueSeguridad.EmailDesarrollo);
                if (dev is not null)
                {
                    await sm.SignInWithClaimsAsync(dev, isPersistent: false, new[] { new Claim(ClaimsSig.Desarrollo, "1") });
                    ctx.User = await sm.CreateUserPrincipalAsync(dev);
                    ((ClaimsIdentity)ctx.User.Identity!).AddClaim(new Claim(ClaimsSig.Desarrollo, "1"));
                }
            }
            await next();
        });
    }

    public static IApplicationBuilder UseAltaMfaAdministradores(this WebApplication app) => app.Use(async (ctx, next) =>
    {
        var path = ctx.Request.Path;
        var cuenta = path.Equals("/mi-cuenta") || path.Equals("/cuenta/salir") || path.Equals("/cuenta/dejar-ver-como");
        var recursoPublico = path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content") ||
            path.StartsWithSegments("/css") || path.StartsWithSegments("/js") || path.StartsWithSegments("/lib") ||
            path.StartsWithSegments("/img") || path.StartsWithSegments("/images") || path.StartsWithSegments("/fonts") ||
            path.StartsWithSegments("/salud") || path.Equals("/favicon.ico") || path.Equals("/csp-reporte") || EsRutaLogo(path);
        if (!cuenta && !recursoPublico && ctx.User.Identity?.IsAuthenticated == true &&
            RequiereAltaMfa(ctx.User, ctx.RequestServices.GetRequiredService<ServicioPermisos>(), app.Configuration))
        {
            if (HttpMethods.IsGet(ctx.Request.Method) && !path.StartsWithSegments("/api") &&
                !path.StartsWithSegments("/_blazor") && !path.StartsWithSegments("/uploads") && !EsRutaModelo(path))
                ctx.Response.Redirect("/mi-cuenta?mfa=1#mfa");
            else
                Rechazar(ctx, StatusCodes.Status403Forbidden);
            return;
        }
        await next();
    });

    internal static bool RequiereAltaMfa(ClaimsPrincipal usuario, ServicioPermisos permisos, IConfiguration cfg)
    {
        if (!cfg.GetValue("Seguridad:MfaObligatoriaAdministradores", false) ||
            usuario.HasClaim(c => c.Type is ClaimsSig.Desarrollo or ClaimsSig.Impersonador)) return false;
        var id = usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return permisos.EsAdministradorGlobal(id) && !permisos.TieneMfa(id);
    }

    private static void Rechazar(HttpContext ctx, int codigo)
    {
        if (ctx.Features.Get<IStatusCodePagesFeature>() is { } paginas) paginas.Enabled = false;
        ctx.Response.StatusCode = codigo;
    }

    private static bool EsRutaModelo(PathString path) =>
        path.StartsWithSegments("/modelos-ifc") || path.StartsWithSegments("/modelos3d");

    private static bool EsRutaLogo(PathString path) =>
        path.StartsWithSegments("/uploads/marca", out var resto) &&
        resto.Value is { } archivo && archivo.Length > 1 && !archivo[1..].Contains('/') && !archivo.Contains((char)92) &&
        LogoApp.Extensiones.Contains(Path.GetExtension(archivo), StringComparer.OrdinalIgnoreCase);

    // ---------- Sesión por petición: proyecto activo, ambiente, archivos subidos ----------
    public static IApplicationBuilder UseSesionSig(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var user = ctx.User;
        if (user.Identity?.IsAuthenticated == true && user.FindFirstValue(ClaimTypes.NameIdentifier) is { } uid)
        {
            var proyectos = ctx.RequestServices.GetRequiredService<ServicioProyectos>();
            var permisos = ctx.RequestServices.GetRequiredService<ServicioPermisos>();
            var modulo = CatalogoModulos.ClaveDeRuta(ctx.Request.Path.Value);
            var sesion = CircuitoSeguridad.ConstruirSesion(user, proyectos, permisos, modulo, AuditoriaAccesos.Ip(ctx));
            if (sesion is null && !ctx.Request.Path.StartsWithSegments("/cuenta") && !ctx.Request.Path.StartsWithSegments("/_blazor"))
            {
                // Sin proyecto válido en la cookie: se abre el primero al que tenga acceso y se renueva la cookie.
                var disponibles = permisos.ProyectosDe(uid);
                if (disponibles.Count == 0)
                {
                    if (!Path.HasExtension(ctx.Request.Path.Value) && !ctx.Request.Path.StartsWithSegments("/admin"))
                    { ctx.Response.Redirect("/cuenta/sin-proyectos"); return; }
                }
                else if (!ctx.Response.HasStarted && !user.HasClaim(c => c.Type == ClaimsSig.Impersonador))
                {
                    var um = ctx.RequestServices.GetRequiredService<UserManager<UsuarioSig>>();
                    var u = await um.GetUserAsync(user);
                    if (u is not null && u.Activo)
                    {
                        var elegido = disponibles.FirstOrDefault(p => p.Id == u.ProyectoActivoId)
                            ?? disponibles.FirstOrDefault(p => !p.EsEmpresa) ?? disponibles[0];
                        u.ProyectoActivoId = elegido.Id;
                        await um.UpdateAsync(u);
                        var sm = ctx.RequestServices.GetRequiredService<SignInManager<UsuarioSig>>();
                        // En la misma petición del acceso libre de desarrollo aún no hay cookie que refrescar: se emite de nuevo.
                        var extra = user.Claims.Where(c => c.Type == ClaimsSig.Desarrollo).ToList();
                        if (extra.Count > 0) await sm.SignInWithClaimsAsync(u, isPersistent: false, extra);
                        else await sm.RefreshSignInAsync(u);
                        ctx.User = await sm.CreateUserPrincipalAsync(u);
                        if (extra.Count > 0) ((ClaimsIdentity)ctx.User.Identity!).AddClaims(extra.Select(c => new Claim(c.Type, c.Value)));
                        sesion = CircuitoSeguridad.ConstruirSesion(ctx.User, proyectos, permisos, modulo, AuditoriaAccesos.Ip(ctx));
                    }
                }
            }
            if (sesion is not null)
            {
                if (!proyectos.Preparado(sesion.Proyecto.Id))
                {
                    var error = await proyectos.PrepararAsync(sesion.Proyecto.Id, ctx.RequestAborted);
                    if (error is not null && !Path.HasExtension(ctx.Request.Path.Value))
                    {
                        ctx.Response.StatusCode = 503;
                        await ctx.Response.WriteAsync(error);
                        return;
                    }
                }
                AmbienteSig.Actual = sesion;


            }
        }

        // Archivos subidos (documentos de clientes): solo usuarios con sesión en un proyecto.
        // Modelos 3D derivados de los planos del cliente: también privados.
        var modelo = EsRutaModelo(ctx.Request.Path);
        if (ctx.Request.Path.StartsWithSegments("/uploads") || modelo)
        {
            var ext = Path.GetExtension(ctx.Request.Path.Value ?? "").ToLowerInvariant();
            // Única excepción: el logo de la empresa (uploads/marca, solo imágenes raster) se ve en el login, antes de la sesión.
            // No es un documento de cliente; lo sube solo el administrador (LogoApp, Administración › Sistema).
            var logo = EsRutaLogo(ctx.Request.Path);
            if (AmbienteSig.Actual is null && !logo) { Rechazar(ctx, StatusCodes.Status401Unauthorized); return; }
            // Los JSON/IFC de torres también los dibujan Avance de obra, Línea 3D y la hoja de vida (Trazado).
            if (modelo && AmbienteSig.Actual is { } ses && !ses.Sistema &&
                !new[] { "ifc", "avance-obra", "trazado" }.Any(m => ses.Permisos.Puede(m, Accion.Ver)))
            {
                Rechazar(ctx, StatusCodes.Status403Forbidden);
                return;
            }
            var activo = ext is ".html" or ".htm" or ".svg" or ".svgz" or ".xml" or ".xhtml" or ".js" or ".mjs";
            ctx.Response.OnStarting(() =>
            {
                // Un HTML o SVG subido no debe poder ejecutar código en el dominio de SigWeb (XSS almacenado).
                // (Solo a esos tipos: Chrome no muestra PDF con "sandbox".)
                if (activo) ctx.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'";
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                // Documentos: el navegador puede guardarlos pero los revalida en cada uso (304 barato; tras cerrar sesión no se sirven sin preguntar).
                ctx.Response.Headers.CacheControl = logo ? "private, max-age=3600" : "private, no-cache";
                return Task.CompletedTask;
            });
        }
        await next();
    });

    // ---------- Permiso por módulo en cada pantalla ----------
    public static IApplicationBuilder UseGuardaModulos(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var path = ctx.Request.Path;
        if (!HttpMethods.IsGet(ctx.Request.Method) || EsInfraestructura(path) || ctx.User.Identity?.IsAuthenticated != true)
        { await next(); return; }
        var s = AmbienteSig.Actual;
        if (s is null) { await next(); return; }
        var modulo = CatalogoModulos.ClaveDeRuta(path.Value);
        var requiere = Accion.Ver;
        var q = ctx.Request.Query;
        // Editor: pestaña Formularios = Editar formularios; Vistas = Editar vistas (en algún módulo).
        if (modulo == "editor")
        {
            var pestana = q["pestana"].ToString();
            var puede = pestana == "vistas" ? s.Permisos.PuedeEnAlguno(Accion.EditarVistas)
                : s.Permisos.PuedeEnAlguno(Accion.EditarFormularios) || s.Permisos.PuedeEnAlguno(Accion.EditarVistas);
            if (!puede) { Denegar(ctx, modulo); return; }
            await next(); return;
        }
        if (q.ContainsKey("disenarVista")) requiere |= Accion.EditarVistas;
        if (!s.Permisos.Puede(modulo, requiere)) { Denegar(ctx, modulo); return; }
        await next();
    });

    private static void Denegar(HttpContext ctx, string modulo)
    {
        ctx.RequestServices.GetRequiredService<AuditoriaAccesos>().Registrar("Acceso denegado", false, ctx.Request.Path.Value);
        ctx.Response.Redirect("/acceso-denegado?modulo=" + Uri.EscapeDataString(modulo));
    }

    // ---------- Cabeceras de seguridad ----------
    public static IApplicationBuilder UseCabecerasSeguridad(this WebApplication app)
    {
        var cspPagina = app.Configuration["Seguridad:Csp"] ?? ArranqueSeguridad.CspPorDefecto;
        var cspSoloReporte = app.Configuration.GetValue("Seguridad:CspSoloReporte", false);
        // Trusted Types solo en reporte y a pedido (Seguridad:TrustedTypesReporte = true): mide cuántos innerHTML habría que
        // pasar por una política antes de exigirlo. Apagado por defecto para no llenar el registro de avisos.
        var ttReporte = app.Configuration.GetValue("Seguridad:TrustedTypesReporte", false);
        return app.Use(async (ctx, next) =>
        {
            ctx.Response.OnStarting(() =>
            {
                var h = ctx.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["X-Frame-Options"] = "SAMEORIGIN";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Permissions-Policy"] = "camera=(), microphone=(), payment=(), usb=(), geolocation=(self)";
                if (!h.ContainsKey("Content-Security-Policy"))
                {
                    // Siempre exigidas (no rompen nada): sin incrustar en otros sitios, sin <base> ajeno ni plugins.
                    h["Content-Security-Policy"] = "frame-ancestors 'self'; base-uri 'self'; object-src 'none'; form-action 'self'";
                    // Política completa, exigida (Seguridad:CspSoloReporte = true la deja solo en reporte si algo se rompe).
                    if (cspSoloReporte)
                        h["Content-Security-Policy-Report-Only"] = cspPagina + (ttReporte ? "; require-trusted-types-for 'script'" : "") + "; report-uri /csp-reporte";
                    else
                    {
                        h["Content-Security-Policy"] = cspPagina + "; report-uri /csp-reporte";
                        if (ttReporte) h["Content-Security-Policy-Report-Only"] = "require-trusted-types-for 'script'; report-uri /csp-reporte";
                    }
                }
                // Páginas con datos: que el navegador no las guarde en caché compartida.
                if (ctx.User.Identity?.IsAuthenticated == true && !h.ContainsKey("Cache-Control") && !Path.HasExtension(ctx.Request.Path.Value))
                    h.CacheControl = "no-store";
                return Task.CompletedTask;
            });
            await next();
        });
    }
}

public static class FiltrosEndpoint
{
    // La API exige permiso en el módulo (además de sesión): 403 y registro en la auditoría si no lo tiene.
    public static RouteHandlerBuilder RequiereModulo(this RouteHandlerBuilder b, string modulo, Accion acciones) =>
        b.AddEndpointFilter(async (ctx, next) =>
        {
            var s = AmbienteSig.Actual;
            if (s is null || !s.Permisos.Puede(modulo, acciones))
            {
                ctx.HttpContext.RequestServices.GetRequiredService<AuditoriaAccesos>()
                    .Registrar("API denegada", false, ctx.HttpContext.Request.Path.Value);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
            return await next(ctx);
        });
}
