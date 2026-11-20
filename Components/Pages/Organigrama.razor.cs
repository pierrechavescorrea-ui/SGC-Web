using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Components.Layout;
using SigWeb.Data;
using SigWeb.Data.Impacto;
using SigWeb.Data.Navegacion;
using SigWeb.Data.Organigrama;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Lógica de la pantalla de Organigrama (Organigrama.razor tiene el marcado y los fragmentos del diagrama).
public partial class Organigrama
{
    // ---------- Estado ----------
    private List<VersionOrganigrama> versiones = new();
    private VersionOrganigrama? ver, vigente, borrador;
    private List<NodoOrg> nodos = new(), original = new();
    private Dictionary<Guid, NodoOrg> porClave = new();
    private Dictionary<Guid, List<NodoOrg>> hijos = new();
    private List<NodoOrg> raices = new();
    private List<(NodoOrg Nodo, string Numero, int Nivel)> ordenados = new();
    private Dictionary<Guid, int> totales = new();
    private Dictionary<int, int> cargosPorVersion = new();
    private List<string> areas = new(), personas = new();
    private HashSet<Guid> plegados = new(), coinciden = new();
    private Guid? sel, arrastrado, destino;
    private string vista = "diagrama", buscar = "", mensaje = "";
    private string? resaltar, area;
    private bool editando, guardando, error, pantallaCompleta, verImportar, irAlSeleccionado;
    private int pendientes, niveles;
    private ElementReference pagina, marco;
    private DotNetObjectReference<Organigrama>? refNet;

