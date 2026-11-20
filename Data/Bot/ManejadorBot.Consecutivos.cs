using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Comunicados;
using SigWeb.Models;
using static SigWeb.Data.Bot.ClienteTelegram;

namespace SigWeb.Data.Bot;

// /consecutivo: pedir un consecutivo de comunicado de salida paso a paso (los mismos datos del listado de
// Comunicados): plantilla → fecha de envío → asunto → ¿responde a un comunicado? (cuál) → dirigido a →
// ¿requiere respuesta? → responsable de responder → confirmar. Reserva con ServicioConsecutivos (Origen Telegram).
// /misconsecutivos: los reservados por este chat sin confirmar → ✅ Ya lo envié (fecha) / ❌ Anular (motivo).
// Estado de la conversación en ServicioBotTelegram.Conversaciones (memoria, 30 min). Callbacks "cs:…".
public sealed partial class ManejadorBot
{
    public sealed class ConversacionConsecutivo
    {
        public string Paso = "";                 // plantilla | fecha | fecha-texto | asunto | responde | responde-buscar | dirigido | requiere | responsable | responsable-texto | confirmar | envio-fecha | anular-motivo
        public DateTime Vence = DateTime.Now.AddMinutes(30);
        public int PlantillaId;
        public DateTime Fecha = DateTime.Today;
        public string Asunto = "";
        public string? RespondeA;
        public string? DirigidoA;
        public bool? RequiereRespuesta;
        public string? Responsable;
        public List<string> SugerenciasResponsable = new();
        public int ReservaId;                    // para confirmar envío / anular
    }

    private ConversacionConsecutivo? Conversacion(long chat) =>
        _bot.Conversaciones.TryGetValue(chat, out var c) && c.Vence > DateTime.Now ? c : null;

    private void Guardar(long chat, ConversacionConsecutivo c) { c.Vence = DateTime.Now.AddMinutes(30); _bot.Conversaciones[chat] = c; }

    private void Terminar(long chat) => _bot.Conversaciones.TryRemove(chat, out _);

    private static readonly List<Boton> BotonCancelar = new() { new("✖️ Cancelar", "cs:x") };

    // ---------- Inicio ----------
    private async Task ConsecutivoInicioAsync(long chat, CancellationToken ct)
    {
        List<PlantillaConsecutivo> plantillas;
        try { plantillas = await ServicioConsecutivos.PlantillasAsync(_db); }
        catch { await _tg.EnviarAsync(chat, "Los consecutivos aún no están disponibles (falta aplicar la migración en SGC Web).", null, ct); return; }
        if (plantillas.Count == 0)
        {
            await _tg.EnviarAsync(chat, "No hay plantilla de consecutivo configurada. Créala en SGC Web › Comunicados › Consecutivos.", null, ct);
            return;
        }
        var c = new ConversacionConsecutivo();
        if (plantillas.Count == 1) { c.PlantillaId = plantillas[0].Id; await PedirFechaAsync(chat, c, ct); return; }
        c.Paso = "plantilla"; Guardar(chat, c);
        var kb = plantillas.Select(p => new List<Boton> { new(p.Prefijo, $"cs:p:{p.Id}") }).ToList();
        kb.Add(BotonCancelar);
        await _tg.EnviarAsync(chat, "📨 <b>Nuevo consecutivo</b>\n¿Para cuál destinatario/proyecto?", kb, ct);
    }

    private async Task PedirFechaAsync(long chat, ConversacionConsecutivo c, CancellationToken ct)
    {
        c.Paso = "fecha"; Guardar(chat, c);
        var hoy = DateTime.Today;
        await _tg.EnviarAsync(chat, "📨 <b>Nuevo consecutivo</b>\n1/6 · <b>Fecha de envío</b>", new()
        {
            new() { new($"Hoy {hoy:dd/MM}", "cs:f:0"), new($"Mañana {hoy.AddDays(1):dd/MM}", "cs:f:1") },
            new() { new("Otra fecha…", "cs:f:x") },
            BotonCancelar,
        }, ct);
    }

    private Task PedirAsuntoAsync(long chat, ConversacionConsecutivo c, CancellationToken ct)
    {
        c.Paso = "asunto"; Guardar(chat, c);
        return _tg.EnviarAsync(chat, $"2/6 · <b>Asunto o descripción</b> del comunicado (envío {c.Fecha:dd/MM/yyyy}). Escríbelo:", new() { BotonCancelar }, ct);
    }

