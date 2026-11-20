using System.Globalization;
using System.Text;

namespace SigWeb.Data.Iconos;

// Galería de íconos de trazo para los módulos que crea el administrador (creador de módulos) y para cualquier pantalla que
// pida un ícono por clave. Mismo lenguaje que los íconos del menú (EncabezadoPagina.Trazos): viewBox 24×24, solo trazo
// (el <svg> contenedor pone fill="none" stroke="currentColor" y el grosor), puntas redondeadas, geometría en 2,5–21,5.
// Trazos = interior del <svg>. EncabezadoPagina.Trazos busca aquí las claves que no conoce, así un módulo creado puede
// usar cualquiera de estas en el menú, el encabezado y el panel vacío.
// Ícono nuevo = una línea en su categoría (clave única en minúsculas con guiones; Palabras = sinónimos para el buscador).
public sealed record IconoModulo(string Clave, string Nombre, string Categoria, string Trazos, string Palabras = "");

public static partial class GaleriaIconosModulo
{
    public const string Transmision = "Líneas de transmisión";
    public const string Electrico = "Subestaciones y eléctrico";
    public const string Construccion = "Construcción";
    public const string Calidad = "Calidad";
    public const string Seguridad = "Seguridad y salud";
    public const string Gestion = "Gestión";
    public const string Ambiental = "Ambiental y topografía";

    // Orden en que el selector muestra las categorías.
    public static readonly IReadOnlyList<string> Categorias = new[] { Transmision, Electrico, Construccion, Calidad, Seguridad, Gestion, Ambiental };

    private static IReadOnlyList<IconoModulo>? _todos;
    private static Dictionary<string, IconoModulo>? _porClave;

    public static IReadOnlyList<IconoModulo> Todos => _todos ??= Armar();

    private static IReadOnlyList<IconoModulo> Armar()
    {
        var lista = new List<IconoModulo>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Si dos categorías repiten una clave, gana la primera (nunca se cae la app por un ícono).
        foreach (var i in IconosTransmision().Concat(IconosElectrico()).Concat(IconosConstruccion()).Concat(IconosCalidad()))
            if (vistas.Add(i.Clave)) lista.Add(i);
        return lista.OrderBy(i => Orden(i.Categoria)).ToList();
    }

    private static int Orden(string categoria)
    {
        var i = ((IList<string>)Categorias).IndexOf(categoria);
        return i < 0 ? Categorias.Count : i;
    }

