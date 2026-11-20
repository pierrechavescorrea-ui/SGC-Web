using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SigWeb.Models;

namespace SigWeb.Data.CreadorModulos;

// ---------- Creador de módulos: lo que el administrador diseña ----------
// Un módulo creado NO genera código: es esta definición (JSON versionado en la base central) que la app interpreta.
// Identidad estable: Clave de paso y de campo se generan al crearlos y nunca cambian (renombrar = cambiar Titulo/Etiqueta),
// así los datos guardados sobreviven a cualquier cambio de diseño. Quitar un campo publicado no borra sus valores: solo deja
// de pintarse (queda en la tabla como columna oculta).

public enum TipoCampoModulo
{
    Texto = 0,        // una línea
    Parrafo = 1,      // varias líneas
    Numero = 2,
    Fecha = 3,
    Lista = 4,        // opciones propias o de un catálogo (Catálogos)
    SiNo = 5,
    Archivos = 6,     // adjuntos del registro
    Vinculo = 7,      // registros de otro módulo de la app (torres, ensayos, equipos, planos…) vía ServicioVinculos
    Titulo = 8,       // texto fijo / subtítulo dentro de un paso (no guarda valor)
    Chequeo = 9,      // lista de chequeo: ítems (Opciones) que se marcan; guarda los marcados "A; B"
    Firma = 10,       // firma a mano en pantalla; se guarda como imagen PNG (adjunto del campo)
    Foto = 11,        // fotos con la cámara del celular o archivo (adjuntos de imagen)
    // ---- Herramientas de la app: muestran datos vivos de una fuente (no guardan valor en el registro) ----
    Tabla = 20, Grafico = 21, Kpi = 22, Estadistica = 23, Mapa = 24, Pdf = 25, Modelo3D = 26, Carpetas = 27, LineaTiempo = 28,
    // ---- Diseño ----
    Separador = 40, Grupo = 41,
}

// Cómo se comporta un control en el registro.
public enum ModoCampo { Editable = 0, SoloVista = 1, Calculado = 2 }

// Cálculo de gráficos, indicadores y estadística sobre las filas de la fuente.
public enum CalculoCampo { Conteo = 0, Suma = 1, Promedio = 2, Minimo = 3, Maximo = 4, Porcentaje = 5 }

public sealed class DefinicionModulo
{
    // Prefijo del código de cada registro ("INS" → INS-0001). Vacío = solo el número.
    public string Prefijo { get; set; } = "";
    public int CifrasCodigo { get; set; } = 4;
    // Flujo de estados del registro, en orden. El primero es el inicial. Vacío = el módulo no maneja estados.
    public List<EstadoModulo> Estados { get; set; } = new();
    public List<PasoModulo> Pasos { get; set; } = new();
    // Campo cuyo valor nombra el registro en listas y vínculos (además del código). null = el primer campo de texto.
    public string? CampoTitulo { get; set; }

    public IEnumerable<CampoModulo> Campos => Pasos.SelectMany(p => p.Campos);
    public CampoModulo? Campo(string clave) => Campos.FirstOrDefault(c => c.Clave == clave);
    public IEnumerable<CampoModulo> CamposConValor => Campos.Where(c => c.GuardaValor);

    public CampoModulo? CampoQueTitula =>
        (CampoTitulo is null ? null : Campo(CampoTitulo))
        ?? Campos.FirstOrDefault(c => c.Tipo is TipoCampoModulo.Texto)
        ?? Campos.FirstOrDefault(c => c.Tipo is TipoCampoModulo.Lista or TipoCampoModulo.Vinculo or TipoCampoModulo.Fecha);

    public string Codigo(int consecutivo)
    {
        var n = consecutivo.ToString().PadLeft(Math.Clamp(CifrasCodigo, 1, 8), '0');
        return string.IsNullOrWhiteSpace(Prefijo) ? n : $"{Prefijo.Trim()}-{n}";
    }

    // ---------- JSON ----------
    private static readonly JsonSerializerOptions Opc = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Json() => JsonSerializer.Serialize(this, Opc);

