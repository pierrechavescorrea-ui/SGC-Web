using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SigWeb.Data.Seguridad;

// Proyecto del scope actual (lo toma del AmbienteSig al crearse). AppDbContext usa su cadena de conexión.
public sealed class ContextoProyecto
{
    private readonly ProyectoActivo _inicial;
    private readonly ServicioProyectos _proyectos;

    public ProyectoActivo Proyecto => _proyectos.Activo(_inicial.Id)
        ?? throw new PermisoDenegadoException("El proyecto ya no está disponible.");

    public ContextoProyecto(ServicioProyectos proyectos, IHostEnvironment env, ILogger<ContextoProyecto> log)
    {
        var p = AmbienteSig.Actual?.Proyecto;
        if (p is null)
        {
            // Sin sesión ni tarea del sistema no se sabe a qué base ir. En desarrollo se usa el piloto (y se avisa)
            // para no tumbar la pantalla; en producción es un error: nunca se adivina el proyecto.
            if (!env.IsDevelopment())
                throw new InvalidOperationException("No hay proyecto activo para abrir la base de datos.");
            p = proyectos.Piloto() ?? throw new InvalidOperationException("No hay proyectos configurados.");
            log.LogWarning("AppDbContext sin proyecto en el ambiente: se usa el piloto ({Codigo}).\n{Pila}", p.Codigo, Environment.StackTrace);
        }
        _proyectos = proyectos;
        _inicial = p;
    }
}

// Base de la Empresa para leer módulos compartidos (Direccionamiento estratégico, Certificaciones…) desde un proyecto.
// Solo lectura: cualquier SaveChanges sobre este contexto se rechaza.
public sealed class FabricaDbCompartida
{
    private readonly ServicioProyectos _proyectos;
    public FabricaDbCompartida(ServicioProyectos proyectos) { _proyectos = proyectos; }

    // null = el módulo es propio del proyecto actual (usar el AppDbContext de siempre).
    public AppDbContext? ParaModulo(string modulo)
    {
        var actual = AmbienteSig.Actual?.Proyecto;
        if (actual is null || actual.EsEmpresa) return null;
        var (_, fuente) = _proyectos.Modulo(actual.Id, modulo);
        if (fuente != FuenteModulo.Empresa) return null;
        var empresa = _proyectos.Empresa();
        if (empresa is null) return null;
        // La base de la Empresa puede no estar creada todavía (servidor nuevo, base central recién creada): se prepara aquí
        // una vez. Si no se puede, el módulo trabaja con la base del proyecto en vez de tumbar la página con un error 500.
        if (!_proyectos.Preparado(empresa.Id) && Task.Run(() => _proyectos.PrepararAsync(empresa.Id)).GetAwaiter().GetResult() is not null)
            return null;
        var o = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(empresa.CadenaConexion)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .AddInterceptors(new InterceptorSoloLectura()).Options;
        return new AppDbContext(o);
    }
}

public sealed class InterceptorSoloLectura : SaveChangesInterceptor
{
    private static InvalidOperationException Error() => new PermisoDenegadoException("Este módulo se comparte desde la Empresa: aquí es solo de consulta.");
    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r) => throw Error();
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default) => throw Error();
}

public sealed class PermisoDenegadoException : InvalidOperationException
{
    public PermisoDenegadoException(string mensaje) : base(mensaje) { }
}

// dotnet ef (migraciones de AppDbContext): sin sesión, usa DefaultConnection (user-secrets en desarrollo).
public sealed class AppDbContextDiseno : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cfg = ConfiguracionDiseno.Leer();
        var o = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cfg.GetConnectionString("DefaultConnection")).Options;
        return new AppDbContext(o);
    }
}

public sealed class SeguridadDbContextDiseno : IDesignTimeDbContextFactory<SeguridadDbContext>
{
    public SeguridadDbContext CreateDbContext(string[] args)
    {
        var cfg = ConfiguracionDiseno.Leer();
        var central = cfg.GetConnectionString("Central");
        if (string.IsNullOrWhiteSpace(central))
        {
            var b = new Npgsql.NpgsqlConnectionStringBuilder(cfg.GetConnectionString("DefaultConnection"));
            b.Database = (b.Database ?? "sigweb") + "_central";
            central = b.ConnectionString;
        }
        // Mismo esquema de Identity que en ejecución (Version3 de .NET 10, con tabla de llaves de acceso / passkeys):
        // sin esto la migración sale con el esquema 1 y al arrancar EF reclama "cambios pendientes del modelo".
        var sp = new ServiceCollection()
            .Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(o => o.Stores.SchemaVersion = ArranqueSeguridad.EsquemaIdentidad)
            .BuildServiceProvider();
        return new SeguridadDbContext(new DbContextOptionsBuilder<SeguridadDbContext>().UseNpgsql(central).UseApplicationServiceProvider(sp).Options);
    }
}

internal static class ConfiguracionDiseno
{
    public static IConfiguration Leer()
    {
        // Igual que Program.cs (línea 5): sin este interruptor las fechas salen "timestamp with time zone" en las migraciones
        // y al arrancar EF reclama cambios pendientes del modelo.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var entorno = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{entorno}.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
