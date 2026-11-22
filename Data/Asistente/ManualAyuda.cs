using System.Text.RegularExpressions;

namespace SigWeb.Data.Asistente;

// Asistente de ayuda INTERNO (sin API ni servicios externos): lee el manual de usuario (docs/manual/*.md), lo parte en
// secciones y responde buscando la sección más pertinente con BM25 (relevancia por términos), dando prioridad al capítulo
// de la pantalla donde está el usuario. Las opciones se completan con los controles de la interfaz.
// Singleton: el índice se arma una vez y se rehace solo si cambia algún archivo del manual.
public sealed class ManualAyuda
{
    // Opción con enlace directo (encabezado `opciones:` del capítulo): vista, pestaña o botón de una pantalla.
    // Ruta = URL local exacta (empieza con "/"); Resaltar = title/aria-label/texto del control que se ilumina al llegar.
    public sealed record Opcion(string Nombre, string Ruta, string? Resaltar, Capitulo Capitulo)
    {
        public IReadOnlyList<string> Terminos { get; } = TextoAyuda.Terminos(Regex.Replace(Nombre, @"\([^)]*\)", " ")).Distinct().ToList();
        public string Modulo => AccesoAyuda.Modulo(Ruta);
        public Seguridad.Accion Requiere => AccesoAyuda.RequisitoControl(Nombre,Resaltar,Ruta);
        public string Etiqueta => Capitulo.Titulo + " › " + Nombre;
    }

    public sealed record Capitulo(string Archivo, string Modulo, string Titulo, string Menu, IReadOnlyList<string> Rutas, IReadOnlyList<string> Palabras)
    {
        public List<Opcion> Opciones { get; } = new();
        public List<LectorInterfaz.Control> Campos { get; } = new();

        // Primera ruta sin parámetros = a dónde lleva el botón «Ir a…».
        public string? RutaIr => Rutas.FirstOrDefault(r => !r.Contains('{'));
    }

    public sealed class Seccion
    {
        public required string Id { get; init; }              // "documentos#3"
        public required Capitulo Capitulo { get; init; }
        public required string Titulo { get; init; }          // título de la sección (### o ##)
        public string? Padre { get; init; }                   // ## al que pertenece un ###
        public required string Markdown { get; init; }        // cuerpo, sin «Por confirmar» ni comentarios
        public Dictionary<string, double> Tf { get; } = new(StringComparer.Ordinal);
        public HashSet<string> TerminosTitulo { get; } = new();
        public double Largo { get; set; }
    }

    private readonly string _carpeta;
    private readonly ILogger<ManualAyuda> _log;
    private readonly object _candado = new();
    private List<Seccion> _secciones = new();
    private List<Capitulo> _capitulos = new();
    private Dictionary<string, int> _df = new(StringComparer.Ordinal);
    private double _largoMedio = 1;
    private readonly IWebHostEnvironment _entorno;
    private string _firma = "";
    private DateTime _revisado = DateTime.MinValue;

    public ManualAyuda(IWebHostEnvironment env, ILogger<ManualAyuda> log)
    {
        _entorno = env;
        _log = log;
        // En desarrollo está en la raíz del proyecto; al publicar se copia junto a la app (ver SigWeb.csproj).
        var candidatas = new[] { Path.Combine(env.ContentRootPath, "docs", "manual"), Path.Combine(AppContext.BaseDirectory, "docs", "manual") };
        _carpeta = candidatas.FirstOrDefault(Directory.Exists) ?? candidatas[0];
    }

    public IReadOnlyList<Capitulo> Capitulos { get { Asegurar(); return _capitulos; } }
    public IReadOnlyList<Seccion> Secciones { get { Asegurar(); return _secciones; } }
    public Seccion? PorId(string id) { Asegurar(); return _secciones.FirstOrDefault(s => s.Id == id); }

