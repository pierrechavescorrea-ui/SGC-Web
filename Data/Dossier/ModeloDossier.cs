using SigWeb.Data.Seguridad;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier;

// Dossier: compila en un ZIP las tablas (con los Campos de cada módulo) y los adjuntos de los
// elementos que el usuario elige. Cada módulo que aporta información es una IFuenteDossier
// (Data/Dossier/Fuentes); para agregar un módulo basta con crear su fuente y sumarla a FuentesDossier.Todas.

// Archivo de un elemento. Clase = categoría para filtrar (Vigente, Remisión, Fotografías…);
// Carpeta = subcarpeta dentro de la carpeta del elemento (null = la clase). Enlace = dirección https
// (va al ZIP como acceso directo .url). Actual = false para versiones anteriores u obsoletas.
public sealed record ArchivoDossier(string Clase, string Nombre, string Ruta, bool Enlace = false, bool Actual = true, string? Carpeta = null);

public sealed record HojaDossier(string Nombre, IReadOnlyList<string> Columnas, IEnumerable<FilaDossier> Filas, MotorTabla? Motor = null);
public sealed record FilaDossier(int ElementoId, params object?[] Valores);

public sealed class ElementoDossier
{
    public int Id { get; init; }
    public string? Grupo { get; init; }
    public List<ArchivoDossier> Archivos { get; } = new();
    public List<(string Nombre, Func<string, byte[]> Crear)> ArchivosDeTabla { get; } = new();
}

// Lo que una fuente entrega: la tabla (motor de Campos en solo lectura, mismas columnas, orden y
// fórmulas que ve el usuario en el módulo) y sus elementos con adjuntos.
public sealed class FuenteCargada
{
    public required IFuenteDossier Fuente { get; init; }
    public required MotorTabla Motor { get; init; }
    public List<HojaDossier> Hojas { get; } = new();
    public List<ElementoDossier> Elementos { get; init; } = new();
    // Columna (clave) que nombra la carpeta de cada elemento por defecto.
    public string ColumnaNombre { get; init; } = "";
    // Nombre del agrupador (Grupo, Tipo documental…) o null si la fuente no agrupa.
    public string? NombreGrupo { get; init; }
    // Clases en el orden en que se muestran.
    public List<string> Clases { get; init; } = new();
    // true si hay versiones anteriores u obsoletas (opción "solo actuales").
    public bool TieneVersiones { get; init; }

    private Dictionary<int, ElementoDossier>? porId;
    public ElementoDossier? Elemento(int id) => (porId ??= Elementos.ToDictionary(e => e.Id)).GetValueOrDefault(id);

    // Columnas exportables: las visibles del módulo (o todas, ocultas incluidas).
    public List<CampoTabla> Columnas(bool soloVisibles) => (soloVisibles ? Motor.Visibles : Motor.Ordenados).ToList();

    // Texto de la celda (sin lanzar si una fórmula falla).
    public string Texto(int id, string clave)
    {
        var c = Motor.PorClave(clave);
        return c is null ? "" : Motor.Texto(id, c, out _);
    }
}

public interface IFuenteDossier
{
    string Clave { get; }
    string Nombre { get; }
    // Clave de EncabezadoPagina.Trazos (ícono del módulo).
    string Icono { get; }
    Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct);
}

