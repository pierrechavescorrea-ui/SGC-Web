using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace SigWeb.Data.Direccionamiento;

// Lectura, división y unión de los PDF del Direccionamiento estratégico (PdfPig).
// Analizar: lee el texto de cada página y propone las partes (documentos) del PDF agrupando las páginas
// consecutivas con el mismo código de formato (ej. "F03-RESPON-01"); de cada parte sugiere tipo, título,
// versión, revisión y fecha a partir del rótulo. Todo es sugerencia: el usuario la corrige antes de guardar.
// Las páginas sin código (portada, índice) quedan como partes aparte y sin marcar.
public static class PdfDireccionamiento
{
    // Tipos conocidos, en el orden en que se muestran. El usuario puede escribir otros.
    public static readonly string[] Tipos =
    {
        "Visión", "Misión", "Política integrada", "Política", "Objetivos", "Valores",
        "Carta de responsabilidad social", "Código de ética", "Otro"
    };

    public static int OrdenTipo(string? tipo)
    {
        var i = Array.FindIndex(Tipos, t => string.Equals(t, tipo, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? Tipos.Length - 1 : i;
    }

    // Palabra clave (texto normalizado) → tipo. Gana la que aparece primero en la página; a igual
    // posición, la más larga ("POLITICA INTEGRADA" antes que "POLITICA").
    private static readonly (string Clave, string Tipo)[] Claves =
    {
        ("CODIGO DE ETICA", "Código de ética"),
        ("CARTA DE RESPONSABILIDAD SOCIAL", "Carta de responsabilidad social"),
        ("POLITICA INTEGRADA", "Política integrada"),
        ("POLITICA", "Política"),
        ("OBJETIVOS", "Objetivos"),
        ("VALORES", "Valores"),
        ("MISION", "Misión"),
        ("VISION", "Visión"),
        ("INDICE", "Índice"),
        ("DIRECCIONAMIENTO ESTRATEGICO", "Portada"),
    };

    private static readonly Regex ReCodigo = new(@"\b([A-Z]{1,4}\d{1,3}-[A-Z]{2,12}-\d{1,3})\b", RegexOptions.Compiled);
    // Los números del rótulo a veces salen con espacios entre dígitos ("0 1", "18-07-202 5"): se toleran y se quitan.
    private const string Num = @"\d(?:\s?\d){0,2}";
    private const string Fec = @"\d(?:\s?\d)?\s?[-/.]\s?\d(?:\s?\d)?\s?[-/.]\s?\d(?:\s?\d){1,3}";
    private static readonly Regex ReVersion = new($@"\bVERSION\s*(?:N[O°º.]*\s*)?:?\s*({Num})\b", RegexOptions.Compiled);
    private static readonly Regex ReRevision = new($@"\bREVISION\s*(?:N[O°º.]*\s*)?:?\s*({Num})\b", RegexOptions.Compiled);
    private static readonly Regex ReFechaRotulo = new($@"\bFECHA\s*:?\s*({Fec})\b", RegexOptions.Compiled);
    private static readonly Regex ReFecha = new($@"\b({Fec})\b", RegexOptions.Compiled);

    public sealed class Pagina
    {
        public int Numero { get; init; }
        public string? Codigo { get; init; }
        public string? Version { get; init; }
        public string? Revision { get; init; }
        public DateTime? Fecha { get; init; }
        public string? Tipo { get; init; }
        public string? Titulo { get; init; }
        public bool ConTexto { get; init; }
    }

    public sealed class Parte
    {
        public int Desde { get; set; }
        public int Hasta { get; set; }
        public string? Tipo { get; set; }
        public string? Titulo { get; set; }
        public string? Codigo { get; set; }
        public string? Version { get; set; }
        public string? Revision { get; set; }
        public DateTime? Fecha { get; set; }
        public bool Incluir { get; set; }
    }

    public sealed record Analisis(int Paginas, List<Parte> Partes, bool SinTexto);

    public static Analisis Analizar(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var paginas = new List<Pagina>();
        foreach (var p in doc.GetPages())
        {
            string texto;
            try { texto = ContentOrderTextExtractor.GetText(p, false); } catch { texto = p.Text ?? ""; }
            paginas.Add(LeerPagina(p.Number, texto));
        }
        var sinTexto = paginas.All(p => !p.ConTexto);
        return new Analisis(paginas.Count, Agrupar(paginas), sinTexto);
    }

    private static Pagina LeerPagina(int numero, string texto)
    {
        var lineas = texto.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var norm = Normalizar(texto);
        string? codigo = null;
        var iCod = norm.IndexOf("CODIGO", StringComparison.Ordinal);
        var mCod = iCod >= 0 ? ReCodigo.Match(norm, iCod) : Match.Empty;
        if (!mCod.Success) mCod = ReCodigo.Match(norm);
        if (mCod.Success) codigo = mCod.Groups[1].Value;

        var mVer = ReVersion.Match(norm);
        var mRev = ReRevision.Match(norm);
        DateTime? fecha = null;
        var mFec = ReFechaRotulo.Match(norm);
        if (!mFec.Success) { var todas = ReFecha.Matches(norm); if (todas.Count > 0) mFec = todas[^1]; }
        if (mFec.Success) fecha = LeerFecha(SinEspacios(mFec.Groups[1].Value));

        // Tipo: la palabra clave que aparece primero (descartando "CODIGO" del rótulo, que no es "CODIGO DE ETICA").
        string? tipo = null, clave = null; int pos = int.MaxValue;
        foreach (var (k, t) in Claves)
        {
            var m = Regex.Match(norm, $@"\b{Regex.Escape(k)}\b");
            if (!m.Success) continue;
            if (m.Index < pos || (m.Index == pos && clave is not null && k.Length > clave.Length)) { pos = m.Index; tipo = t; clave = k; }
        }
        // Portada e índice solo si la página no tiene rótulo de formato.
        if (codigo is not null && tipo is "Portada" or "Índice") tipo = null;

        return new Pagina
        {
            Numero = numero, Codigo = codigo, Tipo = tipo, Titulo = tipo is null ? null : TituloDe(lineas, clave!, tipo),
            Version = mVer.Success ? SinEspacios(mVer.Groups[1].Value) : null, Revision = mRev.Success ? SinEspacios(mRev.Groups[1].Value) : null,
            Fecha = fecha, ConTexto = norm.Length > 0
        };
    }

    // Título: para "Política" se toma el renglón con la palabra y los siguientes en mayúsculas
    // (los títulos largos vienen partidos en varias líneas); para los demás, el nombre del tipo.
    private static string TituloDe(List<string> lineas, string clave, string tipo)
    {
        if (tipo != "Política") return tipo;
        for (int i = 0; i < lineas.Count; i++)
        {
            var n = Normalizar(lineas[i]);
            var k = n.IndexOf(clave, StringComparison.Ordinal);
            if (k < 0) continue;
            var partes = new List<string> { lineas[i] };
            for (int j = i + 1; j < lineas.Count && j < i + 5; j++)
            {
                var l = lineas[j];
                if (l.Length > 60 || !EsMayusculas(l)) break;
                partes.Add(l);
            }
            var t = string.Join(" ", partes);
            // Recorta lo que esté antes de la palabra clave ("06. POLÍTICA DE…").
            var corte = t.IndexOf(t.Split(' ').FirstOrDefault(w => Normalizar(w).StartsWith("POLITICA")) ?? "", StringComparison.Ordinal);
            if (corte > 0) t = t[corte..];
            return Oracion(t);
        }
        return tipo;
    }

    private static string SinEspacios(string s) => Regex.Replace(s, @"\s", "");

    private static bool EsMayusculas(string s)
    {
        var letras = s.Where(char.IsLetter).ToList();
        return letras.Count >= 2 && letras.All(char.IsUpper);
    }

    private static string Oracion(string s)
    {
        s = Regex.Replace(s, @"\s+", " ").Trim().ToLower(new CultureInfo("es-CO"));
        return s.Length == 0 ? s : char.ToUpper(s[0], new CultureInfo("es-CO")) + s[1..];
    }

    private static List<Parte> Agrupar(List<Pagina> paginas)
    {
        var partes = new List<Parte>();
        Parte? actual = null;
        foreach (var p in paginas)
        {
            bool mismo = actual is not null && (
                (p.Codigo is not null && string.Equals(p.Codigo, actual.Codigo, StringComparison.OrdinalIgnoreCase)) ||
                // Páginas seguidas sin rótulo ni título reconocible (contraportada, anexos): una sola parte sin marcar.
                // Si en realidad son continuación del documento anterior, basta ampliar su rango "Hasta".
                (p.Codigo is null && p.Tipo is null && actual.Codigo is null && actual.Tipo is null));
            if (mismo)
            {
                actual!.Hasta = p.Numero;
                // Si las páginas traen rótulos distintos, vale el de fecha más reciente.
                if (p.Fecha is DateTime f && (actual.Fecha is null || f > actual.Fecha))
                { actual.Fecha = f; actual.Version = p.Version ?? actual.Version; actual.Revision = p.Revision ?? actual.Revision; }
                else { actual.Version ??= p.Version; actual.Revision ??= p.Revision; }
                continue;
            }
            actual = new Parte
            {
                Desde = p.Numero, Hasta = p.Numero, Codigo = p.Codigo, Tipo = p.Tipo, Titulo = p.Titulo,
                Version = p.Version, Revision = p.Revision, Fecha = p.Fecha,
                Incluir = p.Codigo is not null || (p.Tipo is not null && p.Tipo is not "Portada" and not "Índice")
            };
            partes.Add(actual);
        }
        // PDF sin rótulos reconocibles (escaneado o un solo documento suelto): una sola parte marcada.
        if (partes.Count > 0 && partes.All(x => !x.Incluir) && partes.All(x => x.Tipo is null or "Portada"))
            partes = new List<Parte> { new() { Desde = 1, Hasta = paginas.Count, Incluir = true, Tipo = partes[0].Tipo == "Portada" ? null : partes[0].Tipo } };
        if (partes.Count == 0 && paginas.Count > 0)
            partes.Add(new Parte { Desde = 1, Hasta = paginas.Count, Incluir = true });
        foreach (var x in partes.Where(x => x.Tipo is "Portada" or "Índice")) x.Titulo = x.Tipo;
        return partes;
    }

    private static DateTime? LeerFecha(string s)
    {
        var f = s.Replace('/', '-').Replace('.', '-');
        string[] formatos = { "d-M-yyyy", "dd-MM-yyyy", "d-M-yy", "dd-MM-yy" };
        return DateTime.TryParseExact(f, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    public static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant(), @"\s+", " ").Trim();
    }

    // ---------- División y unión ----------
    // Copia las páginas desde..hasta (1 = primera) en un PDF nuevo. Si es el PDF completo, devuelve el original.
    public static byte[] Extraer(byte[] pdf, int desde, int hasta)
    {
        using var origen = PdfDocument.Open(pdf);
        var total = origen.NumberOfPages;
        desde = Math.Clamp(desde, 1, total); hasta = Math.Clamp(hasta, desde, total);
        if (desde == 1 && hasta == total) return pdf;
        using var b = new PdfDocumentBuilder();
        for (int n = desde; n <= hasta; n++) b.AddPage(origen, n);
        return b.Build();
    }

    // Une varios PDF en uno, en el orden dado (compilado del Direccionamiento).
    public static byte[] Unir(IReadOnlyList<byte[]> pdfs)
    {
        var abiertos = pdfs.Select(p => PdfDocument.Open(p)).ToList();
        try
        {
            using var b = new PdfDocumentBuilder();
            foreach (var d in abiertos)
                for (int n = 1; n <= d.NumberOfPages; n++) b.AddPage(d, n);
            return b.Build();
        }
        finally { foreach (var d in abiertos) d.Dispose(); }
    }

    public static int ContarPaginas(byte[] pdf)
    {
        using var d = PdfDocument.Open(pdf);
        return d.NumberOfPages;
    }
}