    [SupplyParameterFromQuery(Name = "v")] public int? VersionQ { get; set; }
    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }
    [SupplyParameterFromQuery(Name = "cargo")] public Guid? CargoQ { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (VistaQ is "lista" or "cambios") vista = VistaQ;
        CargarVersiones(VersionQ);
        if (CargoQ is Guid c && porClave.ContainsKey(c)) { sel = c; irAlSeleccionado = true; await CargarHistorialAsync(); }
    }

    private bool listoJs;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (vista == "diagrama")
            {
                refNet ??= DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("sigOrganigrama.preparar", marco, refNet, !listoJs);
                listoJs = true;
                if (irAlSeleccionado && sel is Guid s) { irAlSeleccionado = false; await JS.InvokeVoidAsync("sigOrganigrama.irA", marco, s.ToString()); }
            }
            else listoJs = false;
            if (enfocarNombre) { enfocarNombre = false; await JS.InvokeVoidAsync("sigOrganigrama.enfocar", ".org-ficha .org-foco"); }
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        try { if (pantallaCompleta) await JS.InvokeVoidAsync("sigOrganigrama.salirPantallaCompleta"); } catch { }
        refNet?.Dispose();
    }

    // ---------- Carga ----------
    private void CargarVersiones(int? elegir = null)
    {
        Db.ChangeTracker.Clear();
        versiones = ServicioOrganigrama.Versiones(Db);
        vigente = versiones.FirstOrDefault(v => v.Estado == EstadoOrganigrama.Vigente);
        borrador = versiones.FirstOrDefault(v => v.Estado == EstadoOrganigrama.Borrador);
        cargosPorVersion = Db.CargosOrganigrama.AsNoTracking().GroupBy(c => c.VersionOrganigramaId).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        ver = (elegir is int id ? versiones.FirstOrDefault(v => v.Id == id) : null) ?? vigente ?? borrador ?? versiones.FirstOrDefault();
        CargarNodos();
        personas = Db.CargosOrganigrama.AsNoTracking().Where(c => c.Ocupante != null).Select(c => c.Ocupante!).ToList()
            .SelectMany(ServicioOrganigrama.Personas).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
        // Comparación por defecto: la versión mostrada contra su base (o la anterior).
        if (ver is not null)
        {
            compB = ver.Id;
            compA = ver.VersionBaseId is int b && versiones.Any(v => v.Id == b) ? b
                : versiones.Where(v => v.Numero < ver.Numero).OrderByDescending(v => v.Numero).Select(v => v.Id).FirstOrDefault(ver.Id);
            Comparar();
        }
    }

    private void CargarNodos()
    {
        nodos = ver is null ? new() : ServicioOrganigrama.Nodos(Db, ver.Id);
        original = nodos.Select(n => n.Copia()).ToList();
        if (sel is Guid s && nodos.All(n => n.Clave != s)) { sel = null; historial.Clear(); }
        Recalcular();
    }

    // Todo lo que pinta la pantalla se precalcula aquí (nunca consultas desde el render).
    private void Recalcular()
    {
        porClave = nodos.ToDictionary(n => n.Clave);
        hijos = nodos.Where(n => n.Jefe is Guid j && porClave.ContainsKey(j)).GroupBy(n => n.Jefe!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Orden).ToList());
        raices = nodos.Where(n => n.Jefe is null || !porClave.ContainsKey(n.Jefe.Value)).OrderBy(n => n.Orden).ToList();
        ordenados = ServicioOrganigrama.EnOrden(nodos);
        niveles = ordenados.Count == 0 ? 0 : ordenados.Max(o => o.Nivel);
        totales.Clear();
        foreach (var r in raices) ContarTotal(r);
        // Áreas en el orden en que aparecen de arriba hacia abajo (así la de la dirección toma el acento).
        areas = ordenados.Select(o => o.Nodo.Area).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!).Distinct().ToList();
        if (area is not null && !areas.Contains(area)) area = null;
        CalcularCoincidencias();
        pendientes = editando ? ContarPendientes() : 0;
    }

    private int ContarTotal(NodoOrg n)
    {
        var t = 0;
        if (hijos.TryGetValue(n.Clave, out var hs)) foreach (var h in hs) t += 1 + ContarTotal(h);
        totales[n.Clave] = t;
        return t;
    }

    private int ContarPendientes()
    {
        // Cargos tocados (nuevos, quitados o con algún cambio), no cambios sueltos.
        var claves = ServicioOrganigrama.Comparar(original, nodos).Select(c => c.Clave).ToHashSet();
        var o = original.ToDictionary(n => n.Clave);
        foreach (var n in nodos)
            if (o.TryGetValue(n.Clave, out var x) && (x.Orden != n.Orden || x.Notas != n.Notas || x.Supuesto != n.Supuesto)) claves.Add(n.Clave);
        return claves.Count;
    }

    // ---------- Versiones ----------
    private void SeleccionarVersion(int id)
    {
        if (editando) return;
        pub = null; elimVersion = null;
        CargarVersiones(id);
        _ = Recordar();
    }

    private string? SubtituloVersion() => ver is null ? null
        : $"v{ver.Numero} · {ver.Estado}" + (ver.VigenteDesde is DateTime d ? $" desde {d:dd/MM/yyyy}" : "") + (string.IsNullOrWhiteSpace(ver.Alcance) ? "" : $" · {ver.Alcance}");

    private static string TituloVersion(VersionOrganigrama v) =>
        $"v{v.Numero} · {v.Estado} · {RangoVigencia(v)}" + (string.IsNullOrWhiteSpace(v.MotivoCambio) ? "" : $" · {v.MotivoCambio}");

    // Para el selector: fecha (vigencia o creación), autor (aprobó, revisó o elaboró) y motivo.
    private IReadOnlyList<SelectorVersiones.Item> ItemsVersiones => versiones
        .Select(v => new SelectorVersiones.Item(v.Id, v.Numero, v.Estado, RangoVigencia(v), v.Aprobo ?? v.Reviso ?? v.Elaboro, v.MotivoCambio))
        .ToList();

    private static string RangoVigencia(VersionOrganigrama v) =>
        v.Estado == EstadoOrganigrama.Borrador ? $"creado {v.FechaCreacion:dd/MM/yyyy}"
        : $"{(v.VigenteDesde is DateTime d ? d.ToString("dd/MM/yyyy") : "sin fecha")} → {(v.VigenteHasta is DateTime h ? h.ToString("dd/MM/yyyy") : v.Estado == EstadoOrganigrama.Vigente ? "hoy" : "—")}";

    private async Task CambiarVista(string v)
    {
        vista = v;
        if (v == "diagrama") irAlSeleccionado = sel is not null;
        await Recordar();
    }

    private async Task Recordar()
    {
        try
        {
            await JS.RecordarVistaAsync("/organigrama", new Dictionary<string, object?>
            {
                ["v"] = ver is not null && ver.Id != vigente?.Id ? ver.Id : null,
                ["vista"] = vista == "diagrama" ? null : vista,
                ["cargo"] = sel?.ToString()
            });
        }
        catch { }
    }

    private async Task PantallaCompleta()
    {
        try { pantallaCompleta = await JS.InvokeAsync<bool>("sigOrganigrama.pantallaCompleta", pagina); } catch { }
    }

    [JSInvokable] public void SalioPantallaCompleta() { pantallaCompleta = false; StateHasChanged(); }

    // ---------- Edición ----------
    private async Task Editar()
    {
        mensaje = ""; pub = null; elimVersion = null; verImportar = false;
        if (ver is null)
        {
            guardando = true;
            try
            {
                var nuevo = new VersionOrganigrama
                {
                    Numero = (await Db.VersionesOrganigrama.IgnoreQueryFilters().MaxAsync(v => (int?)v.Numero) ?? 0) + 1,
                    Titulo = "Organigrama", Estado = EstadoOrganigrama.Borrador
                };
                Db.VersionesOrganigrama.Add(nuevo);
                await Db.SaveChangesAsync();
                CargarVersiones(nuevo.Id);
            }
            catch (Exception ex) { Db.ChangeTracker.Clear(); mensaje = "No se pudo crear el borrador: " + ex.Message; error = true; return; }
            finally { guardando = false; }
        }
        if (ver is null) return;
        if (ver.Estado != EstadoOrganigrama.Borrador)
        {
            if (borrador is not null)
            {
                CargarVersiones(borrador.Id);
                mensaje = $"Editando el borrador v{borrador.Numero}."; error = false;
            }
            else
            {
                guardando = true;
                try
                {
                    var nuevo = await ServicioOrganigrama.CrearBorradorAsync(Db, ver.Id);
                    var desde = ver.Numero;
                    CargarVersiones(nuevo.Id);
                    mensaje = $"Borrador v{nuevo.Numero} creado a partir de la v{desde}."; error = false;
                }
                catch (Exception ex) { mensaje = "No se pudo crear el borrador: " + ex.Message; error = true; return; }
                finally { guardando = false; }
            }
        }
        editando = true;
        Recalcular();
        await Recordar();
    }

    private void CancelarEdicion()
    {
        editando = false; elimNodo = null; FinArrastre(); autoNodo = null;
        CargarNodos();
    }

    private async Task Guardar()
    {
        if (ver is null) return;
        var errores = ServicioOrganigrama.Validar(nodos);
        if (errores.Count > 0) { mensaje = string.Join(" ", errores); error = true; return; }
        guardando = true;
        try
        {
            NormalizarOrden();
            await ServicioOrganigrama.GuardarAsync(Db, ver.Id, nodos);
            mensaje = $"v{ver.Numero} guardada ({pendientes} cambio(s))."; error = false;
            editando = false; elimNodo = null;
            CargarVersiones(ver.Id);
        }
        catch (Exception ex) { mensaje = "No se pudo guardar: " + ex.Message; error = true; }
        finally { guardando = false; }
    }

    private void NormalizarOrden()
    {
        var i = 0;
        foreach (var (n, _, _) in ServicioOrganigrama.EnOrden(nodos)) n.Orden = i++;
    }

    private void Cambiar(NodoOrg n, Action<NodoOrg> cambio)
    {
        if (!editando) return;
        cambio(n);
        if (n.Jefe is null) n.Staff = false;
        Recalcular();
    }

    private void CambiarJefe(NodoOrg n, string? valor)
    {
        Guid? j = Guid.TryParse(valor, out var g) ? g : null;
        if (j is Guid jj && !PuedeSerJefe(n, porClave[jj])) return;
        Cambiar(n, x => { x.Jefe = j; x.Orden = SiguienteOrden(j); });
    }

    // Un cargo no puede quedar bajo sí mismo ni bajo alguien que depende de él.
    private bool PuedeSerJefe(NodoOrg n, NodoOrg jefe)
    {
        if (jefe.Clave == n.Clave) return false;
        var j = jefe.Jefe;
        var vistos = new HashSet<Guid>();
        while (j is Guid g && vistos.Add(g))
        {
            if (g == n.Clave) return false;
            j = porClave.TryGetValue(g, out var p) ? p.Jefe : null;
        }
        return true;
    }

    private int SiguienteOrden(Guid? jefe) =>
        (jefe is Guid j && hijos.TryGetValue(j, out var hs) ? hs : raices).Select(h => h.Orden).DefaultIfEmpty(-1).Max() + 1;

    private void AgregarCargo(Guid? jefe, bool staff)
    {
        if (!editando) return;
        var padre = jefe is Guid j && porClave.TryGetValue(j, out var p) ? p : null;
        var n = new NodoOrg { Nombre = "", Jefe = padre?.Clave, Area = padre?.Area, Staff = staff && padre is not null, Orden = SiguienteOrden(padre?.Clave) };
        nodos.Add(n);
        if (padre is not null) plegados.Remove(padre.Clave);
        sel = n.Clave; historial.Clear(); irAlSeleccionado = true; enfocarNombre = true;
        autoNodo = n.Clave; autoCampos = padre is null ? new() : new() { "jefe", "tipo", "area" }; autoSeq++;
        Recalcular();
    }

    // Mover entre hermanos (misma clase: línea o apoyo).
    private void Mover(NodoOrg n, int paso)
    {
        var hermanos = (n.Jefe is Guid j && hijos.TryGetValue(j, out var hs) ? hs : raices).Where(h => h.Staff == n.Staff).OrderBy(h => h.Orden).ToList();
        var i = hermanos.IndexOf(n);
        var k = i + paso;
        if (i < 0 || k < 0 || k >= hermanos.Count) return;
        (hermanos[i].Orden, hermanos[k].Orden) = (hermanos[k].Orden, hermanos[i].Orden);
        if (hermanos[i].Orden == hermanos[k].Orden) hermanos[k].Orden += paso;
        Recalcular();
    }

    // ---------- Arrastrar y soltar ----------
    // Se arrastra un cargo existente (cambiar de jefe o de posición) o una pieza de la paleta (cargo nuevo de línea o de apoyo).
    // Se suelta sobre una tarjeta (queda bajo ese jefe) o sobre una línea de mando (ranura: entre hermanos, en esa posición).
    // Al soltar se llenan solos los datos de la dependencia (jefe, área, tipo, posición) y la ficha queda lista para el resto.
    private string? nuevoTipo;                       // "linea" | "apoyo" mientras se arrastra una pieza de la paleta
    private (Guid Ref, bool Despues)? ranura;        // ranura activa: antes/después del cargo Ref (mismo jefe)
    private Guid? autoNodo;                          // cargo recién soltado: la ficha resalta lo que se llenó solo
    private HashSet<string> autoCampos = new();
    private int autoSeq;

    private bool Arrastrando => arrastrado is not null || nuevoTipo is not null;

    private void Arrastrar(NodoOrg n) { if (editando) { arrastrado = n.Clave; nuevoTipo = null; } }
    private void ArrastrarNuevo(string tipo) { if (editando) { nuevoTipo = tipo; arrastrado = null; } }

    private void Entrar(NodoOrg n) { if (Arrastrando && arrastrado != n.Clave) { destino = n.Clave; ranura = null; } }
    private void EntrarRanura(NodoOrg refNodo, bool despues) { if (Arrastrando) { ranura = (refNodo.Clave, despues); destino = null; } }

    private bool RanuraActiva(NodoOrg n, bool despues) => ranura is { } r && r.Ref == n.Clave && r.Despues == despues;

    private void Soltar(NodoOrg jefe)
    {
        if (nuevoTipo is { } t) Colocar(null, jefe, t == "apoyo", null);
        else if (arrastrado is Guid a && porClave.TryGetValue(a, out var n)) Colocar(n, jefe, n.Staff, null);
        FinArrastre();
    }

    private void SoltarRanura(NodoOrg refNodo, bool despues)
    {
        var jefe = refNodo.Jefe is Guid j && porClave.TryGetValue(j, out var pj) ? pj : null;
        if (jefe is not null)
        {
            if (nuevoTipo is { } t) Colocar(null, jefe, t == "apoyo", t == "apoyo" ? null : (refNodo, despues));
            else if (arrastrado is Guid a && porClave.TryGetValue(a, out var n)) Colocar(n, jefe, false, (refNodo, despues));
        }
        FinArrastre();
    }

    private void FinArrastre() { arrastrado = destino = null; nuevoTipo = null; ranura = null; }

    // n = null: cargo nuevo. pos = null: al final de los de su tipo.
    private void Colocar(NodoOrg? n, NodoOrg jefe, bool staff, (NodoOrg Ref, bool Despues)? pos)
    {
        if (!editando) return;
        var campos = new HashSet<string> { "jefe", "tipo" };
        if (n is null)
        {
            n = new NodoOrg { Nombre = "", Jefe = jefe.Clave, Area = jefe.Area, Staff = staff };
            nodos.Add(n);
            if (jefe.Area is not null) campos.Add("area");
            enfocarNombre = true;
        }
        else
        {
            if (!PuedeSerJefe(n, jefe)) { mensaje = $"«{n.Nombre}» no puede quedar bajo sí mismo ni bajo uno de sus subordinados."; error = true; return; }
            if (n.Jefe == jefe.Clave && n.Staff == staff && pos is null) return;
            // El área se hereda si no tenía o si era la del jefe anterior (la había heredado).
            var areaAnterior = n.Jefe is Guid ja && porClave.TryGetValue(ja, out var pa) ? pa.Area : null;
            if (jefe.Area is not null && (n.Area is null || n.Area == areaAnterior) && n.Area != jefe.Area) { n.Area = jefe.Area; campos.Add("area"); }
            n.Jefe = jefe.Clave;
            n.Staff = staff;
        }
        // Posición: se reordenan los hermanos del mismo tipo.
        var hermanos = nodos.Where(h => h.Jefe == jefe.Clave && h.Staff == staff && h.Clave != n.Clave).OrderBy(h => h.Orden).ToList();
        var i = hermanos.Count;
        if (pos is { } ps && hermanos.IndexOf(ps.Ref) is var k && k >= 0) i = k + (ps.Despues ? 1 : 0);
        hermanos.Insert(i, n);
        for (var x = 0; x < hermanos.Count; x++) hermanos[x].Orden = x;
        campos.Add("posicion");

        plegados.Remove(jefe.Clave);
        sel = n.Clave; historial.Clear(); irAlSeleccionado = true;
        autoNodo = n.Clave; autoCampos = campos; autoSeq++;
        Recalcular();
    }

    private string Auto(NodoOrg n, string campo) => autoNodo == n.Clave && autoCampos.Contains(campo) ? "org-auto" : "";

    // ---------- Quitar un cargo del borrador ----------
    private sealed class QuitarNodo
    {
        public Guid Clave;
        public bool Reasignar = true;
        public InformeImpacto Informe = new();
        public bool Entendido;
    }

    private QuitarNodo? elimNodo;

    private void PedirQuitar(NodoOrg n)
    {
        var desc = Descendientes(n.Clave).ToList();
        var jefe = n.Jefe is Guid j && porClave.TryGetValue(j, out var p) ? p.Nombre : null;
        var inf = new InformeImpacto { Elemento = $"Cargo «{n.Nombre}»" };
        var directos = hijos.TryGetValue(n.Clave, out var hs) ? hs.Count : 0;
        if (directos > 0)
            inf.Dependencias.Add(new DependenciaImpacto { Menu = "Organigrama", Detalle = jefe is null ? "subordinados directos que quedan como raíz (o se quitan con él)" : $"subordinados directos que pasan a «{jefe}» (o se quitan con él)", Cantidad = directos, Peso = 2 });
        if (desc.Count > directos)
            inf.Dependencias.Add(new DependenciaImpacto { Menu = "Organigrama", Detalle = "cargos más abajo en su rama", Cantidad = desc.Count - directos, Peso = 1 });
        var personasRama = ServicioOrganigrama.Personas(n.Ocupante).Count;
        if (personasRama > 0)
            inf.Dependencias.Add(new DependenciaImpacto { Menu = "Organigrama", Detalle = "persona(s) que dejan de figurar en el cargo", Cantidad = personasRama, Peso = 1 });
        inf.Puntaje = inf.Dependencias.Sum(d => d.Puntos);
        inf.Nivel = inf.Puntaje switch { < 6 => NivelImpacto.Bajo, < 25 => NivelImpacto.Medio, _ => NivelImpacto.Alto };
        inf.Recomendacion = "Al guardar, lo quitado va a la Papelera; la versión vigente no cambia.";
        elimNodo = new QuitarNodo { Clave = n.Clave, Informe = inf };
    }

    private void ConfirmarQuitar()
    {
        if (elimNodo is null || !porClave.TryGetValue(elimNodo.Clave, out var n)) { elimNodo = null; return; }
        var desc = Descendientes(n.Clave).ToHashSet();
        if (elimNodo.Reasignar)
        {
            var orden = SiguienteOrden(n.Jefe);
            foreach (var h in hijos.TryGetValue(n.Clave, out var hs) ? hs : new())
            {
                h.Jefe = n.Jefe; h.Orden = orden++;
                if (h.Jefe is null) h.Staff = false;
            }
            nodos.Remove(n);
        }
        else nodos.RemoveAll(x => x.Clave == n.Clave || desc.Contains(x.Clave));
        sel = n.Jefe; elimNodo = null; historial.Clear();
        Recalcular();
    }

    private IEnumerable<Guid> Descendientes(Guid c)
    {
        if (!hijos.TryGetValue(c, out var hs)) yield break;
        foreach (var h in hs)
        {
            yield return h.Clave;
            foreach (var d in Descendientes(h.Clave)) yield return d;
        }
    }

    // ---------- Selección, filtros y resalte ----------
    private NodoOrg? Sel() => sel is Guid s && porClave.TryGetValue(s, out var n) ? n : null;

    private async Task Seleccionar(Guid c)
    {
        if (sel == c && vista == "diagrama" && !editando) { sel = null; historial.Clear(); await Recordar(); return; }
        sel = c; elimNodo = null;
        await CargarHistorialAsync();
        await Recordar();
    }

    private async Task SeleccionarYVer(Guid c)
    {
        sel = c; elimNodo = null;
        // Que se vea: se despliegan sus jefes.
        var j = porClave.TryGetValue(c, out var n) ? n.Jefe : null;
        while (j is Guid g && porClave.TryGetValue(g, out var p)) { plegados.Remove(g); j = p.Jefe; }
        irAlSeleccionado = true;
        await CargarHistorialAsync();
        await Recordar();
    }

    private void CerrarFicha() { sel = null; elimNodo = null; historial.Clear(); _ = Recordar(); }

    private void Resaltar(string que) { resaltar = resaltar == que ? null : que; area = null; CalcularCoincidencias(); }

    private void FiltrarArea(string a) { area = area == a ? null : a; resaltar = null; CalcularCoincidencias(); }

    private void LimpiarResalte() { resaltar = null; area = null; buscar = ""; CalcularCoincidencias(); }

    private void Buscar()
    {
        CalcularCoincidencias();
        if (buscar.Trim().Length >= 2 && coinciden.Count > 0)
        {
            var primero = ordenados.First(o => coinciden.Contains(o.Nodo.Clave)).Nodo.Clave;
            var j = porClave[primero].Jefe;
            while (j is Guid g && porClave.TryGetValue(g, out var p)) { plegados.Remove(g); j = p.Jefe; }
            if (vista == "diagrama") _ = IrA(primero);
        }
    }

    private async Task IrA(Guid c)
    {
        await Task.Yield();
        try { await JS.InvokeVoidAsync("sigOrganigrama.irA", marco, c.ToString()); } catch { }
    }

    private bool HayFiltro => resaltar is not null || area is not null || buscar.Trim().Length >= 2;

    private void CalcularCoincidencias()
    {
        coinciden.Clear();
        if (!HayFiltro) return;
        var q = Normal(buscar);
        foreach (var n in nodos)
        {
            var ok = resaltar switch
            {
                "sin" => string.IsNullOrWhiteSpace(n.Ocupante),
                "sup" => n.Supuesto is not null,
                _ => true
            };
            if (area is not null) ok &= n.Area == area;
            if (q.Length >= 2) ok &= Normal(n.Nombre).Contains(q) || Normal(n.Ocupante ?? "").Contains(q) || Normal(n.Area ?? "").Contains(q);
            if (ok) coinciden.Add(n.Clave);
        }
    }

    private bool Tenue(NodoOrg n) => HayFiltro && !coinciden.Contains(n.Clave);

    private static string Normal(string s)
    {
        var t = s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(t.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }

    private void PlegarTodo()
    {
        plegados.Clear();
        // Se ve hasta el tercer nivel.
        foreach (var (n, _, nivel) in ordenados) if (nivel >= 3 && hijos.ContainsKey(n.Clave)) plegados.Add(n.Clave);
    }

    private void AlternarPlegado(Guid c) { if (!plegados.Remove(c)) plegados.Add(c); }

    // ---------- Datos para pintar ----------
    private static readonly string[] Paleta =
        { "var(--org-acento)", "#2563eb", "#0d9488", "#16a34a", "#ca8a04", "#ea580c", "#7c3aed", "#db2777", "#0891b2", "#65a30d", "#475569", "#b45309" };

    private string ColorArea(string? a)
    {
        if (string.IsNullOrWhiteSpace(a)) return "#8a94a6";
        var i = areas.IndexOf(a);
        return i < 0 ? "#8a94a6" : Paleta[i % Paleta.Length];
    }

    private int Directos(NodoOrg n) => hijos.TryGetValue(n.Clave, out var hs) ? hs.Count : 0;
    private int Total(NodoOrg n) => totales.TryGetValue(n.Clave, out var t) ? t : 0;
    private string? NombreJefe(NodoOrg n) => n.Jefe is Guid j && porClave.TryGetValue(j, out var p) ? p.Nombre : null;

    private List<NodoOrg> Linea(NodoOrg n) => hijos.TryGetValue(n.Clave, out var hs) ? hs.Where(h => !h.Staff).ToList() : new();
    private List<NodoOrg> Apoyos(NodoOrg n) => hijos.TryGetValue(n.Clave, out var hs) ? hs.Where(h => h.Staff).ToList() : new();

    // Hijos todos sin subordinados (y 2 o más) = columna vertical compacta, como en el Excel.
    private bool Vertical(NodoOrg n)
    {
        var l = Linea(n);
        return l.Count >= 2 && l.All(h => !hijos.ContainsKey(h.Clave));
    }

    private string ClaseLi(NodoOrg n) => Apoyos(n).Count > 0 ? "org-con-apoyo" : "";

    private static string Iniciales(string nombre)
    {
        var p = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 1 || char.IsLetter(x[0])).ToList();
        if (p.Count == 0) return "?";
        var a = char.ToUpperInvariant(p[0].TrimEnd('.')[0]);
        var b = p.Count > 1 ? char.ToUpperInvariant(p[^1][0]) : (char?)null;
        return b is null ? a.ToString() : $"{a}{b}";
    }

    // ---------- Diagrama ----------


    // ---------- Ficha del cargo ----------
    private sealed record PasoHistorial(int Numero, string Estado, string Nombre, string? Ocupante, string? Jefe, string? Area);
    private List<PasoHistorial> historial = new();
    private bool enfocarNombre;

    private async Task CargarHistorialAsync()
    {
        historial.Clear();
        if (sel is not Guid c) return;
        try
        {
            var filas = await Db.CargosOrganigrama.AsNoTracking().Where(x => x.Clave == c).ToListAsync();
            var idsJefe = filas.Where(f => f.JefeId != null).Select(f => f.JefeId!.Value).ToList();
            var jefes = await Db.CargosOrganigrama.AsNoTracking().Where(x => idsJefe.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Nombre);
            var vers = versiones.ToDictionary(v => v.Id);
            historial = filas.Where(f => vers.ContainsKey(f.VersionOrganigramaId))
                .Select(f => new PasoHistorial(vers[f.VersionOrganigramaId].Numero, vers[f.VersionOrganigramaId].Estado, f.Nombre, f.Ocupante,
                    f.JefeId is int j && jefes.TryGetValue(j, out var jn) ? jn : null, f.Area))
                .OrderByDescending(h => h.Numero).ToList();
        }
        catch { historial.Clear(); }
    }


    // ---------- Tabla de cambios (comparación y publicar) ----------

    // ---------- Comparar ----------
    private int compA, compB;
    private List<CambioOrg> cambiosComp = new();
    private TipoCambioOrg? filtroCambio;

    private void Comparar()
    {
        filtroCambio = null;
        if (compA == 0 || compB == 0) { cambiosComp = new(); return; }
        var a = compA == ver?.Id && editando ? original : ServicioOrganigrama.Nodos(Db, compA);
        var b = compB == ver?.Id ? nodos : ServicioOrganigrama.Nodos(Db, compB);
        cambiosComp = ServicioOrganigrama.Comparar(a, b);
    }

    // ---------- Publicar ----------
    private DatosPublicacion? pub;
    private string pubTitulo = "", pubAlcance = "";
    private List<CambioOrg> cambiosPub = new();
    private List<string> erroresPub = new();

    private void AbrirPublicar()
    {
        if (ver is null || ver.Estado != EstadoOrganigrama.Borrador) return;
        if (pub is not null) { pub = null; return; }
        elimVersion = null; verImportar = false; mensaje = "";
        pub = new DatosPublicacion();
        pubTitulo = ver.Titulo; pubAlcance = ver.Alcance ?? "";
        cambiosPub = vigente is null ? new() : ServicioOrganigrama.Comparar(ServicioOrganigrama.Nodos(Db, vigente.Id), nodos);
        erroresPub = ServicioOrganigrama.Validar(nodos);
        if (vigente is not null && cambiosPub.Count == 0) erroresPub.Add($"No hay cambios frente a la v{vigente.Numero}.");
    }

    private async Task Publicar()
    {
        if (ver is null || pub is null) return;
        guardando = true;
        try
        {
            Db.ChangeTracker.Clear();
            var v = await Db.VersionesOrganigrama.FirstAsync(x => x.Id == ver.Id);
            v.Titulo = pubTitulo.Trim(); v.Alcance = ServicioOrganigrama.Limpio(pubAlcance);
            await Db.SaveChangesAsync();
            await ServicioOrganigrama.PublicarAsync(Db, ver.Id, pub);
            mensaje = $"v{ver.Numero} publicada como vigente."; error = false;
            var id = ver.Id;
            pub = null;
            CargarVersiones(id);
        }
        catch (Exception ex) { mensaje = "No se pudo publicar: " + ex.Message; error = true; }
        finally { guardando = false; }
    }

    // ---------- Eliminar versión ----------
    private sealed class Eliminacion
    {
        public int Id;
        public InformeImpacto Informe = new();
        public bool Entendido;
    }

    private Eliminacion? elimVersion;

    private async Task PedirEliminarVersion()
    {
        if (ver is null) return;
        if (elimVersion is not null) { elimVersion = null; return; }
        pub = null; verImportar = false; mensaje = "";
        elimVersion = new Eliminacion { Id = ver.Id, Informe = await Impacto.VersionOrganigrama(ver.Id) };
    }

    private async Task ConfirmarEliminarVersion()
    {
        if (elimVersion is null) return;
        try
        {
            Db.ChangeTracker.Clear();
            var v = await Db.VersionesOrganigrama.FirstOrDefaultAsync(x => x.Id == elimVersion.Id);
            if (v is not null)
            {
                v.Eliminado = true; v.FechaEliminacion = DateTime.Now;
                await Db.SaveChangesAsync();
                mensaje = $"v{v.Numero} enviada a la Papelera."; error = false;
            }
        }
        catch (Exception ex) { mensaje = "No se pudo eliminar: " + ex.Message; error = true; }
        elimVersion = null; sel = null;
        CargarVersiones();
        await Recordar();
    }

    // ---------- Exportar ----------
    private async Task Exportar(string formato)
    {
        if (ver is null) return;
        if (formato == "pdf")
        {
            if (vista != "diagrama") { vista = "diagrama"; StateHasChanged(); await Task.Delay(50); }
            try { await JS.InvokeVoidAsync("sigOrganigrama.imprimir", marco, $"Organigrama v{ver.Numero}"); } catch { }
            return;
        }
        var bytes = ServicioOrganigrama.Exportar(ver, nodos, versiones);
        await JS.InvokeVoidAsync("descargarArchivo", $"Organigrama_v{ver.Numero}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Convert.ToBase64String(bytes));
    }

    // ---------- Importar ----------
    private sealed class Importacion
    {
        public string Archivo = "";
        public List<NodoOrg> Nodos = new();
        public List<string> Avisos = new();
        public int Reusados;
        public int BaseNumero;
    }

    private Importacion? importe;

    private void AlternarImportar()
    {
        verImportar = !verImportar; importe = null;
        if (verImportar) { pub = null; elimVersion = null; }
    }

    private async Task LeerImportacion(InputFileChangeEventArgs e)
    {
        try
        {
            using var ms = new MemoryStream();
            await e.File.OpenReadStream(20L * 1024 * 1024).CopyToAsync(ms);
            ms.Position = 0;
            var (lista, avisos) = ServicioOrganigrama.Importar(ms);
            var baseV = vigente ?? ver;
            var reusados = 0;
            if (baseV is not null) reusados = ReusarClaves(lista, ServicioOrganigrama.Nodos(Db, baseV.Id));
            importe = new Importacion { Archivo = e.File.Name, Nodos = lista, Avisos = avisos, Reusados = reusados, BaseNumero = baseV?.Numero ?? 0 };
        }
        catch (Exception ex) { mensaje = "No se pudo leer el Excel: " + ex.Message; error = true; importe = null; }
    }

    // Liga lo importado a los cargos de la versión base (misma Clave → se ve como cambio y no como cargo nuevo):
    // 1) la columna Clave del exportado; 2) cargo + ocupante; 3) cargo con el mismo jefe ya ligado (repetido hasta
    // que no ligue más: así se ligan los repetidos como «Capataz civil»); 4) cargo de nombre único en ambos lados.
    private static int ReusarClaves(List<NodoOrg> importados, List<NodoOrg> baseN)
    {
        string K(string? s) => (s ?? "").Trim().ToLowerInvariant();
        var clavesBase = baseN.Select(b => b.Clave).ToHashSet();
        var mapa = new Dictionary<Guid, Guid>();                       // clave importada → clave base
        foreach (var n in importados.Where(n => clavesBase.Contains(n.Clave))) mapa[n.Clave] = n.Clave;
        var libres = baseN.Where(b => !mapa.ContainsValue(b.Clave)).ToList();
        void Ligar(NodoOrg n, NodoOrg b) { mapa[n.Clave] = b.Clave; libres.Remove(b); }
        IEnumerable<NodoOrg> Pendientes() => importados.Where(n => !mapa.ContainsKey(n.Clave)).ToList();

        foreach (var n in Pendientes())
        {
            if (K(n.Ocupante).Length == 0) continue;
            var m = libres.Where(b => K(b.Nombre) == K(n.Nombre) && K(b.Ocupante) == K(n.Ocupante)).ToList();
            if (m.Count == 1) Ligar(n, m[0]);
        }
        bool avanzo;
        do
        {
            avanzo = false;
            foreach (var n in Pendientes())
            {
                Guid? jefeBase = n.Jefe is Guid j ? (mapa.TryGetValue(j, out var jb) ? jb : (Guid?)Guid.Empty) : null;
                if (jefeBase == Guid.Empty) continue;              // su jefe aún no está ligado
                var m = libres.Where(b => K(b.Nombre) == K(n.Nombre) && b.Jefe == jefeBase).ToList();
                var hermanos = Pendientes().Count(x => K(x.Nombre) == K(n.Nombre) && x.Jefe == n.Jefe);
                if (m.Count >= 1 && (m.Count == 1 || hermanos == m.Count)) { Ligar(n, m.OrderBy(b => b.Orden).First()); avanzo = true; }
            }
        } while (avanzo);
        foreach (var n in Pendientes())
        {
            var m = libres.Where(b => K(b.Nombre) == K(n.Nombre)).ToList();
            if (m.Count == 1 && Pendientes().Count(x => K(x.Nombre) == K(n.Nombre)) == 1) Ligar(n, m[0]);
        }
        var reusados = mapa.Count;
        foreach (var n in importados) if (n.Jefe is Guid j && mapa.TryGetValue(j, out var nj)) n.Jefe = nj;
        foreach (var n in importados) if (mapa.TryGetValue(n.Clave, out var nc)) n.Clave = nc;
        return reusados;
    }

    private async Task AplicarImportacion()
    {
        if (importe is null || importe.Nodos.Count == 0) return;
        guardando = true;
        try
        {
            VersionOrganigrama destinoV;
            if (borrador is not null) destinoV = borrador;
            else if (vigente is not null) destinoV = await ServicioOrganigrama.CrearBorradorAsync(Db, vigente.Id);
            else
            {
                destinoV = new VersionOrganigrama
                {
                    Numero = (await Db.VersionesOrganigrama.IgnoreQueryFilters().MaxAsync(v => (int?)v.Numero) ?? 0) + 1,
                    Titulo = "Organigrama", Estado = EstadoOrganigrama.Borrador
                };
                Db.VersionesOrganigrama.Add(destinoV);
                await Db.SaveChangesAsync();
            }
            await ServicioOrganigrama.GuardarAsync(Db, destinoV.Id, importe.Nodos);
            Db.ChangeTracker.Clear();
            var v = await Db.VersionesOrganigrama.FirstAsync(x => x.Id == destinoV.Id);
            v.Origen = importe.Archivo;
            await Db.SaveChangesAsync();
            mensaje = $"{importe.Nodos.Count} cargos importados al borrador v{v.Numero}."; error = false;
            verImportar = false; importe = null;
            CargarVersiones(v.Id);
        }
        catch (Exception ex) { mensaje = "No se pudo importar: " + ex.Message; error = true; }
        finally { guardando = false; }
    }
}
