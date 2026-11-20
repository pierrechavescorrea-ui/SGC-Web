using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Formularios;
using SigWeb.Data.Navegacion;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Components.Catalogos;

// Diseñador de formularios (Configuración › Catálogos › Formularios). Lista de módulos por capítulo (como el Dossier),
// boceto en vivo del formulario elegido (pasos + rejilla de 12 columnas; arrastrar y estirar con el motor del panel de
// Inicio) y propiedades del campo o paso elegido. Todo se edita en memoria (`trabajo`) hasta Guardar = versión nueva.
// Reglas que protegen la lógica de la app:
//   · campos del código: se mueven dentro de su paso, se renombran y cambian de tamaño; los obligatorios, el principal
//     o los marcados Fijo NO se ocultan; nunca se borran ni cambian de tipo;
//   · bloques compuestos (Acciones propuestas…): se mueven entre los pasos y se renombran, nada más;
//   · campos nuevos: todo (nombre, tipo, catálogo u opciones, paso, tamaño, obligatorio, ocultar, borrar → Papelera);
//   · renombrar un campo del código que también es columna de la lista renombra la columna (lo heredan tabla, Excel,
//     Dossier); renombrar un campo nuevo renombra su CampoTabla (y las fórmulas que lo citan).
public partial class DisenadorFormularios : IDisposable
{
    [Inject] private ServicioFormularios Servicio { get; set; } = default!;
    [Inject] private IServiceScopeFactory Scopes { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Parameter] public EventCallback<string> OnGestionarCatalogo { get; set; }
    // Formulario que se abre al entrar (editor?form=clave; p. ej. al volver de Catálogos).
    [Parameter] public string? FormularioInicial { get; set; }
    public string? FormularioActual => actual?.Clave;
    // Pide a Catálogos el formulario completo de catálogo nuevo (módulo del formulario, nombre sugerido); al guardar,
    // Catálogos vuelve aquí y llama CatalogoCreadoAsync. Este componente sigue montado: nada se pierde.
    [Parameter] public EventCallback<(string Modulo, string Nombre)> OnCrearCatalogo { get; set; }

    private static readonly string[] OrdenGrupos = { "Contexto y liderazgo", "Planificación", "Apoyo", "Operación", "Evaluación y mejora", "Configuración" };

    private RegistroFormularios.Info? actual;
    private Estructura? estructura;
    private Diseno trabajo = new();
    private string jsonGuardado = "";
    private Dictionary<string, int> versiones = new();
    private List<CampoTabla> camposTabla = new();
    private List<string> tiposCatalogo = new();
    private Dictionary<string, int> definiciones = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> moduloCatalogo = new(StringComparer.OrdinalIgnoreCase);   // tipo → ruta ("" = General)
    private string? selCampo, selPaso;
    private string _mensaje = "";
    private int avisoN;
    private string mensaje { get => _mensaje; set { _mensaje = value; avisoN++; } }
    private bool error, ocupado;
    private readonly Stack<string> deshacer = new();
    private readonly Dictionary<string, ElementReference> refsPaso = new();
    private DotNetObjectReference<DisenadorFormularios>? yo;
    private string? arrastrandoPaso;

    // Campo nuevo
    private bool verNuevo;
    private string ncNombre = "", ncCatalogo = "", ncOpciones = "", ncCatalogoNuevo = "";
    private TipoCampoTabla ncTipo = TipoCampoTabla.Texto;
    private string ncOrigen = "existente";     // existente | nuevo | opciones
    private string ncPaso = "";
    private bool ncMultiple;                   // lista que acepta varios valores                // paso donde quedará el campo nuevo (el elegido al abrir "Campo nuevo")

    // Guardar / historial
    private bool pedirMotivo, verHistorial;
    private string motivo = "";
    private List<DisenoFormulario> historial = new();

    private int cambiosTabla;   // nombres u opciones de campos nuevos editados (viven en CampoTabla, no en el diseño)
    private bool HayCambios => actual is not null && (cambiosTabla > 0 || DisenoModelo.AJson(trabajo) != jsonGuardado);

    protected override async Task OnInitializedAsync()
    {
        yo = DotNetObjectReference.Create(this);
        versiones = await Servicio.VersionesVigentesAsync();
        await CargarCatalogosAsync();
    }