    // Capítulo de una ruta de la app ("/documentos", "/planes-accion/12" → planes-accion). null = sin capítulo propio.
    public Capitulo? CapituloDeRuta(string? ruta)
    {
        Asegurar();
        var r = "/" + (ruta ?? "").Split('?', '#')[0].Trim('/').ToLowerInvariant();
        Capitulo? mejor = null; int largo = -1;
        foreach (var c in _capitulos)
            foreach (var patron in c.Rutas)
            {
                var p = patron.Trim().ToLowerInvariant();
                var fijo = p.Contains('{') ? p[..p.IndexOf('{')].TrimEnd('/') : p.TrimEnd('/');
                if (fijo.Length == 0) fijo = "/";
                bool coincide = r == fijo || (fijo != "/" && r.StartsWith(fijo + "/", StringComparison.Ordinal));
                if (coincide && fijo.Length > largo) { mejor = c; largo = fijo.Length; }
            }
        return mejor;
    }

    public sealed record Resultado(Seccion Seccion, double Puntaje);

    // Las secciones más pertinentes para la pregunta. rutaActual da prioridad al capítulo de esa pantalla.
    // contexto = capítulo del que se venía hablando (pregunta de seguimiento: «¿y cómo lo elimino?»); pesa un poco más.
    public List<Resultado> Buscar(string pregunta, string? rutaActual, int cuantos = 4, Capitulo? contexto = null, IEnumerable<string>? extra = null)
    {
        Asegurar();
        var terminos = Corregir(TextoAyuda.Terminos(pregunta).Concat(extra ?? Enumerable.Empty<string>())).Distinct().ToList();
        if (terminos.Count == 0 || _secciones.Count == 0) return new();
        var actual = CapituloDeRuta(rutaActual);
        const double k1 = 2.5, b = 0.20;
        int n = _secciones.Count;
        var res = new List<Resultado>();
        foreach (var s in _secciones)
        {
            double puntaje = 0; int presentes = 0;
            foreach (var t in terminos)
            {
                if (!s.Tf.TryGetValue(t, out var tf)) continue;
                presentes++;
                var df = _df.TryGetValue(t, out var d) ? d : 0;
                var idf = Math.Log(1 + (n - df + 0.5) / (df + 0.5));
                puntaje += idf * (tf * (k1 + 1)) / (tf + k1 * (1 - b + b * s.Largo / _largoMedio));
            }
            if (presentes == 0) continue;
            // Cubrir más palabras de la pregunta pesa más que repetir una sola.
            puntaje *= 0.12 + 0.88 * presentes / terminos.Count;
            puntaje *= 1 + 1.8 * terminos.Count(s.TerminosTitulo.Contains) / Math.Max(terminos.Count, s.TerminosTitulo.Count);
            if (actual is not null && s.Capitulo == actual) puntaje *= 1.35;
            if (contexto is not null && s.Capitulo == contexto) puntaje *= 1.25;
            // Preguntas frecuentes («¿…?») que contienen casi todas las palabras de la pregunta: suelen ser la respuesta exacta.
            if (s.Titulo.StartsWith('¿') && presentes == terminos.Count && terminos.Count >= 2) puntaje *= 1.15;
            res.Add(new Resultado(s, puntaje));
        }
        return res.OrderByDescending(r => r.Puntaje).Take(cuantos).ToList();
    }

    // ---------- Errores de tipeo ----------
    // Un término que no está en el manual se cambia por la palabra del manual más parecida (Damerau-Levenshtein: 1 cambio
    // para 4–6 letras, 2 para 7 o más; a igual distancia, la más frecuente). Números y códigos no se corrigen.
    private IEnumerable<string> Corregir(IEnumerable<string> terminos)
    {
        foreach (var t in terminos)
        {
            if (t.Length < 4 || t.Any(char.IsDigit) || _df.ContainsKey(t)) { yield return t; continue; }
            var max = t.Length >= 7 ? 2 : 1;
            string? mejor = null; int dMejor = int.MaxValue, fMejor = 0;
            foreach (var (v, f) in _df)
            {
                if (Math.Abs(v.Length - t.Length) > max) continue;
                var d = Distancia(t, v, max);
                if (d > max) continue;
                if (d < dMejor || (d == dMejor && f > fMejor)) { mejor = v; dMejor = d; fMejor = f; }
            }
            yield return mejor ?? t;
        }
    }

