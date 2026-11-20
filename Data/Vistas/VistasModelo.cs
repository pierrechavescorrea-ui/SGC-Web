using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Vistas;

// Vistas configurables de un registro: un dashboard de widgets en una rejilla de 12 columnas (motor sigPanel de
// js/inicio.js). Cada widget = tipo + posición/tamaño + su configuración (pasos, indicador, campo, texto…).
public sealed class Widget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Tipo { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; } = 4;
    public int H { get; set; } = 4;
    public string? Titulo { get; set; }              // null = el del tipo
    public List<string> Pasos { get; set; } = new(); // widget "pasos": claves de los pasos del formulario
    public string? Kpi { get; set; }                 // widget "kpi": cuál indicador
    public string? Campo { get; set; }               // widget "campo": clave del campo en el formulario
    public string? Texto { get; set; }               // widget "nota": indicación que se ve mientras el plan no tiene nota
    // widget "nota": su texto es de CADA registro, guardado en un campo oculto de la tabla del módulo ("c:{id}" en Campo).
}

public sealed record TipoWidget(string Tipo, string Nombre, string Descripcion, string Icono, int W, int H, int MinW = 2, int MinH = 2);

public static class VistasModelo
{
    public const int IdClasica = 0;
    public const string NombreClasica = "Clásica (por defecto)";
    private static readonly JsonSerializerOptions Opc = new() { PropertyNameCaseInsensitive = true };

    // Módulos con vistas configurables (Catálogos › Vistas). Módulo nuevo = una línea aquí + sus widgets en su página.
    public sealed record ModuloVista(string Modulo, string Nombre, string Ruta, string Vista);
    public static readonly IReadOnlyList<ModuloVista> Modulos = new[]
    {
        new ModuloVista("planes-accion", "Planes de acción", "planes-accion", "Plan por plan"),
        new ModuloVista("costos-no-calidad", "Costos de no calidad", "costos-no-calidad", "Pantalla principal"),
        new ModuloVista("riesgos", "Riesgos", "riesgos", "Matriz (pantalla principal)"),
        new ModuloVista("partes-interesadas", "Partes interesadas", "partes-interesadas", "Mapa (pantalla principal)"),
        new ModuloVista("indicadores", "Indicadores", "indicadores", "Indicador por indicador"),
    };

