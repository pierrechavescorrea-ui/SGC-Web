using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SigWeb.Data.Seguridad;

// Registro de servicios y arranque de la capa multiproyecto + seguridad.
public static class ArranqueSeguridad
{
    public const string EmailDesarrollo = "desarrollador@sigweb.local";
    // Desarrollo: la primera pantalla es el login; el acceso libre solo se activa con «entrar sin contraseña» (cookie de sesión).
    public const string CookieAccesoLibre = "sig.dev.acceso";
    // Identificador de este arranque de la app: la cookie de acceso libre y la marca de desarrollo de la sesión lo llevan;
    // al reiniciar la app dejan de valer y la primera pantalla vuelve a ser el login (aunque el navegador restaure cookies).
    public static readonly string IdArranque = Guid.NewGuid().ToString("N");
    public const string PoliticaAdmin = "AdminSGC";
    public const string PoliticaAuditoria = "AuditoriaSGC";

    // Esquema de las tablas de Identity (fijo: la fábrica de diseño de migraciones usa el mismo).
    public static readonly Version EsquemaIdentidad = Microsoft.AspNetCore.Identity.IdentitySchemaVersions.Version3;

    // Política de contenido completa (Seguridad:Csp la reemplaza; Seguridad:CspSoloReporte = true la deja solo en reporte).
    // Exigida: sin scripts en línea ni CDNs (todas las librerías van en wwwroot/lib; manejadores con data-accion,
    // js/acciones.js). Estilos en línea sí (Blazor y los componentes usan style="…"). Teselas e imágenes por https
    // (Esri, AWS terrarium, OpenStreetMap), Google Fonts y WebAssembly (web-ifc, Cesium). Los avisos llegan a /csp-reporte.
    public const string CspPorDefecto =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "img-src 'self' data: blob: https:; " +
        "connect-src 'self' wss: https:; " +
        "worker-src 'self' blob:; " +
        "frame-src 'self' blob:; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";

