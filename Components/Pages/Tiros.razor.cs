using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data.Costos;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Lógica de Tiros (el marcado y el fragmento de columna están en Tiros.razor).
// Modelo de edición: tirosEd (tiros en memoria, Id < 0 = nuevo) + asig (torre → tiro). Lo pendiente NO se cuenta por
// acciones sino comparando con lo cargado (Diferencias): mover y devolver = sin cambios; Guardar solo se activa con
// diferencias reales y esas mismas diferencias se escriben como filas de CambioTiro.
public partial class Tiros
{
    private sealed record TorreOp(int Id, string Nombre, int? TiroId);
    private sealed class TiroEd { public int Id; public string Nombre = ""; public int Orden; }
    private sealed record Pendiente(string Clave, string Tipo, string Elemento, string? Antes, string? Despues, int? TorreId, int? TiroId);
    private sealed record Paso(Dictionary<int, int?> Asig, List<TiroEd> Tiros);
    private sealed record Lote(Guid Id, DateTime Fecha, string? Motivo, List<CambioTiro> Filas);

    // ---------- Datos cargados ----------
    private List<Tiro> tiros = new();
    private List<TorreOp> torres = new();                              // orden natural (rango con Mayús)
    private Dictionary<int, string> estadoTorre = new();               // estado vigente hoy (Estado de las torres)
    private Dictionary<string, ItemCatalogo> catalogoEstados = new(StringComparer.Ordinal);
    private List<string> ordenEstados = new();

    // ---------- Edición ----------
    private List<TiroEd> tirosEd = new();
    private Dictionary<int, int?> asig = new();
    private List<Pendiente> pendientes = new();
    private readonly List<Paso> historial = new();
    private HashSet<int> sel = new();
    private int? ultimo;
    private int sigNuevo = -1;
    private bool edicion, guardando, colorEstado = true;
    private string buscar = "", motivo = "";
    private int destacado;

    // ---------- Otros ----------
    private string vista = "tablero";
    private string? mensaje;
    private bool mensajeError;
    private string filtro = "";
    private ElementReference raiz;
    private DotNetObjectReference<Tiros>? refNet;

    private Tiro? elim;
    private SigWeb.Data.Impacto.InformeImpacto? impacto;
    private bool impactoOk;
    private bool BloqueadoPorImpacto => impacto is null || (impacto.RequiereEntender && !impactoOk);

    private List<Lote> lotes = new();
    private string buscarHist = "";
    private string? errorHist;

    [SupplyParameterFromQuery(Name = "tiro")] public int? TiroQ { get; set; }
    private int? tiroAplicado;

    protected override void OnInitialized() => Cargar();

    protected override void OnParametersSet()
    {
        if (TiroQ == tiroAplicado) return;
        tiroAplicado = TiroQ;
        if (TiroQ is int id && tiros.Any(t => t.Id == id)) { vista = "tablero"; Destacar(id); }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (motorPendiente)
        {
            motorPendiente = false;
            try { motor = await MotorTabla.CargarAsync(Db, "tiros", ColumnasNativas(), tiros.Select(t => (t.Id, t.Nombre))); }
            catch { motor = null; }
            StateHasChanged();
        }
        if (vista == "tablero")
        {
            refNet ??= DotNetObjectReference.Create(this);
            try { await JS.InvokeVoidAsync("sigTirosTablero.iniciar", raiz, refNet); } catch { }
        }
        if (irA is string ancla)
        {
            irA = null;
            try { await JS.InvokeVoidAsync("sigIrA", ancla); } catch { }
        }
    }
    private string? irA;

    public async ValueTask DisposeAsync()
    {
        try { await JS.InvokeVoidAsync("sigTirosTablero.destruir", raiz); } catch { }
        refNet?.Dispose();
    }

