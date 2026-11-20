using SigWeb.Data.Seguridad;
using Microsoft.EntityFrameworkCore;
namespace SigWeb.Data;

public static class ArchivosHojaVidaEndpoints
{
    public static void MapArchivosHojaVida(this WebApplication app)
    {
        app.MapGet("/api/hojavida/archivos/{id:int}", async (int id, bool? descargar, AppDbContext db, IWebHostEnvironment env, HttpContext http) =>
        {
            // Ver = mostrar en el navegador; descargar exige el permiso Descargar de Trazado.
            var permisos = SigWeb.Data.Seguridad.AmbienteSig.Actual?.Permisos;
            if (descargar == true && permisos?.Puede("trazado", SigWeb.Data.Seguridad.Accion.Descargar) != true) return Results.StatusCode(403);
            var a = await db.ArchivosCarpetaHojaVida.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, http.RequestAborted);
            if (a is null) return Results.NotFound();
            var raiz = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads", "hojavida")) + Path.DirectorySeparatorChar;
            var ruta = Path.GetFullPath(Path.Combine(env.WebRootPath, a.Ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) || !File.Exists(ruta)) return Results.NotFound();
            var tipo = Path.GetExtension(a.NombreOriginal).ToLowerInvariant() switch {
                ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif", ".webp" => "image/webp", ".txt" => "text/plain; charset=utf-8", _ => "application/octet-stream"
            };
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(ruta, tipo, descargar == true || tipo == "application/octet-stream" ? a.NombreOriginal : null, enableRangeProcessing: true);
        }).RequiereModulo("trazado", SigWeb.Data.Seguridad.Accion.Ver);
    }
}