    public static string CadenaCentral(IConfiguration cfg)
    {
        var c = cfg.GetConnectionString("Central");
        if (!string.IsNullOrWhiteSpace(c)) return c;
        var b = new NpgsqlConnectionStringBuilder(cfg.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=sigweb_dev;Username=postgres");
        b.Database = (b.Database ?? "sigweb") + "_central";
        return b.ConnectionString;
    }

    public static void AddSeguridadSigWeb(this WebApplicationBuilder builder)
    {
        var s = builder.Services;
        var cfg = builder.Configuration;
        var dev = builder.Environment.IsDevelopment();

        builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

        s.AddDbContext<SeguridadDbContext>(o => o.UseNpgsql(CadenaCentral(cfg)));
        s.AddSingleton<ServicioProyectos>();
        s.AddSingleton<ServicioPermisos>();
        s.AddSingleton<AuditoriaAccesos>();
        s.AddHostedService(sp => sp.GetRequiredService<AuditoriaAccesos>());
        s.AddSingleton<EnviadorCorreo>();
        s.AddSingleton<InterceptorEscritura>();
        s.AddSingleton<VerComoTokens>();
        s.AddScoped<ContextoProyecto>();
        s.AddScoped<FabricaDbCompartida>();
        s.AddScoped<ServicioInvitaciones>();
        s.AddScoped<UsuarioActual>();
        s.AddScoped<ServicioPreferenciasUsuario>();
        s.AddScoped<CircuitHandler, CircuitoSeguridad>();

        // Base de cada proyecto: la del proyecto activo del scope (ContextoProyecto ← AmbienteSig).
        s.AddDbContext<AppDbContext>((sp, o) =>
        {
            o.UseNpgsql(sp.GetRequiredService<ContextoProyecto>().Proyecto.CadenaConexion);
            o.AddInterceptors(sp.GetRequiredService<InterceptorEscritura>());
        });

        // ---------- Identity ----------
        s.AddCascadingAuthenticationState();
        s.AddScoped<AuthenticationStateProvider, ProveedorEstadoSig>();
        s.AddAuthentication(o =>
        {
            o.DefaultScheme = IdentityConstants.ApplicationScheme;
            o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();

        s.AddIdentityCore<UsuarioSig>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedAccount = false;   // la cuenta solo nace desde una invitación al correo
                o.Password.RequiredLength = 12;
                o.Password.RequiredUniqueChars = 5;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = cfg.GetValue("Seguridad:IntentosAntesDeBloquear", 5);
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(cfg.GetValue("Seguridad:MinutosBloqueo", 15));
                o.Tokens.AuthenticatorIssuer = "SGC Web";
                o.Stores.SchemaVersion = EsquemaIdentidad;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SeguridadDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<FabricaClaimsSig>()
            .AddErrorDescriber<ErroresIdentidadEs>()
            .AddPasswordValidator<ValidadorContrasenaSig>();
        // Enlaces de restablecer contraseña: 2 horas.
        s.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

        s.ConfigureApplicationCookie(o =>
        {
            o.LoginPath = "/cuenta/ingresar";
            o.LogoutPath = "/cuenta/salir";
            o.AccessDeniedPath = "/acceso-denegado";
            o.Cookie.Name = dev ? "sigweb.sesion" : "__Host-sigweb";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = dev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromHours(cfg.GetValue("Seguridad:HorasSesion", 10));
            o.SlidingExpiration = true;
        });
        s.Configure<SecurityStampValidatorOptions>(o =>
        {
            o.ValidationInterval = TimeSpan.FromMinutes(5);
            // Al renovar la cookie se conservan "ver como" y la marca de desarrollo.
            o.OnRefreshingPrincipal = c =>
            {
                var id = c.NewPrincipal?.Identities.FirstOrDefault();
                if (id is null || c.CurrentPrincipal is null) return Task.CompletedTask;
                foreach (var tipo in new[] { ClaimsSig.Impersonador, ClaimsSig.Desarrollo, "amr" })
                    foreach (var claim in c.CurrentPrincipal.FindAll(tipo))
                        if (!id.HasClaim(tipo, claim.Value)) id.AddClaim(new Claim(tipo, claim.Value));
                return Task.CompletedTask;
            };
        });

        s.AddAuthorizationBuilder()
            // Todo exige sesión salvo lo marcado [AllowAnonymous] (login, invitación, privacidad, salud).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(PoliticaAdmin, p => p.RequireRole(RolesFabrica.AdministradorGlobal))
            .AddPolicy(PoliticaAuditoria, p => p.RequireRole(RolesFabrica.AdministradorGlobal, RolesFabrica.AuditorGlobal));

        // ---------- Data Protection (cookies y tokens) en la base central: sirve con varias instancias ----------
        s.AddDataProtection().SetApplicationName("SigWeb").PersistKeysToDbContext<SeguridadDbContext>();

        s.AddAntiforgery(o =>
        {
            o.Cookie.SecurePolicy = dev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
        });

        // ---------- Detrás del balanceador de AWS (ALB): IP y esquema reales ----------
        s.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Un salto = solo el ALB. Con más saltos que proxies reales, un atacante podría falsear su IP en
            // X-Forwarded-For y esquivar el límite de intentos (Proxy:Saltos = 2 solo si hay CloudFront + ALB).
            o.ForwardLimit = Math.Clamp(cfg.GetValue("Proxy:Saltos", 1), 1, 3);
            // Solo cuando la app no es alcanzable sin pasar por el balanceador (grupo de seguridad de AWS).
            if (cfg.GetValue<bool>("Proxy:ConfiarEnCualquierOrigen"))
            {
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            }
            foreach (var red in (cfg["Proxy:Redes"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (System.Net.IPNetwork.TryParse(red, out var n)) o.KnownIPNetworks.Add(n);
        });
        s.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = false; });

        // ---------- Límite de peticiones (fuerza bruta, abuso de correo, raspado de la API) ----------
        s.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (c, ct) =>
            {
                c.HttpContext.RequestServices.GetRequiredService<AuditoriaAccesos>()
                    .Registrar(c.HttpContext, "Límite de peticiones", false,
                        c.HttpContext.Request.Path.StartsWithSegments("/api/dossier") ? "/api/dossier" : c.HttpContext.Request.Path.Value);
                c.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await c.HttpContext.Response.WriteAsync("Demasiados intentos. Espere unos minutos e intente de nuevo.", ct);
            };
            o.AddPolicy("dossier", http =>
                RateLimitPartition.GetConcurrencyLimiter(http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                    _ => new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 }));
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "?";
                var path = http.Request.Path;
                if (HttpMethods.IsPost(http.Request.Method) && (path.StartsWithSegments("/cuenta") || path.Equals("/mi-cuenta")))
                    return RateLimitPartition.GetFixedWindowLimiter("cuenta|" + ip, _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = cfg.GetValue("Seguridad:PostsCuentaPorMinuto", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
                if (path.StartsWithSegments("/api") || path.StartsWithSegments("/csp-reporte"))
                    return RateLimitPartition.GetSlidingWindowLimiter("api|" + (http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ip),
                        _ => new SlidingWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 });
                return RateLimitPartition.GetNoLimiter("libre");
            });
        });

        s.AddHealthChecks()
            .AddDbContextCheck<SeguridadDbContext>("base-central", tags: new[] { "listo" })
            .AddCheck<SaludProyectos>("bases-proyectos", tags: new[] { "listo" });
    }

    // ---------- Arranque: base central, roles, proyectos y bases de proyecto ----------
    public static async Task InicializarAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SigWeb.Seguridad");
        var db = sp.GetRequiredService<SeguridadDbContext>();
        var cfg = app.Configuration;

        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "Falta la migración de la base central (usuarios y permisos). En la carpeta del proyecto ejecute:\n" +
                "  dotnet ef migrations add SeguridadInicial --context SeguridadDbContext -o Migrations/Seguridad\n" +
                "y vuelva a iniciar la app (la base central se crea y migra sola al arrancar).");
        await db.Database.MigrateAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in new[] { RolesFabrica.AdministradorGlobal, RolesFabrica.AuditorGlobal })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        await SembrarRolesAsync(db);
        await SembrarProyectosAsync(db, sp.GetRequiredService<ServicioProyectos>(), cfg, log);

        var um = sp.GetRequiredService<UserManager<UsuarioSig>>();
        if (app.Environment.IsDevelopment())
        {
            var devu = await um.FindByEmailAsync(EmailDesarrollo);
            if (devu is null)
            {
                devu = new UsuarioSig { UserName = EmailDesarrollo, Email = EmailDesarrollo, EmailConfirmed = true, NombreCompleto = "Desarrollador",
                    AutorizoDatosEn = DateTime.UtcNow, VersionPolitica = Privacidad.Version };
                await um.CreateAsync(devu);
            }
            if (!await um.IsInRoleAsync(devu, RolesFabrica.AdministradorGlobal)) await um.AddToRoleAsync(devu, RolesFabrica.AdministradorGlobal);
        }
        else
        {
            // Fuera de desarrollo el usuario de desarrollo no puede existir activo.
            var devu = await um.FindByEmailAsync(EmailDesarrollo);
            if (devu is not null && devu.Activo) { devu.Activo = false; await um.UpdateAsync(devu); await um.UpdateSecurityStampAsync(devu); }
        }

        await AdministradorInicialAsync(sp, cfg, log);

        // Bases de los proyectos: el piloto siempre; los demás al arrancar fuera de desarrollo (en desarrollo, al entrar).
        var proyectos = sp.GetRequiredService<ServicioProyectos>();
        proyectos.Invalidar();
        foreach (var p in proyectos.Todos().Where(p => p.Estado == EstadoProyecto.Activo))
        {
            if (app.Environment.IsDevelopment() && !p.EsPiloto) continue;
            var error = await proyectos.PrepararAsync(p.Id);
            if (error is not null) log.LogError("Proyecto {Codigo}: {Error}", p.Codigo, error);
        }
    }

    private static async Task SembrarRolesAsync(SeguridadDbContext db)
    {
        var existentes = await db.RolesSig.Include(r => r.Permisos).ToListAsync();
        foreach (var def in RolesFabrica.Lista)
        {
            var rol = existentes.FirstOrDefault(r => r.Clave == def.Clave);
            if (rol is null)
            {
                rol = new RolSig { Clave = def.Clave, Nombre = def.Nombre, Descripcion = def.Descripcion, Orden = def.Orden, DeFabrica = true };
                db.RolesSig.Add(rol);
                existentes.Add(rol);
            }
            // Módulos nuevos del catálogo: se agregan con el valor de fábrica (lo ya editado no se toca).
            foreach (var m in CatalogoModulos.Lista)
                if (!rol.Permisos.Any(p => p.Modulo == m.Clave))
                    rol.Permisos.Add(new PermisoRol { Modulo = m.Clave, Acciones = RolesFabrica.Permiso(def.Clave, m) });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SembrarProyectosAsync(SeguridadDbContext db, ServicioProyectos servicio, IConfiguration cfg, ILogger log)
    {
        if (await db.Proyectos.AnyAsync()) return;
        // Primera vez: la base de siempre pasa a ser el proyecto piloto; la Empresa tendrá su propia base.
        var piloto = servicio.BasePiloto;
        db.Proyectos.Add(new Proyecto
        {
            Codigo = (cfg["Multiproyecto:CodigoPiloto"] ?? "PILOTO").ToUpperInvariant(),
            Nombre = cfg["Multiproyecto:NombrePiloto"] ?? "Proyecto piloto",
            BaseDatos = piloto, EsPiloto = true, Color = "#EC1C24",
        });
        db.Proyectos.Add(new Proyecto
        {
            Codigo = "EMPRESA", Nombre = cfg["Multiproyecto:NombreEmpresa"] ?? "Empresa · SGC corporativo",
            BaseDatos = (piloto + "_empresa").ToLowerInvariant(), EsEmpresa = true, Color = "#4b5563",
            Descripcion = "Módulos de nivel empresa que se comparten con los proyectos (Direccionamiento estratégico, Certificaciones, Manuales de funciones).",
        });
        await db.SaveChangesAsync();
        log.LogInformation("Multiproyecto: se crearon el proyecto piloto (base {Base}) y el espacio de la Empresa.", piloto);
    }

    // Primer administrador fuera de desarrollo: Seguridad:AdministradorInicial = correo. Si aún no hay ningún
    // administrador, se crea una invitación (correo + enlace en el registro de la app, una sola vez).
    private static async Task AdministradorInicialAsync(IServiceProvider sp, IConfiguration cfg, ILogger log)
    {
        var email = cfg["Seguridad:AdministradorInicial"];
        if (string.IsNullOrWhiteSpace(email)) return;
        var um = sp.GetRequiredService<UserManager<UsuarioSig>>();
        var admins = await um.GetUsersInRoleAsync(RolesFabrica.AdministradorGlobal);
        if (admins.Any(a => a.Activo && a.Email != EmailDesarrollo)) return;
        var db = sp.GetRequiredService<SeguridadDbContext>();
        var norm = ServicioInvitaciones.NormalizarEmail(email);
        if (await db.Invitaciones.AnyAsync(i => i.Email == norm && i.Estado == EstadoInvitacion.Pendiente && i.Vence > DateTime.UtcNow)) return;
        var inv = sp.GetRequiredService<ServicioInvitaciones>();
        var r = await inv.InvitarAsync(norm, null, null, null, true, null, "Primer administrador de SGC Web.", "sistema", inv.UrlBase("http://localhost:5265"));
        log.LogWarning("Invitación del administrador inicial creada para {Email}. Enlace (un solo uso): {Enlace}", norm, r.Enlace);
    }
}

// Estado de las bases de los proyectos para /salud/listo.
public sealed class SaludProyectos : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly ServicioProyectos _p;
    public SaludProyectos(ServicioProyectos p) { _p = p; }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken ct = default)
    {
        var fallas = new List<string>();
        foreach (var p in _p.Todos().Where(p => p.Estado == EstadoProyecto.Activo && _p.Preparado(p.Id)))
            if (_p.Activo(p.Id) is { } a && !(await _p.EstadoBaseAsync(a, ct)).Conecta) fallas.Add(p.Codigo);
        return fallas.Count == 0
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Sin conexión: " + string.Join(", ", fallas));
    }
}