public static class FuentesDossier
{
    // Orden por defecto de las carpetas del ZIP.
    private static readonly IReadOnlyList<IFuenteDossier> Fijas = new IFuenteDossier[]
    {
        // En el orden del menú (la pantalla los agrupa por capítulo con MapaNavegacion).
        new Fuentes.FuenteDireccionamiento(),
        new Fuentes.FuentePartesInteresadas(),
        new Fuentes.FuenteOrganigrama(),
        new Fuentes.FuenteRiesgos(),
        new Fuentes.FuenteCostos(),
        new Fuentes.FuenteEstadoTorres(),
        new Fuentes.FuenteCertificaciones(),
        new Fuentes.FuenteEquipos(),
        new Fuentes.FuenteComunicados(),
        new Fuentes.FuentePlanos(),
        new Fuentes.FuenteDocumentos(),
        new Fuentes.FuenteMatrizPie(),
        new Fuentes.FuenteTorres(),
        new Fuentes.FuenteRevisionesTrazado(),
        new Fuentes.FuenteAvanceObra(),
        new Fuentes.FuenteActividadesObra(),
        new Fuentes.FuenteRegistrosAvance(),
        new Fuentes.FuenteRegistrosTendido(),
        new Fuentes.FuenteModelos3D(),
        new Fuentes.FuenteMateriales(),
        new Fuentes.FuenteInformesEnsayo(),
        new Fuentes.FuenteResultadosEnsayo(),
        new Fuentes.FuenteIndicadores(),
        new Fuentes.FuenteAuditorias(),
        new Fuentes.FuenteCostosNoCalidad(),
        new Fuentes.FuentePlanesAccion(),
        new Fuentes.FuenteActividades(),
        new Fuentes.FuenteTiros(),
        new Fuentes.FuenteCatalogos(),
    };

    // Program conecta la lectura del singleton después de cargarlo: no hay dependencia del Dossier en el registro.
    // El delegado lee la instantánea publicada actual, también después de publicar o archivar otro módulo.
    public static Func<IEnumerable<IFuenteDossier>>? Creados { get; set; }
    public static IReadOnlyList<IFuenteDossier> Todas => Creados is { } creados
        ? Fijas.Concat(creados()).DistinctBy(f => f.Clave).ToArray() : Fijas;

    public static IFuenteDossier? De(string clave) => Todas.FirstOrDefault(f => f.Clave == clave);

    // Ruta del menú de cada fuente (la mayoría usa su clave) y módulo de permisos (CatalogoModulos): el Dossier solo ofrece y solo
    // genera los módulos que el usuario puede ver, y lee los compartidos (Direccionamiento, Certificaciones…) de la base de la Empresa.
    private static readonly Dictionary<string, string> Rutas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["torres"] = "trazado", ["informes"] = "ensayos", ["resultados-ensayo"] = "ensayos",
        ["revisiones-trazado"] = "trazado", ["estado-torres"] = "costos",
        ["actividades-obra"] = "avance-obra", ["registros-avance"] = "avance-obra", ["registros-tendido"] = "avance-obra",
    };
    public static string RutaDe(string clave) => Rutas.TryGetValue(clave, out var r) ? r : clave;
    public static string ModuloDe(string clave) => CatalogoModulos.ClaveDeRuta(RutaDe(clave));

    public static bool Permitida(string clave, Accion accion, Func<string, Accion, bool> puede)
    {
        if (De(clave) is null) return false;
        var modulo = ModuloDe(clave);
        return modulo != "inicio" && puede(modulo, accion | Accion.Ver);
    }
}

// ---------- Lo que el usuario pidió (se arma en la pantalla y viaja al endpoint de descarga) ----------

public sealed record SolicitudDossier
{
    public string Nombre { get; set; } = "Dossier";
    public bool CarpetaPorElemento { get; set; } = true;   // una carpeta por elemento (si no, archivos sueltos con prefijo)
    public bool Subcarpetas { get; set; } = true;          // dentro del elemento, una carpeta por clase
    public bool PorGrupo { get; set; }                     // carpetas de grupo (disciplina, tipo documental…) antes del elemento
    public bool Numerar { get; set; } = true;              // "01 Documentos", "02 Planos"…
    public bool LibroUnico { get; set; }                   // todas las tablas en un solo Excel en la raíz
    public bool SoloVisibles { get; set; } = true;         // columnas visibles del módulo (Campos)
    public List<ModuloSolicitado> Modulos { get; set; } = new();
}

public sealed class ModuloSolicitado
{
    public string Clave { get; set; } = "";
    public bool Tabla { get; set; } = true;
    public bool Adjuntos { get; set; } = true;
    public bool SoloActuales { get; set; } = true;
    public List<int> Elementos { get; set; } = new();       // en el orden de la tabla
    public List<string> Clases { get; set; } = new();       // clases incluidas
    public List<string> ColumnasNombre { get; set; } = new();   // columnas que nombran la carpeta del elemento (1 o 2)
}
