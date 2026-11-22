using SigWeb.Data.Seguridad;
using Microsoft.EntityFrameworkCore;
namespace SigWeb.Data;

public static class ArchivosHojaVidaEndpoints
{
    public static void MapArchivosHojaVida(this WebApplication app)
    {
        app.MapGet("/api/hojavida/archivos/{id:int}", async (int id, bool? descargar, AppDbContext db, IWebHostEnvironment env, HttpContext http) =>
        {
            var puedeDescargar = AmbienteSig.Actual?.Permisos.Puede("trazado", Accion.Descargar) == true;
            if (descargar == true && !puedeDescargar) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var a = await db.ArchivosCarpetaHojaVida.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, http.RequestAborted);
            if (a is null) return Results.NotFound();
            var ruta = ArchivosSeguros.RutaFisica(env.WebRootPath, a.Ruta, "hojavida");
            if (ruta is null || !File.Exists(ruta)) return Results.NotFound();
            var tipo = Path.GetExtension(a.NombreOriginal).ToLowerInvariant() switch {
                ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif", ".webp" => "image/webp", ".txt" => "text/plain; charset=utf-8", _ => "application/octet-stream"
            };
            var comoAdjunto = descargar == true || tipo == "application/octet-stream";
            if (comoAdjunto && !puedeDescargar)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(ruta, tipo, comoAdjunto ? a.NombreOriginal : null, enableRangeProcessing: true);
        }).RequiereModulo("trazado", SigWeb.Data.Seguridad.Accion.Ver);
    }
}
