using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using SigWeb.Data.Seguridad;

namespace SigWeb.Components.Admin;

// Base de las pantallas de Administración: cada operación abre su propio scope (DbContext central nuevo,
// sin estado compartido en el circuito), valida de nuevo que quien opera sea administrador y deja el mensaje.
public abstract class PaginaAdmin : ComponentBase
{
    [Inject] protected UsuarioActual Actual { get; set; } = default!;
    [Inject] protected IServiceScopeFactory Scopes { get; set; } = default!;
    [Inject] protected ServicioPermisos Permisos { get; set; } = default!;
    [Inject] protected ServicioProyectos Proyectos { get; set; } = default!;
    [Inject] protected AuditoriaAccesos Auditoria { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected ILogger<PaginaAdmin> Log { get; set; } = default!;

    protected string? Mensaje;
    protected bool MensajeError;
    protected bool Ocupado;

    // Pantallas que también puede consultar el auditor global (solo lectura).
    protected virtual bool PermiteAuditor => false;
    protected bool Autorizado => Actual.EsAdministrador || PermiteAuditor && Actual.EsAuditor;
    protected bool PuedeModificar => Actual.EsAdministrador && !Actual.VeComoOtro;

    protected async Task<T> ConDb<T>(Func<SeguridadDbContext, IServiceProvider, Task<T>> f)
    {
        using var scope = Scopes.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<SeguridadDbContext>(), scope.ServiceProvider);
    }

    protected Task ConDb(Func<SeguridadDbContext, IServiceProvider, Task> f) =>
        ConDb<bool>(async (db, sp) => { await f(db, sp); return true; });

    protected static UserManager<UsuarioSig> Usuarios(IServiceProvider sp) => sp.GetRequiredService<UserManager<UsuarioSig>>();

    // Ejecuta un cambio: exige administrador, captura errores sin tumbar la página y anota el mensaje.
    protected async Task<bool> Cambiar(Func<Task> f, string? ok = null)
    {
        if (!PuedeModificar) { Error("Solo el administrador del SGC puede hacer este cambio."); return false; }
        if (Ocupado) return false;
        Ocupado = true;
        try
        {
            await f();
            if (ok is not null) Exito(ok);
            return true;
        }
        catch (OperacionAdminException ex) { Error(ex.Message); return false; }
        catch (Exception ex)
        {
            Log.LogError(ex, "Error en Administración");
            Error("No se pudo completar: " + (ex.InnerException?.Message ?? ex.Message));
            return false;
        }
        finally { Ocupado = false; }
    }

    protected void Exito(string m) { Mensaje = m; MensajeError = false; }
    protected void Error(string m) { Mensaje = m; MensajeError = true; }
    protected void Registrar(string evento, string? detalle, bool exito = true) => Auditoria.Registrar(evento, exito, detalle, Actual.Sesion);

    protected static string Fecha(DateTime? d) => d is null ? "—" : d.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    protected static string Dia(DateTime? d) => d is null ? "—" : d.Value.ToLocalTime().ToString("dd/MM/yyyy");
}

// Error esperado de una operación (validación): se muestra tal cual.
public sealed class OperacionAdminException(string mensaje) : Exception(mensaje);