    private Task PedirRespondeAsync(long chat, ConversacionConsecutivo c, CancellationToken ct)
    {
        c.Paso = "responde"; Guardar(chat, c);
        return _tg.EnviarAsync(chat, "3/6 · ¿<b>Responde a un comunicado</b>?", new()
        {
            new() { new("Sí", "cs:r:si"), new("No", "cs:r:no") }, BotonCancelar,
        }, ct);
    }

    private Task PedirDirigidoAsync(long chat, ConversacionConsecutivo c, CancellationToken ct)
    {
        c.Paso = "dirigido"; Guardar(chat, c);
        return _tg.EnviarAsync(chat, "4/6 · ¿<b>Dirigido a</b> quién? (persona o cargo). Escríbelo u omítelo:",
            new() { new() { new("Omitir", "cs:d:-") }, BotonCancelar }, ct);
    }

    private Task PedirRequiereAsync(long chat, ConversacionConsecutivo c, CancellationToken ct)
    {
        c.Paso = "requiere"; Guardar(chat, c);
        return _tg.EnviarAsync(chat, "5/6 · ¿<b>Requiere respuesta</b>?", new()
        {
            new() { new("Sí", "cs:q:si"), new("No", "cs:q:no") }, BotonCancelar,
        }, ct);
    }

