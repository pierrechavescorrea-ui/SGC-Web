using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Registro solo por invitación. El administrador invita un correo a un proyecto con un rol; el enlace (token
// aleatorio de 256 bits, se guarda solo su SHA-256) sirve una vez y vence (Seguridad:InvitacionDias, 7 por defecto).
// Si el correo ya tiene cuenta, se le da el acceso al proyecto directamente y se le avisa.
public sealed class ServicioInvitaciones
{
    private readonly SeguridadDbContext _db;
    private readonly UserManager<UsuarioSig> _usuarios;
    private readonly EnviadorCorreo _correo;
    private readonly AuditoriaAccesos _auditoria;
    private readonly ServicioPermisos _permisos;
    private readonly ServicioProyectos _proyectos;
    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _entorno;

    public ServicioInvitaciones(SeguridadDbContext db, UserManager<UsuarioSig> usuarios, EnviadorCorreo correo, AuditoriaAccesos auditoria,
        ServicioPermisos permisos, ServicioProyectos proyectos, IConfiguration cfg, IHostEnvironment entorno)
    {
        _db = db; _usuarios = usuarios; _correo = correo; _auditoria = auditoria; _permisos = permisos; _proyectos = proyectos; _cfg = cfg; _entorno = entorno;
    }

    public sealed record Resultado(bool Ok, string Mensaje, string? Enlace = null, bool Enviado = false);

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string NuevoToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    // Dirección pública para los enlaces. En producción va en Aplicacion:UrlPublica (nunca se toma del encabezado
    // Host de la petición: un atacante podría falsearlo y recibir el enlace).
    public string UrlBase(string? respaldo)
    {
        var u = _cfg["Aplicacion:UrlPublica"];
        if (Uri.TryCreate(u, UriKind.Absolute, out var publica) && publica.Scheme == Uri.UriSchemeHttps &&
            publica.Host.Length > 0 && publica.UserInfo.Length == 0 && publica.Query.Length == 0 && publica.Fragment.Length == 0)
            return publica.AbsoluteUri.TrimEnd('/');
        if (_entorno.IsDevelopment() && Uri.TryCreate(respaldo, UriKind.Absolute, out var local) && local.IsLoopback &&
            local.Scheme is "http" or "https")
            return local.AbsoluteUri.TrimEnd('/');
        return "";
    }

    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<Resultado> InvitarAsync(string email, string? nombre, int? proyectoId, int? rolId, bool adminGlobal,
        DateTime? accesoVence, string? mensaje, string creadaPor, string urlBase)
    {
        email = NormalizarEmail(email);
        if (urlBase.Length == 0) return new(false, "Defina Aplicacion:UrlPublica con la URL HTTPS de SGC Web.");
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) return new(false, "Correo no válido.");
        if (!adminGlobal && (proyectoId is null || rolId is null)) return new(false, "Elija proyecto y rol.");

        if (proyectoId is int destino && !await ProyectoDisponibleAsync(destino))
            return new(false, "El proyecto ya no está disponible.");

        var existente = await _usuarios.FindByEmailAsync(email);
        if (existente is not null && !existente.Anonimizado)
        {
            if (adminGlobal && !await _usuarios.IsInRoleAsync(existente, RolesFabrica.AdministradorGlobal))
                await _usuarios.AddToRoleAsync(existente, RolesFabrica.AdministradorGlobal);
            if (proyectoId is int pid && rolId is int rid)
                await AsignarAsync(existente.Id, pid, rid, accesoVence, creadaPor);
            _permisos.Invalidar();
            var p = proyectoId is int x ? _proyectos.Proyecto(x)?.Nombre : null;
            var enviado = await _correo.EnviarAsync(email, "Nuevo acceso en SigWeb", EnviadorCorreo.Plantilla("Tiene un nuevo acceso",
                $"<p>Se le dio acceso{(p is null ? "" : " al proyecto <b>" + WebUtility.HtmlEncode(p) + "</b>")} en SGC Web.</p>",
                "Entrar a SigWeb", urlBase + "/cuenta/ingresar"));
            _auditoria.Registrar("Acceso concedido", true, $"{email} → proyecto {proyectoId}, rol {rolId}", usuarioId: creadaPor);
            return new(true, "El usuario ya tenía cuenta: se le dio el acceso" + (enviado ? " y se le avisó por correo." : "."), null, enviado);
        }

        // Una invitación pendiente por correo: las anteriores se revocan.
        foreach (var vieja in await _db.Invitaciones.Where(i => i.Email == email && i.Estado == EstadoInvitacion.Pendiente).ToListAsync())
            vieja.Estado = EstadoInvitacion.Revocada;

        var token = NuevoToken();
        var inv = new Invitacion
        {
            Email = email, Nombre = nombre?.Trim(), ProyectoId = proyectoId, RolSigId = rolId, AdministradorGlobal = adminGlobal,
            AccesoVence = accesoVence, Mensaje = mensaje?.Trim(), CreadaPor = creadaPor, TokenHash = Hash(token),
            Vence = DateTime.UtcNow.AddDays(Math.Clamp(_cfg.GetValue("Seguridad:InvitacionDias", 7), 1, 30)), Envios = 1,
        };
        _db.Invitaciones.Add(inv);
        await _db.SaveChangesAsync();
        var enlace = urlBase + "/cuenta/invitacion?t=" + token;
        var ok = await EnviarInvitacionAsync(inv, enlace);
        _auditoria.Registrar("Invitación creada", true, $"{email} (proyecto {proyectoId}, rol {rolId}{(adminGlobal ? ", administrador" : "")})", usuarioId: creadaPor);
        return new(true, ok ? "Invitación enviada." : "Invitación creada, pero el correo no salió: copie el enlace y envíelo por otro medio.", enlace, ok);
    }

    public async Task<Resultado> ReenviarAsync(int id, string porUsuario, string urlBase)
    {
        if (urlBase.Length == 0) return new(false, "Defina Aplicacion:UrlPublica con la URL HTTPS de SGC Web.");
        var inv = await _db.Invitaciones.FindAsync(id);
        if (inv is null || inv.Estado != EstadoInvitacion.Pendiente) return new(false, "La invitación ya no está pendiente.");
        if (inv.ProyectoId is int destino && !await ProyectoDisponibleAsync(destino))
            return new(false, "El proyecto ya no está disponible.");
        var token = NuevoToken();
        inv.TokenHash = Hash(token);
        inv.Vence = DateTime.UtcNow.AddDays(Math.Clamp(_cfg.GetValue("Seguridad:InvitacionDias", 7), 1, 30));
        inv.Envios++;
        await _db.SaveChangesAsync();
        var enlace = urlBase + "/cuenta/invitacion?t=" + token;
        var ok = await EnviarInvitacionAsync(inv, enlace);
        _auditoria.Registrar("Invitación reenviada", true, inv.Email, usuarioId: porUsuario);
        return new(true, ok ? "Invitación reenviada (el enlace anterior ya no sirve)." : "Enlace nuevo creado, pero el correo no salió.", enlace, ok);
    }

    public async Task RevocarAsync(int id, string porUsuario)
    {
        var inv = await _db.Invitaciones.FindAsync(id);
        if (inv is null || inv.Estado != EstadoInvitacion.Pendiente) return;
        inv.Estado = EstadoInvitacion.Revocada;
        await _db.SaveChangesAsync();
        _auditoria.Registrar("Invitación revocada", true, inv.Email, usuarioId: porUsuario);
    }

    private Task<bool> EnviarInvitacionAsync(Invitacion inv, string enlace)
    {
        Func<string?, string> e = s => WebUtility.HtmlEncode(s ?? "");
        var proyecto = inv.ProyectoId is int p ? _proyectos.Proyecto(p)?.Nombre : null;
        var cuerpo = $"<p>Hola{(string.IsNullOrWhiteSpace(inv.Nombre) ? "" : " " + e(inv.Nombre))}:</p>" +
            $"<p>Fue invitado a SGC Web{(proyecto is null ? "" : ", proyecto <b>" + e(proyecto) + "</b>")}. Cree su contraseña con el botón; " +
            $"el enlace sirve una sola vez y vence el {inv.Vence.ToLocalTime():dd/MM/yyyy HH:mm}.</p>" +
            (string.IsNullOrWhiteSpace(inv.Mensaje) ? "" : $"<p style=\"border-left:3px solid #EC1C24;padding-left:12px;color:#4b5563\">{e(inv.Mensaje)}</p>");
        return _correo.EnviarAsync(inv.Email, "Invitación a SigWeb", EnviadorCorreo.Plantilla("Invitación a SigWeb", cuerpo, "Crear mi cuenta", enlace));
    }

    public async Task<Invitacion?> ValidarAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        var hash = Hash(token);
        var inv = await _db.Invitaciones.FirstOrDefaultAsync(i => i.TokenHash == hash);
        if (inv is null || inv.Estado != EstadoInvitacion.Pendiente) return null;
        if (inv.Vence < DateTime.UtcNow)
        {
            inv.Estado = EstadoInvitacion.Vencida;
            await _db.SaveChangesAsync();
            return null;
        }
        if (inv.ProyectoId is int destino && !await ProyectoDisponibleAsync(destino)) return null;
        return inv;
    }

    private Task<bool> ProyectoDisponibleAsync(int id) => _db.Proyectos.AnyAsync(p => p.Id == id);

    public async Task<(UsuarioSig? Usuario, IEnumerable<string> Errores)> AceptarAsync(string token, string nombre, string clave, string? ip)
    {
        var inv = await ValidarAsync(token);
        if (inv is null) return (null, new[] { "La invitación no es válida o ya venció." });
        if (await _usuarios.FindByEmailAsync(inv.Email) is not null) return (null, new[] { "Ya existe una cuenta con este correo: inicie sesión." });

        var u = new UsuarioSig
        {
            UserName = inv.Email, Email = inv.Email, EmailConfirmed = true, NombreCompleto = nombre.Trim(),
            AutorizoDatosEn = DateTime.UtcNow, VersionPolitica = Privacidad.Version, ProyectoActivoId = inv.ProyectoId,
        };
        var r = await _usuarios.CreateAsync(u, clave);
        if (!r.Succeeded) return (null, r.Errors.Select(e => e.Description));
        if (inv.AdministradorGlobal) await _usuarios.AddToRoleAsync(u, RolesFabrica.AdministradorGlobal);
        if (inv.ProyectoId is int pid && inv.RolSigId is int rid) await AsignarAsync(u.Id, pid, rid, inv.AccesoVence, inv.CreadaPor);
        inv.Estado = EstadoInvitacion.Aceptada;
        inv.AceptadaEn = DateTime.UtcNow;
        inv.UsuarioId = u.Id;
        await _db.SaveChangesAsync();
        _permisos.Invalidar();
        _auditoria.Registrar("Invitación aceptada", true, "Cuenta creada; autorizó el tratamiento de datos (política " + Privacidad.Version + ")",
            usuarioId: u.Id, email: u.Email, ip: ip);
        return (u, Array.Empty<string>());
    }

    public async Task AsignarAsync(string usuarioId, int proyectoId, int rolId, DateTime? vence, string? porUsuario)
    {
        var m = await _db.Miembros.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.ProyectoId == proyectoId);
        if (m is null) { m = new MiembroProyecto { UsuarioId = usuarioId, ProyectoId = proyectoId }; _db.Miembros.Add(m); }
        m.RolSigId = rolId; m.Activo = true; m.Vence = vence; m.AsignadoPor = porUsuario;
        await _db.SaveChangesAsync();
    }
}
