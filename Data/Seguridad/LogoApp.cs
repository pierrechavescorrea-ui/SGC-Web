using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Logo de la app: UNO para todos los proyectos (los proyectos no pueden editarlo). Se guarda en la base de la Empresa
// (ConfiguracionEmpresa.LogoUrl, Id = 1; sin migración) y lo cambia solo el administrador general en Administración › Sistema. Respaldo: el logo que tenía la app piloto
// antes de esto. El archivo va a wwwroot/uploads/marca (no depende de un ícono de la galería de un proyecto).
// Singleton con caché de 5 minutos: el menú lo lee en cada página sin consultar la base.
public sealed class LogoApp
{
    private readonly ServicioProyectos _proyectos;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LogoApp> _log;
    private readonly object _candado = new();
    private string? _url;
    private DateTime _leido = DateTime.MinValue;

    public static readonly string[] Extensiones = { ".png", ".jpg", ".jpeg", ".webp" };
    public const long MaxBytes = 2 * 1024 * 1024;

    public LogoApp(ServicioProyectos proyectos, IWebHostEnvironment env, ILogger<LogoApp> log)
    { _proyectos = proyectos; _env = env; _log = log; }

    public string? Url
    {
        get
        {
            if ((DateTime.UtcNow - _leido).TotalMinutes < 5) return _url;
            lock (_candado)
            {
                if ((DateTime.UtcNow - _leido).TotalMinutes < 5) return _url;
                _url = Leer();
                _leido = DateTime.UtcNow;
                return _url;
            }
        }
    }

    // Empresa primero; si no tiene, el logo que usaba la app piloto (antes multiproyecto el logo era de esa base).
    private string? Leer()
    {
        foreach (var p in new[] { _proyectos.Empresa(), _proyectos.Piloto() }.OfType<ProyectoActivo>().DistinctBy(p => p.Id))
        {
            try
            {
                using var db = Contexto(p, soloLectura: true);
                var url = db.Configuracion.AsNoTracking().Where(c => c.Id == 1).Select(c => c.LogoUrl).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(url)) return url;
            }
            catch (Exception ex) { _log.LogDebug(ex, "Logo no disponible en {Proyecto}", p.Codigo); }
        }
        return null;
    }

    // Guarda la imagen subida y la deja como logo de la app. Devuelve la URL pública.
    public async Task<string> CargarAsync(Stream contenido, string nombreArchivo, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (!Extensiones.Contains(ext)) throw new InvalidOperationException("El logo debe ser PNG, JPG o WebP.");
        var carpeta = Path.Combine(_env.WebRootPath, "uploads", "marca");
        Directory.CreateDirectory(carpeta);
        var nombre = $"logo-{Guid.NewGuid():N}{ext}";
        var ruta = Path.GetFullPath(Path.Combine(carpeta, nombre));
        if (!ruta.StartsWith(Path.GetFullPath(carpeta), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ruta no válida.");
        await using (var f = File.Create(ruta)) await contenido.CopyToAsync(f, ct);
        var url = $"/uploads/marca/{nombre}";
        await EstablecerAsync(url, ct);
        return url;
    }

    // null = quitar el logo (se ve el nombre «SGC Web»).
    public async Task EstablecerAsync(string? url, CancellationToken ct = default)
    {
        var empresa = _proyectos.Empresa() ?? throw new InvalidOperationException("No hay proyecto Empresa configurado: el logo se guarda en su base.");
        await using (var db = Contexto(empresa, soloLectura: false))
        {
            var cfg = await db.Configuracion.FirstOrDefaultAsync(c => c.Id == 1, ct);
            if (cfg is null) { cfg = new SigWeb.Models.ConfiguracionEmpresa { Id = 1 }; db.Configuracion.Add(cfg); }
            cfg.LogoUrl = url;
            await db.SaveChangesAsync(ct);
        }
        // Quitar el logo también en la piloto: si no, el respaldo lo volvería a mostrar.
        if (url is null && _proyectos.Piloto() is { } piloto && piloto.Id != empresa.Id)
        {
            try
            {
                await using var db = Contexto(piloto, soloLectura: false);
                if (await db.Configuracion.FirstOrDefaultAsync(c => c.Id == 1, ct) is { } c) { c.LogoUrl = null; await db.SaveChangesAsync(ct); }
            }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudo quitar el logo anterior de la piloto"); }
        }
        lock (_candado) { _url = url; _leido = DateTime.UtcNow; }
    }

    private static AppDbContext Contexto(ProyectoActivo p, bool soloLectura)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(p.CadenaConexion);
        if (soloLectura) b.AddInterceptors(new InterceptorSoloLectura());
        return new AppDbContext(b.Options);
    }
}