    public static IconoModulo? De(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave)) return null;
        _porClave ??= Todos.ToDictionary(i => i.Clave, StringComparer.OrdinalIgnoreCase);
        return _porClave.GetValueOrDefault(clave.Trim());
    }

    // Interior del <svg> de la clave, o null si no es de la galería.
    public static string? Trazos(string? clave) => De(clave)?.Trazos;

    // Búsqueda sin tildes ni mayúsculas por nombre, clave, categoría y sinónimos; todas las palabras deben aparecer.
    public static IEnumerable<IconoModulo> Buscar(string? texto, string? categoria = null)
    {
        IEnumerable<IconoModulo> q = Todos;
        if (!string.IsNullOrWhiteSpace(categoria)) q = q.Where(i => i.Categoria == categoria);
        var palabras = Normalizar(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length == 0) return q;
        return q.Where(i =>
        {
            var hay = Normalizar($"{i.Nombre} {i.Clave.Replace('-', ' ')} {i.Categoria} {i.Palabras}");
            return palabras.All(p => hay.Contains(p));
        });
    }

    private static string Normalizar(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    // ---------- Líneas de transmisión ----------
    private static IEnumerable<IconoModulo> IconosTransmision() => new IconoModulo[]
    {
        new("torre-celosia", "Torre de celosía", Transmision,
            "<path d='M8.5 21 11 3h2l2.5 18'/><path d='M4.5 7h15M7 11.5h10'/><path d='M5.5 7v3M18.5 7v3'/><path d='m9.6 14.5 5.2 4.5M14.4 14.5 9.2 19'/>",
            "estructura apoyo metalica celosia suspension retencion"),
        new("torre-monoposte", "Poste / monoposte", Transmision,
            "<path d='M12 21V3'/><path d='M9.5 21h5'/><path d='M6 6.5h12M7.5 10.5h9'/><path d='M6.5 6.5v2.3M17.5 6.5v2.3M8.5 10.5v2.3M15.5 10.5v2.3'/>",
            "poste estructura distribucion apoyo"),
        new("linea-transmision", "Línea de transmisión", Transmision,
            "<path d='M3.5 21 5 5l1.5 16M17.5 21 19 5l1.5 16'/><path d='M3 8h4M17 8h4'/><path d='M4 9c4.5 5 11.5 5 16 0'/>",
            "vano circuito conductores trazado red"),
        new("conductor", "Conductor (sección)", Transmision,
            "<circle cx='12' cy='12' r='8.5'/><circle cx='12' cy='12' r='2.2'/><circle cx='12' cy='7.2' r='2.2'/><circle cx='16.2' cy='9.6' r='2.2'/><circle cx='16.2' cy='14.4' r='2.2'/><circle cx='12' cy='16.8' r='2.2'/><circle cx='7.8' cy='14.4' r='2.2'/><circle cx='7.8' cy='9.6' r='2.2'/>",
            "cable hilos acsr aaac aluminio acero cableado"),
        new("aislador-cadena", "Cadena de aisladores", Transmision,
            "<path d='M12 2.5v19'/><ellipse cx='12' cy='7' rx='4.5' ry='1.3'/><ellipse cx='12' cy='10.3' rx='4.5' ry='1.3'/><ellipse cx='12' cy='13.6' rx='4.5' ry='1.3'/><ellipse cx='12' cy='16.9' rx='4.5' ry='1.3'/>",
            "aislador porcelana vidrio polimerico herraje"),
        new("puesta-tierra", "Puesta a tierra (SPT)", Transmision,
            "<path d='M12 3v9'/><path d='M5 12h14M7.5 15.5h9M10 19h4'/>",
            "spt tierra contrapeso varilla resistencia"),
        new("tendido", "Tendido / poleas", Transmision,
            "<path d='M12 2.5v4'/><circle cx='12' cy='11' r='4.5'/><circle cx='12' cy='11' r='1'/><path d='M2.5 14c2.5 0 4.5-1.3 5-3M16.5 11c.5 1.7 2.5 3 5 3'/>",
            "polea regulado tensionado halado conductor"),
        new("servidumbre", "Servidumbre / corredor", Transmision,
            "<path d='M4.5 3v18M19.5 3v18'/><path d='M12 3v2.5M12 8.5V11M12 14v2.5M12 19.5V21'/><path d='M7 12h3M14 12h3M8.5 10.5 7 12l1.5 1.5M15.5 10.5 17 12l-1.5 1.5'/>",
            "franja corredor ancho eje linea predial"),
        new("flecha-vano", "Flecha y vano", Transmision,
            "<path d='M4 4.5V20M20 4.5V20'/><path d='M4 6c4 8 12 8 16 0'/><path d='M7 6h1.5M11.25 6h1.5M15.5 6H17'/><path d='M12 8v3.5'/><path d='m10.8 10.3 1.2 1.2 1.2-1.2'/>",
            "catenaria tension templado distancia seguridad"),
        new("esfera-balizaje", "Balizaje / señalización aérea", Transmision,
            "<path d='M2.5 8c6 4 13 4 19 0'/><circle cx='12' cy='11' r='3.3'/>",
            "esfera baliza aeronautica aviso aereo"),
        new("amortiguador", "Amortiguador", Transmision,
            "<path d='M2.5 7h19'/><path d='M12 7v5'/><path d='M7.5 12h9'/><rect x='3' y='10' width='4.5' height='5' rx='2'/><rect x='16.5' y='10' width='4.5' height='5' rx='2'/>",
            "stockbridge vibracion espaciador herraje"),
        new("dron", "Inspección con dron", Transmision,
            "<rect x='9.5' y='10' width='5' height='4' rx='1'/><path d='M9.5 11 6 8M14.5 11 18 8M9.5 13 6 16M14.5 13l3.5 3'/><path d='M3.5 8h5M15.5 8h5M3.5 16h5M15.5 16h5'/>",
            "drone aereo patrullaje vuelo fotografia"),
        new("mojon", "Mojón / abscisa", Transmision,
            "<path d='M8 21V7l4-4 4 4v14z'/><path d='M5 21h14M10.5 11h3M10.5 14.5h3'/>",
            "hito kilometro abscisa punto referencia"),
        new("perfil", "Planta y perfil", Transmision,
            "<path d='M3 17l4-5 3 2 4-6 3 3 4-4'/><path d='M3 20.5h18'/><path d='M7 12v8.5M14 8v12.5'/>",
            "terreno topografico plantillado altimetria"),
    };

    // ---------- Subestaciones y eléctrico ----------
    private static IEnumerable<IconoModulo> IconosElectrico() => new IconoModulo[]
    {
        new("energia", "Energía / tensión", Electrico,
            "<path d='M13 2.5 5 13.5h6l-1 8 8-11h-6z'/>",
            "rayo voltaje electricidad energizado"),
        new("subestacion", "Subestación", Electrico,
            "<rect x='3.5' y='9' width='17' height='11.5' rx='1.5'/><path d='M7 9V5.5M12 9V3.5M17 9V5.5'/><path d='M5.5 5.5h3M15.5 5.5h3M10.5 3.5h3'/><path d='m12.5 11.5-2 3.5h3l-2 3.5'/>",
            "patio bahia se encerramiento"),
        new("transformador", "Transformador", Electrico,
            "<circle cx='9' cy='12' r='5'/><circle cx='15' cy='12' r='5'/><path d='M9 3v4M15 17v4'/>",
            "potencia trafo devanado"),
        new("interruptor", "Interruptor", Electrico,
            "<path d='M12 3v4.5M12 16.5V21'/><path d='m12 16.5-4.5-7.5'/><path d='M10 7.5h4'/>",
            "breaker maniobra apertura cierre"),
        new("seccionador", "Seccionador", Electrico,
            "<path d='M2.5 15h4M17 15h4.5M17 13v4'/><circle cx='7.5' cy='15' r='1.2'/><path d='m8.5 14.3 8-5.3'/>",
            "cuchilla apertura aislamiento maniobra"),
        new("pararrayos", "Pararrayos / DPS", Electrico,
            "<path d='M12 2.5v3'/><rect x='9' y='5.5' width='6' height='10' rx='1.5'/><path d='m12.8 7.5-2 3.5h2.4l-2 3'/><path d='M12 15.5v3M8 18.5h8M10 21h4'/>",
            "descargador sobretension apantallamiento"),
        new("gabinete", "Gabinete / tablero", Electrico,
            "<rect x='5' y='3' width='14' height='18' rx='1.5'/><path d='M12 3v18'/><path d='M8.5 8v2M15.5 8v2'/><path d='M8 15h1.5M14.5 15H16'/>",
            "celda armario control proteccion"),
        new("unifilar", "Diagrama unifilar", Electrico,
            "<path d='M4 5h16'/><path d='M8 5v4M16 5v4'/><rect x='6' y='9' width='4' height='4' rx='.8'/><rect x='14' y='9' width='4' height='4' rx='.8'/><path d='M8 13v7M16 13v7M6.5 20h3M14.5 20h3'/>",
            "barraje esquema circuito"),
        new("fibra-optica", "Fibra óptica / OPGW", Electrico,
            "<path d='M2.5 19c4 0 6-7 9.5-7'/><circle cx='14' cy='12' r='2'/><path d='M17.5 12h4M16.8 8.6l2.5-2.5M16.8 15.4l2.5 2.5'/>",
            "opgw comunicaciones datos cable guarda"),
        new("termografia", "Termografía", Electrico,
            "<path d='M10 14.5V5a2 2 0 0 1 4 0v9.5a4 4 0 1 1-4 0z'/><path d='M12 9v7'/><path d='M17 6h3M17 9.5h3M17 13h2'/>",
            "temperatura punto caliente termometro conexiones"),
    };

    // ---------- Construcción ----------
    private static IEnumerable<IconoModulo> IconosConstruccion() => new IconoModulo[]
    {
        new("cimentacion", "Cimentación", Construccion,
            "<path d='M2.5 9h5M16.5 9h5'/><path d='M9.5 6h5v9.5'/><path d='M9.5 6v9.5'/><path d='M5 15.5h14v5H5z'/><path d='M12 2.5V6'/>",
            "zapata pedestal stub fundacion pila"),
        new("excavacion", "Excavación", Construccion,
            "<path d='M3 20.5h18'/><path d='M5 20.5c1-3 3-4 5-4s4 1 5 4'/><path d='M14 13 19 3.5'/><path d='m11.5 11.5 3.2 1.7-1.3 2.6a2 2 0 0 1-2.7.8l-.4-.2a2 2 0 0 1-.8-2.7z'/>",
            "pala movimiento tierras zanja"),
        new("concreto", "Concreto", Construccion,
            "<path d='M2.5 16.5h19'/><path d='M2.5 16.5V11h4v5.5'/><path d='M8.5 14.5 10 7.5l9-1 1.5 6z'/><path d='m12.5 7.2 1 6.8M16 6.8l1.2 6.7'/><circle cx='5.5' cy='18.7' r='1.8'/><circle cx='16.5' cy='18.7' r='1.8'/>",
            "mixer hormigon vaciado fundida mezcla"),
        new("acero-refuerzo", "Acero de refuerzo", Construccion,
            "<rect x='6' y='6' width='12' height='12' rx='1.5'/><path d='m6 9 3-3'/><circle cx='8.8' cy='15.2' r='1'/><circle cx='15.2' cy='15.2' r='1'/><circle cx='15.2' cy='8.8' r='1'/>",
            "varilla estribo armadura hierro"),
        new("grua", "Grúa / izaje", Construccion,
            "<path d='M6 21V3'/><path d='M3.5 21h5'/><path d='M6 4.5h15M6 4.5 3 7'/><path d='m6 8 4-3.5'/><path d='M17 4.5v6'/><path d='M15.5 10.5h3V13h-3z'/>",
            "izaje montaje pluma levantamiento"),
        new("retroexcavadora", "Maquinaria", Construccion,
            "<rect x='3' y='16' width='11' height='4.5' rx='2.2'/><path d='M5 16v-5h5l1.5 5'/><path d='M11 12 16 5l4 4.5'/><path d='M20 9.5V13h-2.5'/>",
            "excavadora equipo pesado maquina"),
        new("andamio", "Andamio / trabajo en altura", Construccion,
            "<path d='M6 3v18M18 3v18'/><path d='M6 8h12M6 14h12M6 20h12'/><path d='m6 8 12 6'/>",
            "altura plataforma escalera"),
        new("compactacion", "Compactación", Construccion,
            "<path d='M3 20.5h18'/><rect x='7' y='15' width='10' height='3' rx='1'/><path d='M12 15V8'/><path d='M9 4.5h6V8H9z'/><path d='M15 6h3.5'/>",
            "rana apisonador relleno densidad suelo"),
        new("almacen", "Almacén / bodega", Construccion,
            "<path d='M3 21V9l9-5 9 5v12'/><path d='M7 21v-8h10v8M7 16.5h10'/>",
            "bodega inventario deposito acopio"),
        new("frente-trabajo", "Frente de trabajo", Construccion,
            "<path d='M5 21V3.5'/><path d='M5 4.5h11l-2 3 2 3H5'/><path d='M3 21h6'/>",
            "bandera frente cuadrilla sitio"),
        new("tornilleria", "Tornillería / montaje", Construccion,
            "<path d='M12 3.5 19.4 7.75v8.5L12 20.5l-7.4-4.25v-8.5z'/><circle cx='12' cy='12' r='3'/>",
            "tuerca perno tornillo montaje estructura"),
        new("informe-diario", "Informe diario", Construccion,
            "<rect x='4' y='4.5' width='16' height='16' rx='2'/><path d='M8 2.5v4M16 2.5v4M4 9h16'/><path d='M8 13h8M8 16.5h5'/>",
            "bitacora diario reporte jornada"),
        new("plano-detalle", "Plano de detalle", Construccion,
            "<rect x='3' y='4' width='18' height='16' rx='1.5'/><path d='M3 15h5v5M13 4v7h8'/><path d='M6 8h4'/>",
            "plano rotulo dibujo detalle constructivo"),
        new("cinta-metrica", "Medición / cinta", Construccion,
            "<circle cx='10' cy='11' r='6.5'/><circle cx='10' cy='11' r='2'/><path d='M16.5 11v6.5h5'/><path d='M19 17.5v-2'/>",
            "metro flexometro medida cantidad"),
    };
}
