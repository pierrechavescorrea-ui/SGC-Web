namespace SigWeb.Data.Seguridad;

// Acciones que se conceden por módulo (bits: un rol guarda la suma en un entero).
[Flags]
public enum Accion
{
    Ninguna = 0,
    Ver = 1,                  // entrar al módulo y consultar
    Descargar = 2,            // bajar archivos, exportar Excel/PDF/Word, dossier (sin esto: ver sin descargar)
    Comentar = 4,             // colaborar: seguimientos, observaciones y evidencias sin cambiar el registro
    Crear = 8,                // agregar registros y adjuntos
    Editar = 16,              // modificar registros
    Eliminar = 32,            // enviar a la Papelera
    Importar = 64,            // cargas masivas (Excel, PDF, KMZ, DWG, IFC)
    Aprobar = 128,            // aprobar versiones, cerrar, verificar eficacia
    ConfigurarCampos = 256,   // columnas, fórmulas y formatos condicionales de las tablas
    EditarVistas = 512,       // diseñar vistas configurables
    EditarFormularios = 1024, // diseñador de formularios
    Administrar = 2048,       // catálogos propios, Papelera del módulo, ajustes del módulo
    Todas = 4095,
}

public static class Acciones
{
    public sealed record Def(Accion Accion, string Nombre, string Corto, string Descripcion);

    public static readonly IReadOnlyList<Def> Lista = new Def[]
    {
        new(Accion.Ver, "Ver", "Ver", "Entrar al módulo y consultar"),
        new(Accion.Descargar, "Descargar", "Desc.", "Bajar archivos y exportar (sin esto: ver sin descargar)"),
        new(Accion.Comentar, "Colaborar", "Colab.", "Seguimientos, observaciones y evidencias"),
        new(Accion.Crear, "Crear", "Crear", "Agregar registros y adjuntos"),
        new(Accion.Editar, "Editar", "Editar", "Modificar registros"),
        new(Accion.Eliminar, "Eliminar", "Elim.", "Enviar a la Papelera"),
        new(Accion.Importar, "Importar", "Imp.", "Cargas masivas (Excel, PDF, KMZ, DWG, IFC)"),
        new(Accion.Aprobar, "Aprobar", "Apr.", "Aprobar versiones, cerrar, verificar eficacia"),
        new(Accion.ConfigurarCampos, "Configurar campos", "Campos", "Columnas, fórmulas y formatos condicionales"),
        new(Accion.EditarVistas, "Editar vistas", "Vistas", "Diseñar vistas configurables"),
        new(Accion.EditarFormularios, "Editar formularios", "Form.", "Diseñador de formularios"),
        new(Accion.Administrar, "Administrar", "Admin.", "Catálogos, Papelera y ajustes del módulo"),
    };

    // Acciones que cambian datos (para saber si alguien es "solo lectura" en un módulo).
    public const Accion Escritura = Accion.Comentar | Accion.Crear | Accion.Editar | Accion.Eliminar | Accion.Importar |
        Accion.Aprobar | Accion.ConfigurarCampos | Accion.EditarVistas | Accion.EditarFormularios | Accion.Administrar;

    public static string Texto(Accion a) =>
        a == Accion.Ninguna ? "Sin acceso" : string.Join(", ", Lista.Where(d => a.HasFlag(d.Accion)).Select(d => d.Nombre));
}

public enum AmbitoModulo { Proyecto, Empresa, Ambos }

// Catálogo de módulos con su clave (= ruta del menú, la misma de MapaNavegacion) y valores por defecto.
public static class CatalogoModulos
{
    public sealed record Def(string Clave, string Nombre, string Grupo, AmbitoModulo Ambito,
        bool EnEmpresa, bool EnProyecto, bool CompartidoDesdeEmpresa = false, bool Sensible = false, bool Configuracion = false);

    private const string Ctx = "Contexto y liderazgo", Pla = "Planificación", Apo = "Apoyo", Ope = "Operación",
        Eva = "Evaluación y mejora", Cfg = "Configuración", Gen = "General";

