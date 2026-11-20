using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Comunicados;
using SigWeb.Models;

namespace SigWeb.Components.Comunicados;

public partial class PanelConsecutivos
{
    [Parameter] public EventCallback OnCerrar { get; set; }
    // El listado de Comunicados cambió (envío confirmado: comunicado nuevo y respuesta anotada al original).
    [Parameter] public EventCallback OnComunicadosCambiados { get; set; }
    // Ir al listado filtrado por ese consecutivo.
    [Parameter] public EventCallback<string> OnVerComunicado { get; set; }
    private string? confirmado;
    private List<PlantillaConsecutivo> plantillas = new();
    private List<PlantillaConsecutivo> activas => plantillas.Where(p => p.Activa).ToList();
    private List<ReservaConsecutivo> reservas = new();
    private List<Comunicado> resultados = new();
    private ReservaConsecutivo? nueva, accion;
    private PlantillaConsecutivo? plantilla;
    private bool ocupado, faltaMigracion, verPlantillas, anulando, responde, busquedaRealizada;
    private string mensaje = "", busqueda = "", busquedaRespuesta = "", previsto = "", requiere = "", motivo = "";
    private string? asignado;
    private DateTime fechaReal = DateTime.Today;
    private EstadoConsecutivo? estado = EstadoConsecutivo.Reservado;
    private readonly (EstadoConsecutivo? Valor, string Texto)[] filtros = [(null,"Todos"),(EstadoConsecutivo.Reservado,"Reservados"),(EstadoConsecutivo.Enviado,"Enviados"),(EstadoConsecutivo.Anulado,"Anulados")];
    private IEnumerable<ReservaConsecutivo> Filtradas => reservas.Where(r => (estado is null || r.Estado == estado) && (string.IsNullOrWhiteSpace(busqueda) || string.Join(" ", r.Codigo, r.Asunto, r.DirigidoA, r.RespondeA, r.ResponsableRespuesta, r.SolicitadoPor, r.Origen).Contains(busqueda.Trim(), StringComparison.OrdinalIgnoreCase)));
    private static string Fecha(DateTime? fecha) => fecha?.ToString("dd/MM/yyyy") ?? "—";
    protected override Task OnInitializedAsync() => Cargar();
    private async Task Ejecutar(Func<AppDbContext, Task> trabajo)
    {
        if (ocupado) return;
        ocupado = true; mensaje = "";
        try { using var scope = Scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await trabajo(db); }
        catch (Exception ex)
        {
            var error = ex;
            while (error.InnerException is not null) error = error.InnerException;
            faltaMigracion = error is Npgsql.PostgresException pg && pg.SqlState is "42P01" or "42703";
            mensaje = faltaMigracion ? "Falta aplicar la migración de consecutivos" : ex is InvalidOperationException ? ex.Message : "No se pudo completar la operación. Intenta nuevamente.";
        }
        finally { ocupado = false; }
    }
    private async Task Leer(AppDbContext db)
    {
        plantillas = await ServicioConsecutivos.PlantillasAsync(db, false);
        reservas = await db.ReservasConsecutivo.AsNoTracking().OrderByDescending(r => r.FechaSolicitud).ThenByDescending(r => r.Id).ToListAsync();
        faltaMigracion = false;
    }
    private Task Cargar() => Ejecutar(async db =>
    {
        await ServicioConsecutivos.ConciliarAsync(db); await Leer(db);
        if (plantillas.Count == 0) { verPlantillas = true; plantilla = await ServicioConsecutivos.SugerirPlantillaAsync(db) ?? new(); }
    });
    private async Task Nuevo()
    {
        if (activas.Count == 0) { await AbrirPlantillas(); mensaje = "Confirma una plantilla activa para reservar."; return; }
        nueva = new() { PlantillaConsecutivoId = activas[0].Id, FechaEnvioPrevista = DateTime.Today, Tipo = "Externa", ResponsableRespuesta = activas[0].Destinatario };
        accion = null; asignado = null; responde = false; requiere = ""; resultados.Clear(); busquedaRespuesta = ""; busquedaRealizada = false;
        await Previsualizar();
    }
    private Task Previsualizar() => Ejecutar(async db =>
    {
        if (nueva is null) return;
        var p = activas.FirstOrDefault(p => p.Id == nueva.PlantillaConsecutivoId);
        if (p is null) { previsto = ""; return; }
        var fecha = nueva.FechaEnvioPrevista;
        var id = p.Id;
        var numero = await ServicioConsecutivos.SiguienteNumeroAsync(db, p, ServicioConsecutivos.Anio2(fecha));
        if (nueva is not null && nueva.PlantillaConsecutivoId == id && nueva.FechaEnvioPrevista == fecha) previsto = ServicioConsecutivos.Codigo(p, numero, ServicioConsecutivos.Anio2(fecha));
    });
    private async Task CambiarPlantilla() { if (nueva is not null) nueva.ResponsableRespuesta = activas.FirstOrDefault(p => p.Id == nueva.PlantillaConsecutivoId)?.Destinatario; await Previsualizar(); }
    private void LimpiarRespuesta() { if (nueva is not null) nueva.RespondeA = null; resultados.Clear(); busquedaRealizada = false; }
    private Task BuscarRespuesta() => Ejecutar(async db => { resultados = await ServicioConsecutivos.BuscarPorResponderAsync(db, busquedaRespuesta); busquedaRealizada = true; });
    private void ElegirRespuesta(Comunicado c) { if (nueva is not null) nueva.RespondeA = c.Consecutivo; resultados.Clear(); busquedaRealizada = false; }
    private Task Reservar() => Ejecutar(async db =>
    {
        if (nueva is null) return;
        if (string.IsNullOrWhiteSpace(nueva.Asunto) || string.IsNullOrWhiteSpace(nueva.SolicitadoPor)) throw new InvalidOperationException("Completa el asunto y solicitado por.");
        if (responde && string.IsNullOrWhiteSpace(nueva.RespondeA)) throw new InvalidOperationException("Selecciona el comunicado al que respondes.");
        if (nueva.FechaEnvioPrevista == default) throw new InvalidOperationException("Indica la fecha prevista.");
        var r = await ServicioConsecutivos.ReservarAsync(db, new(nueva.PlantillaConsecutivoId, nueva.FechaEnvioPrevista, nueva.Asunto, nueva.DirigidoA, nueva.Tipo, responde ? nueva.RespondeA : null, requiere == "" ? null : requiere == "si", nueva.ResponsableRespuesta, nueva.Observaciones, nueva.SolicitadoPor, "SigWeb"));
        asignado = r.Codigo; nueva = null; await Leer(db);
    });
    private async Task Copiar() { try { await JS.InvokeVoidAsync("navigator.clipboard.writeText", asignado); } catch (JSException) { mensaje = "No se pudo copiar. Selecciona el código para copiarlo."; } }
    private void AbrirAccion(ReservaConsecutivo r, bool anular) { accion = r; anulando = anular; motivo = ""; fechaReal = DateTime.Today; nueva = null; mensaje = ""; }
    private Task CerrarReserva() => Ejecutar(async db =>
    {
        if (accion is null) return;
        if (anulando) { if (string.IsNullOrWhiteSpace(motivo)) throw new InvalidOperationException("Escribe el motivo de la anulación."); await ServicioConsecutivos.AnularAsync(db, accion.Id, motivo, "SigWeb"); }
        else
        {
            if (fechaReal == default) throw new InvalidOperationException("Indica la fecha real de envío.");
            var com = await ServicioConsecutivos.ConfirmarEnvioAsync(db, accion.Id, fechaReal, "SigWeb");
            confirmado = com.Consecutivo;
            await OnComunicadosCambiados.InvokeAsync();
        }
        accion = null; await Leer(db);
    });
    private Task AbrirPlantillas() => Ejecutar(async db => { verPlantillas = !verPlantillas; if (verPlantillas && plantillas.Count == 0) plantilla = await ServicioConsecutivos.SugerirPlantillaAsync(db) ?? new(); });
    // Próximo consecutivo real de la plantilla en edición (listado + reservas + "Iniciar en"), para la vista previa.
    private string? proximoPlantilla;
    private Task CalcularProximoPlantilla() => Ejecutar(async db =>
    {
        if (plantilla is null) { proximoPlantilla = null; return; }
        var anio = ServicioConsecutivos.Anio2(DateTime.Today);
        var copia = new PlantillaConsecutivo { Id = plantilla.Id, Remite = plantilla.Remite ?? "", Destinatario = plantilla.Destinatario ?? "", Proyecto = plantilla.Proyecto ?? "",
            CodigoProyecto = plantilla.CodigoProyecto ?? "", ReiniciaCadaAnio = plantilla.ReiniciaCadaAnio, CifrasNumero = plantilla.CifrasNumero,
            NumeroInicial = plantilla.NumeroInicial, AnioNumeroInicial = anio };
        proximoPlantilla = ServicioConsecutivos.Codigo(copia, await ServicioConsecutivos.SiguienteNumeroAsync(db, copia, anio), anio);
    });

