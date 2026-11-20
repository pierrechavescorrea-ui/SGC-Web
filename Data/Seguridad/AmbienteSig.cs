using System.Security.Claims;

namespace SigWeb.Data.Seguridad;

// Proyecto (inquilino) con el que se trabaja: define la base de datos de AppDbContext.
public sealed record ProyectoActivo(int Id, string Codigo, string Nombre, string BaseDatos, string CadenaConexion,
    bool EsEmpresa, bool EsPiloto, string? Color);

// Permisos ya calculados de un usuario en un proyecto (módulo → acciones).
public sealed class PermisosEfectivos
{
    public static readonly PermisosEfectivos Ninguno = new(new Dictionary<string, Accion>(), false, false, null, false);

    private readonly IReadOnlyDictionary<string, Accion> _porModulo;

    public PermisosEfectivos(IReadOnlyDictionary<string, Accion> porModulo, bool adminGlobal, bool auditorGlobal, string? rol, bool miembro)
    {
        _porModulo = porModulo; AdminGlobal = adminGlobal; AuditorGlobal = auditorGlobal; Rol = rol; EsMiembro = miembro;
    }

    public bool AdminGlobal { get; }
    public bool AuditorGlobal { get; }
    public string? Rol { get; }
    // Tiene acceso al proyecto (miembro activo o rol global).
    public bool EsMiembro { get; }
    public IReadOnlyDictionary<string, Accion> PorModulo => _porModulo;

    public Accion De(string modulo) => _porModulo.TryGetValue(modulo, out var a) ? a : Accion.Ninguna;
    public bool Puede(string modulo, Accion accion) => (De(modulo) & accion) == accion && accion != Accion.Ninguna;
    public bool PuedeAlguna(string modulo, Accion acciones) => (De(modulo) & acciones) != 0;
    public bool PuedeEnAlguno(Accion accion) => _porModulo.Values.Any(a => (a & accion) == accion);
}

// Sesión de quien hace la operación en curso: proyecto, usuario, permisos y módulo de la pantalla.
// Viaja en un AsyncLocal (AmbienteSig): lo fija el middleware en cada petición HTTP, el CircuitHandler en
// cada actividad del circuito de Blazor y el bot de Telegram en cada mensaje. Así también lo ven los
// scopes propios que abren los servicios (IServiceScopeFactory.CreateScope) y las tareas en segundo plano.
public sealed class SesionSig
{
    public required ProyectoActivo Proyecto { get; init; }
    public ClaimsPrincipal? Usuario { get; init; }
    public string? UsuarioId { get; init; }
    public string? Email { get; init; }
    public PermisosEfectivos Permisos { get; init; } = PermisosEfectivos.Ninguno;
    public string? Modulo { get; set; }
    // Tareas internas (arranque, sembradores, bot): sin control de escritura por módulo.
    public bool Sistema { get; init; }
    public string? Ip { get; init; }
    public string? Impersonador { get; init; }
}

public static class AmbienteSig
{
    private static readonly AsyncLocal<SesionSig?> _actual = new();

    public static SesionSig? Actual
    {
        get => _actual.Value;
        set => _actual.Value = value;
    }

    public static int? ProyectoId => _actual.Value?.Proyecto.Id;

    // Clave de caché separada por proyecto (las cachés singleton no deben mezclar datos de dos proyectos).
    public static string Clave(string clave) => (_actual.Value?.Proyecto.Id ?? 0) + "|" + clave;

    // Ejecuta en nombre de un proyecto como tarea del sistema (arranque, bot, tareas programadas).
    public static IDisposable Usar(SesionSig sesion)
    {
        var previa = _actual.Value;
        _actual.Value = sesion;
        return new Restaurar(previa);
    }

    private sealed class Restaurar(SesionSig? previa) : IDisposable
    {
        public void Dispose() => _actual.Value = previa;
    }
}

public static class ClaimsSig
{
    public const string Proyecto = "sig:proyecto";
    public const string Nombre = "sig:nombre";
    public const string Impersonador = "sig:impersonador";
    public const string Desarrollo = "sig:desarrollo";
    public const string Foto = "sig:foto";   // versión de la foto de perfil (ticks); sin claim = sin foto
}
