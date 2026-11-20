namespace SigWeb.Data.Seguridad;

// Ruta física de un archivo subido a partir de la ruta guardada en la base ("/uploads/…"):
// null si sale de wwwroot/uploads (evita que una ruta manipulada borre o lea archivos de la app).
public static class ArchivosSeguros
{
    public static string? RutaFisica(string webRoot, string? rutaWeb)
    {
        if (string.IsNullOrWhiteSpace(rutaWeb)) return null;
        var raiz = Path.GetFullPath(Path.Combine(webRoot, "uploads")) + Path.DirectorySeparatorChar;
        var fisica = Path.GetFullPath(Path.Combine(webRoot, rutaWeb.Split('?', '#')[0].TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));
        return fisica.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ? fisica : null;
    }
}
