using System.Net;
using System.Text.RegularExpressions;

namespace SigWeb.Data.Asistente;

public static class LectorInterfaz
{
    public sealed record Control(string Ruta, string Nombre);
    private static readonly Regex Pagina = new(@"^\s*@page\s+""(/[^""]*)""", RegexOptions.Multiline);
    private static readonly Regex Componente = new(@"<([A-Z][\w]+)\b");
    private static readonly Regex Etiqueta = new(
        """<(?:button|a|input|select|summary)\b[^<>]*?\b(?:title|aria-label)\s*=\s*["']([^"'@\r\n<>]+)["']""",
        RegexOptions.IgnoreCase);
    private static readonly Regex TextoBoton = new(@"<button\b[^<>]*>\s*([^@<>{}]+?)\s*</button>", RegexOptions.IgnoreCase);
    private static readonly Regex Comentarios = new(@"@\*[\s\S]*?\*@|<!--[\s\S]*?-->");

    public static List<Control> Leer(string carpeta)
    {
        var archivos = Directory.GetFiles(carpeta,"*.razor",SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToDictionary(p => p,p => Comentarios.Replace(File.ReadAllText(p),"").Split("@code",2)[0]);
        var hijos = archivos.Keys.GroupBy(Path.GetFileNameWithoutExtension)
            .Where(g => g.Count() == 1).ToDictionary(g => g.Key!,g => g.First());
        var indice = new HashSet<Control>();
        foreach (var (pagina,contenido) in archivos)
        foreach (Match ruta in Pagina.Matches(contenido))
        {
            var visitados = new HashSet<string>();
            void Recorrer(string archivo)
            {
                if (!visitados.Add(archivo)) return;
                var markup = archivos[archivo];
                foreach (var regex in new[] { Etiqueta, TextoBoton })
                foreach (Match m in regex.Matches(markup))
                {
                    var nombre = WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
                    if (nombre.Length is > 1 and <= 120) indice.Add(new(ruta.Groups[1].Value,nombre));
                }
                foreach (Match m in Componente.Matches(markup))
                    if (hijos.TryGetValue(m.Groups[1].Value,out var hijo) && !Pagina.IsMatch(archivos[hijo]))
                        Recorrer(hijo);
            }
            Recorrer(pagina);
        }
        return indice.OrderBy(x => x.Ruta,StringComparer.Ordinal).ThenBy(x => x.Nombre,StringComparer.Ordinal).ToList();
    }
}
