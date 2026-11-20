using System.Globalization;

namespace SigWeb.Data.Costos;

// Formatos de la pantalla de Costos y presupuestos (cultura de la app para separadores).
public static class FormatoCostos
{
    public static string Dinero(decimal? v) => v is decimal d ? "$ " + d.ToString("N0", CultureInfo.CurrentCulture) : "";
    public static string Millones(decimal? v) => v is decimal d ? "$ " + (d / 1_000_000m).ToString("N1", CultureInfo.CurrentCulture) + " M" : "";
    public static string Num(decimal? v, int dec = 2) => v is decimal d ? d.ToString("N" + dec, CultureInfo.CurrentCulture) : "";
    public static string Pct(decimal? v, int dec = 1) => v is decimal d ? (d * 100m).ToString("N" + dec, CultureInfo.CurrentCulture) + " %" : "";
    public static string Km(decimal? v) => v is decimal d ? d.ToString("0.##", CultureInfo.CurrentCulture) + " km" : "—";
    public static string Fecha(DateTime? f) => f is DateTime d ? d.ToString("dd/MM/yyyy") : "";
    public static string Iso(DateTime? f) => f is DateTime d ? d.ToString("yyyy-MM-dd") : "";

    // Color de una celda de avance 0–1 (rojo → verde, como el resto de la app).
    public static string ColorPct(decimal? p) => p is decimal d
        // Fondo rojo→verde con el texto que le corresponde; los temas oscuros cambian saturación, luz y texto (css/costos.css).
        ? $"hsl({(int)Math.Round(Math.Clamp(d, 0m, 1m) * 120m)} var(--cp-pct-s, 70%) var(--cp-pct-l, 88%));color:var(--cp-pct-txt, #1c2430)"
        : "transparent";
}
