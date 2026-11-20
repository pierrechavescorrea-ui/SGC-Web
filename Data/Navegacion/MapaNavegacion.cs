namespace SigWeb.Data.Navegacion;

// Ruta (primer tramo de la dirección) → grupo y módulo del menú. Lo usa la ruta de navegación
// del encabezado de página (EncabezadoPagina). Si cambia el menú (NavMenu.razor), cambiar aquí.
public static class MapaNavegacion
{
    // Estilo: "listados" = tablas con el estilo de css/listados.css (data-estilo del encabezado),
    // independiente del grupo del menú.
    public sealed record Modulo(string Grupo, string Nombre, string Href, string? Estilo = null, string? Icono = null);

    // Grupos según los capítulos de ISO 9001 (mismos títulos que NavMenu.razor).
    private const string Contexto = "Contexto y liderazgo", Planif = "Planificación", Apoyo = "Apoyo", Operacion = "Operación",
        Evaluacion = "Evaluación y mejora", Config = "Configuración";
    private const string L = "listados";

    private static readonly Dictionary<string, Modulo> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["direccionamiento"] = new(Contexto, "Direccionamiento estratégico", "direccionamiento"),
        ["partes-interesadas"] = new(Contexto, "Partes interesadas", "partes-interesadas"),
        ["organigrama"] = new(Contexto, "Organigrama", "organigrama"),
        ["manuales-funciones"] = new(Contexto, "Manuales de funciones", "manuales-funciones"),
        ["riesgos"] = new(Planif, "Riesgos", "riesgos"),
        ["costos"] = new(Planif, "Costos y presupuestos", "costos"),
        ["certificaciones"] = new(Apoyo, "Certificaciones", "certificaciones"),
        ["equipos"] = new(Apoyo, "Equipos", "equipos", L),
        ["comunicados"] = new(Apoyo, "Comunicados", "comunicados", L),
        ["planos"] = new(Apoyo, "Planos", "planos", L),
        ["documentos"] = new(Apoyo, "Documentos", "documentos", L),
        ["dossier"] = new(Apoyo, "Dossier", "dossier"),
        ["matriz-pie"] = new(Operacion, "Matriz PIE", "matriz-pie"),
        ["trazado"] = new(Operacion, "Trazado", "trazado"),
        ["avance-obra"] = new(Operacion, "Avance de obra", "avance-obra"),
        ["ifc"] = new(Operacion, "Modelos 3D", "ifc"),
        ["modelos3d"] = new(Operacion, "Modelos 3D", "ifc"),
        ["materiales"] = new(Operacion, "Materiales", "materiales", L),
        ["ensayos"] = new(Operacion, "Ensayos", "ensayos", L),
        ["indicadores"] = new(Evaluacion, "Indicadores", "indicadores"),
        ["auditoria-interna"] = new(Evaluacion, "Auditoría interna", "auditoria-interna"),
        ["planes-accion"] = new(Evaluacion, "Planes de acción", "planes-accion"),
        ["costos-no-calidad"] = new(Evaluacion, "Costos de no calidad", "costos-no-calidad"),
        ["actividades"] = new(Config, "Actividades", "actividades", L),
        ["tiros"] = new(Config, "Tiros", "tiros"),
        ["catalogos"] = new(Config, "Catálogos", "catalogos"),
        ["editor"] = new(Config, "Editor", "editor"),
        ["temas"] = new(Config, "Temas", "temas"),
        ["papelera"] = new("Papelera", "Papelera", "papelera"),
    };

    public static readonly string[] OrdenGrupos = { Contexto, Planif, Apoyo, Operacion, Evaluacion, Config };

    // Program enlaza el registro publicado sin introducir una dependencia de navegación en su singleton.
    public static Func<IEnumerable<Modulo>>? Creados { get; set; }

    // Módulos del menú en su orden (sin duplicados de ruta ni la Papelera): para elegir "dónde" (Catálogos, diseñador).
    public static IReadOnlyList<Modulo> Modulos() =>
        Mapa.Values.Concat(Creados?.Invoke() ?? []).Where(m => m.Grupo != "Papelera")
            .GroupBy(m => m.Href).Select(g => g.First()).ToList();

    // Dirección relativa (sin el dominio) → módulo, o null (Home, páginas sin grupo).
    public static Modulo? De(string rutaRelativa)
    {
        var ruta = rutaRelativa.Split('?', '#')[0].Trim('/');
        var partes = ruta.Split('/');
        var primero = partes[0];
        if (primero.Equals("mod", StringComparison.OrdinalIgnoreCase) && partes.Length >= 2)
        {
            var href = "mod/" + partes[1];
            return Creados?.Invoke().FirstOrDefault(m => m.Href.Equals(href, StringComparison.OrdinalIgnoreCase));
        }
        return Mapa.TryGetValue(primero, out var m) ? m : null;
    }
}