    // Nunca lanza: una definición dañada se lee vacía (la app sigue en pie y el diseñador avisa).
    public static DefinicionModulo Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DefinicionModulo();
        try { return JsonSerializer.Deserialize<DefinicionModulo>(json, Opc) ?? new DefinicionModulo(); }
        catch { return new DefinicionModulo(); }
    }

    public DefinicionModulo Copia() => Leer(Json());

    public static string ClaveNueva(string prefijo) => prefijo + Guid.NewGuid().ToString("N")[..8];

    // Errores que impiden publicar (lista vacía = se puede).
    public List<string> Validar()
    {
        var e = new List<string>();
        if (Pasos.Count == 0) e.Add("El módulo necesita al menos un paso.");
        if (!CamposConValor.Any()) e.Add("El módulo necesita al menos un campo que guarde información.");
        foreach (var p in Pasos)
        {
            if (string.IsNullOrWhiteSpace(p.Titulo)) e.Add("Hay un paso sin título.");
            if (p.Campos.Count == 0) e.Add($"El paso «{p.Titulo}» no tiene campos.");
        }
        foreach (var c in Campos)
        {
            var donde = $"«{c.Etiqueta}»";
            if (string.IsNullOrWhiteSpace(c.Etiqueta)) e.Add("Hay un campo sin nombre.");
            if (c.Tipo == TipoCampoModulo.Lista && string.IsNullOrWhiteSpace(c.Catalogo) && c.Opciones.Count(o => !string.IsNullOrWhiteSpace(o)) == 0)
                e.Add($"La lista {donde} no tiene opciones ni catálogo.");
            if (c.Tipo == TipoCampoModulo.Vinculo && string.IsNullOrWhiteSpace(c.Fuente))
                e.Add($"El vínculo {donde} no dice a qué módulo enlaza.");
            if (c.EsHerramientaApp && string.IsNullOrWhiteSpace(c.Fuente))
                e.Add($"{CampoModulo.NombreTipo(c.Tipo)} {donde}: falta elegir de qué módulo salen los datos.");
            if (c.FiltroCampo is not null && Campo(c.FiltroCampo) is not { Tipo: TipoCampoModulo.Vinculo })
                e.Add($"{donde} se filtra por un selector de registro que ya no existe.");
            if (c.Tipo == TipoCampoModulo.Chequeo && c.Opciones.Count(o => !string.IsNullOrWhiteSpace(o)) == 0)
                e.Add($"La lista de chequeo {donde} no tiene ítems.");
        }
        foreach (var g in Campos.Where(c => c.GuardaValor).GroupBy(c => Normalizar(c.Etiqueta)).Where(g => g.Key != "" && g.Count() > 1))
            e.Add($"Hay {g.Count()} campos llamados «{g.First().Etiqueta}»: cada campo debe tener un nombre distinto (es su columna en la tabla).");
        if (Estados.Count > 0 && Estados.Any(s => string.IsNullOrWhiteSpace(s.Nombre))) e.Add("Hay un estado sin nombre.");
        foreach (var g in Estados.GroupBy(s => Normalizar(s.Nombre)).Where(g => g.Key != "" && g.Count() > 1))
            e.Add($"El estado «{g.First().Nombre}» está repetido.");
        if (!Regex.IsMatch(Prefijo ?? "", @"^[A-Za-z0-9\-]{0,12}$")) e.Add("El prefijo del código solo admite letras, números y guiones (máx. 12).");
        return e.Distinct().ToList();
    }

    // Da posición a los controles que no la tienen (módulos hechos antes del lienzo o recién agregados): en el orden del paso,
    // de izquierda a derecha según su ancho, debajo de los ya colocados. Ajusta tamaños fuera de rango. Idempotente.
    public void Acomodar()
    {
        foreach (var p in Pasos)
        {
            int fila = 0, col = 0, altoFila = 0;
            foreach (var c in p.Campos.Where(c => c.X is not null && c.Y is not null))
            {
                c.Ancho = Math.Clamp(c.Ancho, 1, 12); c.X = Math.Clamp(c.X!.Value, 0, 12 - c.Ancho); c.Y = Math.Max(0, c.Y!.Value);
                if (c.Alto <= 0) c.Alto = CampoModulo.AltoInicial(c.Tipo);
                fila = Math.Max(fila, c.Y.Value + c.Alto);
            }
            foreach (var c in p.Campos.Where(c => c.X is null || c.Y is null))
            {
                c.Ancho = Math.Clamp(c.Ancho <= 0 ? CampoModulo.AnchoInicial(c.Tipo) : c.Ancho, 1, 12);
                if (c.Alto <= 0) c.Alto = CampoModulo.AltoInicial(c.Tipo);
                if (col + c.Ancho > 12) { fila += altoFila; col = 0; altoFila = 0; }
                c.X = col; c.Y = fila;
                col += c.Ancho; altoFila = Math.Max(altoFila, c.Alto);
            }
        }
    }

    public static string Normalizar(string? s) => SigWeb.Data.Asistente.TextoAyuda.SinTildes((s ?? "").Trim().ToLowerInvariant());
}