    private void Cargar()
    {
        motorPendiente = true;
        Db.ChangeTracker.Clear();
        tiros = Db.Tiros.AsNoTracking().Where(t => !t.Eliminado).ToList()
            .OrderBy(t => t.Orden).ThenBy(t => OrdenNatural(t.Nombre)).ToList();

        // Mismo nombre que en la Matriz PIE (catálogo Localización); si la torre no tiene, su código.
        var nombres = Db.Catalogos.AsNoTracking()
            .Where(c => c.Tipo == "Localización" && c.Activo && c.ElementoTrazadoId != null).ToList()
            .GroupBy(c => c.ElementoTrazadoId!.Value).ToDictionary(g => g.Key, g => g.First().Nombre);
        var vivos = tiros.Select(t => t.Id).ToHashSet();
        torres = Db.ElementosTrazado.AsNoTracking().Where(e => !e.Eliminado).ToList()
            .Select(e => new TorreOp(e.Id, nombres.TryGetValue(e.Id, out var n) ? n : e.Codigo,
                                     e.TiroId is int tid && vivos.Contains(tid) ? tid : null))
            .OrderBy(t => OrdenNatural(t.Nombre)).ToList();

        // Estado de cada torre hoy (mismos datos y colores que Costos › Estado de las torres). Si falla, sin color.
        try
        {
            var hoy = DateTime.Today;
            estadoTorre = Db.EstadosTorreManual.AsNoTracking().Where(e => e.Desde <= hoy).ToList()
                .GroupBy(e => e.ElementoTrazadoId)
                .Select(g => g.OrderByDescending(e => e.Desde).ThenByDescending(e => e.Id).First())
                .Where(e => !string.IsNullOrWhiteSpace(e.Estado))
                .ToDictionary(e => e.ElementoTrazadoId, e => e.Estado);
            var orden = Db.ConfiguracionCostos.AsNoTracking().OrderBy(x => x.Id).Select(x => x.OrdenEstadosTorre).FirstOrDefault();
            var cat = EstadoTorres.Ordenar(Db.Catalogos.AsNoTracking().Where(c => c.Tipo == EstadoTorres.Catalogo && c.Activo).ToList(), orden);
            catalogoEstados = new(StringComparer.Ordinal);
            foreach (var c in cat) catalogoEstados.TryAdd(EstadoTorres.Clave(c.Nombre), c);
            ordenEstados = (cat.Count > 0 ? cat.Select(c => c.Nombre) : EstadoTorres.Fabrica.Select(f => f.Nombre)).Select(EstadoTorres.Clave).ToList();
        }
        catch { estadoTorre = new(); catalogoEstados = new(StringComparer.Ordinal); ordenEstados = new(); }

        ReiniciarEdicion();
    }

    private void ReiniciarEdicion()
    {
        tirosEd = tiros.Select(t => new TiroEd { Id = t.Id, Nombre = t.Nombre, Orden = t.Orden }).ToList();
        asig = torres.ToDictionary(t => t.Id, t => t.TiroId);
        historial.Clear(); sel.Clear(); pendientes.Clear();
        ultimo = null; motivo = ""; sigNuevo = -1;
    }

    // "3", "5N", "15A" → por número y luego por sufijo (igual que la Matriz PIE).
    private static (int, string) OrdenNatural(string n)
    {
        var d = new string(n.TakeWhile(char.IsDigit).ToArray());
        return (int.TryParse(d, out var v) ? v : int.MaxValue, n);
    }

    // ---------- Lectura para el tablero ----------
    private IEnumerable<TiroEd> TirosOrdenados => tirosEd.OrderBy(t => t.Orden).ThenBy(t => OrdenNatural(t.Nombre));
    private List<TorreOp> TorresDe(int? tiroId) => torres.Where(t => asig.GetValueOrDefault(t.Id) == tiroId).ToList();
    private List<TorreOp> SinTiro => TorresDe(null);
    private int PosicionDe(TiroEd t) => TirosOrdenados.ToList().IndexOf(t);
    private bool NombreVacio => tirosEd.Any(t => string.IsNullOrWhiteSpace(t.Nombre));