    // Módulos del código. Los creados por el administrador (Creador de módulos) se suman con FijarCreados.
    public static readonly IReadOnlyList<Def> Fijos = new Def[]
    {
        new("inicio", "Inicio (panel)", Gen, AmbitoModulo.Ambos, true, true),
        new("direccionamiento", "Direccionamiento estratégico", Ctx, AmbitoModulo.Empresa, true, true, CompartidoDesdeEmpresa: true),
        new("partes-interesadas", "Partes interesadas", Ctx, AmbitoModulo.Ambos, true, true),
        new("organigrama", "Organigrama", Ctx, AmbitoModulo.Ambos, true, true),
        new("manuales-funciones", "Manuales de funciones", Ctx, AmbitoModulo.Empresa, true, true, CompartidoDesdeEmpresa: true),
        new("riesgos", "Riesgos", Pla, AmbitoModulo.Ambos, true, true),
        new("costos", "Costos y presupuestos", Pla, AmbitoModulo.Proyecto, false, true, Sensible: true),
        new("certificaciones", "Certificaciones", Apo, AmbitoModulo.Empresa, true, true, CompartidoDesdeEmpresa: true),
        new("equipos", "Equipos", Apo, AmbitoModulo.Ambos, true, true),
        new("comunicados", "Comunicados", Apo, AmbitoModulo.Ambos, true, true),
        new("planos", "Planos", Apo, AmbitoModulo.Proyecto, false, true),
        new("documentos", "Documentos", Apo, AmbitoModulo.Ambos, true, true),
        new("dossier", "Dossier", Apo, AmbitoModulo.Ambos, true, true),
        new("matriz-pie", "Matriz PIE", Ope, AmbitoModulo.Proyecto, false, true),
        new("trazado", "Trazado", Ope, AmbitoModulo.Proyecto, false, true),
        new("avance-obra", "Avance de obra", Ope, AmbitoModulo.Proyecto, false, true),
        new("ifc", "Modelos 3D", Ope, AmbitoModulo.Proyecto, false, true),
        new("materiales", "Materiales", Ope, AmbitoModulo.Proyecto, false, true),
        new("ensayos", "Ensayos", Ope, AmbitoModulo.Proyecto, false, true),
        new("indicadores", "Indicadores", Eva, AmbitoModulo.Ambos, true, true),
        new("auditoria-interna", "Auditoría interna", Eva, AmbitoModulo.Ambos, true, true),
        new("planes-accion", "Planes de acción", Eva, AmbitoModulo.Ambos, true, true),
        new("costos-no-calidad", "Costos de no calidad", Eva, AmbitoModulo.Ambos, true, true, Sensible: true),
        new("actividades", "Actividades", Cfg, AmbitoModulo.Proyecto, false, true, Configuracion: true),
        new("tiros", "Tiros", Cfg, AmbitoModulo.Proyecto, false, true, Configuracion: true),
        new("catalogos", "Catálogos", Cfg, AmbitoModulo.Ambos, true, true, Configuracion: true),
        new("editor", "Editor (formularios y vistas)", Cfg, AmbitoModulo.Ambos, true, true, Configuracion: true),
        new("temas", "Temas", Cfg, AmbitoModulo.Ambos, true, true),
        new("telegram", "Consultas por Telegram", Gen, AmbitoModulo.Ambos, true, true),
        new("papelera", "Papelera", Gen, AmbitoModulo.Ambos, true, true, Configuracion: true),
    };

    // Lista completa = módulos del código + módulos creados publicados, cada creado al final de su capítulo (así los grupos
    // de Roles, Proyectos y Dossier siguen en el orden del menú). Se reemplaza entera (copia inmutable): nunca se modifica en sitio.
    public static IReadOnlyList<Def> Lista => _estado.Lista;

    private sealed record Estado(IReadOnlyList<Def> Lista, Dictionary<string, Def> PorClave);
    private static volatile Estado _estado = Armar(Array.Empty<Def>());

    // Prefijo de la clave de permiso de un módulo creado ("mod-inspecciones"); su ruta es /mod/{clave}.
    public const string PrefijoCreado = "mod-";
    public static bool EsCreado(string? clave) => clave is not null && clave.StartsWith(PrefijoCreado, StringComparison.OrdinalIgnoreCase);

    public static void FijarCreados(IEnumerable<Def> creados) => _estado = Armar(creados.Where(c => EsCreado(c.Clave)).ToList());