    // ---------- Widgets de los demás módulos (componente Components/Layout/VistaWidgets) ----------
    // Opciones: widgets con una variante (indicador, gráfico…) que se elige en el diseño y se guarda en Widget.Kpi.
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<TipoWidget>> TiposPorModulo = new Dictionary<string, IReadOnlyList<TipoWidget>>
    {
        ["costos-no-calidad"] = new[]
        {
            new TipoWidget("tablero", "Tablero", "Cifra del periodo con tendencia, 4 indicadores y composición por rubro", "indicador", 12, 6, 6, 4),
            new TipoWidget("kpi", "Indicador", "Una cifra: costo del periodo, planes con costo, costo promedio o rubro principal", "indicador", 3, 2, 2, 2),
            new TipoWidget("composicion", "Composición por rubro", "Barra segmentada con la leyenda de rubros", "grafico", 6, 4, 3, 2),
            new TipoWidget("grafico", "Gráfico", "Pareto por rubro, costo por mes, costo por agrupación o planes con mayor costo", "grafico", 6, 7, 3, 4),
            new TipoWidget("plan", "Plan por plan", "Lista de planes con el detalle del elegido", "plan-accion", 12, 12, 6, 6),
            new TipoWidget("tabla", "Tabla", "Un plan por fila, un rubro por columna, con totales", "tabla", 12, 9, 6, 4),
        },
        ["riesgos"] = new[]
        {
            new TipoWidget("kpi", "Indicador", "Riesgos, extremos/altos, materializados, sin seguimiento o planes abiertos", "indicador", 3, 2, 2, 2),
            new TipoWidget("mapa", "Mapa de calor", "Probabilidad × impacto: clic en una celda filtra", "riesgos", 5, 8, 3, 5),
            new TipoWidget("tarjetas", "Riesgos (tarjetas)", "Los riesgos de la celda elegida, por severidad", "listado", 7, 8, 3, 4),
            new TipoWidget("niveles", "Riesgos por nivel", "Barras por nivel de riesgo (escala del catálogo)", "grafico", 4, 4, 3, 3),
            new TipoWidget("categorias", "Riesgos por categoría", "Barras por categoría", "grafico", 4, 5, 3, 3),
        },
        ["partes-interesadas"] = new[]
        {
            new TipoWidget("kpi", "Indicador", "Partes, cuadrantes, vencimientos, inactivas o hallazgos abiertos", "indicador", 3, 2),
            new TipoWidget("mapa", "Mapa de influencia × interés", "Elija un cuadrante para filtrar las tarjetas", "grafico", 6, 10, 4, 6),
            new TipoWidget("tarjetas", "Tarjetas de partes", "Partes del cuadrante elegido y sus fichas", "listado", 6, 10, 3, 4),
            new TipoWidget("seguimiento", "Seguimiento", "Línea de tiempo o próximos seguimientos", "calendario", 8, 7, 4, 4),
            new TipoWidget("resultados", "Resultados de los seguimientos", "Resumen anual y detalle por parte", "indicador", 12, 12, 6, 6),
            new TipoWidget("version", "Versión vigente y borrador", "Identifica las versiones y permite abrirlas", "versiones", 4, 3),
        },
        ["indicadores"] = new[]
        {
            new TipoWidget("lista", "Lista de indicadores", "Indicadores con su último valor y la tira de 12 meses", "listado", 4, 12, 3, 5),
            new TipoWidget("detalle", "Detalle del indicador", "Último valor, meta, meses que cumplen, mosaicos de mes y gráfico", "indicador", 8, 12, 4, 6),
            new TipoWidget("kpi", "Indicador", "Una cifra del indicador elegido: último valor, meses que cumplen, promedio o meta", "indicador", 3, 2, 2, 2),
            new TipoWidget("tablero", "Tablero de indicadores", "Tarjetas de todos los indicadores", "tablero", 12, 8, 4, 4),
            new TipoWidget("ficha", "Ficha del indicador", "Fórmula, objetivo, frecuencia, fuente y responsable", "informacion", 4, 6, 3, 3),
        },
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<(string Clave, string Nombre)>>> OpcionesPorModulo =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<(string, string)>>>
        {
            ["costos-no-calidad"] = new Dictionary<string, IReadOnlyList<(string, string)>>
            {
                ["kpi"] = new[] { ("total", "Costo del periodo"), ("planes", "Planes con costo"), ("promedio", "Costo promedio por plan"), ("rubro", "Rubro principal"), ("mes", "Mes de mayor costo") },
                ["grafico"] = new[] { ("pareto", "Costo por rubro (Pareto)"), ("mes", "Costo por mes"), ("grupo", "Costo por agrupación"), ("planes", "Planes con mayor costo") },
            },
            ["riesgos"] = new Dictionary<string, IReadOnlyList<(string, string)>>
            {
                ["kpi"] = new[] { ("total", "Riesgos"), ("altos", "Extremos y altos"), ("materializados", "Materializados"), ("sinseg", "Sin seguimiento (30 d)"), ("planes", "Planes abiertos") },
            },
            ["partes-interesadas"] = new Dictionary<string, IReadOnlyList<(string, string)>>
            {
                ["kpi"] = new[]
                {
                    ("total", "Partes interesadas"), ("cerca", "Gestionar de cerca"), ("satisfecho", "Mantener satisfecho"),
                    ("informado", "Mantener informado"), ("minimo", "Mínimo esfuerzo"), ("vencidos", "Seguimientos vencidos"),
                    ("por-vencer", "Seguimientos por vencer"), ("inactivas", "Partes inactivas"), ("hallazgos", "Hallazgos abiertos"),
                },
                ["seguimiento"] = new[] { ("linea", "Línea de tiempo"), ("proximos", "Próximos seguimientos") },
            },
            ["indicadores"] = new Dictionary<string, IReadOnlyList<(string, string)>>
            {
                ["kpi"] = new[] { ("ultimo", "Último valor"), ("cumplen", "Meses que cumplen"), ("promedio", "Promedio del año"), ("meta", "Meta") },
            },
        };

    public static IReadOnlyList<TipoWidget> TiposDe(string modulo) =>
        modulo == "planes-accion" ? WidgetsPlan : TiposPorModulo.TryGetValue(modulo, out var l) ? l : Array.Empty<TipoWidget>();

    public static IReadOnlyList<(string Clave, string Nombre)> OpcionesDe(string modulo, string tipo) =>
        modulo == "planes-accion" && tipo == "kpi" ? KpisPlan
        : OpcionesPorModulo.TryGetValue(modulo, out var d) && d.TryGetValue(tipo, out var l) ? l : Array.Empty<(string, string)>();

    // Disposición de una vista nueva en cada módulo (parecida a la clásica).
    public static List<Widget> InicialDe(string modulo) => modulo switch
    {
        "costos-no-calidad" => new()
        {
            new() { Tipo = "tablero", X = 0, Y = 0, W = 12, H = 6 },
            new() { Tipo = "plan", X = 0, Y = 6, W = 12, H = 12 },
        },
        "riesgos" => new()
        {
            new() { Tipo = "kpi", Kpi = "total", X = 0, Y = 0, W = 3, H = 2 },
            new() { Tipo = "kpi", Kpi = "altos", X = 3, Y = 0, W = 3, H = 2 },
            new() { Tipo = "kpi", Kpi = "materializados", X = 6, Y = 0, W = 3, H = 2 },
            new() { Tipo = "kpi", Kpi = "planes", X = 9, Y = 0, W = 3, H = 2 },
            new() { Tipo = "mapa", X = 0, Y = 2, W = 5, H = 9 },
            new() { Tipo = "tarjetas", X = 5, Y = 2, W = 7, H = 9 },
        },
        "partes-interesadas" => new()
        {
            new() { Tipo = "kpi", Kpi = "total", X = 0, Y = 0, W = 3, H = 2 },
            new() { Tipo = "kpi", Kpi = "vencidos", X = 3, Y = 0, W = 3, H = 2 },
            new() { Tipo = "kpi", Kpi = "por-vencer", X = 6, Y = 0, W = 3, H = 2 },
            new() { Tipo = "kpi", Kpi = "hallazgos", X = 9, Y = 0, W = 3, H = 2 },
            new() { Tipo = "mapa", X = 0, Y = 2, W = 6, H = 11 },
            new() { Tipo = "tarjetas", X = 6, Y = 2, W = 6, H = 11 },
        },
        "indicadores" => new()
        {
            new() { Tipo = "lista", X = 0, Y = 0, W = 4, H = 13 },
            new() { Tipo = "detalle", X = 4, Y = 0, W = 8, H = 13 },
        },
        _ => InicialPlan(),
    };

    // Widgets de Planes de acción.
    public static readonly IReadOnlyList<TipoWidget> WidgetsPlan = new[]
    {
        new TipoWidget("encabezado", "Encabezado", "Código, descripción, etiquetas y anterior/siguiente", "plan-accion", 12, 3, 4, 2),
        new TipoWidget("kpi", "Indicador", "Estado, avance, próxima acción, días, acciones abiertas o vencidas, adjuntos o eficacia", "indicador", 3, 2, 2, 2),
        new TipoWidget("acciones", "Acciones (línea de tiempo)", "Las acciones propuestas con su estado, responsable y productos", "seguimiento", 8, 8, 3, 3),
        new TipoWidget("tabla-acciones", "Acciones (tabla)", "Las acciones en una tabla compacta", "tabla", 8, 6, 4, 3),
        new TipoWidget("ficha", "Ficha del hallazgo", "Descripción, análisis de causa, lugares, riesgos y adjuntos", "informacion", 4, 8, 3, 3),
        new TipoWidget("pasos", "Pasos del formulario", "Uno o varios pasos con sus campos y valores (también los campos nuevos)", "formularios", 6, 5, 3, 2),
        new TipoWidget("campo", "Un campo", "El valor de un solo campo del formulario", "editar", 3, 2, 2, 2),
        new TipoWidget("adjuntos", "Adjuntos", "Todas las carpetas del plan con sus archivos (ícono por tipo)", "adjuntar", 4, 5, 3, 3),
        new TipoWidget("etiquetas", "Etiquetas", "Tipo, origen, procesos, fecha, reportó y auditoría como fichas", "filtro", 6, 2, 2, 2),
        new TipoWidget("riesgos", "Riesgos", "Riesgos asociados al plan", "riesgos", 4, 3, 2, 2),
        new TipoWidget("lugares", "Lugares", "Torres, elementos, tiros o localizaciones del plan", "ubicacion", 4, 3, 2, 2),
        new TipoWidget("eficacia", "Verificación de eficacia", "Criterio, fecha programada, resultado y evidencia", "aprobar", 6, 4, 3, 3),
        new TipoWidget("nota", "Nota", "Nota propia de cada registro, editable en la vista", "documento", 4, 2, 2, 1),
        new TipoWidget("analisis-causa", "Análisis de causa", "Diagrama de las herramientas (5 porqués, Ishikawa, árbol, Pareto…) con maximizar y editar", "causas", 6, 7, 3, 3),
    };

    public static readonly IReadOnlyList<(string Clave, string Nombre)> KpisPlan = new[]
    {
        ("estado", "Estado del plan"), ("avance", "Avance"), ("proxima", "Próxima acción"), ("dias", "Días desde el hallazgo"),
        ("abiertas", "Acciones por cerrar"), ("vencidas", "Acciones vencidas"), ("adjuntos", "Adjuntos"), ("eficacia", "Eficacia"),
    };

    public static TipoWidget? TipoDe(string tipo) => WidgetsPlan.FirstOrDefault(t => t.Tipo == tipo);
    public static TipoWidget? TipoDe(string modulo, string tipo) => TiposDe(modulo).FirstOrDefault(t => t.Tipo == tipo);

    // Punto de partida de una vista nueva: la misma disposición de la vista clásica.
    public static List<Widget> InicialPlan() => new()
    {
        new() { Tipo = "encabezado", X = 0, Y = 0, W = 12, H = 3 },
        new() { Tipo = "kpi", Kpi = "estado", X = 0, Y = 3, W = 3, H = 2 },
        new() { Tipo = "kpi", Kpi = "avance", X = 3, Y = 3, W = 3, H = 2 },
        new() { Tipo = "kpi", Kpi = "proxima", X = 6, Y = 3, W = 3, H = 2 },
        new() { Tipo = "kpi", Kpi = "dias", X = 9, Y = 3, W = 3, H = 2 },
        new() { Tipo = "acciones", X = 0, Y = 5, W = 8, H = 9 },
        new() { Tipo = "ficha", X = 8, Y = 5, W = 4, H = 9 },
    };

    public static List<Widget> Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<Widget>>(json, Opc) ?? new(); } catch { return new(); }
    }

    public static string Escribir(List<Widget> l) => JsonSerializer.Serialize(l);

    // ---------- Guardado ----------
    public static async Task<List<VistaRegistro>> ListarAsync(AppDbContext db, string modulo)
    {
        try { return await db.VistasRegistro.AsNoTracking().Where(v => v.Modulo == modulo).OrderBy(v => v.Nombre).ToListAsync(); }
        catch { return new(); }   // sin la migración aplicada
    }

    public static async Task<int> GuardarAsync(AppDbContext db, string modulo, int id, string nombre, List<Widget> widgets)
    {
        var v = id > 0 ? await db.VistasRegistro.FirstOrDefaultAsync(x => x.Id == id) : null;
        if (v is null) { v = new VistaRegistro { Modulo = modulo }; db.VistasRegistro.Add(v); }
        v.Nombre = string.IsNullOrWhiteSpace(nombre) ? "Vista sin nombre" : nombre.Trim();
        v.Json = Escribir(widgets);
        v.FechaModificacion = DateTime.Now;
        await db.SaveChangesAsync();
        return v.Id;
    }

    // La predeterminada es una sola por módulo (0 = la clásica).
    public static async Task MarcarPredeterminadaAsync(AppDbContext db, string modulo, int id)
    {
        foreach (var v in await db.VistasRegistro.Where(x => x.Modulo == modulo).ToListAsync()) v.Predeterminada = v.Id == id;
        await db.SaveChangesAsync();
    }
}
