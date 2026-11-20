using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data.Bot;
using SigWeb.Data.Seguridad;
using SigWeb.Models;

namespace SigWeb.Components.Admin;

public partial class AdminTelegram
{
    [Inject] private ServicioBotTelegram Bot { get; set; } = default!;
    [Inject] private IConfiguration Cfg { get; set; } = default!;

    private sealed record UsuarioOpcion(string Id, string Nombre);
    private sealed record Chat(long ChatId, int ProyectoId, int RegistroId, bool Anterior, string Nombre,
        string? UsuarioId, string? UsuarioTelegram, bool Activo, DateTime Vinculado, DateTime? UltimoUso)
    {
        public (long, int) Clave => (ChatId, ProyectoId);
    }

    private List<Chat> chats = new();
    private List<UsuarioOpcion> usuarios = new();
    private List<string> basesNoDisponibles = new();
    private IEnumerable<Proyecto> ProyectosActivos => Proyectos.Todos().Where(p => p.Estado == EstadoProyecto.Activo);
    private bool cargando = true;
    private bool SinEdicion => Ocupado || cargando || !PuedeModificar;
    private string texto = "", codUsuario = "", codNombre = "", nombreEdicion = "", usuarioEdicion = "";
    private int filtroProyecto, codProyecto, proyectoEdicion;
    private (long, int)? editando, eliminando;

    private bool qrAmpliado;
    private string? mensajeCopia, qrEnlace, qrDatos;
    private string? EnlaceBot => string.IsNullOrWhiteSpace(Bot.UsuarioBot) ? null : $"https://t.me/{Bot.UsuarioBot}";
    private bool PermitirSinUsuario => Cfg.GetValue("Telegram:PermitirChatsSinUsuario", true);

