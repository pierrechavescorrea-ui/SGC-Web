namespace SigWeb.Data.Estadistica;

// Estadística descriptiva e inferencial básica de una muestra (estilo MegaStat / Análisis de datos de Excel).
// Cada fórmula está documentada en docs/estadistica-ensayos.md con su referencia. Varianza y desviación son MUESTRALES (n − 1),
// igual que VAR.S / DESVEST.M de Excel; asimetría = COEFICIENTE.ASIMETRIA (SKEW), curtosis = CURTOSIS (KURT, exceso),
// cuartiles = CUARTIL.INC (QUARTILE.INC).
public enum ReglaClases { Sturges, Scott, FreedmanDiaconis, Raiz, Manual }

public sealed class Clase
{
    public double Desde { get; init; }
    public double Hasta { get; init; }
    public int Frecuencia { get; set; }
    public double Centro => (Desde + Hasta) / 2;
}

public sealed class ResultadoEstadistico
{
    public int N { get; init; }
    public double Suma { get; init; }
    public double Media { get; init; }
    public double Mediana { get; init; }
    public List<double> Modas { get; init; } = new();   // vacío = ningún valor se repite
    public int FrecuenciaModa { get; init; }
    public double? ModaAgrupada { get; set; }            // moda de la clase modal (fórmula de Czuber)
    public double Min { get; init; }
    public double Max { get; init; }
    public double Rango => Max - Min;
    public double Varianza { get; init; }
    public double Desviacion { get; init; }
    public double DesviacionPob { get; init; }
    public double ErrorEstandar { get; init; }
    public double? CoefVariacion { get; init; }          // %; null si la media es 0
    public double Q1 { get; init; }
    public double Q3 { get; init; }
    public double Iqr => Q3 - Q1;
    public double P5 { get; init; }
    public double P95 { get; init; }
    public double DesvMediaAbs { get; init; }            // DESVPROM
    public double? Asimetria { get; init; }              // G1 (n ≥ 3)
    public double? ErrorAsimetria { get; init; }         // SES
    public double? Curtosis { get; init; }               // G2, exceso (n ≥ 4)
    public double? ErrorCurtosis { get; init; }          // SEK
    public double Confianza { get; init; }
    public double? IcMediaInf { get; init; }
    public double? IcMediaSup { get; init; }
    public double? MargenError { get; init; }
    public double? TCritico { get; init; }
    public double? IcDesvInf { get; init; }
    public double? IcDesvSup { get; init; }
    public double LimiteInfTukey => Q1 - 1.5 * Iqr;
    public double LimiteSupTukey => Q3 + 1.5 * Iqr;
    public List<double> AtipicosLeves { get; init; } = new();
    public List<double> AtipicosExtremos { get; init; } = new();
    public double? AndersonDarling { get; init; }        // A² ajustado (A*)
    public double? PAndersonDarling { get; init; }
    public double? JarqueBera { get; init; }
    public double? PJarqueBera { get; init; }
    public double[] DentroSigma { get; init; } = new double[3]; // % real dentro de ±1σ, ±2σ, ±3σ
    public List<Clase> Clases { get; init; } = new();
    public double AnchoClase { get; init; }
    public double[] Ordenados { get; init; } = Array.Empty<double>();
    public double? Lie { get; init; }
    public double? Lse { get; init; }
    public double? Cp { get; init; }
    public double? Cpk { get; init; }
    public double? FueraEspecNormal { get; init; }       // % esperado fuera de especificación (normal)
    public double? FueraEspecReal { get; init; }          // % observado fuera de especificación
    public bool EsNormal => PAndersonDarling is double p ? p >= 0.05 : PJarqueBera is not double pj || pj >= 0.05;
}