    private async Task CargarCatalogosAsync()
    {
        using var scope = Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try { SigWeb.Data.Catalogos.ModulosCatalogo.ClasificarPendientes(db); } catch { }
        tiposCatalogo = await MotorTabla.TiposCatalogoAsync(db);
        var defs = await db.DefinicionesCatalogo.AsNoTracking().Where(d => !d.Eliminado).Select(d => new { d.Id, d.Nombre, d.Modulo }).ToListAsync();
        definiciones = defs.GroupBy(d => d.Nombre, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        moduloCatalogo = defs.GroupBy(d => d.Nombre, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Modulo ?? "", StringComparer.OrdinalIgnoreCase);
        foreach (var d in definiciones.Keys.Where(k => !tiposCatalogo.Contains(k, StringComparer.OrdinalIgnoreCase))) tiposCatalogo.Add(d);
        tiposCatalogo = tiposCatalogo.OrderBy(t => t).ToList();
    }

    // ---------- Lista de formularios por capítulo ----------

    private static string GrupoDe(RegistroFormularios.Info f) => MapaNavegacion.De(f.Ruta)?.Grupo ?? "Otros";
    private static string ModuloDe(RegistroFormularios.Info f) => MapaNavegacion.De(f.Ruta)?.Nombre ?? f.Ruta;

    private IEnumerable<IGrouping<string, RegistroFormularios.Info>> Grupos() =>
        RegistroFormularios.Todos.GroupBy(GrupoDe).OrderBy(g => Array.IndexOf(OrdenGrupos, g.Key) is var i && i >= 0 ? i : 99);

    private async Task Elegir(RegistroFormularios.Info f)
    {
        if (actual?.Clave == f.Clave) return;
        if (HayCambios) { mensaje = "Hay cambios sin guardar en " + actual!.Nombre + ": guárdelos o descártelos antes de cambiar de formulario."; error = true; return; }
        actual = f; mensaje = ""; error = false;
        await CargarFormularioAsync();
    }

    private async Task CargarFormularioAsync()
    {
        if (actual is null) return;
        await JS.InvokeVoidAsync("sigDisenador.limpiar");
        refsPaso.Clear();
        selCampo = selPaso = null; verNuevo = false; verHistorial = false; deshacer.Clear(); cambiosTabla = 0;
        estructura = await Servicio.EstructuraAsync(actual.Clave);
        var d = await Servicio.DisenoAsync(actual.Clave);
        await CargarCamposTablaAsync();
        trabajo = estructura is null ? new Diseno() : Completar(estructura, d);
        jsonGuardado = DisenoModelo.AJson(trabajo);
    }

    private async Task CargarCamposTablaAsync()
    {
        using var scope = Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Campos asociados por opción al día: nombres "Base - Opción" y un campo por cada opción nueva de las plantillas.
        try { await CamposPorOpcion.SincronizarAsync(db, actual!.Tabla); } catch (Exception ex) { Console.Error.WriteLine("Campos por opción: " + ex.Message); }
        camposTabla = await db.CamposTabla.AsNoTracking()
            .Where(c => c.Tabla == actual!.Tabla && !c.Eliminado)
            .OrderBy(c => c.Orden).ToListAsync();
        // Opciones de los catálogos de las listas (para "Campos según la opción").
        var tipos = camposTabla.Where(c => c.Tipo == TipoCampoTabla.Lista && c.CatalogoTipo != null).Select(c => c.CatalogoTipo!).Distinct().ToList();
        opcionesCatalogo = (await db.Catalogos.AsNoTracking().Where(i => tipos.Contains(i.Tipo) && i.Activo).Select(i => new { i.Tipo, i.Nombre }).ToListAsync())
            .GroupBy(i => i.Tipo).ToDictionary(g => g.Key, g => g.Select(i => i.Nombre).OrderBy(n => n).ToList());
    }

    // ---------- Campos según la opción (un campo ligado a cada opción de una lista) ----------
    private Dictionary<string, List<string>> opcionesCatalogo = new();
    private string? depOpcion;                        // opción para la que se está creando un campo
    private string depNombre = "";
    private TipoCampoTabla depTipo = TipoCampoTabla.Texto;

    private List<string> OpcionesDeLista(CampoTabla t) =>
        t.Tipo != TipoCampoTabla.Lista ? new()
        : t.CatalogoTipo is not null ? opcionesCatalogo.GetValueOrDefault(t.CatalogoTipo) ?? new()
        : (t.Opciones ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    private IEnumerable<CampoTabla> HijosDe(CampoTabla t, string opcion) =>
        camposTabla.Where(c => c.CampoPadreId == t.Id && string.Equals(c.OpcionPadre, opcion, StringComparison.OrdinalIgnoreCase));

    private void AbrirDependiente(string opcion) { depOpcion = opcion; depNombre = ""; depTipo = TipoCampoTabla.Texto; }

    // Guardar de un campo ligado a una opción. Orden: 1) ¿todas las opciones o solo esta? (antes de crear nada),
    // 2) validaciones de la opción elegida, 3) se crea y sale el mensaje final.
    private async Task CrearDependiente(CampoTabla padre, string opcion)
    {
        if (actual is null) return;
        var baseNombre = DisenoModelo.Normal(depNombre);
        if (baseNombre.Length == 0) { mensaje = "Escriba el nombre del campo."; error = true; return; }
        // ¿El mismo campo para todas las opciones de la lista (también las que se agreguen después) o uno por uno?
        if (OpcionesDeLista(padre).Count > 1 && !camposTabla.Any(p => CamposPorOpcion.EsPlantilla(p) && p.CampoPadreId == padre.Id
                && string.Equals(p.Nombre, baseNombre, StringComparison.OrdinalIgnoreCase)))
        {
            mensaje = "";
            preguntaTodas = new(padre.Id, baseNombre, opcion, depTipo);
            return;
        }
        if (await CrearUnoAsync(padre, baseNombre, opcion, depTipo) is { } ent)
        { mensaje = $"«{ent.Nombre}» aparecerá cuando {padre.Nombre} = {opcion}. Guarde el formulario para publicarlo."; error = false; }
    }

    // Valida y crea el campo "Base - Opción" ligado a la opción; lo pone en el paso del campo padre.
    // Null = no se creó (el motivo queda en mensaje).
    private async Task<CampoTabla?> CrearUnoAsync(CampoTabla padre, string baseNombre, string opcion, TipoCampoTabla tipo)
    {
        if (actual is null) return null;
        if (HijosDe(padre, opcion).Any(c => string.Equals(CamposPorOpcion.Base(c), baseNombre, StringComparison.OrdinalIgnoreCase)))
        { mensaje = $"Ya hay un campo \"{baseNombre}\" para la opción {opcion}."; error = true; return null; }
        // Nombre = lo escrito + " - " + la opción ("Valor - Consultoría"); el mismo nombre sirve en cada opción de la lista.
        var nombre = CamposPorOpcion.NombreDe(baseNombre, opcion);
        if (camposTabla.Any(c => string.Equals(DisenoModelo.Normal(c.Nombre), nombre, StringComparison.OrdinalIgnoreCase))
            || estructura?.Campos.Any(c => string.Equals(c.Etiqueta, nombre, StringComparison.OrdinalIgnoreCase)) == true)
        { mensaje = $"Ya hay un campo \"{nombre}\" en este formulario."; error = true; return null; }
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var motor = await MotorTabla.CargarLecturaAsync(db, actual.Tabla, Enumerable.Empty<ColumnaNativa>(), Enumerable.Empty<(int, string)>());
            var id = await motor.GuardarCampoAsync(db, new CampoTabla { Nombre = nombre, Tipo = tipo });
            var ent = await db.CamposTabla.FirstAsync(c => c.Id == id);
            ent.CampoPadreId = padre.Id; ent.OpcionPadre = opcion;
            await db.SaveChangesAsync();
            await CargarCamposTablaAsync();
            var pasoPadre = trabajo.Campos.FirstOrDefault(c => c.CampoTablaId == padre.Id)?.Seccion;
            var claveSel = selCampo;
            if (Tabla(id) is { } t) AgregarDisponible(t, pasoPadre);
            // Se queda en el campo de lista: así se agregan los campos de las demás opciones sin volver a buscarlo.
            selCampo = claveSel;
            depOpcion = null;
            return ent;
        }
        catch (Exception ex) { mensaje = "No se pudo crear: " + ex.Message; error = true; return null; }
    }

    // ---------- Aplicar un campo asociado a todas las opciones de la lista ----------
    private sealed record PreguntaTodas(int PadreId, string Base, string Opcion, TipoCampoTabla Tipo);
    private PreguntaTodas? preguntaTodas;

    // El campo queda como plantilla de la lista: cada opción (también las futuras) tiene su campo "Base - Opción" y en el
    // formulario se ve un solo bloque con una casilla por opción elegida. Los campos "Base - …" que ya existían se integran.
    private async Task AplicarATodasAsync()
    {
        if (actual is null || preguntaTodas is not { } q) return;
        preguntaTodas = null;
        var padre = Tabla(q.PadreId);
        if (padre is null) return;
        // Validación antes de crear nada: así no queda un campo suelto si no se puede aplicar a todas.
        if (camposTabla.Any(c => !CamposPorOpcion.EsAsociado(c) && string.Equals(DisenoModelo.Normal(c.Nombre), q.Base, StringComparison.OrdinalIgnoreCase)))
        { mensaje = $"Ya hay un campo \"{q.Base}\" en este formulario: renómbrelo para aplicar \"{q.Base}\" a todas las opciones."; error = true; return; }
        // El campo de esta opción (si ya existía "Base - Opción", se integra a la plantilla).
        var hijo = HijosDe(padre, q.Opcion).FirstOrDefault(c => string.Equals(CamposPorOpcion.Base(c), q.Base, StringComparison.OrdinalIgnoreCase))
                   ?? await CrearUnoAsync(padre, q.Base, q.Opcion, q.Tipo);
        if (hijo is null) return;
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var motor = await MotorTabla.CargarLecturaAsync(db, actual.Tabla, Enumerable.Empty<ColumnaNativa>(), Enumerable.Empty<(int, string)>());
            var idP = await motor.GuardarCampoAsync(db, new CampoTabla { Nombre = q.Base, Tipo = hijo.Tipo, Decimales = hijo.Decimales });
            var ent = await db.CamposTabla.FirstAsync(c => c.Id == idP);
            ent.CampoPadreId = padre.Id; ent.OpcionPadre = CamposPorOpcion.Todas;
            ent.Visible = false;                                  // la plantilla no es columna: lo son los campos de cada opción
            await db.SaveChangesAsync();
            var creados = await CamposPorOpcion.SincronizarAsync(db, actual.Tabla);
            await CargarCamposTablaAsync();
            var plantilla = Tabla(idP)!;
            var hijos = CamposPorOpcion.HijosDe(plantilla, camposTabla);
            ColocarPlantilla(plantilla, hijo.Id);
            selCampo = DisenoModelo.ClaveNuevo(padre.Id);
            mensaje = $"«{q.Base}» quedó en todas las opciones de {padre.Nombre} ({hijos.Count} campo(s) \"{q.Base} - …\"; {creados} nuevo(s)). Las opciones que se agreguen después lo tendrán solas. Guarde el formulario para publicarlo.";
            error = false;
        }
        catch (Exception ex) { mensaje = "No se pudo aplicar a todas: " + ex.Message; error = true; }
    }