public sealed class EstadoModulo
{
    public string Nombre { get; set; } = "";
    // Clave de la paleta de formatos (MotorTabla.Paleta: verde, rojo, amarillo, azul, gris…).
    public string Color { get; set; } = "gris";
    // Final = el registro queda cerrado (solo se edita con Aprobar/Administrar).
    public bool Final { get; set; }
}

public sealed class PasoModulo
{
    public string Clave { get; set; } = DefinicionModulo.ClaveNueva("p");
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    // El usuario puede marcar el paso como "No aplica" (sus obligatorios dejan de exigirse).
    public bool PermiteNoAplica { get; set; }
    public List<CampoModulo> Campos { get; set; } = new();
}

public sealed class CampoModulo
{
    public string Clave { get; set; } = DefinicionModulo.ClaveNueva("c");
    public string Etiqueta { get; set; } = "";
    public TipoCampoModulo Tipo { get; set; }
    public bool Obligatorio { get; set; }
    // Columnas (de 12) que ocupa en el formulario: 3, 4, 6, 8 o 12.
    public int Ancho { get; set; } = 6;
    public string? Ayuda { get; set; }
    // Lista: catálogo de Catálogos (nombre del tipo) o, si es null, Opciones propias.
    public string? Catalogo { get; set; }
    public List<string> Opciones { get; set; } = new();
    // Lista y Vínculo: admite varios.
    public bool Multiple { get; set; }
    // Número
    public int? Decimales { get; set; }
    public string? Unidad { get; set; }
    // Vínculo: clave de la fuente (IFuenteDossier.Clave: "trazado", "equipos", "planos"…) y columnas de esa fuente que se ven.
    public string? Fuente { get; set; }
    public List<string> ColumnasFuente { get; set; } = new();
    // Se ve como columna en la lista del módulo.
    public bool EnLista { get; set; } = true;

    // ---------- Diseñador (lienzo de 12 columnas × filas de 40 px) ----------
    // Posición en el paso (null = sin colocar: Acomodar() la calcula siguiendo el orden) y alto en filas.
    public int? X { get; set; }
    public int? Y { get; set; }
    public int Alto { get; set; }
    public ModoCampo Modo { get; set; } = ModoCampo.Editable;
    // Visible solo si otro campo (clave) tiene este valor (null = siempre).
    public string? VisibleSiCampo { get; set; }
    public string? VisibleSiValor { get; set; }

    // ---------- Herramientas de la app (Tabla, Gráfico, KPI, Estadística, Mapa, PDF, 3D, Carpetas, Línea de tiempo) ----------
    // Fuente = de dónde salen los datos (misma clave que los vínculos). FiltroCampo = clave de un campo Vínculo de este módulo:
    // solo las filas relacionadas con lo que el usuario eligió allí. ColumnaEnlace = columna de la fuente que nombra ese registro
    // (p. ej. la columna «Torre» de los resultados de ensayo); null = misma fuente que el vínculo (se compara el Id).
    public string? FiltroCampo { get; set; }
    public string? ColumnaEnlace { get; set; }
    public CalculoCampo Calculo { get; set; } = CalculoCampo.Conteo;
    public string? ColumnaValor { get; set; }      // número a sumar/promediar, o columna a comparar (Porcentaje)
    public string? ColumnaAgrupar { get; set; }    // categorías del gráfico
    public string? ValorObjetivo { get; set; }     // Porcentaje: filas cuya ColumnaValor es este texto (p. ej. «Conforme»)
    public string? ColumnaFecha { get; set; }      // línea de tiempo / gráfico por mes
    public string? ColumnaLat { get; set; }        // mapa
    public string? ColumnaLon { get; set; }
    public string TipoGrafico { get; set; } = "barras";   // barras | lineas | torta

    [JsonIgnore] public bool EsHerramientaApp => Tipo is >= TipoCampoModulo.Tabla and <= TipoCampoModulo.LineaTiempo;
    [JsonIgnore] public bool EsDiseno => Tipo is TipoCampoModulo.Titulo or TipoCampoModulo.Separador or TipoCampoModulo.Grupo;
    [JsonIgnore] public bool EsAdjunto => Tipo is TipoCampoModulo.Archivos or TipoCampoModulo.Foto or TipoCampoModulo.Firma;
    [JsonIgnore] public bool GuardaValor => !EsHerramientaApp && !EsDiseno;
    [JsonIgnore] public bool Editable => Modo == ModoCampo.Editable && GuardaValor;

