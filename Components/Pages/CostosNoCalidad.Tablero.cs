using System.Globalization;
using System.Text;
using SigWeb.Data.CostosNoCalidad;

namespace SigWeb.Components.Pages;

// Tablero de Costos de no calidad: cifra principal + tendencia, indicadores alineados y composición por rubro.
// Colores por RUBRO, fijos (el color sigue al rubro, nunca a su puesto): los 7 de mayor costo histórico toman los
// tonos 1–7 de la paleta categórica validada; el resto va a "Otros" (gris). Un filtro no los repinta.
public partial class CostosNoCalidad
{
    // Paleta categórica (orden fijo, validada para daltonismo en claro): azul, naranja, aguamarina, amarillo, magenta, verde, violeta.
    private static readonly string[] Categorica = { "#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6" };
    private const string ColorOtros = "#9aa0a8";
    private const string Otros = "Otros";
    private Dictionary<string, string> colorRubro = new();

    // Al cargar: puesto de cada rubro por su costo total (todos los planes, sin filtros).
    private void AsignarColores()
    {
        colorRubro = new();
        if (datos is null) return;
        var orden = datos.Rubros.Select(r => (R: r, V: datos.TotalRubro(datos.Planes, r))).Where(x => x.V != 0)
            .OrderByDescending(x => x.V).ThenBy(x => x.R).Select(x => x.R).ToList();
        for (int i = 0; i < orden.Count && i < Categorica.Length; i++) colorRubro[orden[i]] = Categorica[i];
    }
    private string ColorDe(string rubro) => colorRubro.TryGetValue(rubro, out var c) ? c : ColorOtros;
    private string Agrupado(string rubro) => colorRubro.ContainsKey(rubro) ? rubro : Otros;

    public sealed record ParteRubro(string Rubro, double Valor, double Pct, string Color);

    // Partes de la barra de composición: rubros con color propio en orden de mayor a menor, y "Otros" al final.
    private ParteRubro[] Composicion(IEnumerable<PlanCosto> planes)
    {
        if (datos is null) return Array.Empty<ParteRubro>();
        var l = planes.ToList();
        var total = l.Sum(p => p.Total);
        if (total == 0) return Array.Empty<ParteRubro>();
        var porGrupo = datos.Rubros.Select(r => (G: Agrupado(r), V: datos.TotalRubro(l, r))).Where(x => x.V != 0)
            .GroupBy(x => x.G).Select(g => (G: g.Key, V: g.Sum(x => x.V))).ToList();
        return porGrupo.Where(x => x.G != Otros).OrderByDescending(x => x.V)
            .Concat(porGrupo.Where(x => x.G == Otros))
            .Select(x => new ParteRubro(x.G, x.V, x.V / total, x.G == Otros ? ColorOtros : ColorDe(x.G))).ToArray();
    }

    private ParteRubro[] ComposicionPlan(PlanCosto p) =>
        datos is null || p.Total == 0 ? Array.Empty<ParteRubro>()
        : datos.Rubros.Select(r => (R: r, V: datos.ValorRubro(p, r))).Where(x => x.V != 0).OrderByDescending(x => x.V)
            .Select(x => new ParteRubro(x.R, x.V, x.V / p.Total, ColorDe(x.R))).ToArray();

    // ---------- Periodo, tendencia y comparación ----------
    // Con año elegido: ene–dic de ese año contra el año anterior. Sin año: los 12 meses hasta el mes actual contra los 12 previos.
    private (DateTime Desde, DateTime Hasta) Periodo() =>
        fAnio != 0 ? (new DateTime(fAnio, 1, 1), new DateTime(fAnio, 12, 1))
        : (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11), new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

    private double[] Tendencia()
    {
        var (desde, _) = Periodo();
        var l = FiltradosSinAnio().ToList();
        return Enumerable.Range(0, 12).Select(i =>
        {
            var m = desde.AddMonths(i);
            return l.Where(p => p.Fecha.Year == m.Year && p.Fecha.Month == m.Month).Sum(p => p.Total);
        }).ToArray();
    }

    // Variación contra el periodo anterior (mismos filtros salvo el año). Null = sin base para comparar.
    private (double Pct, string Contra)? Variacion()
    {
        var (desde, hasta) = Periodo();
        var l = FiltradosSinAnio().ToList();
        double Suma(DateTime d, DateTime h) => l.Where(p => new DateTime(p.Fecha.Year, p.Fecha.Month, 1) >= d && new DateTime(p.Fecha.Year, p.Fecha.Month, 1) <= h).Sum(p => p.Total);
        var actual = Suma(desde, hasta);
        var antes = Suma(desde.AddMonths(-12), hasta.AddMonths(-12));
        if (antes == 0) return null;
        return ((actual - antes) / antes, fAnio != 0 ? $"vs {fAnio - 1}" : "vs 12 meses previos");
    }

    // Línea de tendencia (SVG 120×36): trazo y área; el último mes con punto de acento.
    private (string Linea, string Area, double Ux, double Uy) Sparkline(double[] v)
    {
        const double W = 120, H = 36, P = 3;
        var max = v.Length == 0 ? 0 : v.Max();
        var ci = CultureInfo.InvariantCulture;
        (double X, double Y) Pt(int i) => (P + i * (W - 2 * P) / Math.Max(1, v.Length - 1), max == 0 ? H - P : H - P - v[i] / max * (H - 2 * P));
        var sb = new StringBuilder();
        for (int i = 0; i < v.Length; i++) { var (x, y) = Pt(i); sb.Append(i == 0 ? "M" : " L").Append(x.ToString("0.#", ci)).Append(',').Append(y.ToString("0.#", ci)); }
        var linea = sb.ToString();
        var (ux, uy) = v.Length == 0 ? (0d, 0d) : Pt(v.Length - 1);
        var area = v.Length == 0 ? "" : $"{linea} L{ux.ToString("0.#", ci)},{H} L{P.ToString(ci)},{H} Z";
        return (linea, area, ux, uy);
    }

    private string EtiquetaPeriodo => fAnio != 0 ? $"Año {fAnio}" : "Últimos 12 meses";

    // Valor compacto para cifras grandes: $ 16,6 M · $ 840 mil.
    private static string Compacto(double v) => "$ " + ServicioCostosNoCalidad.Corto(v);
}