    // Distancia de edición con transposiciones, cortando cuando supera el máximo.
    private static int Distancia(string a, string b, int max)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            int minFila = int.MaxValue;
            for (int j = 1; j <= b.Length; j++)
            {
                int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                int v = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + costo);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) v = Math.Min(v, d[i - 2, j - 2] + 1);
                d[i, j] = v;
                if (v < minFila) minFila = v;
            }
            if (minFila > max) return max + 1;
        }
        return d[a.Length, b.Length];
    }

    // ---------- Índice ----------
    private List<LectorInterfaz.Control> LeerControles(bool desarrollo,string componentes)
    {
        try
        {
            if (desarrollo) return LectorInterfaz.Leer(componentes);
            var publicado = Path.Combine(AppContext.BaseDirectory,"asistente-interfaz.json");
            return File.Exists(publicado) ? System.Text.Json.JsonSerializer.Deserialize<List<LectorInterfaz.Control>>(File.ReadAllText(publicado)) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _log.LogWarning(ex,"Asistente: el índice de controles no está disponible");
            return new();
        }
    }

    private void Asegurar()
    {
        if ((DateTime.UtcNow - _revisado).TotalSeconds < 30 && _secciones.Count > 0) return;
        lock (_candado)
        {
            _revisado = DateTime.UtcNow;
            if (!Directory.Exists(_carpeta)) { _log.LogWarning("Asistente: no se encontró el manual en {Carpeta}", _carpeta); return; }
            var archivos = Directory.GetFiles(_carpeta, "*.md").Where(f => !Path.GetFileName(f).StartsWith('_') && !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase)).ToList();
            var componentes = Path.Combine(_entorno.ContentRootPath,"Components");
            var desarrollo = _entorno.IsDevelopment() && Directory.Exists(componentes);
            var fuentes = desarrollo ? archivos.Concat(Directory.EnumerateFiles(componentes,"*.razor",SearchOption.AllDirectories)) : archivos;
            var firma = string.Join('|', fuentes.Order(StringComparer.Ordinal).Select(f => f + File.GetLastWriteTimeUtc(f).Ticks));
            if (firma == _firma && _secciones.Count > 0) return;
            try
            {
                Indexar(archivos, LeerControles(desarrollo,componentes));
                _firma = firma;
            }
            catch (Exception ex) { _log.LogError(ex, "Asistente: no se pudo leer el manual"); }
        }
    }

    private static readonly Regex Comentario = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    private void Indexar(List<string> archivos, List<LectorInterfaz.Control> controles)
    {
        var capitulos = new List<Capitulo>();
        var secciones = new List<Seccion>();
        foreach (var f in archivos.OrderBy(x => x))
        {
            var texto = Comentario.Replace(File.ReadAllText(f), "");
            var (meta, cuerpo) = SepararEncabezado(texto);
            var cap = new Capitulo(Path.GetFileNameWithoutExtension(f),
                meta.GetValueOrDefault("modulo") ?? Path.GetFileNameWithoutExtension(f),
                meta.GetValueOrDefault("titulo") ?? Path.GetFileNameWithoutExtension(f),
                meta.GetValueOrDefault("menu") ?? "",
                Lista(meta.GetValueOrDefault("rutas")), Lista(meta.GetValueOrDefault("palabras")));
            cap.Opciones.AddRange(LeerOpciones(meta.GetValueOrDefault("opciones"), cap));
            capitulos.Add(cap);
            Partir(cap, cuerpo, secciones);
        }

        foreach (var grupo in controles.GroupBy(x => x.Ruta))
        {
            var ruta = grupo.Key;
            var cap = capitulos.FirstOrDefault(c => c.Rutas.Contains(ruta));
            if (cap == null)
            {
                var nombre = Navegacion.MapaNavegacion.De(ruta)?.Nombre;
                if (nombre == null) continue;
                cap = new Capitulo(ruta.Trim('/'),Seguridad.CatalogoModulos.ClaveDeRuta(ruta),nombre,"",new[] { ruta }, Array.Empty<string>());
                capitulos.Add(cap);
            }
            cap.Campos.AddRange(grupo.Where(c => c.Campo));
            foreach (var control in grupo.Where(c => !c.Campo))
                if (!cap.Opciones.Any(o => o.Ruta.Split('?')[0] == ruta && (o.Nombre == control.Nombre || o.Resaltar?.TrimStart('!') == control.Nombre)))
                    cap.Opciones.Add(new(control.Nombre,DestinoControl(ruta,control.Nombre),control.Nombre,cap));
        }
        var df = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var s in secciones)
        {
            void Sumar(IEnumerable<string> terminos, double peso) { foreach (var t in terminos) s.Tf[t] = s.Tf.GetValueOrDefault(t) + peso; }
            var cuerpo = TextoAyuda.Terminos(s.Markdown);
            Sumar(cuerpo, 1);
            s.TerminosTitulo.UnionWith(TextoAyuda.Terminos(s.Titulo));
            Sumar(s.TerminosTitulo, 10);
            if (s.Padre is not null) Sumar(TextoAyuda.Terminos(s.Padre), 0.25);
            Sumar(TextoAyuda.Terminos(s.Capitulo.Titulo + " " + s.Capitulo.Menu), 0.6);
            Sumar(s.Capitulo.Palabras.SelectMany(p => TextoAyuda.Terminos(p)), 0.15);
            s.Largo = Math.Max(20, cuerpo.Count);
            foreach (var t in s.Tf.Keys) df[t] = df.GetValueOrDefault(t) + 1;
        }
        _capitulos = capitulos; _secciones = secciones; _df = df;
        _largoMedio = secciones.Count == 0 ? 1 : secciones.Average(s => s.Largo);
        _log.LogInformation("Asistente: manual indexado ({Capitulos} capítulos, {Secciones} secciones)", capitulos.Count, secciones.Count);
    }

    // Cada ### es una sección; un ## sin ### (o su texto antes del primer ###) también. Se omiten «Relacionado» y las
    // líneas «> Por confirmar» (dudas abiertas: el asistente no las da como hechos).
    private static void Partir(Capitulo cap, string cuerpo, List<Seccion> secciones)
    {
        string? h2 = null, h3 = null; var buf = new List<string>();
        void Cerrar()
        {
            var titulo = h3 ?? h2;
            var md = string.Join('\n', buf).Trim();
            buf.Clear();
            if (titulo is null || md.Length == 0) return;
            if (titulo.Equals("Relacionado", StringComparison.OrdinalIgnoreCase)) return;
            secciones.Add(new Seccion { Id = $"{cap.Archivo}#{secciones.Count(x => x.Capitulo == cap)}", Capitulo = cap, Titulo = titulo, Padre = h3 is null ? null : h2, Markdown = md });
        }
        foreach (var crudo in cuerpo.Replace("\r", "").Split('\n'))
        {
            var l = crudo.TrimEnd();
            if (l.StartsWith("# ")) continue;
            if (l.StartsWith("## ")) { Cerrar(); h2 = l[3..].Trim(); h3 = null; continue; }
            if (l.StartsWith("### ")) { Cerrar(); h3 = l[4..].Trim(); continue; }
            if (l.TrimStart().StartsWith("> Por confirmar", StringComparison.OrdinalIgnoreCase)) continue;
            if (l.Trim() == "---") continue;
            buf.Add(l);
        }
        Cerrar();
    }

    private static (Dictionary<string, string> Meta, string Cuerpo) SepararEncabezado(string texto)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var t = texto.Replace("\r", "");
        if (!t.StartsWith("---\n")) return (meta, t);
        var fin = t.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (fin < 0) return (meta, t);
        string? clave = null;
        foreach (var linea in t[4..fin].Split('\n'))
        {
            // Lista en bloque (`clave:` vacía y luego líneas `  - valor`): se guarda una línea por elemento.
            if (clave is not null && linea.Length > 0 && char.IsWhiteSpace(linea[0]) && linea.TrimStart().StartsWith("- "))
            {
                var el = linea.TrimStart()[2..].Trim();
                meta[clave] = meta[clave].Length == 0 ? el : meta[clave] + "\n" + el;
                continue;
            }
            if (clave is not null && linea.TrimStart().StartsWith('#')) continue;   // línea comentada dentro de la lista
            clave = null;
            var i = linea.IndexOf(':');
            if (i <= 0) continue;
            var valor = linea[(i + 1)..];
            var hash = valor.IndexOf(" #", StringComparison.Ordinal);   // comentario al final de la línea
            if (hash >= 0) valor = valor[..hash];
            meta[linea[..i].Trim()] = valor.Trim();
            if (valor.Trim().Length == 0) clave = linea[..i].Trim();
        }
        return (meta, t[(fin + 4)..]);
    }

    // "Nombre: /ruta?x=y | Texto a resaltar" (una por línea). Solo rutas locales: empiezan con "/" y no con "//".
    private static IEnumerable<Opcion> LeerOpciones(string? valor, Capitulo cap)
    {
        if (string.IsNullOrWhiteSpace(valor)) yield break;
        foreach (var l in valor.Split('\n'))
        {
            var i = l.IndexOf(": /", StringComparison.Ordinal);
            if (i <= 0) continue;
            var nombre = l[..i].Trim().Trim('\'', '"');
            var resto = l[(i + 2)..];
            var barra = resto.IndexOf('|');
            var ruta = (barra < 0 ? resto : resto[..barra]).Trim();
            var resaltar = barra < 0 ? null : resto[(barra + 1)..].Trim().Trim('\'', '"');
            if (nombre.Length == 0 || !ruta.StartsWith('/') || ruta.StartsWith("//") || ruta.Contains('\\') || ruta.Any(char.IsWhiteSpace)) continue;
            yield return new Opcion(nombre, ruta, string.IsNullOrEmpty(resaltar) ? null : resaltar, cap);
        }
    }

    // La página de riesgos muestra el exportador en la vista Lista (Riesgos.VerQ).
    private static string DestinoControl(string ruta,string control) =>
        ruta == "/riesgos" && control == "Exportar" ? "/riesgos?ver=lista" : ruta;

    private static bool CoincidePlantilla(string plantilla,string ruta)
    {
        var segmentos = plantilla.Trim('/').Split('/');
        var actuales = ruta.Trim('/').Split('/');
        return segmentos.Length == actuales.Length && segmentos.Zip(actuales).All(par =>
            par.First.StartsWith('{') ? Regex.IsMatch(par.Second, "^[a-zA-Z0-9_-]+$") : par.First == par.Second);
    }

    private static readonly char[] Separadores = { ' ', '\t', '«', '»', '"', '.', ':', '¿', '?' };
    private static string Clave(string s) =>
        string.Join(' ', TextoAyuda.SinTildes(s).ToLowerInvariant().Split(Separadores, StringSplitOptions.RemoveEmptyEntries));

    private static bool EsCapitulo(Capitulo c, string clave) =>
        Clave(c.Titulo) == clave || (c.Menu.Length > 0 && Clave(c.Menu.Split('›')[^1]) == clave);

    private static Opcion? DeCapitulo(Capitulo c) => c.RutaIr is { } r ? new Opcion(c.Titulo, r, null, c) : null;

    // Opción a la que remite un texto del manual (lo que va en **negrita** o un [enlace](otro.md)).
    // Orden: opción del capítulo de contexto → «Capítulo › Opción» → título de un capítulo → opción única en toda la app.
    // null = no es una opción conocida (queda como texto).
    public Opcion? ResolverOpcion(string texto, Capitulo? contexto, string? destinoMd = null)
    {
        Asegurar();
        if (destinoMd is not null && !destinoMd.Contains("://"))
        {
            var archivo = Path.GetFileNameWithoutExtension(destinoMd.Split('#')[0]);
            if (_capitulos.FirstOrDefault(c => c.Archivo.Equals(archivo, StringComparison.OrdinalIgnoreCase)) is { } cd) return DeCapitulo(cd);
        }
        var k = Clave(texto);
        if (k.Length == 0 || k.Length > 80) return null;
        Opcion? En(Capitulo? c, string n) => c?.Opciones.FirstOrDefault(o => Clave(o.Nombre) == n);

        if (En(contexto, k) is { } o0) return o0;                       // nombre completo (puede llevar ›)
        var partes = texto.Split('›', '>').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (partes.Count > 1)
        {
            var ultimo = Clave(partes[^1]);
            for (int j = partes.Count - 2; j >= 0; j--)
            {
                var previo = Clave(partes[j]);
                if (_capitulos.FirstOrDefault(c => EsCapitulo(c, previo)) is { } cap) return En(cap, ultimo) ?? DeCapitulo(cap);
                if (En(contexto, previo) is not null && En(contexto, ultimo) is { } oc) return oc;
            }
            return _capitulos.FirstOrDefault(c => EsCapitulo(c, ultimo)) is { } cu ? DeCapitulo(cu) : null;
        }
        if (_capitulos.FirstOrDefault(c => EsCapitulo(c, k)) is { } ct) return DeCapitulo(ct);
        var unicas = _capitulos.SelectMany(c => c.Opciones).Where(o => Clave(o.Nombre) == k).Take(2).ToList();
        return unicas.Count == 1 ? unicas[0] : null;
    }

    // Opciones que la pregunta nombra directamente («ajustes de avance de obra», «dónde está la vista tendido»): todas las
    // palabras del nombre de la opción deben estar en la pregunta. Un nombre de una sola palabra exige además que la
    // pregunta nombre su módulo o que el usuario esté en esa pantalla (si no, «torre» o «mapa» saldrían en todo).
    public List<Opcion> OpcionesPara(string pregunta, string? rutaActual, int cuantas = 3)
    {
        Asegurar();
        var q = Corregir(TextoAyuda.Terminos(pregunta)).ToHashSet();
        q.UnionWith(TextoAyuda.Terminos(pregunta));
        if (q.Count == 0) return new();
        var actual = CapituloDeRuta(rutaActual);
        var literal = " " + Clave(pregunta) + " ";
        var res = new List<(Opcion O, double P)>();
        foreach (var c in _capitulos)
        {
            var nombraModulo = TextoAyuda.Terminos(c.Titulo).Any(q.Contains);
            foreach (var o in c.Opciones)
            {
                // Lo que va entre paréntesis («Importar avance acumulado (Excel)») no se exige en la pregunta.
                var t = o.Terminos;
                var exacto = literal.Contains(" " + Clave(o.Nombre) + " ");
                if (!exacto && (t.Count == 0 || !t.All(q.Contains))) continue;
                if (t.Count == 1 && !nombraModulo && c != actual) continue;
                var enPagina = string.Equals(o.Ruta.Split('?')[0],rutaActual?.Split('?')[0],StringComparison.OrdinalIgnoreCase);
                var control = o.Resaltar != null && (pregunta.Contains("botón") || pregunta.Contains("export"));
                var destino = o;
                if (o.Ruta.Contains('{'))
                {
                    var actualSinConsulta = rutaActual?.Split('?','#')[0];
                    if (actualSinConsulta == null || !CoincidePlantilla(o.Ruta,actualSinConsulta)) continue;
                    destino = o with { Ruta = actualSinConsulta };
                }
                res.Add((destino, (exacto ? 2 + Math.Min(o.Nombre.Length,120) / 120.0 : 0) + t.Count + (nombraModulo ? 2 : 0) + (c == actual ? 1 : 0) + (enPagina ? .2 : 0) + (control ? .4 : 0)));
            }
        }
        return res.OrderByDescending(x => x.P).ThenBy(x => x.O.Nombre.Length)
            .GroupBy(x => x.O.Ruta + "|" + x.O.Resaltar).Select(g => g.First().O).Take(cuantas).ToList();
    }

    // Opción principal de una sección: la primera opción de SU capítulo que la sección menciona en negrita.
    private static readonly Regex NegritaMd = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    public Opcion? OpcionPrincipal(Seccion s)
    {
        foreach (Match m in NegritaMd.Matches(s.Markdown))
            if (ResolverOpcion(m.Groups[1].Value, s.Capitulo) is { } o && o.Capitulo == s.Capitulo && o.Nombre != s.Capitulo.Titulo) return o;
        // El título de la sección también puede nombrar la opción («Cómo usar la vista Tendido»).
        // Nombres muy cortos («?», el botón de ayuda) no cuentan: aparecen en cualquier pregunta frecuente.
        return s.Capitulo.Opciones.Where(o => Clave(o.Nombre).Length >= 3 && Clave(s.Titulo).Contains(Clave(o.Nombre)))
            .OrderByDescending(o => o.Nombre.Length).FirstOrDefault();
    }

    private static List<string> Lista(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? new()
        : valor.Trim().TrimStart('[').TrimEnd(']').Split(',').Select(x => x.Trim().Trim('\'', '"')).Where(x => x.Length > 0).ToList();
}