    // Alto por defecto (filas de 40 px) de cada tipo al soltarlo en el lienzo.
    public static int AltoInicial(TipoCampoModulo t) => t switch
    {
        TipoCampoModulo.Parrafo => 3, TipoCampoModulo.Chequeo => 4, TipoCampoModulo.Firma => 4, TipoCampoModulo.Foto => 4,
        TipoCampoModulo.Archivos => 3, TipoCampoModulo.Tabla => 6, TipoCampoModulo.Grafico => 5, TipoCampoModulo.Kpi => 3,
        TipoCampoModulo.Estadistica => 3, TipoCampoModulo.Mapa => 6, TipoCampoModulo.Pdf => 8, TipoCampoModulo.Modelo3D => 6,
        TipoCampoModulo.Carpetas => 4, TipoCampoModulo.LineaTiempo => 4, TipoCampoModulo.Separador => 1, TipoCampoModulo.Titulo => 1,
        TipoCampoModulo.Grupo => 4, _ => 2,
    };

    public static int AnchoInicial(TipoCampoModulo t) => t switch
    {
        TipoCampoModulo.Parrafo or TipoCampoModulo.Chequeo or TipoCampoModulo.LineaTiempo or TipoCampoModulo.Separador
            or TipoCampoModulo.Titulo or TipoCampoModulo.Grupo => 12,
        TipoCampoModulo.Tabla => 8, TipoCampoModulo.Kpi => 3, TipoCampoModulo.Estadistica => 4, TipoCampoModulo.Firma => 5,
        TipoCampoModulo.Grafico or TipoCampoModulo.Mapa or TipoCampoModulo.Pdf or TipoCampoModulo.Modelo3D or TipoCampoModulo.Foto
            or TipoCampoModulo.Archivos or TipoCampoModulo.Carpetas => 6,
        _ => 4,
    };

    // Tipo de la columna en la tabla de Campos (MotorTabla) del módulo.
    [JsonIgnore]
    public TipoCampoTabla TipoTabla => Tipo switch
    {
        TipoCampoModulo.Numero => TipoCampoTabla.Numero,
        TipoCampoModulo.Fecha => TipoCampoTabla.Fecha,
        TipoCampoModulo.Lista => TipoCampoTabla.Lista,
        TipoCampoModulo.SiNo => TipoCampoTabla.SiNo,
        TipoCampoModulo.Archivos or TipoCampoModulo.Foto or TipoCampoModulo.Firma => TipoCampoTabla.Archivos,
        _ => TipoCampoTabla.Texto,
    };

    // Cuadro de herramientas del diseñador: grupo, nombre, ícono (trazo SVG 24×24 del estilo de la app) y para qué sirve.
    // Dato = se enlaza a una fuente de la app.
    public sealed record Herramienta(TipoCampoModulo Tipo, string Grupo, string Nombre, string Descripcion, string Trazos, bool Dato = false);
    public const string GEntrada = "Entrada de datos", GEnlace = "Enlace a la app", GApp = "Herramientas de la app", GDiseno = "Diseño";

