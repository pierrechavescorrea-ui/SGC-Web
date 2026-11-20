using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Asistente;

// Conversación del asistente de ayuda (scoped = una por pestaña/circuito: el historial sobrevive al cambiar de pantalla).
// Decide QUÉ responder; ManualAyuda busca y MarkdownAyuda dibuja. Todo local: nada sale del servidor.
public sealed class AsistenteAyuda
{
    public enum Quien { Usuario, Asistente }

    // Ruta/Resaltar = enlace directo a la opción que trata la sección (botón «ir» junto a la sugerencia).
    public sealed record Sugerencia(string Texto, string? SeccionId = null, string? Pregunta = null, string? Ruta = null, string? Resaltar = null, string? Destino = null);

    public sealed class Mensaje
    {
        public required Quien De { get; init; }
        public string Texto { get; init; } = "";                 // lo que escribió el usuario, o una frase corta del asistente
        public ManualAyuda.Seccion? Seccion { get; init; }       // sección del manual que responde
        public string? Aviso { get; init; }                      // p. ej. "Su rol no tiene acceso a este módulo"
        public string? RutaIr { get; init; }
        public string? NombreIr { get; init; }
        public string? ResaltarIr { get; init; }                 // control que se ilumina al llegar (title/aria-label/texto)
        public List<Sugerencia> Sugerencias { get; init; } = new();
        public List<Sugerencia> Atajos { get; init; } = new();   // opciones que la pregunta nombra: «Ir directo»
        public DateTime Hora { get; init; } = DateTime.Now;
    }

    private static readonly object Candado = new();
    private readonly ManualAyuda _manual;
    private readonly UsuarioActual _usuario;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AsistenteAyuda> _log;
    public List<Mensaje> Historial { get; } = new();

    public AsistenteAyuda(ManualAyuda manual, UsuarioActual usuario, IWebHostEnvironment env, ILogger<AsistenteAyuda> log)
    { _manual = manual; _usuario = usuario; _env = env; _log = log; }

    private static readonly HashSet<string> Saludos = new() { "hola", "buenas", "buenos", "dias", "tardes", "noches", "hey", "saludos", "ola" };
    private static readonly HashSet<string> Agradece = new() { "gracias", "listo", "perfecto", "ok", "vale", "genial", "excelente", "entendido" };

    // Primer mensaje al abrir el panel (o al pedir "¿qué puedo hacer aquí?").
    public Mensaje Bienvenida(string? ruta)
    {
        var cap = _manual.CapituloDeRuta(ruta);
        var sug = SugerenciasDe(cap, 5);
        if (sug.Count == 0) sug = SugerenciasGenerales();
        return new Mensaje
        {
            De = Quien.Asistente,
            Texto = cap is null
                ? "Hola. Soy el asistente de SGC Web: le explico cómo usar la app con lo que dice el manual. Escriba su pregunta, por ejemplo «¿cómo subo una nueva versión de un documento?»."
                : $"Hola. Está en **{cap.Titulo}**. Pregúnteme cómo hacer algo aquí o en cualquier otro módulo. Esto es lo que más se consulta en esta pantalla:",
            Sugerencias = sug,
        };
    }

    public Mensaje Preguntar(string pregunta, string? ruta)
    {
        pregunta = (pregunta ?? "").Trim();
        if (pregunta.Length > 300) pregunta = pregunta[..300];
        Historial.Add(new Mensaje { De = Quien.Usuario, Texto = pregunta });
        var r = Responder(pregunta, ruta);
        Historial.Add(r);
        return r;
    }

    // Abrir una sección concreta (clic en una sugerencia).
    public Mensaje AbrirSeccion(string id, string textoUsuario)
    {
        Historial.Add(new Mensaje { De = Quien.Usuario, Texto = textoUsuario });
        var s = _manual.PorId(id);
        var r = s is null ? new Mensaje { De = Quien.Asistente, Texto = "No encontré esa sección del manual." } : DesdeSeccion(s, null, new());
        Historial.Add(r);
        return r;
    }

