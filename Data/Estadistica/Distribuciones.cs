namespace SigWeb.Data.Estadistica;

// Funciones de distribución estándar (sin librerías externas). Referencias en docs/estadistica-ensayos.md:
//  - erf/erfc: aproximación de Chebyshev de Numerical Recipes (3.ª ed., §6.2.2), error relativo < 1,2·10⁻⁷.
//  - Normal inversa: algoritmo de P. J. Acklam con un paso de refinamiento de Halley (error < 1·10⁻⁹).
//  - Beta incompleta regularizada (t de Student) y gamma incompleta regularizada (chi-cuadrado): Numerical Recipes §6.1–6.4
//    (fracción continua de Lentz y serie). Las inversas se hallan por bisección sobre la función de distribución.
public static class Distribuciones
{
    // ---------- Normal ----------

    public static double Erfc(double x)
    {
        var z = Math.Abs(x);
        var t = 2.0 / (2.0 + z);
        var ty = 4.0 * t - 2.0;
        double[] c =
        {
            -1.3026537197817094, 6.4196979235649026e-1, 1.9476473204185836e-2, -9.561514786808631e-3, -9.46595344482036e-4,
            3.66839497852761e-4, 4.2523324806907e-5, -2.0278578112534e-5, -1.624290004647e-6, 1.303655835580e-6, 1.5626441722e-8,
            -8.5238095915e-8, 6.529054439e-9, 5.059343495e-9, -9.91364156e-10, -2.27365122e-10, 9.6467911e-11, 2.394038e-12,
            -6.886027e-12, 8.94487e-13, 3.13092e-13, -1.12708e-13, 3.81e-16, 7.106e-15, -1.523e-15, -9.4e-17, 1.21e-16, -2.8e-17,
        };
        double d = 0, dd = 0;
        for (var j = c.Length - 1; j > 0; j--) { var tmp = d; d = ty * d - dd + c[j]; dd = tmp; }
        var r = t * Math.Exp(-z * z + 0.5 * (c[0] + ty * d) - dd);
        return x >= 0 ? r : 2.0 - r;
    }

    public static double NormalCdf(double x, double media = 0, double desv = 1) => 0.5 * Erfc(-(x - media) / (desv * Math.Sqrt(2)));

    public static double NormalPdf(double x, double media = 0, double desv = 1)
    {
        var z = (x - media) / desv;
        return Math.Exp(-0.5 * z * z) / (desv * Math.Sqrt(2 * Math.PI));
    }

    public static double NormalInv(double p, double media = 0, double desv = 1)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        double[] a = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
        double[] b = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
        double[] c = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
        double[] d = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };
        const double bajo = 0.02425, alto = 1 - bajo;
        double x;
        if (p < bajo)
        {
            var q = Math.Sqrt(-2 * Math.Log(p));
            x = (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        else if (p <= alto)
        {
            var q = p - 0.5; var r = q * q;
            x = (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }
        else
        {
            var q = Math.Sqrt(-2 * Math.Log(1 - p));
            x = -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        // Refinamiento de Halley.
        var e = NormalCdf(x) - p;
        var u = e * Math.Sqrt(2 * Math.PI) * Math.Exp(x * x / 2);
        x -= u / (1 + x * u / 2);
        return media + desv * x;
    }

    // ---------- Gamma y beta ----------

    public static double LnGamma(double x)
    {
        double[] cof = { 57.1562356658629235, -59.5979603554754912, 14.1360979747417471, -0.491913816097620199, .339946499848118887e-4,
            .465236289270485756e-4, -.983744753048795646e-4, .158088703224912494e-3, -.210264441724104883e-3, .217439618115212643e-3,
            -.164318106536763890e-3, .844182239838527433e-4, -.261908384015814087e-4, .368991826595316234e-5 };
        double y = x, tmp = x + 5.24218750000000000;
        tmp = (x + 0.5) * Math.Log(tmp) - tmp;
        var ser = 0.999999999999997092;
        for (var j = 0; j < 14; j++) ser += cof[j] / ++y;
        return tmp + Math.Log(2.5066282746310005 * ser / x);
    }

    // Beta incompleta regularizada I_x(a, b).
    public static double BetaInc(double a, double b, double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        var bt = Math.Exp(LnGamma(a + b) - LnGamma(a) - LnGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x));
        return x < (a + 1) / (a + b + 2) ? bt * BetaCf(a, b, x) / a : 1 - bt * BetaCf(b, a, 1 - x) / b;
    }

    private static double BetaCf(double a, double b, double x)
    {
        const double fpmin = 1e-300, eps = 1e-15;
        double qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap;
        if (Math.Abs(d) < fpmin) d = fpmin;
        d = 1 / d; var h = d;
        for (var m = 1; m < 10000; m++)
        {
            var m2 = 2 * m;
            var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + aa * d; if (Math.Abs(d) < fpmin) d = fpmin;
            c = 1 + aa / c; if (Math.Abs(c) < fpmin) c = fpmin;
            d = 1 / d; h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + aa * d; if (Math.Abs(d) < fpmin) d = fpmin;
            c = 1 + aa / c; if (Math.Abs(c) < fpmin) c = fpmin;
            d = 1 / d; var del = d * c; h *= del;
            if (Math.Abs(del - 1) <= eps) break;
        }
        return h;
    }

    // Gamma incompleta regularizada inferior P(a, x).
    public static double GammaP(double a, double x)
    {
        if (x <= 0) return 0;
        if (x < a + 1)
        {
            double ap = a, sum = 1 / a, del = sum;
            for (var n = 0; n < 10000; n++) { ap++; del *= x / ap; sum += del; if (Math.Abs(del) < Math.Abs(sum) * 1e-15) break; }
            return sum * Math.Exp(-x + a * Math.Log(x) - LnGamma(a));
        }
        const double fpmin = 1e-300;
        double b = x + 1 - a, c = 1 / fpmin, d = 1 / b, h = d;
        for (var i = 1; i < 10000; i++)
        {
            var an = -i * (i - a); b += 2;
            d = an * d + b; if (Math.Abs(d) < fpmin) d = fpmin;
            c = b + an / c; if (Math.Abs(c) < fpmin) c = fpmin;
            d = 1 / d; var del = d * c; h *= del;
            if (Math.Abs(del - 1) < 1e-15) break;
        }
        return 1 - Math.Exp(-x + a * Math.Log(x) - LnGamma(a)) * h;
    }

    // ---------- t de Student y chi-cuadrado ----------

    public static double TCdf(double t, double gl)
    {
        var x = gl / (gl + t * t);
        var cola = 0.5 * BetaInc(gl / 2, 0.5, x);
        return t >= 0 ? 1 - cola : cola;
    }

    public static double TInv(double p, double gl) => Invertir(x => TCdf(x, gl), p, -1e4, 1e4);

    public static double ChiCdf(double x, double gl) => x <= 0 ? 0 : GammaP(gl / 2, x / 2);

    public static double ChiInv(double p, double gl) => Invertir(x => ChiCdf(x, gl), p, 0, 1e5);

    private static double Invertir(Func<double, double> cdf, double p, double lo, double hi)
    {
        for (var i = 0; i < 300; i++)
        {
            var m = (lo + hi) / 2;
            if (cdf(m) < p) lo = m; else hi = m;
            if (hi - lo < 1e-12 * Math.Max(1, Math.Abs(m))) break;
        }
        return (lo + hi) / 2;
    }
}
