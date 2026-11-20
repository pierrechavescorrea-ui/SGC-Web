using System.Globalization;
using System.Text;

namespace SigWeb.Data.Asistente;

// Normalización del español para el asistente de ayuda (todo local, sin servicios externos):
// minúsculas, sin tildes, sin palabras vacías, raíz aproximada (cargar/carga/cargue → "carg") y sinónimos
// llevados a una forma común (subir → cargar, borrar → eliminar…). Lo usan por igual el índice y la pregunta.
public static class TextoAyuda
{
    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "a","al","algo","alguien","ante","antes","aqui","asi","aun","cada","como","con","cual","cuales","cuando","de","del","desde","donde",
        "dos","el","ella","ellas","ellos","en","entre","era","es","esa","esas","ese","eso","esos","esta","estas","este","esto","estos","estoy",
        "fue","ha","hace","hacer","hago","hay","la","las","le","les","lo","los","mas","me","mi","mis","muy","ni","no","nos","o","os","otra",
        "otro","para","pero","poco","por","porque","puede","puedo","pueden","que","quiero","se","sea","ser","si","sin","sobre","son","su",
        "sus","tambien","te","tengo","tiene","toda","todas","todo","tu","tus","un","una","unas","uno","unos","usted","va","voy","y","ya",
        "quisiera","necesito","debo","deberia","favor","ayuda","ayudame","ayudeme","hola","gracias","sirve","funciona","hacerlo","quien","cosa","cosas","dime","explicame","indiqueme","solo","saco",
    };

    // Sinónimos → palabra del manual. Se aplican ANTES de sacar la raíz.
    private static readonly Dictionary<string, string> Sinonimos = new(StringComparer.Ordinal)
    {
        ["subir"] = "cargar", ["sube"] = "cargar", ["subo"] = "cargar", ["adjuntar"] = "cargar", ["anexar"] = "cargar",
        ["borrar"] = "eliminar", ["borro"] = "eliminar", ["quitar"] = "eliminar", ["suprimir"] = "eliminar", ["eliminacion"] = "eliminar",
        ["crear"] = "agregar", ["creo"] = "agregar", ["anadir"] = "agregar", ["nuevo"] = "agregar", ["nueva"] = "agregar", ["registrar"] = "agregar",
        ["modificar"] = "editar", ["cambiar"] = "editar", ["corregir"] = "editar", ["actualizar"] = "editar", ["edicion"] = "editar",
        ["recuperar"] = "restaurar", ["devolver"] = "restaurar", ["deshacer"] = "restaurar",
        ["descargar"] = "exportar", ["bajar"] = "exportar", ["clave"] = "contrasena", ["password"] = "contrasena",
        ["encontrar"] = "buscar", ["localizar"] = "buscar", ["busco"] = "buscar", ["ubico"] = "buscar",
        ["borrados"] = "papelera", ["eliminados"] = "papelera", ["reciclaje"] = "papelera",
        ["login"] = "ingresar", ["entrar"] = "ingresar", ["iniciar"] = "ingresar", ["sesion"] = "ingresar",
        ["acceso"] = "permiso", ["accesos"] = "permiso", ["permisos"] = "permiso", ["rol"] = "permiso", ["roles"] = "permiso",
        ["coordenada"] = "coordenadas", ["ubicacion"] = "coordenadas", ["localizacion"] = "coordenadas", ["posicion"] = "coordenadas",
        ["excel"] = "excel", ["xlsx"] = "excel", ["planilla"] = "excel",
        ["foto"] = "foto", ["fotos"] = "foto", ["imagen"] = "foto", ["imagenes"] = "foto",
        ["celular"] = "telegram", ["movil"] = "telegram", ["telefono"] = "telegram",
        ["vencida"] = "vencer", ["vencidas"] = "vencer", ["vence"] = "vencer", ["vencimiento"] = "vencer",
        ["calibrar"] = "calibracion", ["calibraciones"] = "calibracion",
        ["nc"] = "hallazgo", ["noconformidad"] = "hallazgo", ["hallazgos"] = "hallazgo",
        ["abro"] = "abrir", ["anado"] = "agregar", ["ingreso"] = "registrar", ["registro"] = "agregar",
        ["importo"] = "importar", ["borre"] = "eliminar", ["elimino"] = "eliminar", ["recupero"] = "restaurar", ["revertir"] = "restaurar",
        ["paso"] = "editar", ["cambio"] = "editar", ["edito"] = "editar",
        ["conecto"] = "vincular", ["conectar"] = "vincular", ["vinculo"] = "vincular",
        ["duplicadas"] = "repetidas", ["duplicados"] = "repetidos", ["puesto"] = "cargo", ["puestos"] = "cargo",
        ["vercion"] = "version", ["verciones"] = "version", ["exel"] = "excel", ["muchos"] = "varios", ["muchas"] = "varios",
        ["consecutibo"] = "consecutivo", ["hallasgo"] = "hallazgo",
        ["histograma"] = "distribucion", ["histogramas"] = "distribucion",
        ["vijente"] = "vigente", ["sertificado"] = "certificado",
        ["revision"] = "version", ["revisiones"] = "version", ["versiones"] = "version", ["rev"] = "version",
        // Formas frecuentes en obra y errores de escritura comunes.
        ["subi"] = "cargar", ["cargue"] = "cargar", ["montar"] = "cargar", ["pegar"] = "cargar",
        ["descargo"] = "exportar", ["baje"] = "exportar", ["bajo"] = "exportar", ["imprimir"] = "exportar", ["imprimo"] = "exportar",
        ["mirar"] = "ver", ["miro"] = "ver", ["consultar"] = "ver", ["consulto"] = "ver", ["visualizar"] = "ver", ["veo"] = "ver",
        ["tirar"] = "eliminar", ["sacar"] = "eliminar", ["saco"] = "eliminar", ["anular"] = "eliminar",
        ["archivos"] = "archivo", ["adjunto"] = "archivo", ["adjuntos"] = "archivo", ["anexo"] = "archivo", ["anexos"] = "archivo",
        ["soporte"] = "archivo", ["soportes"] = "archivo", ["evidencia"] = "archivo", ["evidencias"] = "archivo",
        ["documentos"] = "documento", ["pdfs"] = "pdf", ["torres"] = "torre", ["apoyo"] = "torre", ["apoyos"] = "torre",
        ["tablero"] = "tablero", ["dashboard"] = "tablero", ["grafica"] = "grafico", ["graficas"] = "grafico", ["graficos"] = "grafico",
        ["filtrar"] = "filtro", ["filtros"] = "filtro", ["ordenar"] = "orden", ["columnas"] = "columna",
        ["usuario"] = "usuario", ["usuarios"] = "usuario", ["contrasenia"] = "contrasena",
        ["olvide"] = "olvido", ["olvidé"] = "olvido",
        ["inspecciones"] = "inspeccion", ["modulos"] = "modulo", ["formularios"] = "formulario",
        ["documetno"] = "documento", ["docuemnto"] = "documento", ["calibrasion"] = "calibracion", ["enzayo"] = "ensayo",
        ["ensallo"] = "ensayo", ["ensayos"] = "ensayo", ["equipos"] = "equipo", ["planos"] = "plano", ["riesgos"] = "riesgo",
    };

    // Frases de queja o duda que no tienen palabras «útiles» pero sí intención: se agregan términos para orientar la búsqueda.
    private static readonly (string Frase, string[] Terminos)[] Frases =
    {
        (" no me deja ", new[] { "permiso" }), (" no puedo ", new[] { "permiso" }), (" no tengo acceso ", new[] { "permiso" }),
        (" no me aparece ", new[] { "problema", "ver" }), (" no aparece ", new[] { "problema", "ver" }), (" no veo ", new[] { "problema", "ver" }),
        (" no sale ", new[] { "problema" }), (" no funciona ", new[] { "problema" }), (" no carga ", new[] { "problema", "cargar" }),
        (" error ", new[] { "problema" }), (" se perdio ", new[] { "papelera", "restaurar" }), (" lo borre ", new[] { "papelera", "restaurar" }),
        (" donde esta ", new[] { "buscar" }), (" donde queda ", new[] { "buscar" }), (" para que sirve ", new[] { "sirve" }),
    };

    // Términos extra por las frases de la pregunta (ya normalizados como el índice).
    public static List<string> TerminosDeFrases(string? texto)
    {
        var t = " " + string.Join(' ', Palabras(texto)) + " ";
        var r = new List<string>();
        foreach (var (f, ts) in Frases)
            if (t.Contains(f)) r.AddRange(ts.Select(x => Raiz(Sinonimos.TryGetValue(x, out var s) ? s : x)));
        return r.Distinct().ToList();
    }

    public static string SinTildes(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // Palabras de un texto (minúsculas, sin tildes ni signos). Conserva números y códigos (F-054 → "f054").
    public static IEnumerable<string> Palabras(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) yield break;
        var t = SinTildes(texto.ToLowerInvariant()).Replace("ñ", "n");
        var sb = new StringBuilder();
        foreach (var ch in t)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch == '-' && sb.Length > 0) continue;   // "F-054" y "PR-05" quedan juntos
            else if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static readonly string[] Finales =
    {
        "amientos","imientos","amiento","imiento","aciones","iciones","acion","icion","adoras","adores","adora","ador",
        "mente","ando","iendo","idos","idas","ido","ida","iones","ion",
        "ar","er","ir","an","en","as","es","os","ue","a","e","o","s",
    };

    // Raíz aproximada: quita una terminación frecuente dejando al menos 4 letras. Suficiente para emparejar
    // formas del mismo verbo o sustantivo (no es un stemmer completo, pero no necesita diccionario).
    public static string Raiz(string palabra)
    {
        if (palabra.Length <= 4 || palabra.Any(char.IsDigit)) return palabra;
        foreach (var f in Finales)
            if (palabra.EndsWith(f, StringComparison.Ordinal) && palabra.Length - f.Length >= 4)
                return palabra[..^f.Length];
        return palabra;
    }

    // Términos para el índice o la búsqueda: sin vacías, con sinónimos y raíz.
    public static List<string> Terminos(string? texto, bool conVacias = false)
    {
        var r = new List<string>();
        foreach (var p in Palabras(texto))
        {
            if (!conVacias && Vacias.Contains(p)) continue;
            if (p.Length == 1 && !char.IsDigit(p[0])) continue;
            var w = Sinonimos.TryGetValue(p, out var s) ? s : p;
            r.Add(Raiz(w));
        }
        return r;
    }

    public static bool EsVacia(string palabra) => Vacias.Contains(palabra);
}