    private Mensaje Responder(string pregunta, string? ruta)
    {
        var palabras = TextoAyuda.Palabras(pregunta).ToList();
        var utiles = palabras.Where(p => !TextoAyuda.EsVacia(p) && !Saludos.Contains(p) && !Agradece.Contains(p)).ToList();

        if (palabras.Count > 0 && utiles.Count == 0)
        {
            if (palabras.Any(Agradece.Contains))
                return new Mensaje { De = Quien.Asistente, Texto = "Con gusto. Si necesita algo más, escríbame." };
            return Bienvenida(ruta);
        }
        if (PreguntaPorEstaPantalla(palabras)) return ResumenPantalla(ruta);

        // Seguimiento: pregunta corta después de una respuesta → se queda en el tema (capítulo) anterior.
        var anterior = Historial.LastOrDefault(m => m.De == Quien.Asistente && m.Seccion is not null)?.Seccion?.Capitulo;
        var contexto = utiles.Count <= 3 ? anterior : null;
        var resultados = _manual.Buscar(pregunta, ruta, 5, contexto, TextoAyuda.TerminosDeFrases(pregunta));
        var atajos = _manual.OpcionesPara(pregunta, ruta).Where(PuedeVerOpcion)
            .Select(o => new Sugerencia(o.Etiqueta, Ruta: o.Ruta, Resaltar: o.Resaltar, Destino: o.Etiqueta)).ToList();
        if (resultados.Count == 0 || resultados[0].Puntaje < UmbralMinimo)
        {
            RegistrarSinRespuesta(pregunta, ruta);
            if (atajos.Count > 0)
                return new Mensaje { De = Quien.Asistente, Texto = "El manual no lo explica con esas palabras, pero puede ir directo a:", Atajos = atajos };
            var cap = _manual.CapituloDeRuta(ruta);
            var sug = resultados.Select(x => ConEnlace(x.Seccion, Etiqueta(x.Seccion))).Take(3).ToList();
            if (sug.Count == 0) sug = SugerenciasDe(cap, 4);
            return new Mensaje
            {
                De = Quien.Asistente,
                Texto = "No encontré eso en el manual de la app. Pruebe con otras palabras (por ejemplo, el nombre del módulo o del botón)"
                    + (sug.Count > 0 ? ", o revise si alguna de estas opciones le sirve:" : "."),
                Sugerencias = sug,
            };
        }

        var mejor = resultados[0];
        // Otras secciones con puntaje cercano: se ofrecen como "también puede servirle".
        var otras = resultados.Skip(1).Where(x => x.Puntaje >= mejor.Puntaje * 0.55 && x.Seccion.Id != mejor.Seccion.Id)
            .Select(x => ConEnlace(x.Seccion, Etiqueta(x.Seccion))).Take(3).ToList();
        var r = DesdeSeccion(mejor.Seccion, ruta, otras);
        r.Atajos.AddRange(atajos.Where(a => !(a.Ruta == r.RutaIr && a.Resaltar == r.ResaltarIr)));
        return r;
    }

    private bool PuedeVerOpcion(ManualAyuda.Opcion o) =>
        PuedeAbrirRuta(o.Ruta) && (o.Capitulo.Modulo is "general" or "configuracion" or "administracion" || PuedeVer(o.Capitulo.Modulo));

    private bool PuedeAbrirRuta(string? ruta) =>
        SigWeb.Data.Navegacion.MenuAdmin.VistaDe(ruta) is not { } vista ||
        (_usuario.EsAdministrador || !vista.SoloAdmin && _usuario.EsAuditor);

    // Por debajo de esto la coincidencia es casual (una palabra suelta poco informativa).
    private const double UmbralMinimo = 2.6;

    private Mensaje DesdeSeccion(ManualAyuda.Seccion s, string? ruta, List<Sugerencia> otras)
    {
        var cap = s.Capitulo;
        string? aviso = null;
        if (cap.Modulo is not ("general" or "configuracion" or "administracion") && !PuedeVer(cap.Modulo))
            aviso = $"Su rol no tiene acceso a {cap.Titulo} en este proyecto. Si lo necesita, pídalo al administrador.";
        var enEsaPantalla = _manual.CapituloDeRuta(ruta) == cap;
        // «Ir a» lleva a la opción exacta que trata la sección (vista, pestaña o botón); si no hay, al módulo.
        // En la misma pantalla solo se ofrece si hay una opción concreta que mostrar.
        var op = aviso is null ? _manual.OpcionPrincipal(s) : null;
        var destino = op?.Ruta ?? cap.RutaIr;
        if (aviso is null && !PuedeAbrirRuta(destino))
            aviso = "Esta opción requiere acceso a Administración.";
        return new Mensaje
        {
            De = Quien.Asistente,
            Seccion = s,
            Aviso = aviso,
            RutaIr = aviso is not null ? null : op is not null ? op.Ruta : enEsaPantalla ? null : cap.RutaIr,
            NombreIr = op?.Etiqueta ?? cap.Titulo,
            ResaltarIr = op?.Resaltar,
            Sugerencias = otras,
        };
    }

