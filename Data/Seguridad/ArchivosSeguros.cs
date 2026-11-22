namespace SigWeb.Data.Seguridad;

// Ruta física de un archivo subido a partir de la ruta guardada en la base ("/uploads/…"):
// null si sale de wwwroot/uploads (evita que una ruta manipulada borre o lea archivos de la app).
public static class ArchivosSeguros
{
    public static string? RutaFisica(string webRoot, string? rutaWeb, string subcarpeta = "", bool decodificar = false)
    {
        if (string.IsNullOrWhiteSpace(rutaWeb) || rutaWeb.Contains("://", StringComparison.Ordinal)) return null;
        try
        {
            var raiz = Path.GetFullPath(Path.Combine(webRoot, "uploads", subcarpeta)) + Path.DirectorySeparatorChar;
            var ruta = rutaWeb.Split('?', '#')[0];
            if (decodificar) ruta = Uri.UnescapeDataString(ruta);
            var relativa = ruta.TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var fisica = Path.GetFullPath(Path.Combine(webRoot, relativa));
            return fisica.StartsWith(raiz, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ? fisica : null;
        }
        catch (ArgumentException) { return null; }
        catch (NotSupportedException) { return null; }
        catch (PathTooLongException) { return null; }
    }
}