public static class Descriptiva
{
    public static ResultadoEstadistico Analizar(IEnumerable<double> valores, double confianza = 0.95, ReglaClases regla = ReglaClases.Sturges,
        int? clasesManual = null, double? lie = null, double? lse = null)
    {
        var x = valores.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToArray();
        var n = x.Length;
        if (n == 0) return new ResultadoEstadistico { Confianza = confianza };

        var suma = x.Sum();
        var media = suma / n;
        double ss = 0, s3 = 0, s4 = 0, dma = 0;
        foreach (var v in x) { var d = v - media; ss += d * d; dma += Math.Abs(d); }
        var varianza = n > 1 ? ss / (n - 1) : 0;
        var desv = Math.Sqrt(varianza);
        var desvPob = Math.Sqrt(ss / n);
        if (desv > 0) foreach (var v in x) { var z = (v - media) / desv; s3 += z * z * z; s4 += z * z * z * z; }

        // Moda: valor(es) con mayor frecuencia; si todos aparecen una vez, no hay moda (igual que MODA de Excel → #N/A).
        var grupos = x.GroupBy(v => v).Select(g => (Valor: g.Key, F: g.Count())).ToList();
        var fMax = grupos.Max(g => g.F);
        var modas = fMax > 1 ? grupos.Where(g => g.F == fMax).Select(g => g.Valor).ToList() : new List<double>();

        double? g1 = null, ses = null, g2 = null, sek = null;
        if (n >= 3 && desv > 0)
        {
            g1 = (double)n / ((n - 1) * (n - 2)) * s3;
            ses = Math.Sqrt(6.0 * n * (n - 1) / ((n - 2.0) * (n + 1) * (n + 3)));
        }
        if (n >= 4 && desv > 0)
        {
            g2 = (double)n * (n + 1) / ((n - 1.0) * (n - 2) * (n - 3)) * s4 - 3.0 * (n - 1) * (n - 1) / ((n - 2.0) * (n - 3));
            sek = 2 * ses!.Value * Math.Sqrt((n * (double)n - 1) / ((n - 3.0) * (n + 5)));
        }

        double? icInf = null, icSup = null, margen = null, tc = null, sdInf = null, sdSup = null;
        var alfa = 1 - confianza;
        if (n >= 2)
        {
            tc = Distribuciones.TInv(1 - alfa / 2, n - 1);
            margen = tc * desv / Math.Sqrt(n);
            icInf = media - margen; icSup = media + margen;
            var chiSup = Distribuciones.ChiInv(1 - alfa / 2, n - 1);
            var chiInf = Distribuciones.ChiInv(alfa / 2, n - 1);
            sdInf = Math.Sqrt((n - 1) * varianza / chiSup);
            sdSup = Math.Sqrt((n - 1) * varianza / chiInf);
        }

        var q1 = Cuantil(x, 0.25); var q3 = Cuantil(x, 0.75); var iqr = q3 - q1;
        var leves = x.Where(v => (v < q1 - 1.5 * iqr && v >= q1 - 3 * iqr) || (v > q3 + 1.5 * iqr && v <= q3 + 3 * iqr)).ToList();
        var extremos = x.Where(v => v < q1 - 3 * iqr || v > q3 + 3 * iqr).ToList();

        // Normalidad: Anderson-Darling (con ajuste de D'Agostino y Stephens, 1986) y Jarque-Bera.
        double? ad = null, pAd = null, jb = null, pJb = null;
        if (n >= 8 && desv > 0)
        {
            double a = 0;
            for (var i = 0; i < n; i++)
            {
                var fi = Acotar(Distribuciones.NormalCdf(x[i], media, desv));
                var fn = Acotar(Distribuciones.NormalCdf(x[n - 1 - i], media, desv));
                a += (2.0 * (i + 1) - 1) * (Math.Log(fi) + Math.Log(1 - fn));
            }
            var a2 = -n - a / n;
            var aj = a2 * (1 + 0.75 / n + 2.25 / ((double)n * n));
            ad = aj;
            pAd = aj >= 0.6 ? Math.Exp(1.2937 - 5.709 * aj + 0.0186 * aj * aj)
                : aj >= 0.34 ? Math.Exp(0.9177 - 4.279 * aj - 1.38 * aj * aj)
                : aj >= 0.2 ? 1 - Math.Exp(-8.318 + 42.796 * aj - 59.938 * aj * aj)
                : 1 - Math.Exp(-13.436 + 101.14 * aj - 223.73 * aj * aj);
            pAd = Math.Clamp(pAd.Value, 0, 1);
        }
        if (n >= 4 && desvPob > 0)
        {
            double m3 = 0, m4 = 0;
            foreach (var v in x) { var d = v - media; m3 += d * d * d; m4 += d * d * d * d; }
            m3 /= n; m4 /= n;
            var b1 = m3 / Math.Pow(desvPob, 3);
            var b2 = m4 / Math.Pow(desvPob, 4) - 3;
            jb = n / 6.0 * (b1 * b1 + b2 * b2 / 4);
            pJb = 1 - Distribuciones.ChiCdf(jb.Value, 2);
        }

        var dentro = new double[3];
        if (desv > 0) for (var k = 1; k <= 3; k++) dentro[k - 1] = 100.0 * x.Count(v => Math.Abs(v - media) <= k * desv) / n;

        var (clases, ancho) = Histograma(x, regla, clasesManual, q1, q3, desv);
        double? modaAg = null;
        if (clases.Count > 1)
        {
            var im = clases.FindIndex(c => c.Frecuencia == clases.Max(k => k.Frecuencia));
            var d1 = clases[im].Frecuencia - (im > 0 ? clases[im - 1].Frecuencia : 0);
            var d2 = clases[im].Frecuencia - (im < clases.Count - 1 ? clases[im + 1].Frecuencia : 0);
            modaAg = d1 + d2 > 0 ? clases[im].Desde + (double)d1 / (d1 + d2) * ancho : clases[im].Centro;
        }

        double? cp = null, cpk = null, fueraN = null, fueraR = null;
        if (desv > 0 && (lie is not null || lse is not null))
        {
            if (lie is double li && lse is double ls && ls > li) cp = (ls - li) / (6 * desv);
            var cpu = lse is double s ? (s - media) / (3 * desv) : double.PositiveInfinity;
            var cpl = lie is double i ? (media - i) / (3 * desv) : double.PositiveInfinity;
            cpk = Math.Min(cpu, cpl);
            fueraN = 100 * ((lie is double i2 ? Distribuciones.NormalCdf(i2, media, desv) : 0) + (lse is double s2 ? 1 - Distribuciones.NormalCdf(s2, media, desv) : 0));
            fueraR = 100.0 * x.Count(v => (lie is double i3 && v < i3) || (lse is double s3b && v > s3b)) / n;
        }

        return new ResultadoEstadistico
        {
            N = n, Suma = suma, Media = media, Mediana = Cuantil(x, 0.5), Modas = modas, FrecuenciaModa = fMax, ModaAgrupada = modaAg,
            Min = x[0], Max = x[^1], Varianza = varianza, Desviacion = desv, DesviacionPob = desvPob,
            ErrorEstandar = n > 0 ? desv / Math.Sqrt(n) : 0, CoefVariacion = media != 0 ? 100 * desv / Math.Abs(media) : null,
            Q1 = q1, Q3 = q3, P5 = Cuantil(x, 0.05), P95 = Cuantil(x, 0.95), DesvMediaAbs = dma / n,
            Asimetria = g1, ErrorAsimetria = ses, Curtosis = g2, ErrorCurtosis = sek,
            Confianza = confianza, IcMediaInf = icInf, IcMediaSup = icSup, MargenError = margen, TCritico = tc, IcDesvInf = sdInf, IcDesvSup = sdSup,
            AtipicosLeves = leves, AtipicosExtremos = extremos, AndersonDarling = ad, PAndersonDarling = pAd, JarqueBera = jb, PJarqueBera = pJb,
            DentroSigma = dentro, Clases = clases, AnchoClase = ancho, Ordenados = x, Lie = lie, Lse = lse, Cp = cp, Cpk = cpk,
            FueraEspecNormal = fueraN, FueraEspecReal = fueraR,
        };
    }

