using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Seguridad;
using SigWeb.Models;
using static SigWeb.Data.Bot.ClienteTelegram;

namespace SigWeb.Data.Bot;

// /proyecto: el chat ligado a un usuario de SGC Web elige qué proyecto consulta (solo los que tiene con
// permiso "Consultas por Telegram"). Callback "px:{id}".
public sealed partial class ManejadorBot
{
    private async Task ProyectosAsync(long chat, int? elegir, CancellationToken ct)
    {
        var central = _sp.GetRequiredService<SeguridadDbContext>();
        var v = await central.VinculosTelegram.FirstOrDefaultAsync(x => x.ChatId == chat && x.Activo, ct);
        if (v?.UsuarioId is null || !await _db.UsuariosBot.AnyAsync(u => u.ChatId == chat && u.Activo, ct))
        {
            await _tg.EnviarAsync(chat, "Este chat no está ligado a un usuario de SGC Web. Para elegir proyecto, vincúlelo desde " +
                "<b>SGC Web › Mi cuenta › Bot de Telegram</b>.", null, ct);
            return;
        }
        var permisos = _sp.GetRequiredService<ServicioPermisos>();
        var proyectos = _sp.GetRequiredService<ServicioProyectos>();
        var lista = permisos.ProyectosDe(v.UsuarioId).Where(p => permisos.De(v.UsuarioId, p.Id).Puede("telegram", Accion.Ver)).ToList();

        if (elegir is int id)
        {
            if (lista.All(p => p.Id != id) || proyectos.Proyecto(id)?.Estado != EstadoProyecto.Activo || proyectos.Activo(id) is not { } activo)
            { await _tg.EnviarAsync(chat, "No tiene acceso a ese proyecto.", null, ct); return; }
            var error = await proyectos.PrepararAsync(id, ct);
            if (error is not null) { await _tg.EnviarAsync(chat, "Ese proyecto no está disponible en este momento.", null, ct); return; }
            // El chat queda autorizado también en la base del proyecto nuevo (tabla UsuariosBot, como siempre).
            await using (var db = proyectos.CrearContexto(activo))
            {
                var u = await db.UsuariosBot.FirstOrDefaultAsync(x => x.ChatId == chat, ct);
                if (u is null) { u = new UsuarioBot { ChatId = chat, Nombre = v.Nombre ?? "", FechaVinculo = DateTime.Now }; db.UsuariosBot.Add(u); }
                u.Activo = true; u.Nombre = v.Nombre ?? ""; u.UsuarioTelegram = v.UsuarioTelegram; u.UltimoUso = DateTime.Now;
                await db.SaveChangesAsync(ct);
            }
            var fila = await central.VinculosTelegram.FirstAsync(x => x.Id == v.Id, ct);
            fila.ProyectoId = id; fila.UltimoUso = DateTime.UtcNow;
            await central.SaveChangesAsync(ct);
            _sp.GetRequiredService<AuditoriaAccesos>().Registrar("Telegram: cambio de proyecto", true, activo.Codigo, usuarioId: v.UsuarioId, proyectoId: id);
            await _tg.EnviarAsync(chat, $"✅ Ahora consulta el proyecto <b>{H(activo.Nombre)}</b>.", null, ct);
            return;
        }

        if (lista.Count == 0) { await _tg.EnviarAsync(chat, "Su usuario no tiene proyectos con consultas por Telegram.", null, ct); return; }
        var kb = lista.Select(p => new List<Boton> { new((p.Id == v.ProyectoId ? "✓ " : "") + Recortar(p.Nombre, 40), $"px:{p.Id}") }).ToList();
        await _tg.EnviarAsync(chat, "¿Qué proyecto quiere consultar?", kb, ct);
    }
}