    // Solo esta opción: ahora sí se valida y se crea.
    private async Task UnoPorUno()
    {
        if (preguntaTodas is not { } q) return;
        preguntaTodas = null;
        if (Tabla(q.PadreId) is not { } padre) return;
        if (await CrearUnoAsync(padre, q.Base, q.Opcion, q.Tipo) is { } ent)
        { mensaje = $"«{ent.Nombre}» aparecerá cuando {padre.Nombre} = {q.Opcion}. Las demás opciones se agregan con ＋ en cada una. Guarde el formulario para publicarlo."; error = false; }
    }

    // Cerrar la pregunta sin elegir: no se crea nada y lo escrito sigue en la casilla.
    private void CancelarPregunta() => preguntaTodas = null;

    // En el diseño, la plantilla toma el lugar de sus campos "Base - …" (o del indicado); ella los pinta en el formulario.
    private void ColocarPlantilla(CampoTabla plantilla, int? lugarDe = null)
    {
        if (trabajo.Campos.Any(c => c.CampoTablaId == plantilla.Id)) return;
        Recordar();
        var hijos = CamposPorOpcion.HijosDe(plantilla, camposTabla).Select(h => h.Id).ToHashSet();
        var lugar = trabajo.Campos.FirstOrDefault(c => c.CampoTablaId == lugarDe) ?? trabajo.Campos.FirstOrDefault(c => c.CampoTablaId is int ct && hijos.Contains(ct));
        trabajo.Campos.RemoveAll(c => c.CampoTablaId is int ct && hijos.Contains(ct));
        if (lugar is not null)
            trabajo.Campos.Add(new CampoD { Clave = DisenoModelo.ClaveNuevo(plantilla.Id), Seccion = lugar.Seccion, X = lugar.X, Y = lugar.Y, W = Math.Max(lugar.W, 4), H = Math.Max(lugar.H, 2), CampoTablaId = plantilla.Id, Obligatorio = lugar.Obligatorio });
        else AgregarDisponible(plantilla, trabajo.Campos.FirstOrDefault(c => c.CampoTablaId == plantilla.CampoPadreId)?.Seccion);
    }

    // Chip "Todas": elige la plantilla (y la pone en el diseño si aún no está).
    private void ElegirPlantilla(CampoTabla plantilla)
    {
        ColocarPlantilla(plantilla);
        ElegirCampo(DisenoModelo.ClaveNuevo(plantilla.Id));
    }

    private IEnumerable<CampoTabla> PlantillasDe(CampoTabla padre) =>
        camposTabla.Where(p => CamposPorOpcion.EsPlantilla(p) && p.CampoPadreId == padre.Id);

    // Cambiar o quitar la condición de un campo ligado (se escribe al guardar el formulario).
    private void CambiarCondicion(CampoTabla t, string? opcion)
    {
        if (string.IsNullOrWhiteSpace(opcion)) { t.CampoPadreId = null; t.OpcionPadre = null; }
        else t.OpcionPadre = opcion;
        cambiosTabla++;
    }

    // Diseño completo y explícito: cada paso con su orden y cada campo del código con su sitio (así el boceto y lo que
    // se guarda coinciden). Lo que el código agregue después entra solo, en el primer hueco.
    private static Diseno Completar(Estructura e, Diseno? d)
    {
        var r = new Diseno();
        var pasos = DisenoModelo.Pasos(e, d);
        for (int i = 0; i < pasos.Count; i++)
        {
            var p = pasos[i];
            var sd = d?.Secciones.FirstOrDefault(s => s.Clave == p.Clave);
            r.Secciones.Add(new SeccionD { Clave = p.Clave, Titulo = sd?.Titulo, Orden = (i + 1) * 10, Nueva = p.Nueva, Modulo = sd?.Modulo });
        }
        foreach (var (clave, (seccion, u)) in DisenoModelo.Disponer(e, d))
        {
            var cd = d?.Campos.FirstOrDefault(c => c.Clave == clave);
            r.Campos.Add(new CampoD
            {
                Clave = clave, Seccion = seccion, X = u.X, Y = u.Y, W = u.W, H = u.H, Etiqueta = cd?.Etiqueta, Oculto = cd?.Oculto ?? false,
                CampoTablaId = cd?.CampoTablaId, Obligatorio = cd?.Obligatorio ?? false
            });
        }
        // Campos nuevos ocultos (Disponer no los devuelve si están ocultos en un paso sin rejilla).
        if (d is not null)
            foreach (var cd in d.Campos.Where(c => c.CampoTablaId is not null && !r.Campos.Any(x => x.Clave == c.Clave)))
                r.Campos.Add(cd);
        return r;
    }

    // ---------- Consultas para pintar ----------

    private List<(string Clave, string Titulo, bool Bloque, bool Nueva)> Pasos() => estructura is null ? new() : DisenoModelo.Pasos(estructura, trabajo);
    private SeccionE? PasoE(string clave) => estructura?.Secciones.FirstOrDefault(s => s.Clave == clave);
    private string TituloOriginal(string clave) => PasoE(clave)?.Titulo ?? trabajo.Secciones.FirstOrDefault(s => s.Clave == clave)?.Titulo ?? "";
    private bool EsRejilla(string paso) => PasoE(paso) is not { Bloque: true } and not { Libre: true };
    private CampoE? Nativo(string clave) => estructura?.Campos.FirstOrDefault(c => c.Clave == clave);
    private CampoD? Diseno(string clave) => trabajo.Campos.FirstOrDefault(c => c.Clave == clave);
    private CampoTabla? Tabla(int? id) => id is null ? null : camposTabla.FirstOrDefault(c => c.Id == id);