    private async Task PedirResponsableAsync(long chat, ConversacionConsecutivo c, CancellationToken ct)
    {
        if (c.RequiereRespuesta != true) { c.Responsable = null; await MostrarResumenAsync(chat, c, ct); return; }
        var p = await _db.PlantillasConsecutivo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.PlantillaId);
        var cfg = await _db.Configuracion.AsNoTracking().Select(x => x.ComunicadosRespondeSalida).FirstOrDefaultAsync();
        c.SugerenciasResponsable = new[] { p?.Destinatario, cfg }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
        c.Paso = "responsable"; Guardar(chat, c);
        var kb = c.SugerenciasResponsable.Select((s, i) => new List<Boton> { new(s, $"cs:rr:{i}") }).ToList();
        kb.Add(new() { new("Otro…", "cs:rr:x") });
        kb.Add(BotonCancelar);
        await _tg.EnviarAsync(chat, "6/6 · ¿<b>Responsable de responder</b>?", kb, ct);
    }

    private async Task MostrarResumenAsync(long chat, ConversacionConsecutivo c, CancellationToken ct)
    {
        c.Paso = "confirmar"; Guardar(chat, c);
        var p = await _db.PlantillasConsecutivo.AsNoTracking().FirstAsync(x => x.Id == c.PlantillaId);
        var anio = ServicioConsecutivos.Anio2(c.Fecha);
        var siguiente = await ServicioConsecutivos.SiguienteNumeroAsync(_db, p, anio);
        var sb = new StringBuilder("📨 <b>Revisa y confirma</b>\n\n");
        sb.Append($"Código: <code>{H(ServicioConsecutivos.Codigo(p, siguiente, anio))}</code> <i>(el número se fija al reservar)</i>\n");
        sb.Append($"Fecha de envío: <b>{c.Fecha:dd/MM/yyyy}</b>\n");
        sb.Append($"Asunto: {H(c.Asunto)}\n");
        sb.Append($"Responde a: {(c.RespondeA is null ? "—" : $"<code>{H(c.RespondeA)}</code>")}\n");
        sb.Append($"Dirigido a: {H(c.DirigidoA ?? "—")}\n");
        sb.Append($"Requiere respuesta: {(c.RequiereRespuesta == true ? "Sí" : c.RequiereRespuesta == false ? "No" : "—")}\n");
        if (c.RequiereRespuesta == true) sb.Append($"Responsable de responder: {H(c.Responsable ?? "—")}\n");
        await _tg.EnviarAsync(chat, sb.ToString(), new() { new() { new("✅ Reservar consecutivo", "cs:ok") }, BotonCancelar }, ct);
    }

    // ---------- Texto escrito durante la conversación ----------
    private async Task<bool> ConsecutivoTextoAsync(long chat, UsuarioBot usuario, string texto, CancellationToken ct)
    {
        var c = Conversacion(chat);
        if (c is null) return false;
        switch (c.Paso)
        {
            case "fecha-texto":
                if (!DateTime.TryParseExact(texto.Trim(), new[] { "d/M/yyyy", "d/M/yy", "d-M-yyyy", "yyyy-MM-dd", "d/M" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                { await _tg.EnviarAsync(chat, "No entendí la fecha. Escríbela así: <code>18/11/2026</code>", new() { BotonCancelar }, ct); return true; }
                if (f.Year < 2000) f = new DateTime(DateTime.Today.Year, f.Month, f.Day);
                c.Fecha = f.Date; await PedirAsuntoAsync(chat, c, ct); return true;
            case "asunto":
                if (texto.Trim().Length < 3) { await _tg.EnviarAsync(chat, "Escribe un asunto un poco más descriptivo.", new() { BotonCancelar }, ct); return true; }
                c.Asunto = texto.Trim(); await PedirRespondeAsync(chat, c, ct); return true;
            case "responde-buscar":
            {
                var l = await ServicioConsecutivos.BuscarPorResponderAsync(_db, texto);
                if (l.Count == 0) { await _tg.EnviarAsync(chat, "No encontré comunicados con eso. Escribe otra parte del consecutivo o del asunto:", new() { new() { new("No responde a ninguno", "cs:ra:0") }, BotonCancelar }, ct); return true; }
                var sb = new StringBuilder("¿Cuál?\n\n");
                for (int i = 0; i < l.Count; i++)
                    sb.Append($"{i + 1}. <code>{H(l[i].Consecutivo)}</code> · {l[i].Fecha:dd/MM/yyyy}" +
                              (string.IsNullOrWhiteSpace(l[i].ConsecutivoRespuesta) ? "" : " · <i>ya respondido</i>") +
                              $"\n    {H(Recortar(l[i].Descripcion, 70))}\n");
                var kb = l.Select((x, i) => new Boton($"{i + 1}. {Recortar(x.Consecutivo, 22)}", $"cs:ra:{x.Id}")).Chunk(2).Select(r => r.ToList()).ToList();
                kb.Add(new() { new("No responde a ninguno", "cs:ra:0") });
                kb.Add(BotonCancelar);
                await _tg.EnviarAsync(chat, sb.ToString(), kb, ct);
                return true;
            }
            case "dirigido":
                c.DirigidoA = texto.Trim(); await PedirRequiereAsync(chat, c, ct); return true;
            case "responsable-texto":
                c.Responsable = texto.Trim(); await MostrarResumenAsync(chat, c, ct); return true;
            case "envio-fecha":
                if (!DateTime.TryParseExact(texto.Trim(), new[] { "d/M/yyyy", "d/M/yy", "yyyy-MM-dd", "d/M" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fe))
                { await _tg.EnviarAsync(chat, "No entendí la fecha. Escríbela así: <code>18/11/2026</code>", new() { BotonCancelar }, ct); return true; }
                if (fe.Year < 2000) fe = new DateTime(DateTime.Today.Year, fe.Month, fe.Day);
                await ConfirmarEnvioAsync(chat, c.ReservaId, fe, usuario, ct); return true;
            case "anular-motivo":
                try
                {
                    await ServicioConsecutivos.AnularAsync(_db, c.ReservaId, texto, $"{usuario.Nombre} (Telegram)");
                    Terminar(chat);
                    await _tg.EnviarAsync(chat, "❌ Consecutivo anulado. El número no se reutiliza.", null, ct);
                }
                catch (InvalidOperationException ex) { Terminar(chat); await _tg.EnviarAsync(chat, H(ex.Message), null, ct); }
                return true;
            default:
                // Se espera un botón: recordar la pregunta.
                await _tg.EnviarAsync(chat, "Responde con los botones de arriba, o cancela con /cancelar.", null, ct);
                return true;
        }
    }

    // ---------- Botones "cs:…" ----------
    private async Task ConsecutivoBotonAsync(long chat, string[] p, UsuarioBot usuario, CancellationToken ct)
    {
        var accion = p.Length > 1 ? p[1] : "";
        var arg = p.Length > 2 ? p[2] : "";
        if (accion == "x") { Terminar(chat); await _tg.EnviarAsync(chat, "Cancelado.", null, ct); return; }

        // Acciones sobre una reserva ya hecha (no dependen de la conversación).
        if (accion == "env" && int.TryParse(arg, out var idEnv)) { await PreguntarFechaEnvioAsync(chat, idEnv, ct); return; }
        if (accion == "envf" && int.TryParse(arg, out var idEf))
        {
            var r = await _db.ReservasConsecutivo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == idEf);
            if (r is null) return;
            if (p.Length > 3 && p[3] == "x")
            {
                Guardar(chat, new ConversacionConsecutivo { Paso = "envio-fecha", ReservaId = idEf });
                await _tg.EnviarAsync(chat, "Escribe la fecha real de envío (dd/mm/aaaa):", new() { BotonCancelar }, ct);
                return;
            }
            var fecha = p.Length > 3 && p[3] == "p" ? r.FechaEnvioPrevista : DateTime.Today;
            await ConfirmarEnvioAsync(chat, idEf, fecha, usuario, ct);
            return;
        }
        if (accion == "an" && int.TryParse(arg, out var idAn))
        {
            var r = await _db.ReservasConsecutivo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == idAn);
            if (r is null || r.Estado != EstadoConsecutivo.Reservado) { await _tg.EnviarAsync(chat, "Ese consecutivo ya no está reservado.", null, ct); return; }
            Guardar(chat, new ConversacionConsecutivo { Paso = "anular-motivo", ReservaId = idAn });
            await _tg.EnviarAsync(chat, $"¿Por qué se anula <code>{H(r.Codigo)}</code>? Escribe el motivo:", new() { BotonCancelar }, ct);
            return;
        }

        var c = Conversacion(chat);
        if (c is null) { await _tg.EnviarAsync(chat, "Esa solicitud ya venció. Empieza de nuevo con /consecutivo.", null, ct); return; }
        switch (accion)
        {
            case "p" when int.TryParse(arg, out var pid):
                c.PlantillaId = pid; await PedirFechaAsync(chat, c, ct); break;
            case "f":
                if (arg == "x") { c.Paso = "fecha-texto"; Guardar(chat, c); await _tg.EnviarAsync(chat, "Escribe la fecha de envío (dd/mm/aaaa):", new() { BotonCancelar }, ct); }
                else { c.Fecha = DateTime.Today.AddDays(arg == "1" ? 1 : 0); await PedirAsuntoAsync(chat, c, ct); }
                break;
            case "r":
                if (arg == "si") { c.Paso = "responde-buscar"; Guardar(chat, c); await _tg.EnviarAsync(chat, "Escribe parte del <b>consecutivo</b> o del asunto del comunicado que respondes:", new() { BotonCancelar }, ct); }
                else { c.RespondeA = null; await PedirDirigidoAsync(chat, c, ct); }
                break;
            case "ra" when int.TryParse(arg, out var cid):
                c.RespondeA = cid == 0 ? null : (await _db.Comunicados.AsNoTracking().Where(x => x.Id == cid).Select(x => x.Consecutivo).FirstOrDefaultAsync());
                await PedirDirigidoAsync(chat, c, ct); break;
            case "d":
                c.DirigidoA = null; await PedirRequiereAsync(chat, c, ct); break;
            case "q":
                c.RequiereRespuesta = arg == "si"; await PedirResponsableAsync(chat, c, ct); break;
            case "rr":
                if (arg == "x") { c.Paso = "responsable-texto"; Guardar(chat, c); await _tg.EnviarAsync(chat, "Escribe quién debe responder:", new() { BotonCancelar }, ct); }
                else if (int.TryParse(arg, out var ir) && ir >= 0 && ir < c.SugerenciasResponsable.Count) { c.Responsable = c.SugerenciasResponsable[ir]; await MostrarResumenAsync(chat, c, ct); }
                break;
            case "ok":
                if (c.Paso != "confirmar") break;
                try
                {
                    var r = await ServicioConsecutivos.ReservarAsync(_db, new ServicioConsecutivos.DatosReserva(
                        c.PlantillaId, c.Fecha, c.Asunto, c.DirigidoA, "Externa", c.RespondeA, c.RequiereRespuesta, c.Responsable, null,
                        usuario.Nombre, "Telegram", chat));
                    Terminar(chat);
                    await _tg.EnviarAsync(chat,
                        $"✅ Tu consecutivo:\n\n<code>{H(r.Codigo)}</code>\n\n" +
                        $"Queda <b>reservado</b> a tu nombre para el {r.FechaEnvioPrevista:dd/MM/yyyy}. Cuando lo envíes, confírmalo aquí " +
                        "(o con /misconsecutivos); si no lo usas, anúlalo.",
                        new() { new() { new("✅ Ya lo envié", $"cs:env:{r.Id}"), new("❌ Anular", $"cs:an:{r.Id}") } }, ct);
                }
                catch (InvalidOperationException ex) { await _tg.EnviarAsync(chat, H(ex.Message), null, ct); }
                break;
        }
    }

    private async Task PreguntarFechaEnvioAsync(long chat, int reservaId, CancellationToken ct)
    {
        var r = await _db.ReservasConsecutivo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reservaId);
        if (r is null) return;
        if (r.Estado != EstadoConsecutivo.Reservado) { await _tg.EnviarAsync(chat, $"<code>{H(r.Codigo)}</code> ya está {(r.Estado == EstadoConsecutivo.Enviado ? "confirmado" : "anulado")}.", null, ct); return; }
        var kb = new List<List<Boton>> { new() { new($"Hoy {DateTime.Today:dd/MM}", $"cs:envf:{r.Id}:h") } };
        if (r.FechaEnvioPrevista.Date != DateTime.Today) kb[0].Add(new($"El {r.FechaEnvioPrevista:dd/MM} (previsto)", $"cs:envf:{r.Id}:p"));
        kb.Add(new() { new("Otra fecha…", $"cs:envf:{r.Id}:x") });
        kb.Add(BotonCancelar);
        await _tg.EnviarAsync(chat, $"¿Cuándo se envió <code>{H(r.Codigo)}</code>?", kb, ct);
    }

    private async Task ConfirmarEnvioAsync(long chat, int reservaId, DateTime fecha, UsuarioBot usuario, CancellationToken ct)
    {
        try
        {
            var com = await ServicioConsecutivos.ConfirmarEnvioAsync(_db, reservaId, fecha, $"{usuario.Nombre} (Telegram)");
            Terminar(chat);
            await _tg.EnviarAsync(chat, $"📬 <code>{H(com.Consecutivo)}</code> confirmado como enviado el {fecha:dd/MM/yyyy}. Ya está en el listado de Comunicados.", null, ct);
        }
        catch (InvalidOperationException ex) { Terminar(chat); await _tg.EnviarAsync(chat, H(ex.Message), null, ct); }
    }

    // ---------- /misconsecutivos ----------
    private async Task MisConsecutivosAsync(long chat, CancellationToken ct)
    {
        List<ReservaConsecutivo> l;
        try
        {
            await ServicioConsecutivos.ConciliarAsync(_db);
            l = await _db.ReservasConsecutivo.AsNoTracking().Where(r => r.ChatIdTelegram == chat && r.Estado == EstadoConsecutivo.Reservado)
                .OrderBy(r => r.FechaEnvioPrevista).ThenBy(r => r.Numero).Take(15).ToListAsync();
        }
        catch { await _tg.EnviarAsync(chat, "Los consecutivos aún no están disponibles (falta aplicar la migración en SGC Web).", null, ct); return; }
        if (l.Count == 0) { await _tg.EnviarAsync(chat, "No tienes consecutivos pendientes de confirmar. Pide uno con /consecutivo.", null, ct); return; }
        var sb = new StringBuilder($"📨 <b>Tus consecutivos sin confirmar</b> ({l.Count})\n\n");
        var kb = new List<List<Boton>>();
        for (int i = 0; i < l.Count; i++)
        {
            var r = l[i];
            var atrasado = r.FechaEnvioPrevista.Date < DateTime.Today ? " ⚠️" : "";
            sb.Append($"{i + 1}. <code>{H(r.Codigo)}</code> · {r.FechaEnvioPrevista:dd/MM}{atrasado}\n    {H(Recortar(r.Asunto, 60))}\n");
            kb.Add(new() { new($"✅ {i + 1} enviado", $"cs:env:{r.Id}"), new($"❌ {i + 1} anular", $"cs:an:{r.Id}") });
        }
        await _tg.EnviarAsync(chat, sb.ToString(), kb, ct);
    }
}