    private string NombreTiro(int? id) =>
        id is int i ? (tirosEd.FirstOrDefault(t => t.Id == i)?.Nombre is { Length: > 0 } n ? n : "(sin nombre)") : "Sin tiro";
    private string NombreTiroOriginal(int? id) =>
        id is int i ? (tiros.FirstOrDefault(t => t.Id == i)?.Nombre ?? $"Tiro {i}") : "Sin tiro";

    private bool TiroCambiado(TiroEd t)
    {
        if (t.Id < 0) return true;
        var o = tiros.FirstOrDefault(x => x.Id == t.Id);
        return o is null || o.Nombre != t.Nombre.Trim() || o.Orden != t.Orden;
    }

    private string? EstadoDe(int torreId) => estadoTorre.TryGetValue(torreId, out var e) ? e : null;

    private int OrdenEstado(string? est)
    {
        if (est is null) return int.MaxValue;
        var i = ordenEstados.IndexOf(EstadoTorres.Clave(est));
        return i < 0 ? int.MaxValue - 1 : i;
    }

    private string Estilo(string? estado)
    {
        if (estado is null) return "";
        if (catalogoEstados.TryGetValue(EstadoTorres.Clave(estado), out var c) && !string.IsNullOrWhiteSpace(c.ColorFondo))
            return $"--f:{c.ColorFondo};--c:{(string.IsNullOrWhiteSpace(c.ColorTexto) ? "#222" : c.ColorTexto)}";
        var d = EstadoTorres.Fabrica.FirstOrDefault(x => EstadoTorres.Clave(x.Nombre) == EstadoTorres.Clave(estado));
        return d is null ? "--f:#d9dde3;--c:#333" : $"--f:{d.Fondo};--c:{d.Texto}";
    }

    private static string Rango(List<TorreOp> l) => l.Count switch { 0 => "", 1 => l[0].Nombre, _ => $"{l[0].Nombre} – {l[^1].Nombre}" };

    private string Tip(TorreOp t, string? est, bool movida)
    {
        var s = t.Nombre + " · " + (est ?? "sin estado");
        if (movida) s += $" · antes en {NombreTiroOriginal(t.TiroId)}";
        return s;
    }

    // ---------- Vistas ----------
    private async Task CambiarVista(string v)
    {
        if (edicion && v != "tablero") return;
        vista = v;
        if (v == "cambios") await CargarHistorialAsync();
    }

    private void Destacar(int tiroId)
    {
        destacado = tiroId;
        irA = $"tt-col-{tiroId}";
    }

    // ---------- Modo edición ----------
    private void EntrarEdicion(int? tiroId = null)
    {
        vista = "tablero";
        ReiniciarEdicion();
        edicion = true;
        mensaje = null;
        if (tiroId is int id)
        {
            sel = TorresDe(id).Select(t => t.Id).ToHashSet();
            Destacar(id);
        }
    }

    private void SalirEdicion()
    {
        edicion = false;
        ReiniciarEdicion();
    }

    private void NuevoDesdeEncabezado()
    {
        if (!edicion) EntrarEdicion();
        CrearTiro(new());
    }

    private void Recalcular() => pendientes = Diferencias();

    private void Anotar() => historial.Add(new Paso(new(asig),
        tirosEd.Select(t => new TiroEd { Id = t.Id, Nombre = t.Nombre, Orden = t.Orden }).ToList()));

    private void DeshacerUltimo()
    {
        if (historial.Count == 0) return;
        var p = historial[^1];
        historial.RemoveAt(historial.Count - 1);
        asig = p.Asig; tirosEd = p.Tiros;
        sel.RemoveWhere(id => !asig.ContainsKey(id));
        Recalcular();
    }