    private IEnumerable<CampoD> CamposDe(string paso, bool ocultos) =>
        trabajo.Campos.Where(c => c.Seccion == paso && c.Oculto == ocultos && (c.CampoTablaId is null ? Nativo(c.Clave) is not null : Tabla(c.CampoTablaId) is not null))
            .OrderBy(c => c.Y).ThenBy(c => c.X);

    private string EtiquetaDe(CampoD c) =>
        c.CampoTablaId is int id ? (Tabla(id)?.Nombre ?? "(campo borrado)")
        : string.IsNullOrWhiteSpace(c.Etiqueta) ? (Nativo(c.Clave)?.Etiqueta ?? c.Clave) : c.Etiqueta!;

    private string TipoDe(CampoD c)
    {
        if (c.CampoTablaId is int id && Tabla(id) is { } t)
            return t.Tipo switch { TipoCampoTabla.Archivos => "archivos", TipoCampoTabla.Lista => "lista", TipoCampoTabla.Fecha => "fecha", TipoCampoTabla.Numero => "numero", TipoCampoTabla.SiNo => "lista", _ => c.H > 1 ? "area" : "texto" };
        var n = Nativo(c.Clave);
        if (n?.Catalogo is not null) return "lista";
        return c.H > 1 ? "area" : "texto";
    }

    // Campos de la tabla que no están en el formulario (creados en Campos o importados de Excel): se pueden agregar.
    private IEnumerable<CampoTabla> Disponibles() => actual?.PintaCamposTabla == true ? Enumerable.Empty<CampoTabla>() :
        camposTabla.Where(c => c.Origen != OrigenCampoTabla.Nativo && !c.Calculado && !trabajo.Campos.Any(d => d.CampoTablaId == c.Id));

    private string LayoutJson(string paso) =>
        JsonSerializer.Serialize(CamposDe(paso, false).Select(c => new { id = c.Clave, x = c.X, y = c.Y, w = c.W, h = c.H }));

