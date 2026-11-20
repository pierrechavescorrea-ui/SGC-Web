using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;

namespace SigWeb.Data.Seguridad;

// Circuito de Blazor (una pestaña interactiva): en CADA actividad (inicio, clic, interop, navegación) fija el
// AmbienteSig con el proyecto y el usuario del circuito y permisos frescos (caché de 2 min). Patrón de
// Microsoft para servicios fuera del scope del circuito ("CircuitServicesAccessor" con AsyncLocal).
public sealed class CircuitoSeguridad : CircuitHandler
{
    private readonly AuthenticationStateProvider _auth;
    private readonly ServicioProyectos _proyectos;
    private readonly ServicioPermisos _permisos;
    private readonly NavigationManager _nav;
    private readonly IConfiguration _cfg;
    private bool _expulsado;

    public CircuitoSeguridad(AuthenticationStateProvider auth, ServicioProyectos proyectos, ServicioPermisos permisos, NavigationManager nav, IConfiguration cfg)
    {
        _auth = auth; _proyectos = proyectos; _permisos = permisos; _nav = nav; _cfg = cfg;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
        async contexto =>
        {
            var previa = AmbienteSig.Actual;
            var sesion = await SesionAsync();
            if (sesion is null)
            {
                // Cuenta desactivada, acceso al proyecto retirado o sesión vencida: fuera del circuito.
                if (!_expulsado)
                {
                    _expulsado = true;
                    try { _nav.NavigateTo("/cuenta/ingresar", forceLoad: true); } catch { }
                }
                return;
            }
            AmbienteSig.Actual = sesion;
            try { await next(contexto); }
            finally { AmbienteSig.Actual = previa; }
        };

    private async Task<SesionSig?> SesionAsync()
    {
        var estado = await _auth.GetAuthenticationStateAsync();
        if (MiddlewareSeguridad.RequiereAltaMfa(estado.User, _permisos, _cfg)) return null;
        return ConstruirSesion(estado.User, _proyectos, _permisos, Modulo());
    }

    private string? Modulo()
    {
        try { return CatalogoModulos.ClaveDeRuta(_nav.ToBaseRelativePath(_nav.Uri)); }
        catch { return null; }
    }

    // Común al circuito y a las peticiones HTTP.
    public static SesionSig? ConstruirSesion(ClaimsPrincipal user, ServicioProyectos proyectos, ServicioPermisos permisos,
        string? modulo, string? ip = null)
    {
        if (user.Identity?.IsAuthenticated != true) return null;
        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (uid is null) return null;
        if (!int.TryParse(user.FindFirstValue(ClaimsSig.Proyecto), out var pid)) return null;
        var proyecto = proyectos.Activo(pid);
        if (proyecto is null) return null;
        var p = permisos.De(uid, pid);
        if (!p.EsMiembro) return null;
        return new SesionSig
        {
            Proyecto = proyecto, Usuario = user, UsuarioId = uid, Email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name,
            Permisos = p, Modulo = modulo, Ip = ip, Impersonador = user.FindFirstValue(ClaimsSig.Impersonador),
        };
    }
}

// Revalida la sesión de los circuitos cada 5 minutos: sello de seguridad (cambio de contraseña, "cerrar todas
// las sesiones", bloqueo) y usuario activo. Si falla, el circuito queda sin usuario y CircuitoSeguridad lo saca.
public sealed class ProveedorEstadoSig : Microsoft.AspNetCore.Components.Server.RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopes;
    public ProveedorEstadoSig(ILoggerFactory log, IServiceScopeFactory scopes) : base(log) { _scopes = scopes; }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState estado, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioSig>>();
        var u = await um.GetUserAsync(estado.User);
        if (u is null || !u.Activo || (u.LockoutEnd is { } le && le > DateTimeOffset.UtcNow)) return false;
        if (!um.SupportsUserSecurityStamp) return true;
        var sello = estado.User.FindFirstValue(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType);
        return sello == await um.GetSecurityStampAsync(u);
    }
}
