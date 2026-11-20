using System.Text;
using SigWeb.Models;

namespace SigWeb.Data.Planes;

// Lo que devuelve el editor de análisis de causa: estructura (JSON), imagen (SVG) y un resumen en texto
// (herramientas usadas, causas raíz marcadas y conclusión). Null en Json = sin diagrama.
public sealed record ResultadoAnalisisCausa(string? Json, string? Svg, string? Resumen);

public static class AnalisisCausa
{
    // Límite de lo que se acepta del navegador (un diagrama grande ronda 100–300 KB).
    public const int MaxBytes = 4 * 1024 * 1024;

    // La imagen se muestra solo como <img src="data:image/svg+xml;base64,…">: el navegador no ejecuta scripts ni
    // carga recursos externos de un SVG pintado como imagen, así el texto del usuario no se interpreta como HTML.
    public static string? DataUri(string? svg) =>
        string.IsNullOrWhiteSpace(svg) ? null : "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

    public static bool Valido(ResultadoAnalisisCausa r) =>
        (r.Json?.Length ?? 0) <= MaxBytes && (r.Svg?.Length ?? 0) <= MaxBytes
        && (r.Svg is null || r.Svg.TrimStart().StartsWith("<svg", StringComparison.Ordinal));

    // Aplica el resultado al plan (en memoria). El texto del campo solo se llena con el resumen si estaba vacío.
    public static string? Aplicar(PlanAccion p, ResultadoAnalisisCausa r)
    {
        if (!Valido(r)) return "El diagrama es demasiado grande o no es válido.";
        p.AnalisisCausaDiagrama = string.IsNullOrWhiteSpace(r.Json) ? null : r.Json;
        p.AnalisisCausaSvg = p.AnalisisCausaDiagrama is null ? null : r.Svg;
        if (string.IsNullOrWhiteSpace(p.AnalisisCausa) && !string.IsNullOrWhiteSpace(r.Resumen)) p.AnalisisCausa = r.Resumen.Trim();
        return null;
    }
}