    private static double Acotar(double p) => Math.Clamp(p, 1e-15, 1 - 1e-15);

    // CUARTIL.INC / PERCENTIL.INC: posición h = (n − 1)·p, interpolación lineal.
    public static double Cuantil(double[] ordenados, double p)
    {
        if (ordenados.Length == 0) return double.NaN;
        var h = (ordenados.Length - 1) * p;
        var lo = (int)Math.Floor(h);
        var hi = Math.Min(lo + 1, ordenados.Length - 1);
        return ordenados[lo] + (h - lo) * (ordenados[hi] - ordenados[lo]);
    }

    public static (List<Clase> Clases, double Ancho) Histograma(double[] x, ReglaClases regla, int? manual, double q1, double q3, double desv)
    {
        var n = x.Length;
        if (n == 0) return (new(), 0);
        double min = x[0], max = x[^1];
        if (max - min < 1e-12) return (new() { new Clase { Desde = min - 0.5, Hasta = max + 0.5, Frecuencia = n } }, 1);
        int k = regla switch
        {
            ReglaClases.Manual => Math.Clamp(manual ?? 10, 1, 200),
            ReglaClases.Raiz => (int)Math.Ceiling(Math.Sqrt(n)),
            ReglaClases.Scott => desv > 0 ? (int)Math.Ceiling((max - min) / (3.49 * desv * Math.Pow(n, -1.0 / 3))) : 1,
            ReglaClases.FreedmanDiaconis => q3 - q1 > 0 ? (int)Math.Ceiling((max - min) / (2 * (q3 - q1) * Math.Pow(n, -1.0 / 3))) : (int)Math.Ceiling(Math.Log2(n)) + 1,
            _ => (int)Math.Ceiling(Math.Log2(n)) + 1,
        };
        k = Math.Clamp(k, 1, 200);
        var ancho = (max - min) / k;
        var clases = Enumerable.Range(0, k).Select(i => new Clase { Desde = min + i * ancho, Hasta = i == k - 1 ? max : min + (i + 1) * ancho }).ToList();
        foreach (var v in x) clases[Math.Min((int)((v - min) / ancho), k - 1)].Frecuencia++;
        return (clases, ancho);
    }

    // Probabilidad de ocurrencia: según la normal ajustada (μ = media, σ = s) y según la frecuencia observada.
    public static (double Normal, double Empirica) Probabilidad(ResultadoEstadistico r, double? desde, double? hasta)
    {
        if (r.N == 0) return (double.NaN, double.NaN);
        var a = desde ?? double.NegativeInfinity; var b = hasta ?? double.PositiveInfinity;
        var pn = r.Desviacion > 0 ? Distribuciones.NormalCdf(b, r.Media, r.Desviacion) - Distribuciones.NormalCdf(a, r.Media, r.Desviacion)
            : (r.Media >= a && r.Media <= b ? 1 : 0);
        var pe = (double)r.Ordenados.Count(v => v >= a && v <= b) / r.N;
        return (pn, pe);
    }

    // Puntos del gráfico Q-Q normal: cuantil teórico con la posición de Blom (i − 3/8)/(n + 1/4).
    public static List<(double Teorico, double Observado)> PuntosQQ(ResultadoEstadistico r)
    {
        var n = r.N;
        return r.Ordenados.Select((v, i) => (r.Media + r.Desviacion * Distribuciones.NormalInv((i + 1 - 0.375) / (n + 0.25)), v)).ToList();
    }
}