    // Lo que cambió de verdad respecto a lo cargado.
    private List<Pendiente> Diferencias()
    {
        var l = new List<Pendiente>();
        foreach (var t in TirosOrdenados)
        {
            var nombre = t.Nombre.Trim();
            if (t.Id < 0)
            {
                l.Add(new($"alta:{t.Id}", TipoCambioTiro.Alta, nombre.Length > 0 ? nombre : "(sin nombre)", null,
                          $"{TorresDe(t.Id).Count} torres", null, t.Id));
                continue;
            }
            var o = tiros.FirstOrDefault(x => x.Id == t.Id);
            if (o is null) continue;
            if (o.Nombre != nombre) l.Add(new($"nom:{t.Id}", TipoCambioTiro.Nombre, o.Nombre, o.Nombre, nombre, null, t.Id));
            if (o.Orden != t.Orden) l.Add(new($"ord:{t.Id}", TipoCambioTiro.Orden, nombre, o.Orden.ToString(), t.Orden.ToString(), null, t.Id));
        }
        foreach (var t in torres)
        {
            var ahora = asig.GetValueOrDefault(t.Id);
            if (ahora != t.TiroId)
                l.Add(new($"tor:{t.Id}", TipoCambioTiro.Asignacion, t.Nombre, NombreTiroOriginal(t.TiroId), NombreTiro(ahora), t.Id, ahora));
        }
        return l;
    }

    private void Revertir(Pendiente c)
    {
        Anotar();
        if (c.Tipo == TipoCambioTiro.Asignacion && c.TorreId is int tid)
            asig[tid] = torres.First(t => t.Id == tid).TiroId;
        else if (c.TiroId is int id && tirosEd.FirstOrDefault(t => t.Id == id) is { } t)
        {
            var o = tiros.FirstOrDefault(x => x.Id == id);
            if (c.Tipo == TipoCambioTiro.Alta) { QuitarNuevoSinAnotar(t); }
            else if (c.Tipo == TipoCambioTiro.Nombre && o is not null) t.Nombre = o.Nombre;
            else if (c.Tipo == TipoCambioTiro.Orden && o is not null) t.Orden = o.Orden;
        }
        Recalcular();
    }

    // ---------- Selección ----------
    private void ClicTorre(int id, MouseEventArgs e)
    {
        if (!edicion) return;
        if (e.ShiftKey && ultimo is int u)
        {
            // Rango en el orden del tablero (columnas de izquierda a derecha, torres de arriba abajo).
            var orden = OrdenTablero();
            int a = orden.IndexOf(u), b = orden.IndexOf(id);
            if (a >= 0 && b >= 0) foreach (var x in orden.Skip(Math.Min(a, b)).Take(Math.Abs(a - b) + 1)) sel.Add(x);
        }
        else if (!sel.Remove(id)) sel.Add(id);
        ultimo = id;
    }

    private List<int> OrdenTablero()
    {
        var l = TorresDe(null).Select(t => t.Id).ToList();
        foreach (var t in TirosOrdenados) l.AddRange(TorresDe(t.Id).Select(x => x.Id));
        return l;
    }

    private void SeleccionarTiro(int? tiroId)
    {
        if (!edicion) return;
        var ids = TorresDe(tiroId).Select(t => t.Id).ToList();
        if (ids.Count > 0 && ids.All(sel.Contains)) sel.ExceptWith(ids); else sel.UnionWith(ids);
    }

    private void SeleccionarTodas()
    {
        if (sel.Count == torres.Count) sel.Clear(); else sel = torres.Select(t => t.Id).ToHashSet();
    }