    private static Estado Armar(IReadOnlyList<Def> creados)
    {
        var lista = new List<Def>(Fijos);
        foreach (var c in creados)
        {
            var ultimo = lista.FindLastIndex(d => d.Grupo == c.Grupo);
            if (ultimo < 0) ultimo = lista.FindLastIndex(d => d.Grupo != Cfg && d.Grupo != Gen);
            lista.Insert(ultimo + 1, c);
        }
        var porClave = new Dictionary<string, Def>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in lista) porClave.TryAdd(d.Clave, d);
        return new Estado(lista, porClave);
    }

    // Capítulos del menú en los que el administrador puede poner un módulo creado.
    public static readonly IReadOnlyList<string> GruposMenu = new[] { Ctx, Pla, Apo, Ope, Eva, Cfg };

    public static Def? De(string? clave) => clave is not null && _estado.PorClave.TryGetValue(clave, out var d) ? d : null;

    // Ruta (primer tramo de la dirección) → clave de módulo. Rutas sin módulo propio = "inicio".
    public static string ClaveDeRuta(string? rutaRelativa)
    {
        var primero = (rutaRelativa ?? "").Split('?', '#')[0].Trim('/').Split('/')[0].ToLowerInvariant();
        return primero switch
        {
            "" => "inicio",
            "modelos3d" => "ifc",
            "linea3d" => "avance-obra",
            // Módulo creado: /mod/{clave} → "mod-{clave}" (exista o no: sin definición no hay permiso y la guarda niega).
            "mod" => PrefijoCreado + SegundoTramo(rutaRelativa),
            _ when _estado.PorClave.ContainsKey(primero) => primero,
            _ => "inicio",
        };
    }

    private static string SegundoTramo(string? ruta)
    {
        var partes = (ruta ?? "").Split('?', '#')[0].Trim('/').Split('/');
        return partes.Length > 1 ? partes[1].ToLowerInvariant() : "";
    }
}

// Roles de fábrica (se siembran una vez; luego la matriz es editable en Administración › Roles y permisos).
public static class RolesFabrica
{
    public const string AdministradorGlobal = "AdministradorSGC";
    public const string AuditorGlobal = "AuditorSGC";

    public sealed record Def(string Clave, string Nombre, string Descripcion, int Orden);

    public static readonly IReadOnlyList<Def> Lista = new Def[]
    {
        new("consulta-restringida", "Consulta sin descarga", "Ve la información del proyecto pero no descarga ni exporta archivos. Útil para cliente o interventoría.", 10),
        new("consulta", "Consulta", "Ve y descarga. No modifica nada.", 20),
        new("colaborador", "Colaborador", "Consulta, registra seguimientos, observaciones y evidencias y crea registros nuevos.", 30),
        new("editor", "Editor", "Crea, edita, importa y envía a la Papelera en los módulos operativos.", 40),
        new("aprobador", "Coordinador / aprobador", "Editor que además aprueba versiones, cierra planes y verifica eficacia; ve costos.", 50),
        new("admin-proyecto", "Administrador del proyecto", "Todo dentro del proyecto: configuración, campos, vistas, formularios, catálogos y Papelera.", 60),
    };

    public static Accion Permiso(string rol, CatalogoModulos.Def m)
    {
        const Accion lectura = Accion.Ver;
        const Accion descarga = Accion.Ver | Accion.Descargar;
        const Accion colabora = descarga | Accion.Comentar | Accion.Crear;
        const Accion edita = colabora | Accion.Editar | Accion.Eliminar | Accion.Importar;
        if (rol == "admin-proyecto") return Accion.Todas;
        if (m.Clave == "temas") return Accion.Ver;
        if (m.Clave == "telegram") return rol == "consulta-restringida" ? Accion.Ver : Accion.Ver | Accion.Descargar;
        if (m.Configuracion)
            return rol switch
            {
                "aprobador" when m.Clave is "actividades" or "tiros" or "catalogos" => Accion.Ver,
                "editor" when m.Clave is "actividades" or "tiros" or "catalogos" => Accion.Ver,
                _ => Accion.Ninguna,
            };
        if (m.Sensible)
            return rol switch { "aprobador" => descarga, _ => Accion.Ninguna };
        return rol switch
        {
            "consulta-restringida" => lectura,
            "consulta" => descarga,
            "colaborador" => colabora,
            "editor" => edita,
            "aprobador" => edita | Accion.Aprobar,
            _ => Accion.Ninguna,
        };
    }
}
