using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Demo;

// Un módulo que siembra su ejemplo de demostración. Se descubren por reflexión (basta crear la clase).
// Cada uno debe ser idempotente: si su tabla principal ya tiene datos, no hace nada.
public interface IModuloDemo
{
    // Clave del módulo en CatalogoModulos (riesgos, documentos, ensayos…), solo informativa.
    string Clave { get; }
    // Dónde se siembra: base de la Empresa y/o bases de los proyectos.
    bool EnEmpresa => false;
    bool EnProyecto => true;
    // Orden de ejecución (menor primero). Trazado = 0; lo que dependa de torres, después.
    int Orden => 100;
    Task SembrarAsync(ContextoDemo c, CancellationToken ct);
}

// Lo que recibe cada módulo: base del proyecto, datos del proyecto y ayudas para archivos.
public sealed class ContextoDemo
{
    public required AppDbContext Db { get; init; }
    public required ProyectoActivo Proyecto { get; init; }
    public required IServiceProvider Servicios { get; init; }
    public required string WebRoot { get; init; }
    public required ILogger Log { get; init; }
    // 0 = primer proyecto (LTN230), 1 = segundo (LTS500), -1 = Empresa. Sirve para variar textos.
    public int Indice { get; init; }

    public string NombreProyecto => Proyecto.Nombre;
    public DateTime Hoy => DateTime.Today;

    // Ids de las torres del proyecto (vacío en la Empresa). Se leen al pedirlos.
    public List<int> Torres() => Db.ElementosTrazado.Where(e => !e.Eliminado).OrderBy(e => e.Id).Select(e => e.Id).ToList();

    // Recursos de ejemplo incluidos en la app (Data/Demo/recursos): PDF y torres.
    public static string CarpetaRecursos => Path.Combine(AppContext.BaseDirectory, "Data", "Demo", "recursos");
    public const string PdfPolitica = "politica-integrada.pdf";
    public const string PdfCertificado = "certificado-acero.pdf";
    public const string PdfEnsayo = "informe-ensayo.pdf";

    // Copia un recurso a wwwroot/uploads/<subcarpeta>/<guid><ext> y devuelve la ruta web y el tamaño.
    // extension = null conserva la del recurso (".bin" para carpetas que guardan sin extensión).
    public (string RutaWeb, long Tamano) CopiarArchivo(string recurso, string subcarpeta, string? extension = null)
    {
        var origen = Path.Combine(CarpetaRecursos, recurso);
        if (!File.Exists(origen)) throw new FileNotFoundException("Falta el recurso de demostración " + recurso, origen);
        var sub = subcarpeta.Trim('/').Replace('\\', '/');
        var uploads = Path.GetFullPath(Path.Combine(WebRoot, "uploads"));
        var dir = Path.GetFullPath(Path.Combine(uploads, sub.Replace('/', Path.DirectorySeparatorChar)));
        if (!dir.StartsWith(uploads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Subcarpeta de demostración fuera de uploads: " + subcarpeta);
        Directory.CreateDirectory(dir);
        var nombre = Guid.NewGuid().ToString("N") + (extension ?? Path.GetExtension(recurso));
        File.Copy(origen, Path.Combine(dir, nombre), overwrite: false);
        return ($"/uploads/{sub}/{nombre}", new FileInfo(origen).Length);
    }
}
