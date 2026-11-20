using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Formularios;

// Diseños de formulario (versión vigente en caché) y estructura declarada por el código.
// Singleton: cada operación abre su propio scope (los formularios lo llaman durante el render de la página).
// Si las tablas aún no existen (migración DisenoFormularios sin aplicar) todo devuelve "sin diseño" y los
// formularios se ven como siempre: nada se rompe.
public class ServicioFormularios
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ConcurrentDictionary<string, Diseno?> _disenos = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _estructuras = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<Dictionary<string, Estructura>> _semilla;
    private readonly ConcurrentDictionary<int, bool> _sinTablasPorProyecto = new();
    private bool _sinTablas
    {
        get => _sinTablasPorProyecto.ContainsKey(SigWeb.Data.Seguridad.AmbienteSig.ProyectoId ?? 0);
        set { if (value) _sinTablasPorProyecto[SigWeb.Data.Seguridad.AmbienteSig.ProyectoId ?? 0] = true; }
    }

    // Avisa a los formularios abiertos que su diseño cambió (se repintan sin recargar la página).
    public event Action<string>? Cambio;

    public ServicioFormularios(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
        _semilla = new Lazy<Dictionary<string, Estructura>>(LeerSemilla);
    }

    public bool Disponible => !_sinTablas;

    // Cada proyecto tiene sus diseños: la caché se separa por proyecto.
    private static string K(string clave) => SigWeb.Data.Seguridad.AmbienteSig.Clave(clave);

    public async Task<Diseno?> DisenoAsync(string clave)
    {
        if (_disenos.TryGetValue(K(clave), out var d)) return d;
        if (_sinTablas) return null;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var json = await db.DisenosFormulario.AsNoTracking().Where(x => x.Clave == clave && x.Vigente)
                .OrderByDescending(x => x.Version).Select(x => x.Json).FirstOrDefaultAsync();
            d = DisenoModelo.DeJson<Diseno>(json);
            if (d is { Original: true }) d = null;
            _disenos[K(clave)] = d;
            return d;
        }
        catch (Exception ex) { Falla(ex); return null; }
    }

    // Estructura conocida: la última que pintó el formulario o, si nunca se abrió, la semilla generada del código.
    public async Task<Estructura?> EstructuraAsync(string clave)
    {
        if (_estructuras.TryGetValue(K(clave), out var j)) return DisenoModelo.DeJson<Estructura>(j);
        if (!_sinTablas)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var json = await db.EstructurasFormulario.AsNoTracking().Where(x => x.Clave == clave).Select(x => x.Json).FirstOrDefaultAsync();
                if (json is not null) { _estructuras[K(clave)] = json; return DisenoModelo.DeJson<Estructura>(json); }
            }
            catch (Exception ex) { Falla(ex); }
        }
        return _semilla.Value.TryGetValue(clave, out var s) ? s : null;
    }

    // El formulario informa lo que declaró al pintarse; solo se escribe si cambió.
    public async Task RegistrarEstructuraAsync(string clave, Estructura e)
    {
        var json = DisenoModelo.AJson(e);
        if (_estructuras.TryGetValue(K(clave), out var previo) && previo == json) return;
        _estructuras[K(clave)] = json;
        if (_sinTablas) return;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fila = await db.EstructurasFormulario.FirstOrDefaultAsync(x => x.Clave == clave);
            if (fila is null) db.EstructurasFormulario.Add(new EstructuraFormulario { Clave = clave, Json = json });
            else if (fila.Json != json) { fila.Json = json; fila.Actualizado = DateTime.Now; }
            else return;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { Falla(ex); }
    }

    // Guarda una versión nueva (queda vigente). Original = volver al formulario del código.
    public async Task<int> GuardarAsync(string clave, Diseno d, string? motivo)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var version = (await db.DisenosFormulario.Where(x => x.Clave == clave).MaxAsync(x => (int?)x.Version) ?? 0) + 1;
        foreach (var v in await db.DisenosFormulario.Where(x => x.Clave == clave && x.Vigente).ToListAsync()) v.Vigente = false;
        db.DisenosFormulario.Add(new DisenoFormulario
        {
            Clave = clave, Version = version, Json = DisenoModelo.AJson(d), Vigente = true,
            Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()
        });
        await db.SaveChangesAsync();
        _disenos[K(clave)] = d.Original ? null : d;
        Cambio?.Invoke(clave);
        return version;
    }

    public async Task<List<DisenoFormulario>> HistorialAsync(string clave)
    {
        if (_sinTablas) return new();
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.DisenosFormulario.AsNoTracking().Where(x => x.Clave == clave).OrderByDescending(x => x.Version).ToListAsync();
        }
        catch (Exception ex) { Falla(ex); return new(); }
    }

    // Versión de cada formulario con diseño propio (para marcar la lista del diseñador).
    public async Task<Dictionary<string, int>> VersionesVigentesAsync()
    {
        if (_sinTablas) return new();
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var l = await db.DisenosFormulario.AsNoTracking().Where(x => x.Vigente).Select(x => new { x.Clave, x.Version, x.Json }).ToListAsync();
            return l.Where(x => DisenoModelo.DeJson<Diseno>(x.Json) is { Original: false }).ToDictionary(x => x.Clave, x => x.Version, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) { Falla(ex); return new(); }
    }

    private void Falla(Exception ex)
    {
        // Tabla inexistente (42P01) = migración pendiente: se deja de intentar hasta reiniciar.
        var interna = ex; while (interna.InnerException is not null) interna = interna.InnerException;
        if (interna.Message.Contains("42P01") || interna.Message.Contains("does not exist") || interna.Message.Contains("no existe"))
            _sinTablas = true;
        Console.Error.WriteLine("Diseño de formularios: " + interna.Message);
    }

    private static Dictionary<string, Estructura> LeerSemilla()
    {
        try
        {
            using var s = typeof(ServicioFormularios).Assembly.GetManifestResourceStream("SigWeb.Data.Formularios.estructuras-semilla.json");
            if (s is null) return new();
            using var r = new StreamReader(s);
            return DisenoModelo.DeJson<Dictionary<string, Estructura>>(r.ReadToEnd()) ?? new();
        }
        catch { return new(); }
    }
}
