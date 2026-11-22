using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace SigWeb.Data.Seguridad;

// Proyectos (inquilinos): lista en caché, cadena de conexión de cada base, módulos habilitados y
// preparación de la base (crearla, migrarla y sembrar los catálogos de fábrica).
public sealed partial class ServicioProyectos
{
    private readonly IConfiguration _cfg;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ServicioProyectos> _log;
    private readonly IHostEnvironment _env;
    private readonly object _lock = new();
    private volatile Cache? _cache;
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _preparando = new();
    private readonly ConcurrentDictionary<int, bool> _preparados = new();

    public ServicioProyectos(IConfiguration cfg, IServiceScopeFactory scopes, ILogger<ServicioProyectos> log, IHostEnvironment env)
    {
        _cfg = cfg; _scopes = scopes; _log = log; _env = env;
    }

    private sealed record Cache(IReadOnlyList<Proyecto> Proyectos, Dictionary<int, ProyectoActivo> Activos,
        Dictionary<(int, string), ModuloProyecto> Modulos);

    // ---------- Cadenas de conexión ----------

    // Plantilla del servidor y credenciales: DefaultConnection (user-secrets en desarrollo, variable de entorno en AWS).
    public string Plantilla => _cfg.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection.");

    public string BasePiloto => new NpgsqlConnectionStringBuilder(Plantilla).Database ?? "sigweb";

    public string CadenaCentral()
    {
        var c = _cfg.GetConnectionString("Central");
        if (!string.IsNullOrWhiteSpace(c)) return c;
        var b = new NpgsqlConnectionStringBuilder(Plantilla) { Database = BasePiloto + "_central" };
        return b.ConnectionString;
    }

    public string CadenaDe(string codigo, string baseDatos)
    {
        // Una base en otro servidor: ConnectionStrings:Proyecto_<CODIGO> (variable ConnectionStrings__Proyecto_<CODIGO>).
        var propia = _cfg.GetConnectionString("Proyecto_" + codigo);
        if (!string.IsNullOrWhiteSpace(propia)) return propia;
        return new NpgsqlConnectionStringBuilder(Plantilla) { Database = baseDatos }.ConnectionString;
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{1,62}$")] private static partial Regex RxBase();
    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{1,29}$")] private static partial Regex RxCodigo();

    public static bool NombreBaseValido(string n) => RxBase().IsMatch(n);
    public static bool CodigoValido(string c) => RxCodigo().IsMatch(c);

    // ---------- Caché ----------

    private Cache Datos
    {
        get
        {
            var c = _cache;
            if (c is not null) return c;
            lock (_lock)
            {
                if (_cache is not null) return _cache;
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
                var lista = db.Proyectos.AsNoTracking().Include(p => p.Modulos).OrderBy(p => p.EsEmpresa ? 0 : 1).ThenBy(p => p.Nombre).ToList();
                var activos = lista.ToDictionary(p => p.Id, p => new ProyectoActivo(p.Id, p.Codigo, p.Nombre, p.BaseDatos,
                    CadenaDe(p.CodigoConexion ?? p.Codigo, p.BaseDatos), p.EsEmpresa, p.EsPiloto, p.Color));
                var modulos = lista.SelectMany(p => p.Modulos).ToDictionary(m => (m.ProyectoId, m.Modulo.ToLowerInvariant()));
                return _cache = new Cache(lista, activos, modulos);
            }
        }
    }

    public void Invalidar() { lock (_lock) _cache = null; }

    public IReadOnlyList<Proyecto> Todos() => Datos.Proyectos;

    public ProyectoActivo? Activo(int id) => Datos.Activos.TryGetValue(id, out var p) ? p : null;

    public Proyecto? Proyecto(int id) => Datos.Proyectos.FirstOrDefault(p => p.Id == id);

    public ProyectoActivo? Empresa() => Datos.Proyectos.FirstOrDefault(p => p.EsEmpresa) is { } e ? Activo(e.Id) : null;

    public ProyectoActivo? Piloto() =>
        (Datos.Proyectos.FirstOrDefault(p => p.EsPiloto) ?? Datos.Proyectos.FirstOrDefault(p => !p.EsEmpresa)) is { } p ? Activo(p.Id) : null;

    // Módulo en un proyecto: habilitado y de dónde salen sus datos (sin fila = valores del catálogo).
    public (bool Habilitado, FuenteModulo Fuente) Modulo(int proyectoId, string modulo)
    {
        var def = CatalogoModulos.De(modulo);
        var p = Proyecto(proyectoId);
        if (Datos.Modulos.TryGetValue((proyectoId, modulo.ToLowerInvariant()), out var m))
            return (m.Habilitado, p?.EsEmpresa == true ? FuenteModulo.Propio : m.Fuente);
        if (def is null) return (true, FuenteModulo.Propio);
        if (p?.EsEmpresa == true) return (def.EnEmpresa, FuenteModulo.Propio);
        return (def.EnProyecto, def.CompartidoDesdeEmpresa && Empresa() is not null ? FuenteModulo.Empresa : FuenteModulo.Propio);
    }

    // ---------- Base de datos de un proyecto ----------

    public AppDbContext CrearContexto(ProyectoActivo p, bool paraMigrar = false)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(p.CadenaConexion);
        // Migrar con un modelo que adelanta a las migraciones no debe tumbar el arranque (EF 9+ lo vuelve error).
        if (paraMigrar) b.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        return new AppDbContext(b.Options);
    }

