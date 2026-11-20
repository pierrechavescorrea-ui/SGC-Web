using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SigWeb.Data.Asistente;

// Markdown del manual → HTML para el panel del asistente. Seguro por construcción: TODO el texto se codifica
// (WebUtility.HtmlEncode) y solo se agregan etiquetas fijas (p, ul, ol, li, table, strong, code, div, a). Lo que nombra una
// opción conocida (negrita o enlace a otro capítulo) se vuelve enlace directo con ruta local (ManualAyuda.ResolverOpcion).
// Soporta lo que usa el manual: párrafos, listas (con anidación), tablas, **negrita**, `código`, [texto](enlace), "> nota".
public static class MarkdownAyuda
{
    public static string AHtml(string markdown, Func<string, string?, Enlace?>? enlace = null)
    {
        var sb = new StringBuilder();
        var lineas = markdown.Replace("\r", "").Split('\n');
        var parrafo = new List<string>();
        var pilaListas = new Stack<(string Tipo, int Sangria)>();

        void CerrarParrafo()
        {
            if (parrafo.Count == 0) return;
            sb.Append("<p>").Append(Linea(string.Join(" ", parrafo), enlace)).Append("</p>");
            parrafo.Clear();
        }
        void CerrarListas(int hasta = -1)
        {
            while (pilaListas.Count > 0 && pilaListas.Peek().Sangria > hasta)
                sb.Append(pilaListas.Pop().Tipo == "ol" ? "</li></ol>" : "</li></ul>");
        }

        for (int i = 0; i < lineas.Length; i++)
        {
            var l = lineas[i];
            var t = l.Trim();
            if (t.Length == 0) { CerrarParrafo(); continue; }

            // Tabla: líneas que empiezan con '|'
            if (t.StartsWith('|'))
            {
                CerrarParrafo(); CerrarListas();
                var filas = new List<string>();
                while (i < lineas.Length && lineas[i].Trim().StartsWith('|')) filas.Add(lineas[i++].Trim());
                i--;
                sb.Append("<table class=\"asis-tabla\">");
                bool encabezado = filas.Count > 1 && Regex.IsMatch(filas[1], @"^\|[\s:\-|]+\|?$");
                for (int f = 0; f < filas.Count; f++)
                {
                    if (encabezado && f == 1) continue;
                    var celdas = filas[f].Trim('|').Split('|');
                    var tag = encabezado && f == 0 ? "th" : "td";
                    sb.Append("<tr>");
                    foreach (var c in celdas) sb.Append('<').Append(tag).Append('>').Append(Linea(c.Trim(), enlace)).Append("</").Append(tag).Append('>');
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
                continue;
            }

            // Nota: "> texto"
            if (t.StartsWith('>'))
            {
                CerrarParrafo(); CerrarListas();
                sb.Append("<div class=\"asis-nota\">").Append(Linea(t.TrimStart('>').Trim(), enlace)).Append("</div>");
                continue;
            }

            // Elemento de lista
            var m = Regex.Match(l, @"^(\s*)([-*]|\d+[.)])\s+(.*)$");
            if (m.Success)
            {
                CerrarParrafo();
                int sangria = m.Groups[1].Value.Replace("\t", "    ").Length;
                var tipo = char.IsDigit(m.Groups[2].Value[0]) ? "ol" : "ul";
                if (pilaListas.Count == 0 || sangria > pilaListas.Peek().Sangria)
                {
                    sb.Append(tipo == "ol" ? "<ol>" : "<ul>");
                    pilaListas.Push((tipo, sangria));
                }
                else
                {
                    CerrarListas(sangria);
                    if (pilaListas.Count == 0) { sb.Append(tipo == "ol" ? "<ol>" : "<ul>"); pilaListas.Push((tipo, sangria)); }
                    else sb.Append("</li>");
                }
                sb.Append("<li>").Append(Linea(m.Groups[3].Value, enlace));
                continue;
            }

            // Continuación de un elemento de lista (línea con sangría dentro de una lista)
            if (pilaListas.Count > 0 && l.StartsWith("  ")) { sb.Append(' ').Append(Linea(t, enlace)); continue; }

            CerrarListas();
            parrafo.Add(t);
        }
        CerrarParrafo(); CerrarListas();
        return sb.ToString();
    }

    // Enlace directo a una opción de la app. Href = ruta local (empieza con "/"); Resaltar = control que se ilumina al llegar.
    public sealed record Enlace(string Href, string? Resaltar, string Titulo);

    private static readonly Regex EnlaceMd = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex Negrita = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex Codigo = new(@"`([^`]+)`", RegexOptions.Compiled);

    // Formato en línea sobre texto YA codificado: los marcadores (** ` [ ]) no se alteran al codificar.
    // Con `enlace`, lo que nombra una opción conocida (negrita o [texto](capitulo.md)) se vuelve <a class="asis-op">.
    private static string Linea(string texto, Func<string, string?, Enlace?>? enlace = null)
    {
        var h = WebUtility.HtmlEncode(texto);
        h = Codigo.Replace(h, "<code>$1</code>");
        h = EnlaceMd.Replace(h, m => Fuerte(m.Groups[1].Value, WebUtility.HtmlDecode(m.Groups[2].Value), enlace));
        h = Negrita.Replace(h, m => m.Groups[1].Value.Contains("<a ") ? m.Groups[1].Value : Fuerte(m.Groups[1].Value, null, enlace));
        return h;
    }

    private static string Fuerte(string htmlCodificado, string? destino, Func<string, string?, Enlace?>? enlace)
    {
        var e = enlace?.Invoke(WebUtility.HtmlDecode(Regex.Replace(htmlCodificado, "<[^>]+>", "")), destino);
        if (e is null || !e.Href.StartsWith('/') || e.Href.StartsWith("//")) return "<strong>" + htmlCodificado + "</strong>";
        var sb = new StringBuilder("<a class=\"asis-op\" href=\"").Append(WebUtility.HtmlEncode(e.Href.TrimStart('/'))).Append('"');
        if (e.Resaltar is not null) sb.Append(" data-resaltar=\"").Append(WebUtility.HtmlEncode(e.Resaltar)).Append('"');
        sb.Append(" title=\"Ir a ").Append(WebUtility.HtmlEncode(e.Titulo)).Append("\"><strong>").Append(htmlCodificado).Append("</strong></a>");
        return sb.ToString();
    }

    // Texto corto del asistente (frases propias con **negrita**).
    public static string Frase(string texto, Func<string, string?, Enlace?>? enlace = null) => Linea(texto, enlace);
}