    private async Task EditarPlantilla(PlantillaConsecutivo p) { proximoPlantilla = null; plantilla = new() { Id = p.Id, Remite = p.Remite, Destinatario = p.Destinatario, Proyecto = p.Proyecto, CodigoProyecto = p.CodigoProyecto, ReiniciaCadaAnio = p.ReiniciaCadaAnio, CifrasNumero = p.CifrasNumero, NumeroInicial = p.NumeroInicial, AnioNumeroInicial = p.AnioNumeroInicial, Activa = p.Activa, Orden = p.Orden }; await CalcularProximoPlantilla(); }
    private Task GuardarPlantilla() => Ejecutar(async db =>
    {
        if (plantilla is null) return;
        var p = plantilla.Id == 0 ? new PlantillaConsecutivo() : await db.PlantillasConsecutivo.FirstAsync(p => p.Id == plantilla.Id);
        p.Remite = (plantilla.Remite ?? "").Trim().ToUpperInvariant(); p.Destinatario = (plantilla.Destinatario ?? "").Trim().ToUpperInvariant(); p.Proyecto = (plantilla.Proyecto ?? "").Trim().ToUpperInvariant(); p.CodigoProyecto = (plantilla.CodigoProyecto ?? "").Trim().ToUpperInvariant(); p.ReiniciaCadaAnio = plantilla.ReiniciaCadaAnio; p.CifrasNumero = Math.Clamp(plantilla.CifrasNumero, 0, 8);
        if (plantilla.NumeroInicial is int ini && ini > 0)
        {
            // Se fija para el año actual solo si cambió (así no se mueve el año al guardar otra cosa).
            if (p.NumeroInicial != ini || p.AnioNumeroInicial is null) p.AnioNumeroInicial = ServicioConsecutivos.Anio2(DateTime.Today);
            p.NumeroInicial = ini;
        }
        else { p.NumeroInicial = null; p.AnioNumeroInicial = null; } p.Activa = plantilla.Activa;
        if (p.Id == 0) db.PlantillasConsecutivo.Add(p);
        await db.SaveChangesAsync(); plantilla = null; await Leer(db);
    });
}