    public bool Preparado(int id) => _preparados.ContainsKey(id);

    // Crea la base si no existe, aplica TODAS las migraciones pendientes de AppDbContext (base nueva o existente; antes de
    // cualquier paso de arranque, que ya consulta columnas nuevas) y siembra los catálogos de fábrica. Una vez por proceso y
    // por base; segura de repetir. Multiproyecto:MigrarAlPreparar = false solo apaga la migración de bases que ya tienen tablas.
    public async Task<string?> PrepararAsync(int proyectoId, CancellationToken ct = default)
    {
        var p = Activo(proyectoId);
        if (p is null) return "El proyecto no existe.";
        if (_preparados.ContainsKey(proyectoId)) return null;
        var candado = _preparando.GetOrAdd(proyectoId, _ => new SemaphoreSlim(1, 1));
        // Si otra preparación de la misma base sigue en curso, no se espera indefinidamente.
        if (!await candado.WaitAsync(TimeSpan.FromMinutes(10), ct).ConfigureAwait(false))
            return "La base del proyecto se está preparando; intente de nuevo en unos minutos.";
        try
        {
            if (_preparados.ContainsKey(proyectoId)) return null;
            await using (var db = CrearContexto(p, paraMigrar: true))
            {
                var creador = db.GetService<IRelationalDatabaseCreator>();
                var existe = await creador.ExistsAsync(ct);
                var conTablas = existe && await creador.HasTablesAsync(ct);
                var migrarExistentes = _cfg.GetValue("Multiproyecto:MigrarAlPreparar", true);
                var pendientes = !conTablas ? db.Database.GetMigrations().ToList()
                    : (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
                if (!existe || !conTablas || (migrarExistentes && pendientes.Count > 0))
                {
                    _log.LogInformation("Proyecto {Codigo}: aplicando {N} migración(es) en la base {Base}.", p.Codigo, pendientes.Count, p.BaseDatos);
                    db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
                    await db.Database.MigrateAsync(ct);
                }
                else if (pendientes.Count > 0)
                    _log.LogWarning("Proyecto {Codigo}: {N} migración(es) sin aplicar (Multiproyecto:MigrarAlPreparar = false).", p.Codigo, pendientes.Count);
                // El modelo va por delante de las migraciones (falta generar una): las columnas nuevas no existirán en ninguna base.
                if (db.Database.HasPendingModelChanges())
                    _log.LogWarning("El modelo de AppDbContext tiene cambios sin migración: genere una con " +
                        "'dotnet ef migrations add <Nombre> --context AppDbContext' y reinicie la app.");
            }
            // Los sembradores son síncronos y alguno espera tareas (.GetResult()). Llamado desde un circuito de Blazor
            // (Administración › Preparar base) eso bloqueaba el contexto de sincronización y el candado quedaba tomado:
            // cambiar a ese proyecto se colgaba para siempre. Se corren en el grupo de hilos, sin contexto.
            await Task.Run(() =>
            {
                using (AmbienteSig.Usar(new SesionSig { Proyecto = p, Sistema = true }))
                {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    ArranqueProyecto.Ejecutar(db, _cfg, scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>(), p);
                }
            }, ct).ConfigureAwait(false);
            _preparados[proyectoId] = true;
            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo preparar la base del proyecto {Codigo}", p.Codigo);
            return "No se pudo preparar la base del proyecto. Consulte al administrador.";
        }
        finally { candado.Release(); }
    }

    public async Task<(bool Conecta, int Pendientes, string? Error)> EstadoBaseAsync(ProyectoActivo p, CancellationToken ct = default)
    {
        try
        {
            await using var db = CrearContexto(p);
            if (!await db.Database.CanConnectAsync(ct)) return (false, 0, "Sin conexión");
            var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
            return (true, pendientes, null);
        }
        catch (Exception ex) { return (false, 0, ex.GetBaseException().Message); }
    }

    // Aplica las migraciones pendientes de la base de un proyecto (Administración › Sistema).
    public async Task<string?> MigrarAsync(int proyectoId, CancellationToken ct = default)
    {
        var p = Activo(proyectoId);
        if (p is null) return "El proyecto no existe.";
        try
        {
            await using var db = CrearContexto(p, paraMigrar: true);
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
            await db.Database.MigrateAsync(ct);
            _log.LogInformation("Proyecto {Codigo}: migraciones aplicadas desde Administración.", p.Codigo);
            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo migrar la base del proyecto {Codigo}", p.Codigo);
            return ex.GetBaseException().Message;
        }
    }
}