    private void SeleccionarCoincidencias()
    {
        var q = buscar.Trim();
        if (q == "") return;
        var tirosQ = tirosEd.Where(t => t.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)).Select(t => (int?)t.Id).ToHashSet();
        sel.UnionWith(torres.Where(t => t.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase) || tirosQ.Contains(asig.GetValueOrDefault(t.Id))).Select(t => t.Id));
    }

    // ---------- Mover torres y tiros ----------
    private void Asignar(IEnumerable<int> ids, int? tiroId)
    {
        var lista = ids.Where(asig.ContainsKey).Where(id => asig[id] != tiroId).ToList();
        if (lista.Count == 0) return;
        Anotar();
        foreach (var id in lista) asig[id] = tiroId;
        sel.Clear();
        Recalcular();
    }

    private void MoverSeleccion(string? destino)
    {
        if (string.IsNullOrEmpty(destino) || !int.TryParse(destino, out var id)) return;
        Asignar(sel.ToList(), id == 0 ? null : id);
    }

    private void CrearTiro(List<int> ids)
    {
        if (!edicion) return;
        Anotar();
        var numeros = tirosEd.Select(t => EstadoTorres.NumeroTiro(t.Nombre) ?? 0).DefaultIfEmpty(0);
        var t = new TiroEd { Id = sigNuevo--, Nombre = $"Tiro {numeros.Max() + 1}", Orden = tirosEd.Count == 0 ? 1 : tirosEd.Max(x => x.Orden) + 1 };
        tirosEd.Add(t);
        foreach (var id in ids.Where(asig.ContainsKey)) asig[id] = t.Id;
        sel.Clear();
        Destacar(t.Id);
        Recalcular();
    }

    private void QuitarNuevo(TiroEd t)
    {
        Anotar();
        QuitarNuevoSinAnotar(t);
        Recalcular();
    }

    private void QuitarNuevoSinAnotar(TiroEd t)
    {
        foreach (var x in torres.Where(x => asig.GetValueOrDefault(x.Id) == t.Id)) asig[x.Id] = x.TiroId is int o && o != t.Id ? o : null;
        tirosEd.Remove(t);
    }

    private void Renombrar(TiroEd t, string? nombre)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre == t.Nombre) return;
        Anotar();
        t.Nombre = nombre;
        Recalcular();
    }

    // Intercambia la posición con el vecino (si tienen el mismo orden, se separan).
    private void MoverTiro(TiroEd t, int paso)
    {
        var l = TirosOrdenados.ToList();
        var i = l.IndexOf(t); var j = i + paso;
        if (i < 0 || j < 0 || j >= l.Count) return;
        Anotar();
        var otro = l[j];
        if (otro.Orden == t.Orden) { for (var k = 0; k < l.Count; k++) l[k].Orden = k + 1; }
        (t.Orden, otro.Orden) = (otro.Orden, t.Orden);
        Recalcular();
    }

    // ---------- Llamadas desde js/tirosTablero.js ----------
    [JSInvokable]
    public async Task Soltar(int[] ids, string tiro)
    {
        await InvokeAsync(() =>
        {
            if (!edicion) return;
            if (tiro == "nuevo") CrearTiro(ids.ToList());
            else if (int.TryParse(tiro, out var id)) Asignar(ids, id == 0 ? null : id);
            StateHasChanged();
        });
    }

    [JSInvokable]
    public async Task Lazo(int[] ids, bool sumar)
    {
        await InvokeAsync(() =>
        {
            if (!edicion) return;
            if (!sumar) sel.Clear();
            sel.UnionWith(ids.Where(asig.ContainsKey));
            StateHasChanged();
        });
    }

    // ---------- Guardar: aplica las diferencias y deja su control de cambios ----------
    private async Task Guardar()
    {
        if (guardando || NombreVacio) return;
        var cambios = Diferencias();
        if (cambios.Count == 0) { SalirEdicion(); return; }
        guardando = true;
        try
        {
            await using var tx = await Db.Database.BeginTransactionAsync();
            var ids = new Dictionary<int, int>();                     // id en memoria (< 0) → id real
            foreach (var t in tirosEd.Where(t => t.Id < 0))
            {
                var ent = new Tiro { Nombre = t.Nombre.Trim(), Orden = t.Orden };
                Db.Tiros.Add(ent);
                await Db.SaveChangesAsync();
                ids[t.Id] = ent.Id;
            }
            int? Real(int? id) => id is int i && i < 0 ? ids[i] : id;

            var existentes = tirosEd.Where(t => t.Id > 0 && TiroCambiado(t)).ToDictionary(t => t.Id);
            foreach (var ent in await Db.Tiros.Where(t => existentes.Keys.Contains(t.Id)).ToListAsync())
            {
                ent.Nombre = existentes[ent.Id].Nombre.Trim();
                ent.Orden = existentes[ent.Id].Orden;
            }

            var movidas = torres.Where(t => asig.GetValueOrDefault(t.Id) != t.TiroId).ToDictionary(t => t.Id, t => Real(asig[t.Id]));
            foreach (var e in await Db.ElementosTrazado.Where(e => movidas.Keys.Contains(e.Id)).ToListAsync())
                e.TiroId = movidas[e.Id];

            var lote = Guid.NewGuid();
            var ahora = DateTime.Now;
            var mot = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            foreach (var c in cambios)
                Db.CambiosTiro.Add(new CambioTiro
                {
                    Lote = lote, Fecha = ahora, Tipo = c.Tipo, Elemento = c.Elemento, Antes = c.Antes, Despues = c.Despues,
                    ElementoTrazadoId = c.TorreId, TiroId = Real(c.TiroId), Motivo = mot,
                });
            await Db.SaveChangesAsync();
            await tx.CommitAsync();

            edicion = false;
            Cargar();
            mensaje = $"Guardado: {cambios.Count} {(cambios.Count == 1 ? "cambio" : "cambios")} (quedan en Control de cambios).";
            mensajeError = false;
        }
        catch (Exception ex)
        {
            Db.ChangeTracker.Clear();
            var m = ex.InnerException?.Message ?? ex.Message;
            mensaje = "No se pudo guardar: " + m + (m.Contains("CambiosTiro", StringComparison.OrdinalIgnoreCase) ? " (falta aplicar la migración de CambiosTiro)" : "");
            mensajeError = true;
        }
        finally { guardando = false; }
    }

    // ---------- Control de cambios ----------
    private async Task CargarHistorialAsync()
    {
        errorHist = null;
        try
        {
            var filas = await Db.CambiosTiro.AsNoTracking().OrderByDescending(c => c.Fecha).ThenBy(c => c.Id).Take(3000).ToListAsync();
            lotes = filas.GroupBy(c => c.Lote)
                .Select(g => new Lote(g.Key, g.Max(c => c.Fecha), g.Select(c => c.Motivo).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)),
                    g.OrderBy(c => c.Tipo == TipoCambioTiro.Asignacion ? 1 : 0).ThenBy(c => c.Id).ToList()))
                .OrderByDescending(l => l.Fecha).ToList();
        }
        catch (Exception ex)
        {
            lotes = new();
            errorHist = "No se pudo leer el control de cambios (¿falta aplicar la migración de CambiosTiro?): " + (ex.InnerException?.Message ?? ex.Message);
        }
    }

    private IEnumerable<Lote> LotesMostrados
    {
        get
        {
            var q = buscarHist.Trim();
            if (q == "") return lotes;
            bool Coincide(string? s) => s?.Contains(q, StringComparison.OrdinalIgnoreCase) == true;
            return lotes.Select(l => Coincide(l.Motivo) ? l
                    : l with { Filas = l.Filas.Where(f => Coincide(f.Elemento) || Coincide(f.Antes) || Coincide(f.Despues) || Coincide(f.Tipo)).ToList() })
                .Where(l => l.Filas.Count > 0);
        }
    }

    private static string Resumen(Lote l)
    {
        var partes = l.Filas.GroupBy(f => f.Tipo).Select(g => g.Key switch
        {
            TipoCambioTiro.Asignacion => $"{g.Count()} {(g.Count() == 1 ? "torre movida" : "torres movidas")}",
            TipoCambioTiro.Alta => $"{g.Count()} {(g.Count() == 1 ? "tiro nuevo" : "tiros nuevos")}",
            TipoCambioTiro.Nombre => $"{g.Count()} {(g.Count() == 1 ? "renombrado" : "renombrados")}",
            TipoCambioTiro.Orden => "orden",
            TipoCambioTiro.Eliminacion => $"{g.Count()} {(g.Count() == 1 ? "eliminado" : "eliminados")}",
            _ => g.Key,
        });
        return string.Join(" · ", partes);
    }

    // ---------- Tabla (campos, fórmulas, exportar) ----------
    private MotorTabla? motor;
    private MotorTabla Motor => motor!;
    private bool verCampos, motorPendiente = true;
    private string? colOrden;
    private bool ordenAsc = true;

    private IEnumerable<CampoTabla> Columnas => motor?.Visibles ?? Enumerable.Empty<CampoTabla>();

    private IEnumerable<Tiro> TirosMostrados
    {
        get
        {
            var q = tiros.Where(x => string.IsNullOrWhiteSpace(filtro) || x.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                                     || (motor is not null && motor.TextoBusqueda(x.Id).Contains(filtro, StringComparison.OrdinalIgnoreCase)));
            return motor is not null && colOrden is not null ? motor.Ordenar(q, t => t.Id, colOrden, ordenAsc) : q;
        }
    }

    private void OrdenarPor(string clave) { if (colOrden == clave) ordenAsc = !ordenAsc; else { colOrden = clave; ordenAsc = true; } }
    private string Flecha(string clave) => colOrden == clave ? (ordenAsc ? " ▲" : " ▼") : "";

    private IEnumerable<ColumnaNativa> ColumnasNativas()
    {
        Tiro? T(int id) => tiros.FirstOrDefault(t => t.Id == id);
        List<TorreOp> De(int id) => torres.Where(t => t.TiroId == id).ToList();
        return new[]
        {
            new ColumnaNativa("Nombre", "Tiro", TipoCampoTabla.Texto, id => T(id)?.Nombre),
            new ColumnaNativa("Orden", "Orden", TipoCampoTabla.Numero, id => T(id)?.Orden),
            new ColumnaNativa("Torres", "Torres", TipoCampoTabla.Texto, id => string.Join(", ", De(id).Select(x => x.Nombre))),
            new ColumnaNativa("NumTorres", "N.º", TipoCampoTabla.Numero, id => De(id).Count),
        };
    }

    // ---------- Eliminar (fuera de edición, con impacto → Papelera; queda en el control de cambios) ----------
    private void PedirEliminar(Tiro t)
    {
        elim = t; impacto = null; impactoOk = false;
        _ = CalcularImpacto(t.Id);
    }

    private async Task CalcularImpacto(int id)
    {
        try { impacto = await Analizador.Tiro(id); }
        catch { impacto = new SigWeb.Data.Impacto.InformeImpacto { Elemento = "No se pudo calcular el impacto" }; }
        await InvokeAsync(StateHasChanged);
    }

    private async Task ConfirmarEliminar()
    {
        if (elim is null) return;
        try
        {
            var ent = await Db.Tiros.FirstOrDefaultAsync(t => t.Id == elim.Id);
            if (ent is not null)
            {
                ent.Eliminado = true; ent.FechaEliminacion = DateTime.Now;
                Db.CambiosTiro.Add(new CambioTiro
                {
                    Lote = Guid.NewGuid(), Fecha = DateTime.Now, Tipo = TipoCambioTiro.Eliminacion, TiroId = ent.Id, Elemento = ent.Nombre,
                    Antes = $"{torres.Count(t => t.TiroId == ent.Id)} torres", Despues = "Papelera",
                });
                await Db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Db.ChangeTracker.Clear();
            mensaje = "No se pudo eliminar: " + (ex.InnerException?.Message ?? ex.Message);
            mensajeError = true;
        }
        elim = null;
        Cargar();
    }
}
