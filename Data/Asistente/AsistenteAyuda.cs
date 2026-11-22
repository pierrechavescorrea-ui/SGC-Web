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
    private readonly List<Mensaje> _historial = new();
    private string? _identidad;
    public List<Mensaje> Historial
    {
        get
        {
            var permisos = _usuario.Permisos;
            var identidad = string.Join('|',_usuario.Id,_usuario.Proyecto?.Id,permisos.Rol,permisos.AdminGlobal,permisos.AuditorGlobal,
                string.Join(',',permisos.PorModulo.OrderBy(p => p.Key).Select(p => p.Key + "=" + (int)p.Value)));
            if (identidad != _identidad) { _historial.Clear(); _identidad = identidad; }
            return _historial;
        }
    }

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
            Texto = PersonaPir.Presentacion(cap?.Titulo),
            Sugerencias = sug,
        };
    }

    // «Nueva conversación»: mismo mensaje de bienvenida con otra frase.
    public Mensaje Reinicio(string? ruta)
    {
        var b = Bienvenida(ruta);
        return new Mensaje { De = Quien.Asistente, Texto = PersonaPir.Reinicio(_manual.CapituloDeRuta(ruta)?.Titulo), Sugerencias = b.Sugerencias };
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
        if (PersonaPir.PreguntaPorMi(pregunta)) return new Mensaje { De = Quien.Asistente, Texto = PersonaPir.QuienSoy() };
        var palabras = TextoAyuda.Palabras(pregunta).ToList();
        var utiles = palabras.Where(p => !TextoAyuda.EsVacia(p) && !Saludos.Contains(p) && !Agradece.Contains(p)).ToList();

        var frase = string.Join(' ', palabras);
        if (frase is "que hace este icono" or "que hace ese icono")
            return new Mensaje { De = Quien.Asistente, Texto = "¿Qué texto aparece al pasar el puntero sobre el ícono? Con ese nombre puedo buscar el control en esta pantalla." };
        var ultima = Historial.Take(Historial.Count - 1).LastOrDefault(m => m.De == Quien.Asistente);
        if (frase.StartsWith("no me refiero a ")) return Responder(frase["no me refiero a ".Length..], ruta);
        if (frase.Contains(" y "))
        {
            var partes = frase.Split(" y ",2);
            if (AccesoAyuda.Requisito(partes[0]) != Accion.Ver && AccesoAyuda.Requisito(partes[1]) != Accion.Ver)
            {
                var tareas = partes.Select(p => _manual.Buscar(p,ruta,1).FirstOrDefault()?.Seccion)
                    .OfType<ManualAyuda.Seccion>().DistinctBy(s => s.Id).Select(s => ConEnlace(s,Etiqueta(s))).ToList();
                if (tareas.Count > 1) return new Mensaje { De = Quien.Asistente, Texto = "¿Con qué tarea desea comenzar?", Sugerencias = tareas };
            }
        }
        if (frase is "y eso donde" or "y donde" or "eso donde" && ultima?.Seccion is { } previa)
            return DesdeSeccion(previa,ruta,new());
        if (Sesion(frase,ruta) is { } personal) return personal;
        if (frase == "y en la tabla" && ultima?.Seccion is { } tema)
        {
            var tablas = tema.Capitulo.Opciones.Where(o => o.Terminos.Contains("tabla") && PuedeVerOpcion(o))
                .Take(3).Select(o => new Sugerencia(o.Etiqueta,Ruta:o.Ruta,Resaltar:o.Resaltar,Destino:o.Etiqueta)).ToList();
            if (tablas.Count > 0) return new Mensaje { De = Quien.Asistente, Texto = $"En {tema.Capitulo.Titulo} puede consultar:", Atajos = tablas };
        }
        if (palabras.Count > 0 && utiles.Count == 0)
        {
            if (palabras.Any(Agradece.Contains))
                return new Mensaje { De = Quien.Asistente, Texto = PersonaPir.Gracias() };
            // Saludo: la presentación solo la primera vez; luego, corto.
            if (Historial.Count(m => m.De == Quien.Usuario) <= 1) return Bienvenida(ruta);
            var b = Bienvenida(ruta);
            return new Mensaje { De = Quien.Asistente, Texto = PersonaPir.Saludo(_manual.CapituloDeRuta(ruta)?.Titulo), Sugerencias = b.Sugerencias };
        }
        if (PreguntaPorEstaPantalla(palabras)) return ResumenPantalla(ruta);

        // Un pronombre después de «Exportar», por ejemplo, no identifica un registro.
        // Busque los controles de la acción en la pantalla antes de comparar capítulos.
        var terminos = TextoAyuda.Terminos(pregunta).Distinct().ToList();
        if (ultima != null && ruta != null && terminos.Count == 1 &&
            palabras.Any(p => p is "lo" or "la" or "los" or "las") &&
            AccesoAyuda.Requisito(pregunta) != Accion.Ver &&
            ultima.Seccion?.Tf.ContainsKey(terminos[0]) != true &&
            _manual.CapituloDeRuta(ruta) is { } pantalla)
        {
            var pagina = ruta.Split('?', '#')[0];
            var controles = pantalla.Opciones.Where(o => o.Ruta.Split('?', '#')[0] == pagina &&
                o.Resaltar != null && o.Terminos.FirstOrDefault() == terminos[0]).ToList();
            if (controles.Count > 0)
            {
                var terminosPantalla = TextoAyuda.Terminos(pantalla.Titulo).ToHashSet();
                var locales = controles.Where(PuedeVerOpcion)
                    .OrderByDescending(o => o.Terminos.Any(terminosPantalla.Contains))
                    .DistinctBy(o => o.Resaltar!.TrimStart('!')).Take(3)
                    .Select(o => new Sugerencia(o.Etiqueta, Ruta: o.Ruta, Resaltar: o.Resaltar!.TrimStart('!'), Destino: o.Etiqueta)).ToList();
                return new Mensaje { De = Quien.Asistente, Atajos = locales, Texto = locales.Count switch
                {
                    0 => $"Su rol ({Rol}) no permite esa acción en {pantalla.Titulo}. Consulte al administrador del proyecto.",
                    1 => $"En {pantalla.Titulo} puede localizar el control aquí.",
                    _ => $"En {pantalla.Titulo}, ¿a cuál de estas opciones se refiere?"
                } };
            }
        }

        // Seguimiento: pregunta corta después de una respuesta → se queda en el tema (capítulo) anterior.
        var anterior = Historial.LastOrDefault(m => m.De == Quien.Asistente && m.Seccion is not null)?.Seccion?.Capitulo;
        var contexto = utiles.Count <= 3 && (palabras.FirstOrDefault() == "y" || anterior == _manual.CapituloDeRuta(ruta)) ? anterior : null;
        var consulta = pregunta;
        if (contexto != null && ultima?.Seccion is { } anteriorSeccion && palabras.FirstOrDefault() == "y")
            consulta += " " + anteriorSeccion.Titulo;
        var resultados = _manual.Buscar(consulta, ruta, 5, contexto, TextoAyuda.TerminosDeFrases(pregunta));
        var coincidencias = _manual.OpcionesPara(pregunta, ruta, int.MaxValue);
        var opciones = coincidencias.Where(PuedeVerOpcion);
        if (coincidencias.FirstOrDefault() is { } primera && (palabras.Contains("boton") || palabras.Contains("exporto"))
            && !PuedeVerOpcion(primera) && !opciones.Any(o => o.Modulo == primera.Modulo && o.Nombre == primera.Nombre))
            return new Mensaje { De = Quien.Asistente, Texto = $"Su rol ({Rol}) no tiene acceso a {primera.Etiqueta}. Consulte al administrador del {(primera.Ruta.StartsWith("/admin") ? "sistema" : "proyecto")}." };
        var nombrePedido = frase.Contains("boton ") ? frase.Split("boton ",2)[1] : "";
        var literales = opciones.Where(o => string.Join(' ',TextoAyuda.Palabras(o.Nombre)) == nombrePedido).ToList();
        if (literales.Count > 0) opciones = literales;
        var enPantalla = opciones.Where(o => o.Ruta.Split('?')[0] == ruta?.Split('?')[0]).ToList();
        var atajos = (enPantalla.Count > 0 && !pregunta.Contains(" en ",StringComparison.OrdinalIgnoreCase) ? enPantalla : opciones).Take(3)
            .Select(o => new Sugerencia(o.Etiqueta, Ruta: o.Ruta, Resaltar: o.Resaltar, Destino: o.Etiqueta)).ToList();
        if (atajos.Count > 0 && (palabras.Contains("boton") || palabras.Contains("icono") || palabras.Contains("exporto")))
            return new Mensaje { De = Quien.Asistente, Texto = atajos.Count == 1 ? "Puede localizar el control con este enlace." : "¿A cuál de estos controles se refiere?", Atajos = atajos };
        if (resultados.Count == 0 || resultados[0].Puntaje < UmbralMinimo)
        {
            RegistrarSinRespuesta(pregunta, ruta);
            if (atajos.Count > 0)
                return new Mensaje { De = Quien.Asistente, Texto = PersonaPir.SoloAtajos, Atajos = atajos };
            var cap = _manual.CapituloDeRuta(ruta);
            var sug = resultados.Select(x => ConEnlace(x.Seccion, Etiqueta(x.Seccion))).Take(3).ToList();
            if (sug.Count == 0) sug = SugerenciasDe(cap, 4);
            return new Mensaje
            {
                De = Quien.Asistente,
                Texto = PersonaPir.SinRespuesta
                    + (sug.Count > 0 ? ", o revise si alguna de estas opciones le sirve:" : "."),
                Sugerencias = sug,
            };
        }

        var mejor = resultados[0];
        if (utiles.Count <= 3 && resultados.Count >= 2 && resultados[1].Puntaje >= mejor.Puntaje * .94 &&
            resultados[1].Seccion.Capitulo != mejor.Seccion.Capitulo)
            return new Mensaje { De = Quien.Asistente, Texto = "¿Sobre cuál de estas opciones pregunta?",
                Sugerencias = resultados.Take(3).Select(r => ConEnlace(r.Seccion,Etiqueta(r.Seccion))).ToList() };
        // Otras secciones con puntaje cercano: se ofrecen como "también puede servirle".
        var otras = resultados.Skip(1).Where(x => x.Puntaje >= mejor.Puntaje * 0.55 && x.Seccion.Id != mejor.Seccion.Id)
            .Select(x => ConEnlace(x.Seccion, Etiqueta(x.Seccion))).Take(3).ToList();
        var r = DesdeSeccion(mejor.Seccion, ruta, otras);
        r.Atajos.AddRange(atajos.Where(a => !(a.Ruta == r.RutaIr && a.Resaltar == r.ResaltarIr)));
        return r;
    }

    private bool PuedeVerOpcion(ManualAyuda.Opcion o) =>
        AccesoAyuda.Permite(_usuario,o.Ruta,o.Requiere);

    private bool PuedeAbrirRuta(string? ruta) =>
        AccesoAyuda.Permite(_usuario,ruta,Accion.Ver);

    // Por debajo de esto la coincidencia es casual (una palabra suelta poco informativa).
    private const double UmbralMinimo = 2.6;

    private Mensaje DesdeSeccion(ManualAyuda.Seccion s, string? ruta, List<Sugerencia> otras)
    {
        var cap = s.Capitulo;
        string? aviso = null;
        if (cap.RutaIr is { } rutaCapitulo && !PuedeAbrirRuta(rutaCapitulo) && !cap.Rutas.Any(PuedeAbrirRuta))
            aviso = $"Su rol no tiene acceso a {cap.Titulo} en este proyecto. Si lo necesita, pídalo al administrador.";
        var enEsaPantalla = _manual.CapituloDeRuta(ruta) == cap;
        // «Ir a» lleva a la opción exacta que trata la sección (vista, pestaña o botón); si no hay, al módulo.
        // En la misma pantalla solo se ofrece si hay una opción concreta que mostrar.
        var op = aviso is null ? _manual.OpcionPrincipal(s) : null;
        var destino = op?.Ruta ?? cap.RutaIr;
        var accion = AccesoAyuda.Requisito(s.Titulo);
        var pasos = AccesoAyuda.Requisito(s.Markdown);
        if (aviso == null && PuedeAbrirRuta(destino) && !AccesoAyuda.Permite(_usuario,destino,pasos))
            aviso = $"Su rol ({Rol}) puede consultar esta sección. Algunos pasos requieren permisos adicionales del administrador del proyecto.";
        if (!PuedeAbrirRuta(destino) || op != null && !PuedeVerOpcion(op) ||
            !AccesoAyuda.Permite(_usuario,destino,accion))
            aviso = $"Su rol ({Rol}) no permite esta operación. Consulte al administrador {(destino?.StartsWith("/admin") == true ? "global" : "del proyecto")}.";
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
        if (o is null || !PuedeVerOpcion(o) || !AccesoAyuda.Permite(_usuario,o.Ruta,AccesoAyuda.Requisito(s.Titulo)))
            return new Sugerencia(texto, s.Id);
        return new Sugerencia(texto, s.Id, Ruta: o.Ruta, Resaltar: o.Resaltar, Destino: o.Etiqueta);
    }

    private string Rol => _usuario.Permisos.Rol ?? (_usuario.EsAdministrador ? "Administrador global" : _usuario.EsAuditor ? "Auditor global" : "sin rol asignado");

    private Mensaje? Sesion(string frase, string? ruta)
    {
        string? texto = null;
        var modulo = CatalogoModulos.Lista.OrderByDescending(m => m.Nombre.Length)
            .FirstOrDefault(m => (" " + frase + " ").Contains(" " + TextoAyuda.SinTildes(m.Nombre.ToLowerInvariant()) + " "))
            ?? CatalogoModulos.De(CatalogoModulos.ClaveDeRuta(ruta));
        if (frase.Contains("mi rol") || frase.Contains("rol tengo") || frase.Contains("quien soy") || frase.Contains("proyecto estoy"))
            texto = $"{(_usuario.Nombre.Length > 0 ? _usuario.Nombre + ". " : "")}Proyecto {_usuario.Proyecto?.Nombre ?? "sin seleccionar"}. Rol {Rol}."
                + (_usuario.EsAdministrador ? " Usted es administrador global." : _usuario.EsAuditor ? " Usted es auditor global." : "");
        else if (frase.Contains("modulos tengo") || frase.Contains("mis modulos"))
            texto = "Módulos disponibles: " + string.Join(", ",CatalogoModulos.Lista.Where(m => _usuario.Puede(m.Clave,Accion.Ver)).Select(m => m.Nombre)) + ".";
        else if (modulo != null && (frase.Contains("que puedo hacer") || frase.StartsWith("puedo ") ||
            frase.StartsWith("por que no puedo ") || frase.Contains("que ve un consultor")))
        {
            var permisos = _usuario.Permisos.De(modulo.Clave);
            texto = $"En {modulo.Nombre}, su rol ({Rol}) tiene estos permisos: {Acciones.Texto(permisos)}.";
            var requiere = AccesoAyuda.Requisito(frase);
            if (!permisos.HasFlag(requiere)) texto += " Para esa operación, solicite acceso al administrador del proyecto.";
            if (frase.Contains("consultor")) texto += $" Para Consulta, la matriz de fábrica indica: {Acciones.Texto(RolesFabrica.Permiso("consulta",modulo))}.";
            if (frase.Contains("que puedo hacer"))
            {
                var resumen = ResumenPantalla(ruta);
                return new Mensaje { De = Quien.Asistente, Texto = texto, Seccion = resumen.Seccion,
                    Sugerencias = resumen.Sugerencias, Atajos = _manual.CapituloDeRuta(ruta)?.Opciones
                        .Where(o => o.Ruta.Split('?')[0] == ruta && PuedeVerOpcion(o)).Take(4)
                        .Select(o => new Sugerencia(o.Nombre,Ruta:o.Ruta,Resaltar:o.Resaltar,Destino:o.Etiqueta)).ToList() ?? new() };
            }
        }
        return texto == null ? null : new Mensaje { De = Quien.Asistente, Texto = texto };
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