    protected override async Task OnInitializedAsync()
    {
        if (!Autorizado) return;
        var preferido = Actual.Proyecto?.Id ?? Proyectos.Piloto()?.Id ?? 0;
        codProyecto = ProyectosActivos.FirstOrDefault(p => p.Id == preferido)?.Id ?? ProyectosActivos.FirstOrDefault()?.Id ?? 0;
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        cargando = true;
        try
        {
            var (centrales, opciones) = await ConDb(async (db, _) =>
            {
                var vinculos = await db.VinculosTelegram.AsNoTracking().ToListAsync();
                var cuentas = await db.Users.AsNoTracking()
                    .Where(u => u.Activo && !u.Anonimizado && u.Email != ArranqueSeguridad.EmailDesarrollo)
                    .OrderBy(u => u.NombreCompleto)
                    .Select(u => new UsuarioOpcion(u.Id, (u.NombreCompleto == "" ? u.Email : u.NombreCompleto) + " · " + u.Email))
                    .ToListAsync();
                return (vinculos, cuentas);
            });
            var registrados = centrales.ToDictionary(v => v.ChatId);
            var porChat = centrales.ToDictionary(v => v.ChatId, v =>
                new Chat(v.ChatId, v.ProyectoId, v.Id, false, v.Nombre ?? "", v.UsuarioId, v.UsuarioTelegram,
                    v.Activo && Proyectos.Proyecto(v.ProyectoId)?.Estado == EstadoProyecto.Activo,
                    ComoUtc(v.Vinculado), v.UltimoUso is DateTime ultimo ? ComoUtc(ultimo) : null));
            var anteriores = new List<Chat>();
            var errores = new List<string>();
            foreach (var p in ProyectosActivos)
            {
                if (Proyectos.Activo(p.Id) is not { } activo) continue;
                try
                {
                    await using var db = Proyectos.CrearContexto(activo);
                    var locales = await db.UsuariosBot.AsNoTracking().ToListAsync();
                    var ids = locales.Select(u => u.ChatId).ToHashSet();
                    foreach (var centro in centrales.Where(v => v.ProyectoId == p.Id && !ids.Contains(v.ChatId)))
                        porChat[centro.ChatId] = porChat[centro.ChatId] with { Activo = false };
                    foreach (var local in locales)
                    {
                        if (!registrados.TryGetValue(local.ChatId, out var v))
                        {
                            anteriores.Add(new Chat(local.ChatId, p.Id, local.Id, true, local.Nombre, null,
                                local.UsuarioTelegram, local.Activo, LocalAUtc(local.FechaVinculo), LocalAUtc(local.UltimoUso)));
                        }
                        else if (v.ProyectoId == p.Id)
                        {
                            var chat = porChat[local.ChatId];
                            porChat[local.ChatId] = chat with
                            {
                                Activo = chat.Activo && local.Activo,
                                UltimoUso = MasReciente(chat.UltimoUso, LocalAUtc(local.UltimoUso))
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning(ex, "No se pudieron leer los chats de Telegram del proyecto {Proyecto}", p.Id);
                    errores.Add(p.Nombre);
                }
            }
            chats = porChat.Values.Concat(anteriores).OrderByDescending(v => v.Activo)
                .ThenByDescending(v => v.UltimoUso).ThenBy(v => v.Nombre).ToList();
            usuarios = opciones;
            basesNoDisponibles = errores;
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "No se pudo cargar el registro central de Telegram");
            Error("No se pudo cargar el registro central de Telegram.");
        }
        finally { cargando = false; }
    }

    // Ambas columnas son timestamp sin zona. El bot guarda UTC en el registro central y hora local en el proyecto.
    private static DateTime ComoUtc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
    private static DateTime? ComoUtc(DateTime? fecha) => fecha is DateTime valor ? ComoUtc(valor) : null;
    private static DateTime LocalAUtc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Local).ToUniversalTime();
    private static DateTime? LocalAUtc(DateTime? fecha) => fecha is DateTime valor ? LocalAUtc(valor) : null;
    private static DateTime? MasReciente(DateTime? a, DateTime? b) => a is null || b > a ? b : a;

    private string NombreProyecto(int id) => Proyectos.Proyecto(id)?.Nombre ?? $"Proyecto {id}";
    private string NombreUsuario(string? id) => id is null ? "—" :
        usuarios.FirstOrDefault(u => u.Id == id)?.Nombre ?? "Usuario no disponible";

    private IEnumerable<Chat> Filtrados()
    {
        var t = texto.Trim().TrimStart('@');
        return chats.Where(v => (filtroProyecto == 0 || filtroProyecto == v.ProyectoId)
            && (t.Length == 0 || v.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase)
                || v.UsuarioTelegram?.Contains(t, StringComparison.OrdinalIgnoreCase) == true
                || v.ChatId.ToString().Contains(t)
                || NombreUsuario(v.UsuarioId).Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    private Task GenerarAsync() => Cambiar(() =>
    {
        if (!Bot.TieneToken || Proyectos.Proyecto(codProyecto)?.Estado != EstadoProyecto.Activo)
            throw new OperacionAdminException("Selecciona un proyecto activo y comprueba la configuración del bot.");
        var usuario = usuarios.FirstOrDefault(u => u.Id == codUsuario);
        if (usuario is null && (!PermitirSinUsuario || codUsuario.Length > 0))
            throw new OperacionAdminException("Selecciona un usuario de SGC Web activo.");
        var nombre = codNombre.Trim();
        if (nombre.Length == 0) nombre = usuario?.Nombre.Split(" · ")[0] ?? "";
        if (nombre.Length == 0)
            throw new OperacionAdminException("Escribe el nombre del chat.");
        Bot.GenerarCodigo(nombre[..Math.Min(nombre.Length, 150)], usuario?.Id, codProyecto);
        Registrar("Código de Telegram generado", $"{nombre} · {NombreProyecto(codProyecto)}");
        codNombre = "";
        return Task.CompletedTask;
    }, "Código generado. Vence en 15 minutos.");

    private Task AnularAsync(string codigo, string nombre, int? pid) => Cambiar(() =>
    {
        if (!Bot.AnularCodigo(codigo))
            throw new OperacionAdminException("El código ya no está pendiente.");
        Registrar("Código de Telegram anulado", $"{nombre} · proyecto {pid}");
        return Task.CompletedTask;
    }, "Código anulado.");

    private void Editar(Chat v)
    {
        editando = v.Clave;
        eliminando = null;
        nombreEdicion = v.Nombre;
        usuarioEdicion = v.UsuarioId ?? "";
        proyectoEdicion = v.ProyectoId;
    }

    private Task GuardarAsync(Chat v)
    {
        var nombre = nombreEdicion.Trim();
        var usuario = string.IsNullOrEmpty(usuarioEdicion) ? null : usuarioEdicion;
        var proyecto = proyectoEdicion;
        return ActualizarAsync(v, "Telegram: vínculo editado", fila =>
        {
            if (nombre.Length is 0 or > 150) throw new OperacionAdminException("Escribe un nombre de hasta 150 caracteres.");
            if (usuario is not null && usuario != fila.UsuarioId && usuarios.All(u => u.Id != usuario))
                throw new OperacionAdminException("Selecciona un usuario de SGC Web activo.");
            return fila with { Nombre = nombre, UsuarioId = usuario, ProyectoId = proyecto };
        });
    }

    private Task ReiniciarAsync(Chat v) => ActualizarAsync(v, "Telegram: activación reiniciada",
        fila => fila with { Activo = true, Vinculado = DateTime.UtcNow });

    private Task AlternarAsync(Chat v) => ActualizarAsync(v, v.Activo ? "Telegram: chat desactivado" : "Telegram: chat activado",
        fila => fila with { Activo = !v.Activo });

    private Task CentralizarAsync(Chat v) => ActualizarAsync(v, "Telegram: chat anterior centralizado", fila => fila, centralizar: true);

    private async Task<ProyectoActivo> AbrirProyectoAsync(int id, bool preparar)
    {
        if (Proyectos.Proyecto(id)?.Estado != EstadoProyecto.Activo || Proyectos.Activo(id) is not { } p)
            throw new OperacionAdminException("Elige un proyecto activo.");
        if (preparar && await Proyectos.PrepararAsync(id) is { } error)
            throw new OperacionAdminException(error);
        return p;
    }

    private static void ComprobarFila(Chat v, VinculoTelegram? central, UsuarioBot? local)
    {
        if (v.Anterior ? central is not null || local?.Id != v.RegistroId :
            central?.Id != v.RegistroId || central.ProyectoId != v.ProyectoId)
            throw new OperacionAdminException("El chat cambió desde la última carga. Actualiza la lista e inténtalo de nuevo.");
    }

    private async Task ActualizarAsync(Chat v, string evento, Func<Chat, Chat> cambio, bool centralizar = false)
    {
        var ok = await Cambiar(async () =>
        {
            using var turno = await Bot.BloquearChatAsync(v.ChatId);
            await ConDb(async (central, _) =>
            {
                var vinculo = await central.VinculosTelegram.SingleOrDefaultAsync(x => x.ChatId == v.ChatId);
                var origen = Proyectos.Activo(v.ProyectoId);
                await using var dbOrigen = origen is null ? null : Proyectos.CrearContexto(origen);
                var local = dbOrigen is null ? null : await dbOrigen.UsuariosBot.SingleOrDefaultAsync(x => x.ChatId == v.ChatId);
                ComprobarFila(v, vinculo, local);

                // Leer el estado vigente antes de aplicar el cambio. UsuarioBot también controla el acceso.
                var actual = v with
                {
                    Nombre = vinculo?.Nombre ?? local?.Nombre ?? v.Nombre,
                    UsuarioId = vinculo?.UsuarioId,
                    UsuarioTelegram = vinculo?.UsuarioTelegram ?? local?.UsuarioTelegram,
                    Activo = (vinculo?.Activo ?? true) && (local?.Activo ?? false),
                    Vinculado = vinculo is not null ? ComoUtc(vinculo.Vinculado) : LocalAUtc(local?.FechaVinculo) ?? v.Vinculado,
                    UltimoUso = MasReciente(ComoUtc(vinculo?.UltimoUso), LocalAUtc(local?.UltimoUso))
                };
                if (local is null && origen is null) actual = actual with { Activo = vinculo?.Activo ?? false };
                var destino = cambio(actual);
                var movido = destino.ProyectoId != actual.ProyectoId;
                var proyecto = await AbrirProyectoAsync(destino.ProyectoId, preparar: movido);
                await using var dbDestino = movido ? Proyectos.CrearContexto(proyecto) : null;
                var db = dbDestino ?? dbOrigen!;
                var fila = movido ? await db.UsuariosBot.SingleOrDefaultAsync(x => x.ChatId == v.ChatId) : local;
                var debeCentralizar = vinculo is not null || centralizar || movido || destino.UsuarioId is not null;
                var localNuevo = fila is null;
                if (fila is null)
                {
                    fila = new UsuarioBot { ChatId = v.ChatId };
                    db.UsuariosBot.Add(fila);
                }
                var anteriores = db.Entry(fila).CurrentValues.Clone();
                fila.Nombre = destino.Nombre;
                fila.UsuarioTelegram = destino.UsuarioTelegram;
                fila.Activo = destino.Activo;
                fila.FechaVinculo = destino.Vinculado.ToLocalTime();
                fila.UltimoUso = destino.UltimoUso?.ToLocalTime();

                await db.SaveChangesAsync();
                if (debeCentralizar)
                {
                    if (vinculo is null)
                    {
                        vinculo = new VinculoTelegram { ChatId = v.ChatId };
                        central.VinculosTelegram.Add(vinculo);
                    }
                    vinculo.Nombre = destino.Nombre;
                    vinculo.UsuarioTelegram = destino.UsuarioTelegram;
                    vinculo.UsuarioId = destino.UsuarioId;
                    vinculo.ProyectoId = destino.ProyectoId;
                    vinculo.Activo = destino.Activo;
                    vinculo.Vinculado = destino.Vinculado.ToUniversalTime();
                    vinculo.UltimoUso = destino.UltimoUso?.ToUniversalTime();
                    try { await central.SaveChangesAsync(); }
                    catch
                    {
                        // Las bases tienen conexiones independientes. Reponer la fila local ante un rechazo central.
                        if (localNuevo) db.UsuariosBot.Remove(fila);
                        else db.Entry(fila).CurrentValues.SetValues(anteriores);
                        await db.SaveChangesAsync();
                        throw;
                    }
                }
                Registrar(evento, $"chat {v.ChatId} · {destino.Nombre} · {NombreProyecto(destino.ProyectoId)} · {NombreUsuario(destino.UsuarioId)}");
            });
        }, "Vínculo actualizado.");
        if (ok) editando = null;
        await CargarAsync();
    }

    private async Task EliminarAsync(Chat v)
    {
        var ok = await Cambiar(async () =>
        {
            using var turno = await Bot.BloquearChatAsync(v.ChatId);
            await ConDb(async (central, _) =>
            {
                var vinculo = await central.VinculosTelegram.SingleOrDefaultAsync(x => x.ChatId == v.ChatId);
                if (v.Anterior)
                {
                    await using var db = Proyectos.CrearContexto(await AbrirProyectoAsync(v.ProyectoId, preparar: false));
                    var local = await db.UsuariosBot.SingleOrDefaultAsync(x => x.ChatId == v.ChatId);
                    ComprobarFila(v, vinculo, local);
                    db.UsuariosBot.Remove(local!);
                    await db.SaveChangesAsync();
                }
                else
                {
                    ComprobarFila(v, vinculo, null);
                    // Si un proyecto no responde, el vínculo central se conserva para poder reintentar.
                    foreach (var p in Proyectos.Todos().OrderBy(p => p.Id == v.ProyectoId))
                    {
                        if (Proyectos.Activo(p.Id) is not { } proyecto) continue;
                        await using var db = Proyectos.CrearContexto(proyecto);
                        await db.UsuariosBot.Where(x => x.ChatId == v.ChatId).ExecuteDeleteAsync();
                    }
                    central.VinculosTelegram.Remove(vinculo!);
                    await central.SaveChangesAsync();
                }
                Registrar("Telegram: vínculo eliminado", $"chat {v.ChatId} · {NombreProyecto(v.ProyectoId)}");
            });
        }, "Vínculo eliminado. El chat deberá vincularse de nuevo.");
        if (ok) eliminando = null;
        await CargarAsync();
    }

    private string QrPng(string enlace)
    {
        if (qrEnlace != enlace)
        {
            using var gen = new QRCoder.QRCodeGenerator();
            using var datos = gen.CreateQrCode(enlace, QRCoder.QRCodeGenerator.ECCLevel.M);
            qrDatos = "data:image/png;base64," + Convert.ToBase64String(new QRCoder.PngByteQRCode(datos).GetGraphic(10));
            qrEnlace = enlace;
        }
        return qrDatos!;
    }

    private async Task CopiarEnlace(string enlace)
    {
        try { await JS.InvokeVoidAsync("navigator.clipboard.writeText", enlace); mensajeCopia = "Enlace copiado"; }
        catch (JSException) { mensajeCopia = "No se pudo copiar el enlace."; }
    }
}
