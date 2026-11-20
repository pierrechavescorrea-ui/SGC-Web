using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Notificaciones;

// Avisos por correo desde la app.
//  · Automáticos: cada proyecto tiene sus reglas (ReglaNotificacion: tipo, frecuencia, hora, destinatarios). El trabajador
//    (TrabajadorNotificaciones) las revisa cada pocos minutos; cuando a una le toca, lee el proyecto en solo lectura,
//    arma UN correo por persona con todo lo suyo y lo deja en la cola (NotificacionesEnviadas) para enviarlo y reintentarlo.
//  · Manuales: desde un registro (plan, auditoría, equipo, indicador…) un usuario envía un aviso a miembros del proyecto
//    con el enlace al registro (componente BotonNotificar).
// Nadie recibe datos de un módulo que no puede ver (se revisa el permiso Ver de cada persona). Cada persona puede dejar de
// recibir un tipo en Mi cuenta › Avisos por correo.
public sealed class ServicioNotificaciones
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ServicioProyectos _proyectos;
    private readonly ServicioPermisos _permisos;
    private readonly EnviadorCorreo _correo;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ServicioNotificaciones> _log;
    private readonly SemaphoreSlim _cola = new(1, 1);

    public ServicioNotificaciones(IServiceScopeFactory scopes, ServicioProyectos proyectos, ServicioPermisos permisos, EnviadorCorreo correo,
        IConfiguration cfg, ILogger<ServicioNotificaciones> log)
    {
        _scopes = scopes; _proyectos = proyectos; _permisos = permisos; _correo = correo; _cfg = cfg; _log = log;
    }

    public sealed record Destinatario(string Email, string Nombre, string? UsuarioId, List<AvisoNotif> Avisos, string Motivo);
    public sealed record Vista(CatalogoNotificaciones.Tipo Tipo, int Elementos, List<Destinatario> Destinatarios, List<string> SinDestinatario);
    public sealed record Miembro(string UsuarioId, string Nombre, string Email, string? Cargo, string? Rol, bool PuedeVer);

    public TimeZoneInfo Zona
    {
        get
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(_cfg["Aplicacion:ZonaHoraria"] ?? "America/Bogota"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }

    public DateTime AhoraLocal => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona);

    public string UrlBase => (_cfg["Aplicacion:UrlPublica"] ?? "").TrimEnd('/');

    // Reglas del proyecto: una por tipo del catálogo (las que faltan salen con los valores sugeridos, inactivas).
    public async Task<List<ReglaNotificacion>> ReglasAsync(int proyectoId)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
        var guardadas = await db.ReglasNotificacion.AsNoTracking().Where(r => r.ProyectoId == proyectoId).ToListAsync();
        return CatalogoNotificaciones.Lista.Select(t => guardadas.FirstOrDefault(g => g.Tipo == t.Clave) ?? new ReglaNotificacion
        {
            ProyectoId = proyectoId, Tipo = t.Clave, Frecuencia = t.Frecuencia, Destino = t.Destino, Activa = false,
        }).ToList();
    }

    public async Task GuardarReglaAsync(ReglaNotificacion r, string usuarioId)
    {
        if (CatalogoNotificaciones.De(r.Tipo) is null) throw new InvalidOperationException("Tipo de aviso desconocido.");
        r.Hora = Math.Clamp(r.Hora, 0, 23);
        r.DiaSemana = Math.Clamp(r.DiaSemana, 0, 6);
        r.DiaMes = Math.Clamp(r.DiaMes, 1, 28);
        var correosMal = PartirCorreos(r.Correos).Where(c => !EsCorreo(c)).ToList();
        if (correosMal.Count > 0) throw new InvalidOperationException("Correo no válido: " + string.Join(", ", correosMal));
        r.Correos = string.Join("; ", PartirCorreos(r.Correos));
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
        var e = await db.ReglasNotificacion.FirstOrDefaultAsync(x => x.ProyectoId == r.ProyectoId && x.Tipo == r.Tipo);
        if (e is null) { e = new ReglaNotificacion { ProyectoId = r.ProyectoId, Tipo = r.Tipo }; db.ReglasNotificacion.Add(e); }
        e.Activa = r.Activa; e.Frecuencia = r.Frecuencia; e.DiaSemana = r.DiaSemana; e.DiaMes = r.DiaMes; e.Hora = r.Hora;
        e.Destino = r.Destino; e.RolSigId = r.RolSigId; e.UsuariosIds = r.UsuariosIds; e.Correos = r.Correos; e.SoloNuevos = r.SoloNuevos;
        e.ModificadoPor = usuarioId; e.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // ¿Le toca hoy a esta regla? (hora local ya cumplida, el día que corresponde y no se ha ejecutado hoy).
    public bool Toca(ReglaNotificacion r, DateTime ahoraLocal)
    {
        if (!r.Activa || ahoraLocal.Hour < r.Hora) return false;
        if (r.UltimaEjecucion is DateTime u && TimeZoneInfo.ConvertTimeFromUtc(u, Zona).Date >= ahoraLocal.Date) return false;
        return r.Frecuencia switch
        {
            FrecuenciaNotificacion.Semanal => (int)ahoraLocal.DayOfWeek == r.DiaSemana,
            FrecuenciaNotificacion.Mensual => ahoraLocal.Day == r.DiaMes,
            _ => true,
        };
    }

    // ---------- Miembros del proyecto (para elegir destinatarios) ----------

    public async Task<List<Miembro>> MiembrosAsync(int proyectoId, string? modulo = null)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
        var hoy = DateTime.UtcNow;
        var filas = await (from m in db.Miembros.AsNoTracking()
                           join u in db.Users.AsNoTracking() on m.UsuarioId equals u.Id
                           join r in db.RolesSig.AsNoTracking() on m.RolSigId equals r.Id
                           where m.ProyectoId == proyectoId && m.Activo && (m.Vence == null || m.Vence > hoy)
                                 && u.Activo && !u.Anonimizado && u.Email != null
                           orderby u.NombreCompleto
                           select new { u.Id, u.NombreCompleto, u.Email, u.Cargo, Rol = r.Nombre }).ToListAsync();
        return filas.Select(f => new Miembro(f.Id, string.IsNullOrWhiteSpace(f.NombreCompleto) ? f.Email! : f.NombreCompleto, f.Email!, f.Cargo, f.Rol,
            modulo is null || _permisos.De(f.Id, proyectoId).Puede(modulo, Accion.Ver))).ToList();
    }

    // ---------- Vista previa y ejecución de una regla ----------

    public async Task<Vista> PrevisualizarAsync(int proyectoId, ReglaNotificacion regla)
    {
        var tipo = CatalogoNotificaciones.De(regla.Tipo) ?? throw new InvalidOperationException("Tipo de aviso desconocido.");
        var p = _proyectos.Activo(proyectoId) ?? throw new InvalidOperationException("El proyecto no existe o no está activo.");
        if (!_proyectos.Modulo(proyectoId, tipo.Modulo).Habilitado) return new Vista(tipo, 0, new(), new() { "El módulo no está habilitado en el proyecto." });

        List<AvisoNotif> avisos;
        await using (var app = _proyectos.CrearContexto(p))
            avisos = CatalogoNotificaciones.Leer(app, AhoraLocal.Date, new[] { tipo.Clave })[tipo.Clave];

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
        var miembros = await MiembrosAsync(proyectoId, tipo.Modulo);
        var porId = miembros.ToDictionary(m => m.UsuarioId);
        var rolMiembro = await db.Miembros.AsNoTracking().Where(m => m.ProyectoId == proyectoId && m.Activo)
            .Select(m => new { m.UsuarioId, m.RolSigId }).ToListAsync();
        var dest = new Dictionary<string, Destinatario>(StringComparer.OrdinalIgnoreCase);
        void Agregar(string email, string nombre, string? uid, IEnumerable<AvisoNotif> lista, string motivo)
        {
            if (!dest.TryGetValue(email, out var d)) dest[email] = d = new Destinatario(email, nombre, uid, new(), motivo);
            foreach (var a in lista) if (!d.Avisos.Any(x => x.Clave == a.Clave)) d.Avisos.Add(a);
        }

        var sinDestino = new List<string>();
        if (regla.Destino.HasFlag(DestinoNotificacion.Responsables))
        {
            var indice = miembros.Where(m => m.PuedeVer).SelectMany(m => Llaves(m.Nombre, m.Email).Select(k => (k, m)))
                .GroupBy(x => x.k).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First().m);   // nombres repetidos: no se adivina
            foreach (var a in avisos.Where(a => !string.IsNullOrWhiteSpace(a.Responsable)))
            {
                var hallados = PartirNombres(a.Responsable!).Select(n => indice.TryGetValue(Normalizar(n), out var m) ? m : null).Where(m => m is not null).ToList();
                if (hallados.Count == 0) { sinDestino.Add(a.Responsable!.Trim()); continue; }
                foreach (var m in hallados) Agregar(m!.Email, m.Nombre, m.UsuarioId, new[] { a }, "Responsable");
            }
        }
        if (avisos.Count > 0)
        {
            if (regla.Destino.HasFlag(DestinoNotificacion.QuienesEditan))
                foreach (var m in miembros.Where(m => _permisos.De(m.UsuarioId, proyectoId).Puede(tipo.Modulo, Accion.Editar)))
                    Agregar(m.Email, m.Nombre, m.UsuarioId, avisos, "Edita el módulo");
            if (regla.Destino.HasFlag(DestinoNotificacion.Rol) && regla.RolSigId is int rol)
                foreach (var m in miembros.Where(m => m.PuedeVer && rolMiembro.Any(x => x.UsuarioId == m.UsuarioId && x.RolSigId == rol)))
                    Agregar(m.Email, m.Nombre, m.UsuarioId, avisos, "Rol");
            if (regla.Destino.HasFlag(DestinoNotificacion.Usuarios))
                foreach (var id in (regla.UsuariosIds ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (porId.TryGetValue(id, out var m) && m.PuedeVer) Agregar(m.Email, m.Nombre, m.UsuarioId, avisos, "Elegido");
            if (regla.Destino.HasFlag(DestinoNotificacion.Correos))
                foreach (var c in PartirCorreos(regla.Correos).Where(EsCorreo))
                    Agregar(c.ToLowerInvariant(), c, null, avisos, "Correo adicional");
        }

        // Quien desactivó este tipo en Mi cuenta no lo recibe.
        var uids = dest.Values.Where(d => d.UsuarioId is not null).Select(d => d.UsuarioId!).ToList();
        var fuera = await db.PreferenciasNotificacion.AsNoTracking()
            .Where(x => uids.Contains(x.UsuarioId) && x.Tipo == tipo.Clave && !x.Recibir).Select(x => x.UsuarioId).ToListAsync();
        foreach (var d in dest.Values.Where(d => d.UsuarioId is not null && fuera.Contains(d.UsuarioId)).ToList()) dest.Remove(d.Email);

        // «Solo nuevos»: se quitan los elementos ya avisados a esa persona (últimos 90 días).
        if (regla.SoloNuevos && dest.Count > 0)
        {
            var desde = DateTime.UtcNow.AddDays(-90);
            var emails = dest.Keys.ToList();
            var previos = await db.NotificacionesEnviadas.AsNoTracking()
                .Where(n => n.ProyectoId == proyectoId && n.Tipo == tipo.Clave && n.Estado == EstadoEnvio.Enviado && n.Creado >= desde && emails.Contains(n.Email!))
                .Select(n => new { n.Email, n.Claves }).ToListAsync();
            foreach (var g in previos.GroupBy(x => x.Email!, StringComparer.OrdinalIgnoreCase))
            {
                var ya = g.SelectMany(x => (x.Claves ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToHashSet();
                if (dest.TryGetValue(g.Key, out var d)) d.Avisos.RemoveAll(a => ya.Contains(Huella(a.Clave)));
            }
        }
        var lista = dest.Values.Where(d => d.Avisos.Count > 0).OrderBy(d => d.Nombre).ToList();
        return new Vista(tipo, avisos.Count, lista, sinDestino.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    // Arma los correos de una regla, los deja en la cola y la procesa. Devuelve cuántos correos quedaron en cola.
    public async Task<int> EjecutarAsync(int proyectoId, ReglaNotificacion regla, string? porUsuario, CancellationToken ct = default)
    {
        var vista = await PrevisualizarAsync(proyectoId, regla);
        var p = _proyectos.Proyecto(proyectoId)!;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
            foreach (var d in vista.Destinatarios)
            {
                var asunto = $"{vista.Tipo.Nombre} ({d.Avisos.Count}) · {p.Nombre}";
                db.NotificacionesEnviadas.Add(new NotificacionEnviada
                {
                    ProyectoId = proyectoId, Tipo = vista.Tipo.Clave, UsuarioId = d.UsuarioId, Email = d.Email, Asunto = Cortar(asunto, 300),
                    Html = HtmlAviso(vista.Tipo, p.Nombre, d), Claves = Cortar(string.Join('\n', d.Avisos.Select(a => Huella(a.Clave))), 8000),
                    Elementos = d.Avisos.Count, Estado = EstadoEnvio.Pendiente, EnviadoPor = porUsuario,
                });
            }
            var guardada = await db.ReglasNotificacion.FirstOrDefaultAsync(r => r.ProyectoId == proyectoId && r.Tipo == regla.Tipo, ct);
            if (guardada is not null && porUsuario is null) guardada.UltimaEjecucion = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await ProcesarColaAsync(ct);
        return vista.Destinatarios.Count;
    }

    // ---------- Aviso manual desde un registro ----------

    public async Task<(int Enviados, string? Error)> EnviarManualAsync(int proyectoId, string modulo, string asunto, string mensaje, string? enlace,
        IReadOnlyCollection<string> usuariosIds, string? correosExtra, string remitenteId, string remitenteNombre)
    {
        asunto = (asunto ?? "").Trim(); mensaje = (mensaje ?? "").Trim();
        if (asunto.Length == 0) return (0, "Escriba el asunto.");
        if (asunto.Length > 200 || mensaje.Length > 4000) return (0, "El asunto o el mensaje es demasiado largo.");
        var permisoRemitente = _permisos.De(remitenteId, proyectoId);
        if (!permisoRemitente.Puede(modulo, Accion.Ver)) return (0, "No tiene acceso a este módulo.");
        var extra = PartirCorreos(correosExtra).ToList();
        if (extra.Any(c => !EsCorreo(c))) return (0, "Revise los correos adicionales.");
        // Personas sin cuenta solo las agrega quien puede editar el módulo (evita sacar información a cualquier correo).
        if (extra.Count > 0 && !permisoRemitente.Puede(modulo, Accion.Editar)) return (0, "Solo quien edita el módulo puede escribir correos de personas sin cuenta.");
        var miembros = (await MiembrosAsync(proyectoId, modulo)).Where(m => m.PuedeVer).ToDictionary(m => m.UsuarioId);
        var dest = usuariosIds.Where(miembros.ContainsKey).Select(id => (miembros[id].Email, (string?)id)).ToList();
        dest.AddRange(extra.Select(c => (c.ToLowerInvariant(), (string?)null)));
        dest = dest.DistinctBy(d => d.Item1, StringComparer.OrdinalIgnoreCase).ToList();
        if (dest.Count == 0) return (0, "Elija al menos un destinatario.");
        if (dest.Count > 50) return (0, "Máximo 50 destinatarios por aviso.");

        var p = _proyectos.Proyecto(proyectoId)!;
        // Enlace solo a una ruta de la app (nunca a otro sitio).
        string? url = null;
        if (!string.IsNullOrWhiteSpace(enlace))
        {
            var rel = enlace.Trim().TrimStart('/');
            if (rel.Contains("://") || rel.StartsWith("/") || rel.StartsWith("\\")) return (0, "Enlace no válido.");
            url = UrlBase.Length > 0 ? $"{UrlBase}/{rel}" : null;
        }
        var cuerpo = new StringBuilder();
        cuerpo.Append($"<p style=\"margin:0 0 12px\"><b>{E(remitenteNombre)}</b> le envía este aviso desde <b>{E(p.Nombre)}</b>.</p>");
        if (mensaje.Length > 0) cuerpo.Append($"<p style=\"margin:0 0 12px;white-space:pre-wrap\">{E(mensaje)}</p>");
        var html = EnviadorCorreo.Plantilla(asunto, cuerpo.ToString(), url is null ? null : "Abrir en SGC Web", url,
            "Aviso enviado por un usuario de SGC Web. Puede dejar de recibir estos avisos en Mi cuenta › Avisos por correo.");

        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
            var uids = dest.Where(d => d.Item2 is not null).Select(d => d.Item2!).ToList();
            var fuera = await db.PreferenciasNotificacion.AsNoTracking()
                .Where(x => uids.Contains(x.UsuarioId) && x.Tipo == CatalogoNotificaciones.Manual && !x.Recibir).Select(x => x.UsuarioId).ToListAsync();
            foreach (var (email, uid) in dest)
                db.NotificacionesEnviadas.Add(new NotificacionEnviada
                {
                    ProyectoId = proyectoId, Tipo = CatalogoNotificaciones.Manual, UsuarioId = uid, Email = email, Asunto = Cortar(asunto, 300),
                    Html = html, Elementos = 1, EnviadoPor = remitenteId,
                    Estado = uid is not null && fuera.Contains(uid) ? EstadoEnvio.Omitido : EstadoEnvio.Pendiente,
                    Error = uid is not null && fuera.Contains(uid) ? "La persona desactivó los avisos manuales." : null,
                });
            await db.SaveChangesAsync();
        }
        await ProcesarColaAsync();
        return (dest.Count, null);
    }

    // ---------- Cola: envía los pendientes y reintenta los fallidos (hasta 5 veces, espaciados) ----------

    public async Task ProcesarColaAsync(CancellationToken ct = default)
    {
        if (!await _cola.WaitAsync(0, ct)) return;   // otro envío ya está procesando la cola
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
            var ahora = DateTime.UtcNow;
            var pendientes = await db.NotificacionesEnviadas
                .Where(n => (n.Estado == EstadoEnvio.Pendiente || n.Estado == EstadoEnvio.Fallido) && n.Intentos < 5 && n.Html != null)
                .OrderBy(n => n.Id).Take(200).ToListAsync(ct);
            foreach (var n in pendientes)
            {
                // Reintentos espaciados: 5, 20, 60 y 180 minutos.
                if (n.Estado == EstadoEnvio.Fallido && n.Creado.AddMinutes(new[] { 0, 5, 20, 60, 180 }[Math.Min(n.Intentos, 4)]) > ahora) continue;
                n.Intentos++;
                var ok = await _correo.EnviarAsync(n.Email!, n.Asunto, n.Html!, ct);
                if (ok) { n.Estado = EstadoEnvio.Enviado; n.Enviado = DateTime.UtcNow; n.Html = null; n.Error = null; }
                else { n.Estado = EstadoEnvio.Fallido; n.Error = "No se pudo enviar (ver el registro del servidor)."; if (n.Intentos >= 5) n.Html = null; }
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) { _log.LogError(ex, "Error procesando la cola de avisos por correo"); }
        finally { _cola.Release(); }
    }

    // ---------- Correo del aviso automático ----------

    private string HtmlAviso(CatalogoNotificaciones.Tipo tipo, string proyecto, Destinatario d)
    {
        var sb = new StringBuilder();
        sb.Append($"<p style=\"margin:0 0 14px\">Hola {E(PrimerNombre(d.Nombre))}, esto requiere gestión en <b>{E(proyecto)}</b>:</p>");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;font-size:14px\">");
        foreach (var a in d.Avisos.Take(60))
        {
            var (fondo, texto) = a.Tono switch
            {
                "rojo" => ("#fdecec", "#b4161c"), "ambar" => ("#fff4e0", "#9a5b00"), "verde" => ("#e8f6ee", "#18794e"), _ => ("#f1f2f4", "#4b5563")
            };
            var titulo = UrlBase.Length > 0
                ? $"<a href=\"{E(UrlBase + "/" + a.Href.TrimStart('/'))}\" style=\"color:#111827;font-weight:600;text-decoration:none\">{E(a.Titulo)}</a>"
                : $"<span style=\"font-weight:600\">{E(a.Titulo)}</span>";
            sb.Append("<tr><td style=\"padding:9px 0;border-bottom:1px solid #f0f0f0;vertical-align:top\">")
              .Append(titulo);
            if (!string.IsNullOrWhiteSpace(a.Detalle)) sb.Append($"<div style=\"color:#6b7280;font-size:13px;margin-top:2px\">{E(a.Detalle)}</div>");
            sb.Append("</td><td style=\"padding:9px 0 9px 12px;border-bottom:1px solid #f0f0f0;text-align:right;vertical-align:top;white-space:nowrap\">")
              .Append($"<span style=\"display:inline-block;padding:2px 9px;border-radius:999px;background:{fondo};color:{texto};font-size:12px;font-weight:600\">{E(a.Etiqueta)}</span>")
              .Append("</td></tr>");
        }
        sb.Append("</table>");
        if (d.Avisos.Count > 60) sb.Append($"<p style=\"color:#6b7280;font-size:13px\">… y {d.Avisos.Count - 60} más en la aplicación.</p>");
        var modulo = UrlBase.Length > 0 ? $"{UrlBase}/{tipo.Modulo}" : null;
        var pie = $"Recibe este aviso porque {E(Motivo(d.Motivo))}. Puede dejar de recibirlo en Mi cuenta › Avisos por correo." +
                  (UrlBase.Length > 0 ? $" <a href=\"{E(UrlBase)}/mi-cuenta#avisos\" style=\"color:#6b7280\">Ajustar</a>" : "");
        return EnviadorCorreo.Plantilla(tipo.Nombre, sb.ToString(), modulo is null ? null : "Abrir el módulo", modulo, pie);
    }

    private static string Motivo(string m) => m switch
    {
        "Responsable" => "figura como responsable",
        "Edita el módulo" => "puede editar este módulo en el proyecto",
        "Rol" => "su rol en el proyecto está incluido en este aviso",
        "Elegido" => "el administrador lo incluyó en este aviso",
        _ => "su correo está incluido en este aviso",
    };

    // ---------- Utilidades ----------

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string Cortar(string s, int n) => s.Length <= n ? s : s[..n];
    private static string PrimerNombre(string n) => n.Contains('@') ? n : n.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? n;
    // Las claves pueden ser largas (incluyen la ruta): se guarda una huella corta.
    public static string Huella(string clave) => Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(clave)))[..16];

    public static IEnumerable<string> PartirCorreos(string? s) =>
        (s ?? "").Split(new[] { ';', ',', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);

    public static bool EsCorreo(string c) => c.Length <= 256 && new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(c) && c.Contains('.');

    // "Ana Pérez, Luis Gómez / CONTRATISTA" → nombres sueltos.
    public static IEnumerable<string> PartirNombres(string s) =>
        System.Text.RegularExpressions.Regex.Split(s, @"\s*(?:,|;|/|\by\b|\n)\s*").Where(x => x.Trim().Length > 1);

    // Formas de llamar a una persona: nombre completo, correo y la parte del correo antes de @.
    private static IEnumerable<string> Llaves(string nombre, string email)
    {
        yield return Normalizar(nombre);
        yield return Normalizar(email);
        yield return Normalizar(email.Split('@')[0].Replace('.', ' ').Replace('_', ' '));
    }

    public static string Normalizar(string s)
    {
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.IsWhiteSpace(ch) ? ' ' : ch);
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " +", " ").Trim();
    }
}