    public static readonly IReadOnlyList<Herramienta> Herramientas = new Herramienta[]
    {
        new(TipoCampoModulo.Texto, GEntrada, "Texto", "Una línea de texto", "<path d='M4 7V5h16v2M12 5v14M9 19h6'/>"),
        new(TipoCampoModulo.Parrafo, GEntrada, "Párrafo", "Texto largo, varias líneas", "<path d='M4 6h16M4 10h16M4 14h16M4 18h10'/>"),
        new(TipoCampoModulo.Numero, GEntrada, "Número", "Con unidad y decimales", "<path d='M9 4 7 20M17 4l-2 16M4 9h16M3 15h16'/>"),
        new(TipoCampoModulo.Fecha, GEntrada, "Fecha", "Día/mes/año", "<rect x='3.5' y='5' width='17' height='15.5' rx='2'/><path d='M3.5 9.5h17M8 3v4M16 3v4'/>"),
        new(TipoCampoModulo.Lista, GEntrada, "Lista / catálogo", "Opciones propias o de Catálogos", "<path d='M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01'/>"),
        new(TipoCampoModulo.SiNo, GEntrada, "Sí / No", "Interruptor", "<rect x='3' y='7' width='18' height='10' rx='5'/><circle cx='16' cy='12' r='3'/>"),
        new(TipoCampoModulo.Chequeo, GEntrada, "Lista de chequeo", "Ítems para marcar", "<path d='m3.5 6 1.5 1.5 3-3M3.5 12.5l1.5 1.5 3-3M3.5 19l1.5 1.5 3-3M12 6h8.5M12 12.5h8.5M12 19h8.5'/>"),
        new(TipoCampoModulo.Firma, GEntrada, "Firma", "Firma a mano en la pantalla", "<path d='M3 17c3-1 4-9 7-9 2 0 0 7 3 7s3-3 5-3 2 2 3 2M3 21h18'/>"),
        new(TipoCampoModulo.Foto, GEntrada, "Foto", "Cámara del celular o archivo", "<path d='M4 8h3l2-3h6l2 3h3v11H4z'/><circle cx='12' cy='13' r='3.5'/>"),
        new(TipoCampoModulo.Archivos, GEntrada, "Adjuntos", "PDF, Excel, planos y otros archivos", "<path d='m21 12-8.5 8.5a5 5 0 0 1-7-7L14 5a3.5 3.5 0 0 1 5 5l-8.5 8.5a2 2 0 0 1-3-3L15 8'/>"),
        new(TipoCampoModulo.Vinculo, GEnlace, "Selector de registro", "Elige una torre, equipo, plano… de la app", "<path d='M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1'/>", true),
        new(TipoCampoModulo.Tabla, GApp, "Tabla de datos", "Registros de cualquier módulo de la app", "<rect x='3' y='4' width='18' height='16' rx='2'/><path d='M3 9h18M3 14h18M9 4v16'/>", true),
        new(TipoCampoModulo.Grafico, GApp, "Gráfico", "Barras, líneas o torta con datos de la app", "<path d='M3 20h18M6 17V10M11 17V6M16 17v-4M20 17V8'/>", true),
        new(TipoCampoModulo.Kpi, GApp, "Indicador (KPI)", "Una cifra calculada", "<circle cx='12' cy='13' r='8'/><path d='m12 13 4-4'/>", true),
        new(TipoCampoModulo.Estadistica, GApp, "Estadística", "Media, desviación, mínimo y máximo", "<path d='M3 20h18M3 17c3.5 0 4.5-11 9-11s5.5 11 9 11'/>", true),
        new(TipoCampoModulo.Mapa, GApp, "Mapa", "Ubica torres y elementos", "<path d='M9 4 3 6v14l6-2 6 2 6-2V4l-6 2z'/><path d='M9 4v14M15 6v14'/>", true),
        new(TipoCampoModulo.Pdf, GApp, "Visor PDF", "Planos y documentos vigentes", "<path d='M6 3h8l4 4v14H6z'/><path d='M14 3v4h4M9 13h6M9 17h4'/>", true),
        new(TipoCampoModulo.Modelo3D, GApp, "Modelo 3D", "La torre en 3D", "<path d='M12 2.5 4 7v9l8 4.5 8-4.5V7z'/><path d='M4 7l8 4.5L20 7M12 11.5v9'/>", true),
        new(TipoCampoModulo.Carpetas, GApp, "Carpetas y archivos", "Archivos de un elemento de la app", "<path d='M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'/>", true),
        new(TipoCampoModulo.LineaTiempo, GApp, "Línea de tiempo", "Eventos ordenados por fecha", "<path d='M3 12h18M7 8v8M12 6v12M17 9v6'/>", true),
        new(TipoCampoModulo.Titulo, GDiseno, "Etiqueta / título", "Texto fijo para ordenar el paso", "<path d='M4 6h16M4 12h10'/>"),
        new(TipoCampoModulo.Separador, GDiseno, "Separador", "Línea divisoria", "<path d='M3 12h18'/>"),
        new(TipoCampoModulo.Grupo, GDiseno, "Grupo (marco)", "Recuadro con título para agrupar", "<rect x='3' y='5' width='18' height='14' rx='2'/><path d='M3 9h7'/>"),
    };

    public static Herramienta? HerramientaDe(TipoCampoModulo t) => Herramientas.FirstOrDefault(h => h.Tipo == t);

    // Compatibilidad: nombre e ícono de <Ico> por tipo.
    public static readonly IReadOnlyList<(TipoCampoModulo Tipo, string Nombre, string Icono, string Descripcion)> Tipos =
        Herramientas.Select(h => (h.Tipo, h.Nombre, h.Dato ? "enlace" : "formularios", h.Descripcion)).ToList();

    public static string NombreTipo(TipoCampoModulo t) => HerramientaDe(t)?.Nombre ?? t.ToString();
}