    // Enlace directo para lo que el manual nombra en una sección (negrita o [enlace](capitulo.md)). Sin permiso
    // para ver el módulo destino = sin enlace (queda en negrita, como antes).
    public MarkdownAyuda.Enlace? Enlace(string texto, string? destinoMd, ManualAyuda.Capitulo? contexto)
    {
        var o = _manual.ResolverOpcion(texto, contexto, destinoMd);
        if (o is null) return null;
        if (!PuedeVerOpcion(o)) return null;
        return new MarkdownAyuda.Enlace(o.Ruta, o.Resaltar, o.Etiqueta);
    }

    private Sugerencia ConEnlace(ManualAyuda.Seccion s, string texto)
    {
        var o = _manual.OpcionPrincipal(s);
        if (o is null || !PuedeVerOpcion(o))
            return new Sugerencia(texto, s.Id);
        return new Sugerencia(texto, s.Id, Ruta: o.Ruta, Resaltar: o.Resaltar, Destino: o.Etiqueta);
    }

    private Mensaje ResumenPantalla(string? ruta)
    {
        var cap = _manual.CapituloDeRuta(ruta);
        var para = cap is null ? null : _manual.Secciones.FirstOrDefault(s => s.Capitulo == cap && s.Titulo.StartsWith("Para qué sirve", StringComparison.OrdinalIgnoreCase));
        if (para is null) return Bienvenida(ruta);
        return new Mensaje { De = Quien.Asistente, Seccion = para, Sugerencias = SugerenciasDe(cap, 6) };
    }

    private static bool PreguntaPorEstaPantalla(List<string> palabras)
    {
        var t = " " + string.Join(' ', palabras) + " ";
        return t.Contains(" que puedo hacer ") || t.Contains(" para que sirve esta ") || t.Contains(" para que sirve este ")
            || t.Contains(" esta pantalla ") || t.Contains(" esta pagina ") || t.Contains(" este modulo ") || t.Trim() is "ayuda" or "menu";
    }

    // Las tareas "Cómo…" del capítulo como botones; si faltan, las preguntas frecuentes («¿…?»).
    private List<Sugerencia> SugerenciasDe(ManualAyuda.Capitulo? cap, int cuantas) => cap is null ? new()
        : _manual.Secciones.Where(s => s.Capitulo == cap && s.Titulo.StartsWith("Cómo", StringComparison.OrdinalIgnoreCase))
            .Concat(_manual.Secciones.Where(s => s.Capitulo == cap && s.Titulo.StartsWith('¿')))
            .Take(cuantas).Select(s => ConEnlace(s, s.Titulo)).ToList();

    private List<Sugerencia> SugerenciasGenerales()
    {
        var g = _manual.Capitulos.FirstOrDefault(c => c.Modulo == "general");
        return SugerenciasDe(g, 5);
    }

    private static string Etiqueta(ManualAyuda.Seccion s) => $"{s.Capitulo.Titulo} › {s.Titulo}";

    private bool PuedeVer(string modulo)
    {
        try { return _usuario.Puede(modulo, Accion.Ver); }
        catch { return true; }   // sin sesión resoluble: no se avisa nada
    }

    // Preguntas sin respuesta → App_Data/asistente/sin-respuesta.log, para completar el manual.
    private void RegistrarSinRespuesta(string pregunta, string? ruta)
    {
        try
        {
            var carpeta = Path.Combine(_env.ContentRootPath, "App_Data", "asistente");
            Directory.CreateDirectory(carpeta);
            var limpia = new string(pregunta.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
            var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm}\t{ruta}\t{limpia}{Environment.NewLine}";
            var archivo = Path.Combine(carpeta, "sin-respuesta.log");
            lock (Candado)
            {
                // Tope de 1 MB: el registro anterior se guarda una vez como .1 (no crece sin límite).
                var info = new FileInfo(archivo);
                if (info.Exists && info.Length > 1_000_000) File.Move(archivo, archivo + ".1", overwrite: true);
                File.AppendAllText(archivo, linea);
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Asistente: no se pudo registrar la pregunta sin respuesta"); }
    }
}
