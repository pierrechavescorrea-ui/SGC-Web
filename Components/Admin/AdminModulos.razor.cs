using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.CreadorModulos;
using SigWeb.Data.Iconos;
using SigWeb.Data.Seguridad;

namespace SigWeb.Components.Admin;

// Administración › Creador de módulos. Lista de módulos creados y diseñador tipo Windows Forms: barra de herramientas de solo
// íconos (1–3 columnas), lienzo de 12 columnas × filas de 40 px con una pestaña por paso, Esquema y Propiedades compactos; todo
// panel se fija o se suelta con su pin (js/disenadorModulos.js guarda posiciones). Deshacer/rehacer con instantáneas del JSON.
// Todo se guarda como borrador en la base central; Publicar lo aplica en todos los proyectos.
public partial class AdminModulos
{
    [Parameter] public int? Id { get; set; }
    [Inject] private RegistroModulosCreados Registro { get; set; } = default!;
    [Inject] private ServicioVinculos Vinculos { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class Editor
    {
        public int Id { get; set; }
        public string Clave { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public string Icono { get; set; } = "inspeccion";
        public string Grupo { get; set; } = "Operación";
        public AmbitoModulo Ambito { get; set; } = AmbitoModulo.Proyecto;
        public EstadoModuloCreado Estado { get; set; }
        public int Version { get; set; }
        public bool TieneBorrador { get; set; }
        public DefinicionModulo Def { get; set; } = new();
        public DefinicionModulo? Publicada { get; set; }
    }

    private List<ModuloCreado> lista = new();
    private Editor? ed;
    private string firmaGuardada = "";
    private bool cargando = true, creando, verIconos;
    private string? nombreNuevo, selCampo, borrarPaso, confirmar, notaPublicar;
    private string vista = "diseno";
    private int? idCargado;
    private int paso;
    private List<string> errores = new();
    private List<string> catalogos = new();
    private List<VersionModuloCreado> versiones = new();
    private readonly Dictionary<string, List<ColumnaVinculo>> columnasFuente = new();
    private ServicioRegistrosModulo.Edicion previa = new();

    // Deshacer / rehacer: instantáneas de la definición (y datos generales) antes de cada cambio.
    private readonly Stack<string> deshacer = new(), rehacer = new();
    private string? instantanea;

    // Paneles: suelto / plegado por panel y columnas de la barra de herramientas (por navegador, en localStorage).
    private sealed class EstadoPanel { public bool Suelto { get; set; } public bool Plegado { get; set; } public int? Cols { get; set; } }
    private readonly Dictionary<string, EstadoPanel> paneles = new()
    {
        ["herramientas"] = new(), ["esquema"] = new(), ["propiedades"] = new(),
    };
    private int colsHerr = 2;

    private ElementReference raizDmz;
    private DotNetObjectReference<AdminModulos>? refNet;
    private bool jsListo;

    private static string Ruta(int id) => $"admin/modulos/{id}";

    protected override async Task OnParametersSetAsync()
    {
        if (!Autorizado) return;
        if (Id == idCargado && (Id is null ? lista.Count > 0 || !cargando : ed is not null)) return;
        idCargado = Id;
        Mensaje = null; errores.Clear(); confirmar = null; vista = "diseno";
        if (Id is null) { ed = null; jsListo = false; await CargarListaAsync(); return; }
        await AbrirAsync(Id.Value);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ed is null || vista != "diseno") { jsListo = false; return; }
        try
        {
            if (!jsListo)
            {
                refNet ??= DotNetObjectReference.Create(this);
                if (!panelesLeidos)
                {
                    panelesLeidos = true;
                    LeerPaneles(await JS.InvokeAsync<string>("sigDisMod.leerPaneles"));
                    await JS.InvokeVoidAsync("sigDisMod.iniciar", raizDmz, refNet);
                    jsListo = true;
                    StateHasChanged();
                    return;
                }
                await JS.InvokeVoidAsync("sigDisMod.iniciar", raizDmz, refNet);
                jsListo = true;
            }
            else await JS.InvokeVoidAsync("sigDisMod.aplicar");
        }
        catch (JSDisconnectedException) { }
        catch (InvalidOperationException) { /* prerender */ }
    }

    private bool panelesLeidos;

    private void LeerPaneles(string? json)
    {
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json ?? "{}") ?? new();
            foreach (var (id, v) in d)
            {
                if (!paneles.TryGetValue(id, out var p)) continue;
                if (v.TryGetProperty("suelto", out var s)) p.Suelto = s.GetBoolean();
                if (v.TryGetProperty("plegado", out var pl)) p.Plegado = pl.GetBoolean();
                if (id == "herramientas" && v.TryGetProperty("cols", out var c) && c.TryGetInt32(out var n)) colsHerr = Math.Clamp(n, 1, 3);
            }
        }
        catch { /* estado dañado: valores por defecto */ }
    }

    public async ValueTask DisposeAsync()
    {
        try { if (jsListo) await JS.InvokeVoidAsync("sigDisMod.detener"); } catch { }
        refNet?.Dispose();
    }

    private async Task CargarListaAsync()
    {
        cargando = true;
        try { lista = await ConDb((db, _) => AdminModulosCreados.ListarAsync(db)); }
        catch (Exception ex) { Log.LogError(ex, "Creador de módulos"); Error("No se pudo leer la lista. ¿Falta aplicar la migración de la base central (ModulosCreados)?"); }
        finally { cargando = false; }
    }

    private async Task AbrirAsync(int id)
    {
        var m = await ConDb((db, _) => db.ModulosCreados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id));
        if (m is null) { ed = null; Error("Ese módulo ya no existe."); await CargarListaAsync(); return; }
        ed = new Editor
        {
            Id = m.Id, Clave = m.Clave, Nombre = m.Nombre, Descripcion = m.Descripcion, Icono = m.Icono, Grupo = m.Grupo, Ambito = m.Ambito,
            Estado = m.Estado, Version = m.Version, TieneBorrador = m.BorradorJson is not null,
            Def = DefinicionModulo.Leer(m.BorradorJson ?? m.DefinicionJson),
            Publicada = m.DefinicionJson is null ? null : DefinicionModulo.Leer(m.DefinicionJson),
        };
        ed.Def.Acomodar();
        firmaGuardada = Firma();
        instantanea = Instantanea();
        deshacer.Clear(); rehacer.Clear();
        paso = 0; selCampo = null; borrarPaso = null;
        previa = new ServicioRegistrosModulo.Edicion();
        await CargarCatalogosAsync();
        foreach (var f in ed.Def.Campos.Where(c => !string.IsNullOrWhiteSpace(c.Fuente)).Select(c => c.Fuente!).Distinct())
            _ = CargarColumnasAsync(f);
    }

    private async Task CargarCatalogosAsync()
    {
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            catalogos = await db.DefinicionesCatalogo.AsNoTracking().Select(d => d.Nombre).Distinct().OrderBy(n => n).ToListAsync();
        }
        catch { catalogos = new(); }
    }

    private async Task CargarColumnasAsync(string fuente)
    {
        if (columnasFuente.ContainsKey(fuente)) return;
        try
        {
            var t = await Vinculos.TablaAsync(fuente);
            columnasFuente[fuente] = t.Columnas.ToList();
        }
        catch { columnasFuente[fuente] = new(); }
        await InvokeAsync(StateHasChanged);
    }

    // ---------- Estado del editor ----------

    private string Firma() => ed is null ? "" : $"{ed.Nombre}|{ed.Descripcion}|{ed.Icono}|{ed.Grupo}|{ed.Ambito}|{ed.Def.Json()}";
    private bool Sucio => ed is not null && Firma() != firmaGuardada;
    private void Marcar() { Mensaje = null; }

    private sealed record Foto(string Nombre, string? Descripcion, string Icono, string Grupo, AmbitoModulo Ambito, string Def);
    private string Instantanea() => JsonSerializer.Serialize(new Foto(ed!.Nombre, ed.Descripcion, ed.Icono, ed.Grupo, ed.Ambito, ed.Def.Json()));

    private void Restaurar(string json)
    {
        var f = JsonSerializer.Deserialize<Foto>(json);
        if (f is null || ed is null) return;
        ed.Nombre = f.Nombre; ed.Descripcion = f.Descripcion; ed.Icono = f.Icono; ed.Grupo = f.Grupo; ed.Ambito = f.Ambito;
        ed.Def = DefinicionModulo.Leer(f.Def);
        instantanea = json;
        if (selCampo is not null && ed.Def.Campo(selCampo) is null) selCampo = null;
        paso = Math.Clamp(paso, 0, Math.Max(0, ed.Def.Pasos.Count - 1));
    }

    // Toda modificación pasa por aquí: guarda la instantánea anterior para Deshacer.
    private void Fijar(Action cambio)
    {
        if (ed is null) return;
        cambio();
        ed.Def.Acomodar();
        var nueva = Instantanea();
        if (nueva == instantanea) return;
        if (instantanea is not null) deshacer.Push(instantanea);
        if (deshacer.Count > 200) { var l = deshacer.Take(200).Reverse().ToList(); deshacer.Clear(); foreach (var x in l) deshacer.Push(x); }
        rehacer.Clear();
        instantanea = nueva;
        Mensaje = null;
    }

    private void Deshacer()
    {
        if (deshacer.Count == 0 || instantanea is null) return;
        rehacer.Push(instantanea);
        Restaurar(deshacer.Pop());
    }

    private void Rehacer()
    {
        if (rehacer.Count == 0 || instantanea is null) return;
        deshacer.Push(instantanea);
        Restaurar(rehacer.Pop());
    }

    private string SubtituloEditor
    {
        get
        {
            if (ed is null) return "";
            var estado = ed.Estado switch
            {
                EstadoModuloCreado.Archivado => "Archivado",
                _ when ed.Version == 0 => "Borrador, nunca publicado",
                _ when ed.TieneBorrador || Sucio => $"Publicado v{ed.Version} · hay cambios sin publicar",
                _ => $"Publicado v{ed.Version}",
            };
            return $"{ed.Grupo} · /mod/{ed.Clave} · {estado}";
        }
    }

    private PasoModulo? PasoActual => ed is null || ed.Def.Pasos.Count == 0 ? null : ed.Def.Pasos[Math.Clamp(paso, 0, ed.Def.Pasos.Count - 1)];
    private int FilasLienzo => Math.Max(14, (PasoActual?.Campos.Select(c => (c.Y ?? 0) + c.Alto).DefaultIfEmpty(0).Max() ?? 0) + 6);
    private CampoModulo? CampoSel => ed is null || selCampo is null ? null : ed.Def.Campo(selCampo);
    private PasoModulo? PasoDe(CampoModulo c) => ed?.Def.Pasos.FirstOrDefault(p => p.Campos.Contains(c));
    private CampoModulo? Publicado(CampoModulo c) => ed?.Publicada?.Campo(c.Clave);
    private string PropTitulo => CampoSel is { } c ? "Propiedades · " + CampoModulo.NombreTipo(c.Tipo) : "Propiedades";

    private static IEnumerable<IReadOnlyList<CampoModulo.Herramienta>> GruposHerramientas =>
        CampoModulo.Herramientas.GroupBy(h => h.Grupo).Select(g => (IReadOnlyList<CampoModulo.Herramienta>)g.ToList());

    // ---------- Paneles ----------

    private bool Suelto(string id) => paneles.TryGetValue(id, out var p) && p.Suelto;
    private bool Plegado(string id) => paneles.TryGetValue(id, out var p) && p.Plegado;
    private bool HayPanelesSueltos => paneles.Values.Any(p => p.Suelto);

    private async Task AlternarPanel(string id)
    {
        var p = paneles[id];
        p.Suelto = !p.Suelto;
        await GuardarPanelAsync(id);
    }

    private async Task AlternarPlegado(string id)
    {
        var p = paneles[id];
        p.Plegado = !p.Plegado;
        await GuardarPanelAsync(id);
    }

    private async Task CambiarColumnas()
    {
        colsHerr = colsHerr % 3 + 1;
        paneles["herramientas"].Cols = colsHerr;
        await GuardarPanelAsync("herramientas");
    }

    private async Task FijarTodo()
    {
        foreach (var (id, p) in paneles) { p.Suelto = false; await GuardarPanelAsync(id); }
    }

    private async Task GuardarPanelAsync(string id)
    {
        var p = paneles[id];
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["suelto"] = p.Suelto, ["plegado"] = p.Plegado, ["cols"] = id == "herramientas" ? colsHerr : null,
        });
        try { await JS.InvokeVoidAsync("sigDisMod.guardarPanel", id, json); } catch { }
    }

    // ---------- Llamadas desde js/disenadorModulos.js ----------

    [JSInvokable]
    public Task Soltar(string tipo, int x, int y) => InvokeAsync(() =>
    {
        if (ed is null || PasoActual is not { } p || !Enum.TryParse<TipoCampoModulo>(tipo, out var t)) return;
        AgregarCampo(p, t, x, y);
        StateHasChanged();
    });

    [JSInvokable]
    public Task Mover(string clave, int x, int y, int ancho, int alto) => InvokeAsync(() =>
    {
        if (ed?.Def.Campo(clave) is not { } c) return;
        selCampo = clave;
        Fijar(() =>
        {
            c.Ancho = Math.Clamp(ancho, 1, 12); c.X = Math.Clamp(x, 0, 12 - c.Ancho); c.Y = Math.Max(0, y); c.Alto = Math.Clamp(alto, 1, 40);
        });
        StateHasChanged();
    });

    [JSInvokable]
    public Task Seleccionar(string? clave) => InvokeAsync(() =>
    {
        selCampo = clave is not null && ed?.Def.Campo(clave) is not null ? clave : null;
        borrarPaso = null;
        StateHasChanged();
    });

    [JSInvokable]
    public Task Tecla(string accion) => InvokeAsync(async () =>
    {
        switch (accion)
        {
            case "guardar": await GuardarAsync(); break;
            case "deshacer": Deshacer(); break;
            case "rehacer": Rehacer(); break;
            case "quitar": if (CampoSel is { } q) QuitarCampo(q); break;
            case "duplicar": if (CampoSel is { } d) DuplicarCampo(d); break;
            default:
                if (CampoSel is { } c && (accion.StartsWith("mover:") || accion.StartsWith("crecer:")))
                {
                    var partes = accion[(accion.IndexOf(':') + 1)..].Split(',');
                    int dx = int.Parse(partes[0]), dy = int.Parse(partes[1]);
                    if (accion.StartsWith("mover:")) Fijar(() => { c.X = Math.Clamp((c.X ?? 0) + dx, 0, 12 - c.Ancho); c.Y = Math.Max(0, (c.Y ?? 0) + dy); });
                    else Fijar(() => { c.Ancho = Math.Clamp(c.Ancho + dx, 1, 12 - (c.X ?? 0)); c.Alto = Math.Clamp(c.Alto + dy, 1, 40); });
                }
                break;
        }
        StateHasChanged();
    });

    // ---------- Lista ----------

    private async Task CrearAsync()
    {
        if (string.IsNullOrWhiteSpace(nombreNuevo)) return;
        ModuloCreado? m = null;
        if (await Cambiar(async () => m = await ConDb((db, _) => AdminModulosCreados.CrearAsync(db, nombreNuevo!, Actual.Nombre))))
        {
            Registrar("Creador de módulos: módulo nuevo", m!.Nombre);
            creando = false; nombreNuevo = null;
            Nav.NavigateTo(Ruta(m.Id));
        }
    }

    private async Task DuplicarAsync()
    {
        if (ed is null) return;
        await GuardarAsync();
        ModuloCreado? m = null;
        if (await Cambiar(async () => m = await ConDb(async (db, _) =>
            {
                var origen = await db.ModulosCreados.AsNoTracking().FirstAsync(x => x.Id == ed.Id);
                var nombre = $"{ed.Nombre} (copia)";
                for (int i = 2; await db.ModulosCreados.AnyAsync(x => x.Nombre == nombre); i++) nombre = $"{ed.Nombre} (copia {i})";
                return await AdminModulosCreados.CrearAsync(db, nombre, Actual.Nombre, origen);
            })))
            Nav.NavigateTo(Ruta(m!.Id));
    }

    // ---------- Guardar / publicar ----------

    private async Task<bool> GuardarAsync()
    {
        if (ed is null) return false;
        var e = ed;
        e.Def.Acomodar();
        var ok = await Cambiar(() => ConDb((db, _) => AdminModulosCreados.GuardarBorradorAsync(db, e.Id, e.Nombre, e.Descripcion, e.Icono, e.Grupo, e.Ambito, e.Def)), "Borrador guardado. Lo publicado no cambia hasta Publicar.");
        if (ok)
        {
            firmaGuardada = Firma();
            e.TieneBorrador = e.Publicada is null || e.Publicada.Json() != e.Def.Json();
            errores = e.Def.Validar();
            // Nombre, ícono y grupo se ven de inmediato en el menú si el módulo ya está publicado.
            if (e.Version > 0) await Registro.RecargarAsync();
        }
        return ok;
    }

    private void PedirPublicar()
    {
        if (ed is null) return;
        errores = ed.Def.Validar();
        if (ed.Publicada is not null) errores.AddRange(AdminModulosCreados.CambiosPeligrosos(ed.Publicada, ed.Def));
        if (errores.Count > 0) { Error("Revise lo que falta antes de publicar."); return; }
        notaPublicar = null;
        confirmar = "publicar";
    }

    private async Task ConfirmarAsync()
    {
        if (ed is null || confirmar is null) return;
        var accion = confirmar;
        var e = ed;
        switch (accion)
        {
            case "publicar":
                if (!await GuardarAsync()) { confirmar = null; return; }
                int version = 0;
                if (await Cambiar(async () => version = await ConDb((db, _) => AdminModulosCreados.PublicarAsync(db, e.Id, notaPublicar, Actual.Nombre))))
                {
                    await Registro.RecargarAsync();
                    Registrar("Creador de módulos: publicado", $"{e.Nombre} v{version}");
                    var pasoActual = paso;
                    await AbrirAsync(e.Id);
                    paso = Math.Min(pasoActual, Math.Max(0, ed!.Def.Pasos.Count - 1));
                    Exito($"Publicado v{version}. Ya está en el menú (Administración › Roles: dé permisos a los roles que lo usarán).");
                }
                break;
            case "archivar":
            case "reactivar":
                if (await Cambiar(() => ConDb((db, _) => AdminModulosCreados.ArchivarAsync(db, e.Id, accion == "archivar"))))
                {
                    await Registro.RecargarAsync();
                    Registrar("Creador de módulos: " + accion, e.Nombre);
                    await AbrirAsync(e.Id);
                    Exito(accion == "archivar" ? "Archivado: salió del menú. Los registros se conservan." : "Reactivado: volvió al menú.");
                }
                break;
            case "descartar":
                if (await Cambiar(() => ConDb((db, _) => AdminModulosCreados.DescartarBorradorAsync(db, e.Id)), "Se descartaron los cambios sin publicar."))
                    await AbrirAsync(e.Id);
                break;
            case "eliminar":
                if (await Cambiar(() => ConDb((db, _) => EliminacionModulos.EliminarAsync(db, e.Id))))
                {
                    await Registro.RecargarAsync();
                    Registrar("Creador de módulos: eliminado", e.Nombre);
                    confirmar = null;
                    Nav.NavigateTo("admin/modulos");
                    return;
                }
                break;
        }
        confirmar = null;
    }

    private async Task VerVersionesAsync()
    {
        vista = "versiones";
        if (ed is null) return;
        try { versiones = await ConDb((db, _) => AdminModulosCreados.VersionesAsync(db, ed.Id)); }
        catch (Exception ex) { Error("No se pudieron leer las versiones: " + ex.Message); }
    }

    private async Task TraerVersionAsync(int version)
    {
        if (ed is null) return;
        var id = ed.Id;
        if (await Cambiar(() => ConDb((db, _) => AdminModulosCreados.TraerVersionAsync(db, id, version)), $"La versión v{version} quedó en el borrador: revísela y publíquela."))
        {
            await AbrirAsync(id);
            vista = "diseno";
        }
    }

    // ---------- Eliminados ----------

    [Inject] private IWebHostEnvironment Env { get; set; } = default!;
    private ModuloCreado? purga;
    private List<EliminacionModulos.ConteoProyecto>? conteos;
    private string? textoPurga;

    private async Task RestaurarAsync(ModuloCreado m)
    {
        if (await Cambiar(() => ConDb((db, _) => EliminacionModulos.RestaurarAsync(db, m.Id)), $"«{m.Nombre}» se restauró."))
        {
            await Registro.RecargarAsync();
            Registrar("Creador de módulos: restaurado", m.Nombre);
            await CargarListaAsync();
        }
    }

    private async Task PedirPurgaAsync(ModuloCreado m)
    {
        purga = m; conteos = null; textoPurga = null;
        StateHasChanged();
        try { conteos = await EliminacionModulos.ContarAsync(Proyectos, m.Clave); }
        catch (Exception ex) { Error("No se pudieron contar los registros: " + ex.Message); purga = null; }
    }

    private async Task PurgarAsync()
    {
        if (purga is null || textoPurga?.Trim() != purga.Nombre) return;
        var m = purga;
        string? resumen = null;
        if (await Cambiar(async () => resumen = await ConDb((db, _) => EliminacionModulos.PurgarAsync(db, Proyectos, Env.WebRootPath, m.Id))))
        {
            Registrar("Creador de módulos: eliminado definitivamente", m.Nombre);
            Exito(resumen!);
            purga = null;
            await CargarListaAsync();
        }
    }

    // ---------- Pasos ----------

    private void ElegirPaso(int i) { paso = i; selCampo = null; borrarPaso = null; }

    private void SeleccionarDesdeArbol(int pasoIdx, string clave) { paso = pasoIdx; selCampo = clave; borrarPaso = null; }

    private void AgregarPaso()
    {
        if (ed is null) return;
        Fijar(() => ed.Def.Pasos.Add(new PasoModulo { Titulo = $"Paso {ed.Def.Pasos.Count + 1}" }));
        paso = ed.Def.Pasos.Count - 1; selCampo = null;
    }

    private void MoverPaso(int d)
    {
        if (ed is null) return;
        var i = paso;
        if (i + d < 0 || i + d >= ed.Def.Pasos.Count) return;
        Fijar(() => Mover(ed.Def.Pasos, i, d));
        paso = i + d;
    }

    private void QuitarPaso(PasoModulo p)
    {
        if (ed is null) return;
        if (ed.Def.Pasos.Count == 1) { Error("El módulo necesita al menos un paso."); return; }
        if (p.Campos.Count > 0 && borrarPaso != p.Clave) { borrarPaso = p.Clave; return; }
        Fijar(() =>
        {
            ed.Def.Pasos.Remove(p);
            if (ed.Def.CampoTitulo is not null && ed.Def.Campo(ed.Def.CampoTitulo) is null) ed.Def.CampoTitulo = null;
        });
        borrarPaso = null; selCampo = null;
        paso = Math.Clamp(paso, 0, ed.Def.Pasos.Count - 1);
    }

    private static void Mover<T>(List<T> l, int i, int d)
    {
        var j = i + d;
        if (j < 0 || j >= l.Count) return;
        (l[i], l[j]) = (l[j], l[i]);
    }

    // ---------- Controles ----------

    private void AgregarCampo(PasoModulo p, TipoCampoModulo tipo, int? x = null, int? y = null)
    {
        var h = CampoModulo.HerramientaDe(tipo);
        var ancho = CampoModulo.AnchoInicial(tipo);
        var c = new CampoModulo
        {
            Tipo = tipo,
            Etiqueta = tipo is TipoCampoModulo.Separador ? "" : NombreLibre(h?.Nombre ?? tipo.ToString()),
            Ancho = ancho, Alto = CampoModulo.AltoInicial(tipo),
            X = x is null ? null : Math.Clamp(x.Value, 0, 12 - ancho), Y = y,
            Modo = tipo is TipoCampoModulo.Kpi ? ModoCampo.Calculado : CampoModulo.HerramientaDe(tipo)?.Dato == true && tipo != TipoCampoModulo.Vinculo ? ModoCampo.SoloVista : ModoCampo.Editable,
            EnLista = tipo is not (TipoCampoModulo.Parrafo or TipoCampoModulo.Archivos or TipoCampoModulo.Foto or TipoCampoModulo.Firma or TipoCampoModulo.Chequeo),
        };
        if (tipo == TipoCampoModulo.Chequeo) c.Opciones = new() { "Ítem 1", "Ítem 2", "Ítem 3" };
        // Las herramientas de datos se filtran por el primer selector de registro del módulo (lo más común).
        if (c.EsHerramientaApp && ed!.Def.Campos.FirstOrDefault(v => v.Tipo == TipoCampoModulo.Vinculo) is { } vin) { c.FiltroCampo = vin.Clave; c.Fuente = vin.Fuente; }
        Fijar(() => p.Campos.Add(c));
        selCampo = c.Clave;
        if (c.Fuente is not null) _ = CargarColumnasAsync(c.Fuente);
    }

    private void AgregarAlFinal(TipoCampoModulo tipo)
    {
        if (PasoActual is { } p) AgregarCampo(p, tipo);
    }

    private string NombreLibre(string baseNombre)
    {
        var usados = ed!.Def.Campos.Select(c => DefinicionModulo.Normalizar(c.Etiqueta)).ToHashSet();
        var n = baseNombre;
        for (int i = 2; usados.Contains(DefinicionModulo.Normalizar(n)); i++) n = $"{baseNombre} {i}";
        return n;
    }

    private void DuplicarCampo(CampoModulo c)
    {
        if (ed is null || PasoDe(c) is not { } p) return;
        var copia = DefinicionModulo.Leer(new DefinicionModulo { Pasos = { new PasoModulo { Campos = { c } } } }.Json()).Pasos[0].Campos[0];
        copia.Clave = DefinicionModulo.ClaveNueva("c");
        copia.Etiqueta = c.EsDiseno ? c.Etiqueta : NombreLibre(c.Etiqueta);
        copia.X = null; copia.Y = null;
        Fijar(() => p.Campos.Insert(p.Campos.IndexOf(c) + 1, copia));
        selCampo = copia.Clave;
    }

    private void QuitarCampo(CampoModulo c)
    {
        if (ed is null || PasoDe(c) is not { } p) return;
        Fijar(() =>
        {
            p.Campos.Remove(c);
            if (ed.Def.CampoTitulo == c.Clave) ed.Def.CampoTitulo = null;
            foreach (var o in ed.Def.Campos.Where(o => o.FiltroCampo == c.Clave)) o.FiltroCampo = null;
            foreach (var o in ed.Def.Campos.Where(o => o.VisibleSiCampo == c.Clave)) { o.VisibleSiCampo = null; o.VisibleSiValor = null; }
        });
        selCampo = null;
    }

    private void MoverAPaso(CampoModulo c, string? clavePaso)
    {
        if (ed is null || PasoDe(c) is not { } origen) return;
        var destino = ed.Def.Pasos.FirstOrDefault(p => p.Clave == clavePaso);
        if (destino is null || destino == origen) return;
        Fijar(() => { origen.Campos.Remove(c); c.X = null; c.Y = null; destino.Campos.Add(c); });
        paso = ed.Def.Pasos.IndexOf(destino);
    }

    private async Task CambiarFuenteAsync(CampoModulo c, string? fuente)
    {
        Fijar(() =>
        {
            c.Fuente = string.IsNullOrWhiteSpace(fuente) ? null : fuente;
            c.ColumnasFuente.Clear(); c.ColumnaEnlace = null; c.ColumnaValor = null; c.ColumnaAgrupar = null; c.ColumnaFecha = null;
            c.ColumnaLat = null; c.ColumnaLon = null;
            if (c.Tipo == TipoCampoModulo.Vinculo && c.Fuente is not null && c.Etiqueta.StartsWith("Selector de registro", StringComparison.Ordinal))
                c.Etiqueta = NombreLibre(Vinculos.Fuente(c.Fuente)?.Nombre ?? c.Etiqueta);
        });
        if (c.Fuente is not null) await CargarColumnasAsync(c.Fuente);
    }

    private void AlternarColumna(CampoModulo c, string col, bool marcar) => Fijar(() =>
    {
        if (marcar) { if (!c.ColumnasFuente.Contains(col)) c.ColumnasFuente.Add(col); }
        else c.ColumnasFuente.Remove(col);
    });

    private void AgregarEstado()
    {
        if (ed is null) return;
        var colores = new[] { "azul", "amarillo", "verde", "gris", "rojo", "violeta" };
        Fijar(() => ed.Def.Estados.Add(new EstadoModulo { Nombre = $"Estado {ed.Def.Estados.Count + 1}", Color = colores[ed.Def.Estados.Count % colores.Length] }));
    }

    private static string OpcionesTexto(CampoModulo c) => string.Join("\n", c.Opciones);
    private void FijarOpciones(CampoModulo c, string? texto) =>
        Fijar(() => c.Opciones = (texto ?? "").Split('\n').Select(o => o.Trim()).Where(o => o.Length > 0).Distinct().ToList());

    private Task TeclaNuevo(KeyboardEventArgs e) => e.Key == "Enter" ? CrearAsync() : Task.CompletedTask;
    private void PedirEliminar() => confirmar = "eliminar";
    private void PedirDescartar() => confirmar = "descartar";
    private void ElegirIcono(string? v) { if (ed is null) return; Fijar(() => ed.Icono = v ?? ed.Icono); verIconos = false; }

    // ---------- Lectura de controles del panel de propiedades ----------

    private static string Txt(ChangeEventArgs e) => (e.Value?.ToString() ?? "").Trim();
    private static string? TxtONull(ChangeEventArgs e) => string.IsNullOrWhiteSpace(e.Value?.ToString()) ? null : e.Value!.ToString()!.Trim();
    private static int Entero(ChangeEventArgs e, int min, int max) => int.TryParse(e.Value?.ToString(), out var n) ? Math.Clamp(n, min, Math.Max(min, max)) : min;
    private static int? EnteroONull(ChangeEventArgs e, int min, int max) => int.TryParse(e.Value?.ToString(), out var n) ? Math.Clamp(n, min, max) : null;

    // ---------- Textos ----------

    private static string TipHerramienta(CampoModulo.Herramienta h) => $"{h.Nombre}|{h.Descripcion}{(h.Dato ? " · se enlaza a datos de la app" : "")} · arrastre al lienzo o doble clic";

    private static readonly (string, string)[] TiposGrafico = { ("barras", "Barras"), ("lineas", "Líneas"), ("torta", "Torta") };

    private static string ModoTexto(ModoCampo m) => m switch { ModoCampo.SoloVista => "Solo vista", ModoCampo.Calculado => "Calculado", _ => "Editable" };

    private static string CalculoTexto(CalculoCampo k) => k switch
    {
        CalculoCampo.Suma => "Suma", CalculoCampo.Promedio => "Promedio", CalculoCampo.Minimo => "Mínimo", CalculoCampo.Maximo => "Máximo",
        CalculoCampo.Porcentaje => "Porcentaje", _ => "Conteo",
    };

    private string? NombreFuente(string? fuente) => fuente is null ? null : Vinculos.Fuente(fuente)?.Nombre ?? fuente;

    private static bool EsCampoSimple(CampoModulo c) =>
        c.Tipo is TipoCampoModulo.Texto or TipoCampoModulo.Numero or TipoCampoModulo.Fecha or TipoCampoModulo.Lista or TipoCampoModulo.SiNo or TipoCampoModulo.Vinculo;

    private string NotaDatos(CampoModulo c)
    {
        var fuente = NombreFuente(c.Fuente) ?? "(elija una fuente)";
        var filtro = c.FiltroCampo is null ? "todos sus registros" : $"los registros relacionados con lo elegido en «{ed!.Def.Campo(c.FiltroCampo)?.Etiqueta}»";
        return $"Muestra datos vivos de {fuente}: {filtro}. Nadie los cambia desde aquí.";
    }

    private static string GaleriaIconosModuloNombre(string clave) => GaleriaIconosModulo.De(clave)?.Nombre ?? clave;

    private static string ColorMuestra(string clave)
    {
        var p = SigWeb.Data.Tablas.MotorTabla.Paleta.FirstOrDefault(x => x.Clave == clave);
        return p.Clave is null ? "" : $"border-left:8px solid {p.Fondo}";
    }

    private static string AmbitoTexto(AmbitoModulo a) => a switch
    {
        AmbitoModulo.Empresa => "Empresa", AmbitoModulo.Ambos => "Proyectos y Empresa", _ => "Proyectos",
    };

    private static string EstadoTexto(ModuloCreado m) => m.Estado switch
    {
        EstadoModuloCreado.Archivado => "Archivado",
        _ when m.Version == 0 => "Borrador",
        _ when m.BorradorJson is not null => $"v{m.Version} · cambios sin publicar",
        _ => $"Publicado v{m.Version}",
    };

    private static string EstadoClase(ModuloCreado m) => m.Estado switch
    {
        EstadoModuloCreado.Archivado => "arch",
        _ when m.Version == 0 => "borr",
        _ when m.BorradorJson is not null => "camb",
        _ => "pub",
    };
}
