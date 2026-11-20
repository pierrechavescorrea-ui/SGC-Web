using Microsoft.EntityFrameworkCore;
using Npgsql;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Notificaciones;

// Revisa las reglas de avisos cada 5 minutos y procesa la cola de envíos. Con varias instancias del servidor solo una lo hace
// a la vez (candado consultivo de PostgreSQL en la base central), así nadie recibe el mismo correo dos veces.
// Notificaciones:Activas = false lo apaga (p. ej. en una copia de pruebas con datos reales).
public sealed class TrabajadorNotificaciones : BackgroundService
{
    private const long Candado = 0x5347_4E6F_7469_66;   // "SGNotif"
    private readonly ServicioNotificaciones _svc;
    private readonly ServicioProyectos _proyectos;
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TrabajadorNotificaciones> _log;

    public TrabajadorNotificaciones(ServicioNotificaciones svc, ServicioProyectos proyectos, IServiceScopeFactory scopes, IConfiguration cfg,
        ILogger<TrabajadorNotificaciones> log)
    {
        _svc = svc; _proyectos = proyectos; _scopes = scopes; _cfg = cfg; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_cfg.GetValue("Notificaciones:Activas", true)) { _log.LogInformation("Avisos por correo apagados (Notificaciones:Activas = false)."); return; }
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
        using var reloj = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try { await CicloAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { _log.LogError(ex, "Avisos por correo: error en el ciclo"); }
        }
        while (await reloj.WaitForNextTickAsync(ct));
    }

    private async Task CicloAsync(CancellationToken ct)
    {
        await using var cn = new NpgsqlConnection(_proyectos.CadenaCentral());
        await cn.OpenAsync(ct);
        await using (var cmd = new NpgsqlCommand("select pg_try_advisory_lock(@k)", cn))
        {
            cmd.Parameters.AddWithValue("k", Candado);
            if (await cmd.ExecuteScalarAsync(ct) is not true) return;   // otra instancia está en ello
        }
        try
        {
            List<ReglaNotificacion> reglas;
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
                reglas = await db.ReglasNotificacion.AsNoTracking().Where(r => r.Activa).ToListAsync(ct);
            }
            var ahora = _svc.AhoraLocal;
            foreach (var r in reglas.Where(r => _svc.Toca(r, ahora)))
            {
                if (_proyectos.Activo(r.ProyectoId) is null) continue;
                try
                {
                    var n = await _svc.EjecutarAsync(r.ProyectoId, r, null, ct);
                    _log.LogInformation("Aviso {Tipo} del proyecto {Proyecto}: {N} correo(s)", r.Tipo, r.ProyectoId, n);
                }
                catch (Exception ex) { _log.LogError(ex, "Aviso {Tipo} del proyecto {Proyecto} falló", r.Tipo, r.ProyectoId); }
            }
            await _svc.ProcesarColaAsync(ct);
        }
        finally
        {
            await using var cmd = new NpgsqlCommand("select pg_advisory_unlock(@k)", cn);
            cmd.Parameters.AddWithValue("k", Candado);
            await cmd.ExecuteScalarAsync(CancellationToken.None);
        }
    }
}
