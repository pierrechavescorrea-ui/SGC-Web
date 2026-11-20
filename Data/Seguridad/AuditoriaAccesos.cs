using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Auditoría de accesos y de cambios de seguridad (base central). Se escribe en segundo plano por una cola
// para no frenar ni mezclar el DbContext de las pantallas. Nunca se registran contraseñas, tokens ni códigos.
// Retención: Auditoria:DiasRetencion (365 por defecto); lo más antiguo se borra cada día (Ley 1581: los datos
// personales, como la IP, se guardan solo el tiempo necesario).
public sealed class AuditoriaAccesos : BackgroundService
{
    private readonly Channel<RegistroAuditoria> _cola = Channel.CreateBounded<RegistroAuditoria>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AuditoriaAccesos> _log;

    public AuditoriaAccesos(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<AuditoriaAccesos> log)
    {
        _scopes = scopes; _cfg = cfg; _log = log;
    }

    public void Registrar(string evento, bool exito, string? detalle = null, SesionSig? sesion = null,
        string? usuarioId = null, string? email = null, string? ip = null, string? navegador = null, int? proyectoId = null)
    {
        sesion ??= AmbienteSig.Actual;
        _cola.Writer.TryWrite(new RegistroAuditoria
        {
            Fecha = DateTime.UtcNow,
            Evento = Recortar(evento, 60)!,
            Exito = exito,
            UsuarioId = usuarioId ?? sesion?.UsuarioId,
            Email = Recortar(email ?? sesion?.Email, 256),
            ProyectoId = proyectoId ?? sesion?.Proyecto.Id,
            Ip = Recortar(ip ?? sesion?.Ip, 64),
            Navegador = Recortar(navegador, 200),
            Detalle = Recortar((sesion?.Impersonador is { } imp ? $"[como otro usuario; admin {imp}] " : "") + detalle, 1000),
        });
    }

    public void Registrar(HttpContext http, string evento, bool exito, string? detalle = null, string? usuarioId = null, string? email = null) =>
        Registrar(evento, exito, detalle, null, usuarioId, email, Ip(http), http.Request.Headers.UserAgent.ToString());

    public static string? Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();

    private static string? Recortar(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var ultimaPurga = DateTime.MinValue;
        var lote = new List<RegistroAuditoria>();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await _cola.Reader.WaitToReadAsync(ct)) break;
                while (lote.Count < 200 && _cola.Reader.TryRead(out var r)) lote.Add(r);
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
                db.Auditoria.AddRange(lote);
                await db.SaveChangesAsync(ct);
                lote.Clear();
                if (ultimaPurga < DateTime.UtcNow.AddDays(-1))
                {
                    var dias = Math.Max(30, _cfg.GetValue("Auditoria:DiasRetencion", 365));
                    var limite = DateTime.UtcNow.AddDays(-dias);
                    await db.Auditoria.Where(a => a.Fecha < limite).ExecuteDeleteAsync(ct);
                    ultimaPurga = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Auditoría: no se pudieron guardar {N} registros", lote.Count);
                lote.Clear();
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