    // ---------- Boceto: el motor de rejilla avisa cada movimiento ----------

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Formulario pedido en la dirección (editor?form=…, p. ej. al volver de Catálogos): se abre ya interactivo
        // (Elegir usa JS, que no existe durante el pre-render).
        if (firstRender && actual is null && RegistroFormularios.De(FormularioInicial) is { } f) { await Elegir(f); StateHasChanged(); return; }
        if (actual is null || estructura is null || yo is null) return;
        foreach (var p in Pasos().Where(p => EsRejilla(p.Clave)))
            if (refsPaso.TryGetValue(p.Clave, out var el))
                await JS.InvokeVoidAsync("sigDisenador.aplicar", el, p.Clave, LayoutJson(p.Clave), yo);
    }

    [JSInvokable]
    public Task Movido(string paso, string json)
    {
        var lista = JsonSerializer.Deserialize<List<Pos>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        bool cambio = false;
        foreach (var p in lista)
        {
            var c = Diseno(p.Id);
            if (c is null || c.Seccion != paso) continue;
            if (c.X != p.X || c.Y != p.Y || c.W != p.W || c.H != p.H)
            {
                if (!cambio) Recordar();
                c.X = p.X; c.Y = p.Y; c.W = p.W; c.H = p.H; cambio = true;
            }
        }
        if (cambio) StateHasChanged();
        return Task.CompletedTask;
    }

    private sealed record Pos(string Id, int X, int Y, int W, int H);

    // ---------- Deshacer ----------

    private void Recordar()
    {
        deshacer.Push(DisenoModelo.AJson(trabajo));
        while (deshacer.Count > 40) { var l = deshacer.ToList(); l.RemoveAt(l.Count - 1); deshacer.Clear(); foreach (var x in Enumerable.Reverse(l)) deshacer.Push(x); }
    }

    private void Deshacer()
    {
        if (deshacer.Count == 0) return;
        trabajo = DisenoModelo.DeJson<Diseno>(deshacer.Pop()) ?? trabajo;
    }

    // ---------- Selección y propiedades ----------

    private void ElegirCampo(string clave) { selCampo = clave; selPaso = null; verNuevo = false; }
    private void ElegirPaso(string clave) { selPaso = clave; selCampo = null; verNuevo = false; }

    private void CambiarEtiqueta(CampoD c, string? v)
    {
        Recordar();
        v = DisenoModelo.Normal(v);
        if (c.CampoTablaId is int id && Tabla(id) is { } t)
        {
            if (v.Length > 0 && t.CampoPadreId is not null && !string.IsNullOrWhiteSpace(t.OpcionPadre)
                && camposTabla.Any(o => o.Id != t.Id && string.Equals(DisenoModelo.Normal(o.Nombre), v, StringComparison.OrdinalIgnoreCase)))
                v = $"{v} ({t.OpcionPadre})";
            if (v.Length > 0 && v != t.Nombre) { t.Nombre = v; cambiosTabla++; }   // se escribe al guardar
        }
        else c.Etiqueta = v.Length == 0 || v == Nativo(c.Clave)?.Etiqueta ? null : v;
    }

    private void CambiarTamano(CampoD c, int? w, int? h)
    {
        Recordar();
        if (w is int ww) c.W = Math.Clamp(ww, 1, DisenoModelo.Columnas);
        if (h is int hh) c.H = Math.Clamp(hh, 1, 8);
        if (c.X + c.W > DisenoModelo.Columnas) c.X = DisenoModelo.Columnas - c.W;
    }

    private void CambiarOculto(CampoD c, bool oculto)
    {
        if (oculto && c.CampoTablaId is null && Nativo(c.Clave) is { Ocultable: false }) return;
        Recordar();
        c.Oculto = oculto;
        if (!oculto)
        {
            // Vuelve al primer hueco libre de su paso.
            var (x, y) = DisenoModelo.Hueco(CamposDe(c.Seccion, false).Where(o => o != c).Select(o => (o.X, o.Y, o.W, o.H)).ToList(), c.W, c.H);
            c.X = x; c.Y = y;
        }
    }

    // Cualquier campo cambia de paso (el formulario diseñado es una sola rejilla), salvo los de un paso con contenido propio.
    private bool PuedeCambiarPaso(CampoD c) =>
        c.CampoTablaId is not null || (Nativo(c.Clave) is { } n && EsRejilla(n.Seccion));

    private void MoverCampoAPaso(CampoD c, string paso) => MoverCampoAPaso(c, paso, null, null);

    private void MoverCampoAPaso(CampoD c, string paso, int? x0, int? y0)
    {
        if (c.Seccion == paso || !EsRejilla(paso) || !PuedeCambiarPaso(c)) return;
        Recordar();
        var otros = CamposDe(paso, false).Select(o => (o.X, o.Y, o.W, o.H)).ToList();
        int x, y;
        if (x0 is int xx && y0 is int yy && !otros.Any(o => xx < o.X + o.W && o.X < xx + c.W && yy < o.Y + o.H && o.Y < yy + c.H))
        { x = Math.Clamp(xx, 0, DisenoModelo.Columnas - c.W); y = yy; }
        else (x, y) = DisenoModelo.Hueco(otros, c.W, c.H);
        c.Seccion = paso; c.X = x; c.Y = y;
        selCampo = c.Clave; selPaso = null;
    }

    // Arrastre desde el boceto: el campo se soltó sobre la rejilla de otro paso (js/inicio.js alSoltarFuera).
    [JSInvokable]
    public Task MovidoAPaso(string id, string paso, int x, int y)
    {
        var c = Diseno(id);
        if (c is null) return Task.CompletedTask;
        if (!PuedeCambiarPaso(c)) { mensaje = $"«{EtiquetaDe(c)}» está en un paso con contenido propio: no puede salir de él."; error = true; }
        else if (!EsRejilla(paso)) { mensaje = "Ese paso tiene contenido propio: no recibe campos."; error = true; }
        else { MoverCampoAPaso(c, paso, x, y); mensaje = ""; }
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void CambiarTituloPaso(string clave, string? v)
    {
        Recordar();
        var s = trabajo.Secciones.First(x => x.Clave == clave);
        v = DisenoModelo.Normal(v);
        s.Titulo = v.Length == 0 || (!s.Nueva && v == TituloOriginal(clave)) ? (s.Nueva ? "Paso nuevo" : null) : v;
    }

    private void MoverPaso(string clave, int dir)
    {
        var orden = Pasos().Select(p => p.Clave).ToList();
        var i = orden.IndexOf(clave); var j = i + dir;
        if (i < 0 || j < 0 || j >= orden.Count) return;
        Recordar();
        (orden[i], orden[j]) = (orden[j], orden[i]);
        Reordenar(orden);
    }

    private void SoltarPaso(string destino)
    {
        var origen = arrastrandoPaso; arrastrandoPaso = null;
        if (origen is null || origen == destino) return;
        var orden = Pasos().Select(p => p.Clave).ToList();
        Recordar();
        orden.Remove(origen);
        orden.Insert(orden.IndexOf(destino), origen);
        Reordenar(orden);
    }

    private void Reordenar(List<string> orden)
    {
        for (int k = 0; k < orden.Count; k++)
        {
            var s = trabajo.Secciones.FirstOrDefault(x => x.Clave == orden[k]);
            if (s is null) { s = new SeccionD { Clave = orden[k] }; trabajo.Secciones.Add(s); }
            s.Orden = (k + 1) * 10;
        }
    }

    private void AgregarPaso()
    {
        Recordar();
        var clave = "p:" + Guid.NewGuid().ToString("N")[..8];
        trabajo.Secciones.Add(new SeccionD { Clave = clave, Titulo = "Paso nuevo", Orden = (trabajo.Secciones.Count == 0 ? 0 : trabajo.Secciones.Max(s => s.Orden)) + 10, Nueva = true });
        selPaso = clave; selCampo = null;
    }

    // Un paso nuevo se puede quitar; sus campos pasan al primer paso con rejilla.
    private void QuitarPaso(string clave)
    {
        var s = trabajo.Secciones.FirstOrDefault(x => x.Clave == clave && x.Nueva);
        if (s is null) return;
        if (s.Modulo is not null)
        { mensaje = $"«{s.Titulo}» está ligado al módulo {SigWeb.Data.CostosNoCalidad.ModuloCostosNoCalidad.NombreModulo(s.Modulo) ?? s.Modulo}: se renombra, pero no se quita."; error = true; return; }
        Recordar();
        var destino = Pasos().FirstOrDefault(p => p.Clave != clave && EsRejilla(p.Clave)).Clave;
        foreach (var c in trabajo.Campos.Where(c => c.Seccion == clave).ToList())
        {
            if (destino is null) { trabajo.Campos.Remove(c); continue; }
            var (x, y) = DisenoModelo.Hueco(CamposDe(destino, false).Select(o => (o.X, o.Y, o.W, o.H)).ToList(), c.W, c.H);
            c.Seccion = destino; c.X = x; c.Y = y;
        }
        trabajo.Secciones.Remove(s);
        refsPaso.Remove(clave);
        selPaso = null;
    }

    private string PasoDestino() =>
        selPaso is not null && EsRejilla(selPaso) ? selPaso
        : selCampo is not null && Diseno(selCampo) is { } c && EsRejilla(c.Seccion) ? c.Seccion
        : Pasos().FirstOrDefault(p => EsRejilla(p.Clave)).Clave ?? "";

    private void AgregarDisponible(CampoTabla t) => AgregarDisponible(t, null);

    private void AgregarDisponible(CampoTabla t, string? pasoElegido)
    {
        var paso = !string.IsNullOrEmpty(pasoElegido) && EsRejilla(pasoElegido) ? pasoElegido : PasoDestino();
        if (paso.Length == 0) { mensaje = "Agregue primero un paso."; error = true; return; }
        Recordar();
        // Adjuntos: recuadro de 6 columnas × 2 filas (título, archivos y «Adjuntar»).
        var w = t.Tipo is TipoCampoTabla.Texto or TipoCampoTabla.Archivos ? 6 : 4;
        var h = t.Tipo == TipoCampoTabla.Archivos ? 2 : 1;
        var (x, y) = DisenoModelo.Hueco(CamposDe(paso, false).Select(o => (o.X, o.Y, o.W, o.H)).ToList(), w, h);
        var c = new CampoD { Clave = DisenoModelo.ClaveNuevo(t.Id), Seccion = paso, X = x, Y = y, W = w, H = h, CampoTablaId = t.Id };
        trabajo.Campos.Add(c);
        selCampo = c.Clave; selPaso = null;
    }

    // ---------- Campo nuevo (se crea ya como CampoTabla; el diseño lo ubica y se guarda con Guardar) ----------

    private void AbrirNuevo()
    {
        ncPaso = PasoDestino();                 // antes de soltar la selección: el campo va al paso que estaba elegido
        verNuevo = true; selCampo = selPaso = null;
        ncNombre = ""; ncTipo = TipoCampoTabla.Texto; ncMultiple = false; ncOrigen = "existente"; ncCatalogo = ""; ncOpciones = ""; ncCatalogoNuevo = "";
    }

    private async Task CrearCampo()
    {
        if (actual is null) return;
        var nombre = DisenoModelo.Normal(ncNombre);
        if (nombre.Length == 0) { mensaje = "Escriba el nombre del campo."; error = true; return; }
        if (camposTabla.Any(c => string.Equals(DisenoModelo.Normal(c.Nombre), nombre, StringComparison.OrdinalIgnoreCase))
            || estructura?.Campos.Any(c => string.Equals(c.Etiqueta, nombre, StringComparison.OrdinalIgnoreCase)) == true)
        { mensaje = $"Ya hay un campo \"{nombre}\" en este formulario."; error = true; return; }
        string? catalogo = null, opciones = null;
        if (ncTipo == TipoCampoTabla.Lista)
        {
            if (ncOrigen == "existente")
            {
                if (string.IsNullOrWhiteSpace(ncCatalogo)) { mensaje = "Elija el catálogo de las opciones."; error = true; return; }
                catalogo = ncCatalogo;
            }
            else if (ncOrigen == "nuevo")
            {
                var items = ncOpciones.Split('\n').Select(DisenoModelo.Normal).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (items.Count == 0) { mensaje = "Escriba al menos una opción (una por línea)."; error = true; return; }
                catalogo = DisenoModelo.Normal(string.IsNullOrWhiteSpace(ncCatalogoNuevo) ? $"{actual.Nombre} — {nombre}" : ncCatalogoNuevo);
                await CrearCatalogoAsync(catalogo, items);
            }
            else
            {
                opciones = string.Join("\n", ncOpciones.Split('\n').Select(DisenoModelo.Normal).Where(x => x.Length > 0));
                if (opciones.Length == 0) { mensaje = "Escriba al menos una opción (una por línea)."; error = true; return; }
            }
        }
        ocupado = true;
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var motor = await MotorTabla.CargarLecturaAsync(db, actual.Tabla, Enumerable.Empty<ColumnaNativa>(), Enumerable.Empty<(int, string)>());
            var id = await motor.GuardarCampoAsync(db, new CampoTabla { Nombre = nombre, Tipo = ncTipo, CatalogoTipo = catalogo, Opciones = opciones, Multiple = ncTipo == TipoCampoTabla.Lista && ncMultiple });
            await CargarCamposTablaAsync();
            await CargarCatalogosAsync();
            verNuevo = false;
            if (actual.PintaCamposTabla)
                mensaje = $"Campo \"{nombre}\" creado: este formulario muestra todos los campos de su tabla, así que ya aparece en él (y en la lista).";
            else
            {
                if (Tabla(id) is { } t) AgregarDisponible(t, ncPaso);
                mensaje = $"Campo \"{nombre}\" creado: ya está en el boceto. Guarde el formulario para publicarlo.";
            }
            error = false;
        }
        catch (Exception ex) { mensaje = "No se pudo crear el campo: " + ex.Message; error = true; }
        finally { ocupado = false; }
    }

    // Catálogo nuevo con sus opciones: queda en Catálogos como cualquier otro (se gestiona allí).
    private async Task CrearCatalogoAsync(string tipo, List<string> items)
    {
        using var scope = Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Nombre único aun en la Papelera: si está allí, se restaura en vez de crear otro (la base lo rechazaría).
        var existente = await db.DefinicionesCatalogo.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Nombre.ToLower() == tipo.ToLower());
        if (existente is null)
            db.DefinicionesCatalogo.Add(new DefinicionCatalogo { Nombre = tipo, EntidadAsociada = actual?.Nombre ?? "", Modulo = actual?.Ruta ?? "", Activo = true, Orden = 999 });
        else if (existente.Eliminado)
        {
            existente.Eliminado = false; existente.FechaEliminacion = null; existente.Activo = true;
            existente.Modulo ??= actual?.Ruta ?? "";
        }
        var ya = await db.Catalogos.Where(i => i.Tipo == tipo).Select(i => i.Nombre).ToListAsync();
        foreach (var n in items.Where(n => !ya.Contains(n, StringComparer.OrdinalIgnoreCase)))
            db.Catalogos.Add(new ItemCatalogo { Tipo = tipo, Nombre = n, Activo = true });
        await db.SaveChangesAsync();
    }

    // Opciones propias de una lista (sin catálogo): se editan aquí, una por línea.
    private void CambiarOpciones(CampoTabla t, string? v)
    {
        Recordar();
        t.Opciones = string.Join("\n", (v ?? "").Split('\n').Select(DisenoModelo.Normal).Where(x => x.Length > 0));
        cambiosTabla++;
    }

    private void CambiarObligatorio(CampoD c, bool v) { Recordar(); c.Obligatorio = v; }

    // Lista de varios valores (vive en el CampoTabla: también cambia la columna de la tabla). Se escribe al guardar.
    private void CambiarMultiple(CampoTabla t, bool v) { if (t.Multiple == v) return; t.Multiple = v; cambiosTabla++; }

    // "Resumen": lo que se ve fuera de la tabla (bot, hojas de vida) y lo que leen los módulos ligados (Costos de no calidad).
    // En una plantilla "Todas las opciones" se aplica a todos sus campos "Base - opción". Se escribe al guardar.
    private void CambiarResumen(CampoTabla t, bool v)
    {
        var lista = CamposPorOpcion.EsPlantilla(t) ? CamposPorOpcion.HijosDe(t, camposTabla).Append(t) : new[] { t };
        foreach (var c in lista.Where(c => c.EnResumen != v)) { c.EnResumen = v; cambiosTabla++; }
    }

    // Quitar del formulario (el campo sigue en la tabla y se puede volver a agregar) o enviar a la Papelera.
    private void QuitarDelFormulario(CampoD c)
    {
        if (c.CampoTablaId is null) return;
        Recordar();
        trabajo.Campos.Remove(c);
        selCampo = null;
    }

    private string? confirmarBorrar;
    private SigWeb.Data.Impacto.InformeImpacto? impactoBorrar;
    private bool impactoBorrarOk;
    private CampoD? CampoABorrar => confirmarBorrar is null ? null : trabajo.Campos.FirstOrDefault(c => c.Clave == confirmarBorrar);

    // Eliminar un campo: ventana de confirmación con lo que se pierde de vista (valores, adjuntos, campos que dependen de él).
    private async Task PedirBorrarCampo(CampoD c)
    {
        if (actual is null || c.CampoTablaId is not int id) return;
        confirmarBorrar = c.Clave; impactoBorrar = null; impactoBorrarOk = false;
        StateHasChanged();
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = Tabla(id);
            var valores = await db.ValoresCampoTabla.CountAsync(v => v.CampoTablaId == id && v.Valor != null && v.Valor != "");
            var archivos = await db.ArchivosCampoFormulario.CountAsync(a => a.CampoTablaId == id);
            var hijos = camposTabla.Count(h => h.CampoPadreId == id);
            var inf = new SigWeb.Data.Impacto.InformeImpacto { Elemento = $"Campo «{t?.Nombre ?? EtiquetaDe(c)}» de {actual.Nombre}" };
            if (valores > 0) inf.Dependencias.Add(new() { Menu = "Registros", Detalle = "valores diligenciados en este campo", Cantidad = valores, Peso = 2 });
            if (archivos > 0) inf.Dependencias.Add(new() { Menu = "Adjuntos", Detalle = "archivos cargados en este campo", Cantidad = archivos, Peso = 2 });
            if (hijos > 0) inf.Dependencias.Add(new() { Menu = "Campos según la opción", Detalle = "dejarán de aparecer (dependen de este campo)", Cantidad = hijos, Peso = 4 });
            if (!actual.Tabla.StartsWith("form:")) inf.Dependencias.Add(new() { Menu = "Lista del módulo", Detalle = "columna, Excel y Dossier", Cantidad = 1, Peso = 1 });
            var puntos = inf.Dependencias.Sum(d => d.Puntos);
            inf.Puntaje = puntos;
            inf.Nivel = hijos > 0 || valores >= 40 ? SigWeb.Data.Impacto.NivelImpacto.Alto
                : valores > 0 || archivos > 0 ? SigWeb.Data.Impacto.NivelImpacto.Medio : SigWeb.Data.Impacto.NivelImpacto.Bajo;
            inf.Recomendacion = valores + archivos == 0
                ? "El campo no tiene datos. Va a la Papelera y se puede restaurar desde allí."
                : "El campo y sus valores van a la Papelera; al restaurarlo vuelven con él.";
            if (confirmarBorrar == c.Clave) impactoBorrar = inf;
        }
        catch (Exception ex) { mensaje = "No se pudo verificar el impacto: " + ex.Message; error = true; confirmarBorrar = null; }
    }

    private void CancelarBorrarCampo() { confirmarBorrar = null; impactoBorrar = null; }

    private async Task BorrarCampo(CampoD c)
    {
        if (actual is null || c.CampoTablaId is not int id) return;
        confirmarBorrar = null; impactoBorrar = null;
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var e = await db.CamposTabla.FirstAsync(x => x.Id == id);
            e.Eliminado = true; e.FechaEliminacion = DateTime.Now;
            await db.SaveChangesAsync();
            trabajo.Campos.Remove(c);
            await CargarCamposTablaAsync();
            // Se publica de una vez: el campo ya no existe y el formulario no debe esperar a "Guardar".
            await GuardarInternoAsync($"Campo \"{e.Nombre}\" enviado a la Papelera");
            mensaje = $"\"{e.Nombre}\" está en la Papelera (se puede restaurar desde allí con sus valores)."; error = false;
            selCampo = null;
        }
        catch (Exception ex) { mensaje = "No se pudo borrar: " + ex.Message; error = true; }
    }

    // Ir a Catálogos deja esta pantalla: con cambios sin guardar se pide guardarlos o descartarlos antes.
    private Task GestionarCatalogo(string tipo)
    {
        if (HayCambios) { mensaje = "Guarde o descarte los cambios del formulario antes de ir a Catálogos."; error = true; return Task.CompletedTask; }
        return OnGestionarCatalogo.InvokeAsync(tipo);
    }

    private Task PedirCatalogoNuevo()
    {
        if (actual is null) return Task.CompletedTask;
        var nombre = DisenoModelo.Normal(ncNombre);
        return OnCrearCatalogo.InvokeAsync((actual.Ruta, nombre.Length == 0 ? "" : nombre));
    }

    // Vuelta desde Catálogos: con el catálogo creado ya elegido en el campo nuevo (null = se canceló).
    public async Task CatalogoCreadoAsync(string? tipo)
    {
        await CargarCatalogosAsync();
        if (tipo is not null)
        {
            verNuevo = true;
            ncTipo = TipoCampoTabla.Lista;
            ncOrigen = "existente";
            ncCatalogo = tipo;
            mensaje = $"Catálogo \"{tipo}\" creado y elegido: termine el campo con Crear."; error = false;
        }
        StateHasChanged();
    }

    // Catálogos para la lista "Catálogo" del campo nuevo: primero los del módulo del formulario, luego General y el resto
    // agrupados por módulo (en el orden del menú).
    private List<(string Grupo, List<string> Tipos)> CatalogosAgrupados()
    {
        string Mod(string t) => moduloCatalogo.TryGetValue(t, out var m) ? m : "";
        var r = new List<(string, List<string>)>();
        if (actual is not null)
        {
            var propios = tiposCatalogo.Where(t => Mod(t) == actual.Ruta).ToList();
            if (propios.Count > 0) r.Add(($"{ModuloDe(actual)} (este módulo)", propios));
        }
        var generales = tiposCatalogo.Where(t => Mod(t) == "").ToList();
        if (generales.Count > 0) r.Add(("General", generales));
        foreach (var m in MapaNavegacion.Modulos().Where(m => m.Href != actual?.Ruta)
                     .OrderBy(m => Array.IndexOf(MapaNavegacion.OrdenGrupos, m.Grupo)))
        {
            var l = tiposCatalogo.Where(t => Mod(t) == m.Href).ToList();
            if (l.Count > 0) r.Add((m.Nombre, l));
        }
        return r;
    }

    // ---------- Guardar / descartar / restablecer / historial ----------

    private void PedirGuardar() { pedirMotivo = true; motivo = ""; }

    private async Task Guardar()
    {
        if (actual is null) return;
        pedirMotivo = false;
        ocupado = true;
        try
        {
            await PropagarNombresAsync();
            var v = await GuardarInternoAsync(motivo);
            mensaje = $"{actual.Nombre}: versión {v} guardada. El formulario ya se ve así en su módulo."; error = false;
        }
        catch (Exception ex) { mensaje = "No se pudo guardar: " + ex.Message; error = true; }
        finally { ocupado = false; }
    }

    private async Task<int> GuardarInternoAsync(string? motivoGuardado)
    {
        var v = await Servicio.GuardarAsync(actual!.Clave, trabajo, motivoGuardado);
        jsonGuardado = DisenoModelo.AJson(trabajo);
        cambiosTabla = 0;
        versiones[actual.Clave] = v;
        deshacer.Clear();
        return v;
    }

    // Lo que otros heredan: nombres de campos nuevos (CampoTabla) y de campos del código que también son columna de la
    // lista del módulo (misma etiqueta) → la columna toma el nombre nuevo; las fórmulas que la citan se actualizan.
    private async Task PropagarNombresAsync()
    {
        using var scope = Scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deTabla = await db.CamposTabla.Where(c => c.Tabla == actual!.Tabla && !c.Eliminado).ToListAsync();
        var cambios = new List<(CampoTabla Campo, string Nuevo)>();
        foreach (var t in camposTabla.Where(c => c.Origen != OrigenCampoTabla.Nativo))
        {
            var e = deTabla.FirstOrDefault(x => x.Id == t.Id);
            if (e is not null && e.Nombre != t.Nombre) cambios.Add((e, t.Nombre));
            if (e is not null && e.Opciones != t.Opciones && e.CatalogoTipo is null && e.Tipo == TipoCampoTabla.Lista) e.Opciones = t.Opciones;
            if (e is not null && e.Tipo == TipoCampoTabla.Lista && e.Multiple != t.Multiple) e.Multiple = t.Multiple;
            if (e is not null && e.EnResumen != t.EnResumen) { e.EnResumen = t.EnResumen; ResumenCampos.Invalidar(e.Tabla); }
            if (e is not null && (e.CampoPadreId != t.CampoPadreId || e.OpcionPadre != t.OpcionPadre)) { e.CampoPadreId = t.CampoPadreId; e.OpcionPadre = t.OpcionPadre; }
        }
        foreach (var c in trabajo.Campos.Where(c => c.CampoTablaId is null && !string.IsNullOrWhiteSpace(c.Etiqueta)))
        {
            var original = Nativo(c.Clave)?.Etiqueta;
            if (original is null) continue;
            var col = deTabla.FirstOrDefault(x => (x.Origen == OrigenCampoTabla.Nativo || actual!.PintaCamposTabla) && string.Equals(DisenoModelo.Normal(x.Nombre), original, StringComparison.OrdinalIgnoreCase));
            if (col is not null && col.Nombre != c.Etiqueta) cambios.Add((col, c.Etiqueta!));
        }
        foreach (var (campo, nuevo) in cambios)
        {
            var viejo = campo.Nombre;
            foreach (var otro in deTabla.Where(x => x.Formula != null)) otro.Formula = FormulaExcel.Renombrar(otro.Formula!, viejo, nuevo);
            var ids = deTabla.Select(x => x.Id).ToList();
            foreach (var v in await db.ValoresCampoTabla.Where(x => ids.Contains(x.CampoTablaId) && x.Formula != null).ToListAsync())
                v.Formula = FormulaExcel.Renombrar(v.Formula!, viejo, nuevo);
            campo.Nombre = nuevo;
        }
        await db.SaveChangesAsync();
    }

    private async Task Descartar() { await CargarFormularioAsync(); mensaje = "Cambios descartados."; error = false; }

    private bool confirmarRestablecer;
    private async Task Restablecer()
    {
        if (actual is null) return;
        if (!confirmarRestablecer) { confirmarRestablecer = true; return; }
        confirmarRestablecer = false;
        // Los pasos ligados a un módulo (Costos de no calidad…) no se pierden: quedan solos sobre el formulario original.
        var ligados = new Diseno();
        ConservarLigados(ligados, trabajo);
        var conLigados = ligados.Secciones.Count > 0;
        await Servicio.GuardarAsync(actual.Clave, conLigados ? ligados : new Diseno { Original = true }, "Restablecido al formulario original");
        if (!conLigados) versiones.Remove(actual.Clave);
        await CargarFormularioAsync();
        mensaje = "El formulario volvió a su diseño original. Los campos nuevos siguen en la tabla y se pueden volver a agregar."
            + (conLigados ? $" Se conservó {string.Join(", ", ligados.Secciones.Select(x => $"«{x.Titulo}»"))} (ligado a un módulo)." : ""); error = false;
    }

    private async Task AbrirHistorial()
    {
        if (actual is null) return;
        verHistorial = !verHistorial;
        if (verHistorial) historial = await Servicio.HistorialAsync(actual.Clave);
    }

    // Trae una versión anterior al boceto (se publica con Guardar, como versión nueva).
    private async Task TraerVersion(DisenoFormulario v)
    {
        if (estructura is null) return;
        Recordar();
        var d = DisenoModelo.DeJson<Diseno>(v.Json);
        await JS.InvokeVoidAsync("sigDisenador.limpiar");
        refsPaso.Clear();
        var antes = trabajo;
        trabajo = d is null || d.Original ? Completar(estructura, null) : Completar(estructura, d);
        ConservarLigados(trabajo, antes);   // una versión anterior al vínculo no se lleva el paso del módulo
        verHistorial = false;
        mensaje = $"Versión {v.Version} cargada en el boceto: Guardar para publicarla."; error = false;
    }

    private string TituloResumen(CampoD sc, CampoTabla t)
    {
        var mod = ModuloDePaso(sc.Seccion) is { } m ? SigWeb.Data.CostosNoCalidad.ModuloCostosNoCalidad.NombreModulo(m) : null;
        var txt = mod is null ? "Lo que se ve fuera de la tabla (bot, hojas de vida)" : $"Lo que se ve fuera de la tabla y lo que toma {mod}";
        return CamposPorOpcion.EsPlantilla(t) ? txt + " · se aplica a todas las opciones" : txt;
    }

    // ---------- Pasos ligados a un módulo ----------
    private string? ModuloDePaso(string clave) => trabajo.Secciones.FirstOrDefault(x => x.Clave == clave)?.Modulo;

    private IEnumerable<(string Formulario, string Clave, string Nombre)> ModulosLigables() =>
        actual is null ? Enumerable.Empty<(string, string, string)>()
        : SigWeb.Data.CostosNoCalidad.ModuloCostosNoCalidad.Ligables.Where(l => l.Formulario == actual.Clave);

    // Un módulo lee un solo paso: ligarlo aquí lo suelta del otro.
    private void LigarModulo(string clave, string? modulo)
    {
        var s = trabajo.Secciones.FirstOrDefault(x => x.Clave == clave && x.Nueva);
        if (s is null) return;
        Recordar();
        modulo = string.IsNullOrWhiteSpace(modulo) ? null : modulo;
        if (modulo is not null) foreach (var o in trabajo.Secciones.Where(x => x.Modulo == modulo)) o.Modulo = null;
        s.Modulo = modulo;
        mensaje = modulo is null ? $"«{s.Titulo}» ya no está ligado a un módulo."
            : $"«{s.Titulo}» ligado a {SigWeb.Data.CostosNoCalidad.ModuloCostosNoCalidad.NombreModulo(modulo)}: sus campos marcados «Resumen» alimentan el módulo. Guarde el formulario para publicarlo.";
        error = false;
    }

    // Copia a "destino" los pasos ligados de "fuente" (y sus campos) que le falten.
    private static void ConservarLigados(Diseno destino, Diseno fuente)
    {
        foreach (var s in fuente.Secciones.Where(x => x.Modulo is not null && x.Nueva))
        {
            if (destino.Secciones.FirstOrDefault(x => x.Clave == s.Clave) is { } ya) { ya.Modulo = s.Modulo; ya.Nueva = true; continue; }
            destino.Secciones.Add(new SeccionD { Clave = s.Clave, Titulo = s.Titulo, Orden = s.Orden, Nueva = true, Modulo = s.Modulo });
            foreach (var c in fuente.Campos.Where(c => c.Seccion == s.Clave && c.CampoTablaId is not null && !destino.Campos.Any(x => x.Clave == c.Clave)))
                destino.Campos.Add(new CampoD { Clave = c.Clave, Seccion = c.Seccion, X = c.X, Y = c.Y, W = c.W, H = c.H, Etiqueta = c.Etiqueta, Oculto = c.Oculto, CampoTablaId = c.CampoTablaId, Obligatorio = c.Obligatorio });
        }
    }

    public void Dispose()
    {
        _ = JS.InvokeVoidAsync("sigDisenador.limpiar").AsTask().ContinueWith(_ => { });
        yo?.Dispose();
    }
}
