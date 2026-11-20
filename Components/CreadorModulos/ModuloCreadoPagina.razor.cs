using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.CreadorModulos;
using SigWeb.Data.Navegacion;
using SigWeb.Data.Seguridad;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Components.CreadorModulos;

// Página de un módulo creado por el administrador (/mod/{clave}): lista con la tabla de Campos (columnas, fórmulas y formatos
// que configure el usuario) y el registro por pasos (?registro={id}|nuevo). Todo sale de la definición publicada.
public partial class ModuloCreadoPagina
{
    [Inject] private RegistroModulosCreados Registro { get; set; } = default!;
    [Inject] private ServicioRegistrosModulo Registros { get; set; } = default!;
    [Inject] private AppDbContext Db { get; set; } = default!;
    [Inject] private UsuarioActual Usuario { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<ModuloCreadoPagina> Log { get; set; } = default!;

    [Parameter] public string Clave { get; set; } = "";
    [SupplyParameterFromQuery(Name = "registro")] public string? RegistroQuery { get; set; }
    // Registro nuevo ya enlazado: ?registro=nuevo&vincular={fuente}:{id} (desde la hoja de vida de una torre, por ejemplo).
    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQuery { get; set; }
    [SupplyParameterFromQuery(Name = "vincular")] public string? VincularQuery { get; set; }
    [Inject] private ServicioVinculos Vinculos { get; set; } = default!;

    private RegistroModulosCreados.Publicado? modulo;
    private List<RegistroModulo> registros = new();
    private MotorTabla? motor;
    private ServicioRegistrosModulo.Edicion? edicion;
    private bool cargando = true, verCampos, ocupado, cambios, pidiendoEliminar, mensajeError;
    private string? mensaje, buscar, filtroEstado, ordenClave, claveCargada, registroAbierto;
    private bool ordenDesc;
    private int limite = 200;
    private bool vistaTablero;

    private string Permiso => modulo?.ClavePermiso ?? "";
    private bool PuedeVer => Usuario.Puede(Permiso, Accion.Ver);
    private bool PuedeCrear => Usuario.Puede(Permiso, Accion.Crear);
    private bool PuedeEditar => Usuario.Puede(Permiso, Accion.Editar);
    private bool PuedeEliminar => Usuario.Puede(Permiso, Accion.Eliminar);
    private bool PuedeAprobar => Usuario.Puede(Permiso, Accion.Aprobar) || Usuario.Puede(Permiso, Accion.Administrar);
    private bool PuedeDescargar => Usuario.Puede(Permiso, Accion.Descargar);
    private bool PuedeCampos => Usuario.Puede(Permiso, Accion.ConfigurarCampos);

    // Un registro en estado final solo lo cambia quien puede Aprobar; un registro nuevo lo llena quien puede Crear.
    private bool SoloLecturaRegistro
    {
        get
        {
            if (edicion is null || modulo is null) return true;
            if (edicion.Id == 0) return !PuedeCrear;
            if (!PuedeEditar) return true;
            var final = modulo.Definicion.Estados.FirstOrDefault(s => s.Nombre == edicion.Estado)?.Final == true;
            return final && !PuedeAprobar;
        }
    }

    private string TituloRegistro
    {
        get
        {
            if (edicion is null || modulo is null) return "";
            var t = modulo.Definicion.CampoQueTitula is { } c ? edicion.Valor(c.Clave) : null;
            return string.IsNullOrWhiteSpace(t) ? edicion.Codigo : $"{edicion.Codigo} · {t}";
        }
    }

    protected override void OnInitialized() => Registro.Cambio += AlCambiarDefinicion;
    public void Dispose() => Registro.Cambio -= AlCambiarDefinicion;

    // El administrador publicó otra versión mientras la página estaba abierta: se recarga la definición (no se pierde lo digitado).
    private void AlCambiarDefinicion() => _ = InvokeAsync(() =>
    {
        var nuevo = Registro.De(Clave);
        if (nuevo is null || modulo is null || nuevo.Version == modulo.Version) return;
        modulo = nuevo;
        StateHasChanged();
    });

    protected override async Task OnParametersSetAsync()
    {
        if (!string.Equals(claveCargada, Clave, StringComparison.OrdinalIgnoreCase))
        {
            claveCargada = Clave;
            modulo = Registro.De(Clave);
            edicion = null; registroAbierto = null; motor = null; registros.Clear();
            mensaje = null; buscar = null; filtroEstado = null; verCampos = false; vistaTablero = VistaQuery == "tablero";
            if (modulo is null || !PuedeVer) { cargando = false; return; }
            await CargarAsync();
        }
        if (modulo is null || !PuedeVer) return;
        if (RegistroQuery != registroAbierto) await AbrirDesdeUrlAsync();
    }

    private async Task CargarAsync()
    {
        if (modulo is null) return;
        cargando = true;
        try
        {
            await Registros.PrepararTablaAsync(modulo);
            registros = await Db.RegistrosModulo.AsNoTracking().Where(r => r.Modulo == modulo.Clave).OrderByDescending(r => r.Consecutivo).ToListAsync();
            var porId = registros.ToDictionary(r => r.Id);
            motor = await MotorTabla.CargarAsync(Db, modulo.Tabla, ServicioRegistrosModulo.ColumnasNativas(porId, modulo.Definicion),
                registros.Select(r => (r.Id, r.Codigo)));
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Módulo creado {Clave}: no se pudo cargar", modulo.Clave);
            Error("No se pudo cargar el módulo. Si es la primera vez, puede faltar la migración de la base del proyecto (RegistrosModulo).");
            motor = null;
        }
        finally { cargando = false; }
    }

    private async Task AbrirDesdeUrlAsync()
    {
        registroAbierto = RegistroQuery;
        cambios = false; pidiendoEliminar = false;
        if (modulo is null) return;
        if (string.IsNullOrEmpty(RegistroQuery)) { edicion = null; return; }
        if (RegistroQuery == "nuevo")
        {
            if (!PuedeCrear) { edicion = null; Error("No tiene permiso para crear registros en este módulo."); return; }
            edicion = Registros.Nueva(modulo.Definicion);
            mensaje = null;
            await PrevincularAsync(edicion);
            return;
        }
        if (int.TryParse(RegistroQuery, out var id))
        {
            edicion = await Registros.CargarAsync(modulo, id);
            mensaje = null;
            if (edicion is null) Error("El registro no existe o está en la Papelera.");
        }
    }

    private async Task PrevincularAsync(ServicioRegistrosModulo.Edicion e)
    {
        if (modulo is null || string.IsNullOrWhiteSpace(VincularQuery)) return;
        var partes = VincularQuery.Split(':');
        if (partes.Length != 2 || !int.TryParse(partes[1], out var id)) return;
        var campo = modulo.Definicion.Campos.FirstOrDefault(c => c.Tipo == TipoCampoModulo.Vinculo && c.Fuente == partes[0]);
        if (campo is null) return;
        try
        {
            var etiquetas = await Vinculos.EtiquetasAsync(partes[0], new[] { id });
            if (etiquetas.TryGetValue(id, out var etq)) e.VinculosDe(campo.Clave).Add(new VinculoElegido(partes[0], id, etq));
        }
        catch { /* sin permiso o fuente no disponible: el usuario lo elige a mano */ }
    }

    // ---------- Lista ----------

    private IEnumerable<CampoTabla> Columnas => motor?.Visibles ?? Enumerable.Empty<CampoTabla>();

    private IEnumerable<RegistroModulo> Mostrados
    {
        get
        {
            IEnumerable<RegistroModulo> q = registros;
            if (filtroEstado is not null) q = q.Where(r => r.Estado == filtroEstado);
            if (!string.IsNullOrWhiteSpace(buscar) && motor is not null)
            {
                var palabras = DefinicionModulo.Normalizar(buscar).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var cols = motor.Visibles.ToList();
                q = q.Where(r =>
                {
                    var texto = DefinicionModulo.Normalizar(r.Codigo + " " + r.Estado + " " + string.Join(" ", cols.Select(c => motor.Texto(r.Id, c, out _))));
                    return palabras.All(p => texto.Contains(p));
                });
            }
            if (ordenClave is not null && motor?.PorClave(ordenClave) is { } col)
            {
                Func<RegistroModulo, object?> k = col.Tipo switch
                {
                    TipoCampoTabla.Numero => r => motor.Valor(r.Id, col) is { } v ? Convert.ToDecimal(v) : (decimal?)null,
                    TipoCampoTabla.Fecha => r => motor.Valor(r.Id, col) as DateTime?,
                    _ => r => col.Clave == "Codigo" ? r.Consecutivo : motor.Texto(r.Id, col, out _),
                };
                q = ordenDesc ? q.OrderByDescending(k) : q.OrderBy(k);
            }
            return q;
        }
    }

    private void Ordenar(string clave)
    {
        if (ordenClave == clave) ordenDesc = !ordenDesc; else { ordenClave = clave; ordenDesc = false; }
    }

    private string Flecha(string clave) => ordenClave != clave ? "" : ordenDesc ? " ▼" : " ▲";

    private async Task CambiarVista(bool tablero)
    {
        vistaTablero = tablero;
        try { await JS.RecordarVistaAsync("/" + modulo!.Ruta, new Dictionary<string, object?> { ["vista"] = tablero ? "tablero" : null }); } catch { }
    }

    // Clic en una cifra del tablero: la lista filtrada por ese estado.
    private async Task VerEstado(string estado)
    {
        filtroEstado = estado;
        await CambiarVista(false);
    }

    // ---------- Registro ----------

    private Task NuevoAsync() { Nav.NavigateTo($"{modulo!.Ruta}?registro=nuevo"); return Task.CompletedTask; }

    private Task AbrirAsync(int id) { Nav.NavigateTo($"{modulo!.Ruta}?registro={id}"); return Task.CompletedTask; }

    private async Task CerrarRegistroAsync()
    {
        if (edicion is not null) Registros.DescartarSubidos(edicion.Archivos.Values.SelectMany(l => l));
        edicion = null; cambios = false;
        // Vuelve a la pantalla anterior real (la lista con sus filtros) o, sin historial, a la lista.
        try { await JS.InvokeVoidAsync("sigVolver", "/" + modulo!.Ruta); }
        catch { Nav.NavigateTo(modulo!.Ruta); }
    }

    private void Cambio() { cambios = true; mensaje = null; }

    private bool PuedeCambiarA(EstadoModulo s)
    {
        if (SoloLecturaRegistro && !(PuedeAprobar && edicion?.Id != 0)) return false;
        return !s.Final || PuedeAprobar || edicion?.Id == 0 && !s.Final;
    }

    private void ElegirEstado(EstadoModulo s)
    {
        if (edicion is null || !PuedeCambiarA(s)) return;
        edicion.Estado = s.Nombre; cambios = true;
    }

    private async Task GuardarAsync()
    {
        if (modulo is null || edicion is null || ocupado) return;
        ocupado = true; mensaje = null;
        try
        {
            var nuevo = edicion.Id == 0;
            var id = await Registros.GuardarAsync(modulo, edicion, Usuario.Nombre);
            cambios = false;
            await CargarAsync();
            Exito(nuevo ? "Registro creado." : "Cambios guardados.");
            edicion = null; registroAbierto = null;
            try { await JS.InvokeVoidAsync("sigVolver", "/" + modulo.Ruta); }
            catch { Nav.NavigateTo(modulo.Ruta); }
            _ = id;
        }
        catch (InvalidOperationException ex) { Error(ex.Message); }
        catch (Exception ex)
        {
            Log.LogError(ex, "Módulo creado {Clave}: no se pudo guardar", modulo.Clave);
            Error("No se pudo guardar: " + (ex.InnerException?.Message ?? ex.Message));
        }
        finally { ocupado = false; }
    }

    private async Task EliminarAsync()
    {
        if (modulo is null || edicion is null) return;
        pidiendoEliminar = false;
        try
        {
            await Registros.EliminarAsync(modulo, edicion.Id);
            edicion = null; registroAbierto = null;
            await CargarAsync();
            Exito("El registro se envió a la Papelera.");
            Nav.NavigateTo(modulo.Ruta);
        }
        catch (Exception ex) { Error("No se pudo eliminar: " + (ex.InnerException?.Message ?? ex.Message)); }
    }

    // ---------- Apariencia ----------

    private static (string Fondo, string Texto) ColorEstado(EstadoModulo s)
    {
        var p = MotorTabla.Paleta.FirstOrDefault(x => x.Clave == s.Color);
        return p.Clave is null ? ("#6c757d", "#ffffff") : (p.Fondo, p.Texto);
    }

    private static string EstiloEstado(EstadoModulo s, bool activo)
    {
        var (f, t) = ColorEstado(s);
        return activo ? $"background:{f};color:{t};border-color:{f}" : $"border-color:{f}";
    }

    private static string Fecha(DateTime? d) => d is null ? "—" : d.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    private void Exito(string m) { mensaje = m; mensajeError = false; }
    private void Error(string m) { mensaje = m; mensajeError = true; }
}
