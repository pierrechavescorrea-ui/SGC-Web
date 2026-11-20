using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SigWeb.Data.AvanceObra;

// Datos de perfil de una torre leídos de la tabla de torres (vista Perfil de Avance de obra).
// Todo sale de columnas de la tabla; lo que no está queda null (no se inventa).
public class PerfilTorre
{
    public double? Abscisa { get; set; }          // abscisa progresiva (m)
    public double? Cota { get; set; }             // cota de diseño (LiDAR) de la torre (m.s.n.m)
    public double? CotaConstruccion { get; set; } // cota de construcción (m.s.n.m)
    public double? AlturaTotal { get; set; }      // altura total de la torre (m)
    public double? Amarre { get; set; }           // altura de fijación del conductor inferior (m)
    public double? Deflexion { get; set; }        // ángulo de deflexión en grados decimales, con el signo de la tabla
}

public static class PerfilLinea
{
    // Campo de ConfigCotasAvance, nombre visible, palabras para encontrar la columna sola.
    public static readonly (string Campo, string Nombre)[] Campos =
    {
        (nameof(ConfigCotasAvance.PerfilAbscisa), "Abscisa"),
        (nameof(ConfigCotasAvance.PerfilCota), "Cota de diseño (LiDAR)"),
        (nameof(ConfigCotasAvance.PerfilCotaConstruccion), "Cota de construcción"),
        (nameof(ConfigCotasAvance.PerfilAlturaTotal), "Altura total de torre"),
        (nameof(ConfigCotasAvance.PerfilAmarre), "Altura de fijación del conductor inferior"),
        (nameof(ConfigCotasAvance.PerfilDeflexion), "Ángulo de deflexión (grados decimales)"),
    };

    // SUPUESTO (por confirmar con diseño): estas familias son de suspensión; las demás (B, BP, BE, C, D, T, T3C…) de
    // retención. Se dedujo de la hoja ALTURAS TORRES de la tabla A29-10 (ángulos de balanceo 61/34 a 67/48 frente a 27/10)
    // y de las deflexiones (AAE 1,3°, D 52,3°).
    public static readonly HashSet<string> FamiliasSuspension = new(StringComparer.OrdinalIgnoreCase) { "A", "AA", "AP", "AL", "AAE", "ADC" };

    public static string Funcion(string familia, string? tipoTexto)
    {
        if (string.IsNullOrWhiteSpace(familia)) return "";
        if (familia.Equals("ML", StringComparison.OrdinalIgnoreCase) || familia.Equals("SN", StringComparison.OrdinalIgnoreCase)
            || (tipoTexto ?? "").Contains("pórtico", StringComparison.OrdinalIgnoreCase)) return "Pórtico";
        return FamiliasSuspension.Contains(familia) ? "Suspensión" : "Retención";
    }

    private static string Norm(string s)
    {
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    // Columna de cada campo: la elegida en Ajustes; si no, la que se reconoce por su nombre (y, si hay varias, la que
    // tiene más valores numéricos y con decimales — evita tomar la columna de grados enteros del ángulo).
    public static Dictionary<string, string?> Claves(ConfigCotasAvance cfg, IReadOnlyCollection<string> claves,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> porTorre)
    {
        double Puntaje(string clave)
        {
            int n = 0, dec = 0;
            foreach (var d in porTorre.Values)
                if (d.TryGetValue(clave, out var v) && Numero(v) is double) { n++; if (v.Contains('.') || v.Contains(',')) dec++; }
            return n + dec * 0.5;
        }
        string? Mejor(Func<string, bool> sirve)
        {
            var l = claves.Where(c => sirve(Norm(c))).ToList();
            return l.Count == 0 ? null : l.OrderByDescending(Puntaje).First();
        }
        bool Excluye(string n) => n.Contains("pata") || n.Contains("cimenta") || n.Contains("fundaci") || n.Contains("plano") || n.Contains("area")
                                  || n.Contains("delta") || n.Contains(" cg") || n.EndsWith("cg") || n.Contains("tipo");
        var auto = new Dictionary<string, string?>
        {
            [nameof(ConfigCotasAvance.PerfilAbscisa)] = Mejor(n => n.Contains("abscisa") || n.Contains("progresiva")),
            [nameof(ConfigCotasAvance.PerfilCotaConstruccion)] = Mejor(n => n.Contains("constru") && !Excluye(n) && !n.Contains("altura")),
            [nameof(ConfigCotasAvance.PerfilCota)] = Mejor(n => n.Contains("lidar") && !Excluye(n))
                ?? Mejor(n => (n.Contains("cota") || n.Contains("msnm") || n.Contains("m.s.n.m")) && !n.Contains("constru") && !Excluye(n)),
            [nameof(ConfigCotasAvance.PerfilAlturaTotal)] = Mejor(n => n.Contains("altura") && n.Contains("total")),
            [nameof(ConfigCotasAvance.PerfilAmarre)] = Mejor(n => n.Contains("fijacion") || (n.Contains("altura") && n.Contains("conductor"))),
            [nameof(ConfigCotasAvance.PerfilDeflexion)] = Mejor(n => n.Contains("deflexi") && (n.Contains("deg") || n.Contains("decimal")))
                ?? Mejor(n => n.Contains("deflexi") && !n.Contains("direcc")),
        };
        var r = new Dictionary<string, string?>();
        foreach (var (campo, _) in Campos)
        {
            var elegida = cfg.Clave(campo);
            r[campo] = !string.IsNullOrWhiteSpace(elegida) ? elegida : auto[campo];
        }
        return r;
    }

    public static PerfilTorre? De(IReadOnlyDictionary<string, string> campos, IReadOnlyDictionary<string, string?> claves)
    {
        double? V(string campo) => claves.TryGetValue(campo, out var k) && k is not null && campos.TryGetValue(k, out var v) ? Numero(v) : null;
        var p = new PerfilTorre
        {
            Abscisa = V(nameof(ConfigCotasAvance.PerfilAbscisa)),
            Cota = V(nameof(ConfigCotasAvance.PerfilCota)),
            CotaConstruccion = V(nameof(ConfigCotasAvance.PerfilCotaConstruccion)),
            AlturaTotal = V(nameof(ConfigCotasAvance.PerfilAlturaTotal)),
            Amarre = V(nameof(ConfigCotasAvance.PerfilAmarre)),
            Deflexion = V(nameof(ConfigCotasAvance.PerfilDeflexion)),
        };
        return p.Abscisa is null || p.Cota is null ? null : p;
    }

    private static readonly Regex RxNum = new(@"^[+-]?\d{1,3}(?:[.,]\d{3})*(?:[.,]\d+)?$|^[+-]?\d+(?:[.,]\d+)?$", RegexOptions.Compiled);

    // Número con signo tal como viene de la tabla ("175.3", "70.646,42", "-38,877"); texto, "-" o vacío = null.
    public static double? Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().Replace(" ", "");
        if (!RxNum.IsMatch(t)) return null;
        int p = t.LastIndexOf('.'), c = t.LastIndexOf(',');
        if (p >= 0 && c >= 0) t = p > c ? t.Replace(",", "") : t.Replace(".", "").Replace(',', '.');
        else if (c >= 0) t = t.Count(ch => ch == ',') > 1 ? t.Replace(",", "") : t.Replace(',', '.');
        else if (t.Count(ch => ch == '.') > 1) t = t.Replace(".", "");
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
