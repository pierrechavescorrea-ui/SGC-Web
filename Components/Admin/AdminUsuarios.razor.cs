using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Seguridad;

namespace SigWeb.Components.Admin;

public partial class AdminUsuarios
{
    [Inject] private VerComoTokens TokensVerComo { get; set; } = default!;
    [Inject] private EnviadorCorreo Correo { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "proyecto")] private int? ProyectoQ { get; set; }
    [SupplyParameterFromQuery(Name = "estado")] private string? EstadoQ { get; set; }
    [SupplyParameterFromQuery(Name = "id")] private string? IdQ { get; set; }

    private sealed class Membresia
    {
        public int Id; public int ProyectoId; public int Ajustes;
        public int RolId { get; set; } public bool Activo { get; set; } public DateTime? Vence { get; set; }
    }
    private sealed class FilaUsuario
    {
        public string Id = "", Nombre = "", Email = "";
        public bool Activo, Anonimizado, Bloqueado, Mfa, Admin, Auditor;
        public DateTimeOffset? BloqueadoHasta;
        public DateTime CreadoEn; public DateTime? UltimoIngreso;
        public List<Membresia> Membresias = new();
    }
    private sealed class Datos { public string? Nombre { get; set; } public string? Cargo { get; set; } public string? Empresa { get; set; } public bool Admin { get; set; } public bool Auditor { get; set; } }
    private sealed class EdicionAjustes
    {
        public required Membresia Miembro { get; init; }
        public Dictionary<string, Accion> Base { get; init; } = new();
        public Dictionary<string, (Accion Conceder, Accion Negar)> Valores { get; init; } = new();
    }

    private bool cargando = true;
    private List<FilaUsuario> usuarios = new();
    private List<Proyecto> proyectos = new();
    private List<RolSig> roles = new();
    private FilaUsuario? sel;
    private Datos datos = new();
    private EdicionAjustes? ajustes;
    private string texto = "";
    private int filtroProyecto;
    private string filtroEstado = "";
    private int nuevoProyecto, nuevoRol;
    private DateTime? nuevoVence;
    private string? confirmarAnonimizar;

    protected override async Task OnInitializedAsync()
    {
        if (!Autorizado) return;
        filtroProyecto = ProyectoQ ?? 0;
        filtroEstado = EstadoQ ?? "";
        await CargarAsync();
        cargando = false;
        if (IdQ is not null) await AbrirAsync(IdQ);
    }

    private async Task CargarAsync()
    {
        await ConDb(async (db, sp) =>
        {
            proyectos = await db.Proyectos.AsNoTracking().OrderByDescending(p => p.EsEmpresa).ThenBy(p => p.Nombre).ToListAsync();
            roles = await db.RolesSig.AsNoTracking().OrderBy(r => r.Orden).ThenBy(r => r.Nombre).ToListAsync();
            if (nuevoRol == 0) nuevoRol = roles.FirstOrDefault(r => r.Clave == "consulta")?.Id ?? roles.FirstOrDefault()?.Id ?? 0;
            var rolesGlobales = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name }).ToListAsync();
            var miembros = await db.Miembros.AsNoTracking().Select(m => new { m.Id, m.UsuarioId, m.ProyectoId, m.RolSigId, m.Activo, m.Vence, N = m.Ajustes.Count }).ToListAsync();
            var ahora = DateTimeOffset.UtcNow;
            usuarios = (await db.Users.AsNoTracking().Where(u => u.Email != ArranqueSeguridad.EmailDesarrollo).OrderBy(u => u.NombreCompleto).ToListAsync())
                .Select(u => new FilaUsuario
                {
                    Id = u.Id, Nombre = string.IsNullOrWhiteSpace(u.NombreCompleto) ? u.Email ?? "" : u.NombreCompleto, Email = u.Email ?? "",
                    Activo = u.Activo, Anonimizado = u.Anonimizado, Mfa = u.TwoFactorEnabled, CreadoEn = u.CreadoEn, UltimoIngreso = u.UltimoIngreso,
                    Bloqueado = u.LockoutEnd is { } le && le > ahora, BloqueadoHasta = u.LockoutEnd,
                    Admin = rolesGlobales.Any(r => r.UserId == u.Id && r.Name == RolesFabrica.AdministradorGlobal),
                    Auditor = rolesGlobales.Any(r => r.UserId == u.Id && r.Name == RolesFabrica.AuditorGlobal),
                    Membresias = miembros.Where(m => m.UsuarioId == u.Id).Select(m => new Membresia
                        { Id = m.Id, ProyectoId = m.ProyectoId, RolId = m.RolSigId, Activo = m.Activo, Vence = m.Vence, Ajustes = m.N }).ToList(),
                }).ToList();
        });
    }

    private IEnumerable<FilaUsuario> Filtrados()
    {
        IEnumerable<FilaUsuario> q = usuarios;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            q = q.Where(u => u.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) || u.Email.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        if (filtroProyecto != 0) q = q.Where(u => u.Membresias.Any(m => m.ProyectoId == filtroProyecto) || u.Admin || u.Auditor);
        q = filtroEstado switch
        {
            "activo" => q.Where(u => u.Activo),
            "inactivo" => q.Where(u => !u.Activo),
            "bloqueado" => q.Where(u => u.Bloqueado),
            "admin" => q.Where(u => u.Admin || u.Auditor),
            "sinmfa" => q.Where(u => u.Activo && !u.Mfa),
            "sinproyecto" => q.Where(u => u.Activo && !u.Admin && !u.Auditor && u.Membresias.All(m => !m.Activo)),
            _ => q,
        };
        return q;
    }

    private async Task AbrirAsync(string id)
    {
        Mensaje = null; ajustes = null; confirmarAnonimizar = null;
        sel = usuarios.FirstOrDefault(u => u.Id == id);
        if (sel is null) return;
        var u = await ConDb((db, _) => db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id));
        datos = new Datos { Nombre = u?.NombreCompleto, Cargo = u?.Cargo, Empresa = u?.Empresa, Admin = sel.Admin, Auditor = sel.Auditor };
    }

    private async Task RecargarSeleccionAsync()
    {
        var id = sel?.Id;
        var m = Mensaje; var me = MensajeError;
        await CargarAsync();
        if (id is not null) await AbrirAsync(id);
        Mensaje = m; MensajeError = me;
    }

    private string NombreProyecto(int id) => proyectos.FirstOrDefault(p => p.Id == id)?.Nombre ?? $"#{id}";
    private static string Inicial(string? s) => string.IsNullOrWhiteSpace(s) ? "?" : s.Trim()[..1].ToUpperInvariant();

    // ---------- Datos y roles globales ----------
    private async Task GuardarDatosAsync()
    {
        if (sel is null) return;
        var s = sel; var d = datos;
        await Cambiar(async () =>
        {
            if (string.IsNullOrWhiteSpace(d.Nombre)) throw new OperacionAdminException("Escriba el nombre.");
            await ConDb(async (db, sp) =>
            {
                var um = Usuarios(sp);
                var u = await um.FindByIdAsync(s.Id) ?? throw new OperacionAdminException("El usuario ya no existe.");
                u.NombreCompleto = d.Nombre!.Trim(); u.Cargo = Limpio(d.Cargo); u.Empresa = Limpio(d.Empresa);
                await um.UpdateAsync(u);
                var esAdmin = await um.IsInRoleAsync(u, RolesFabrica.AdministradorGlobal);
                if (d.Admin != esAdmin)
                {
                    if (!d.Admin)
                    {
                        if (u.Id == Actual.Id) throw new OperacionAdminException("No puede quitarse a sí mismo el rol de administrador.");
                        var admins = (await um.GetUsersInRoleAsync(RolesFabrica.AdministradorGlobal)).Count(a => a.Activo && a.Email != ArranqueSeguridad.EmailDesarrollo);
                        if (admins <= 1) throw new OperacionAdminException("Debe quedar al menos un administrador activo.");
                        await um.RemoveFromRoleAsync(u, RolesFabrica.AdministradorGlobal);
                    }
                    else await um.AddToRoleAsync(u, RolesFabrica.AdministradorGlobal);
                    await um.UpdateSecurityStampAsync(u);
                    Registrar(d.Admin ? "Rol administrador concedido" : "Rol administrador retirado", u.Email);
                }
                var esAud = await um.IsInRoleAsync(u, RolesFabrica.AuditorGlobal);
                if (d.Auditor != esAud)
                {
                    if (d.Auditor) await um.AddToRoleAsync(u, RolesFabrica.AuditorGlobal); else await um.RemoveFromRoleAsync(u, RolesFabrica.AuditorGlobal);
                    await um.UpdateSecurityStampAsync(u);
                    Registrar(d.Auditor ? "Rol auditor concedido" : "Rol auditor retirado", u.Email);
                }
            });
            Permisos.Invalidar();
            Registrar("Usuario modificado", s.Email);
        }, "Usuario guardado.");
        await RecargarSeleccionAsync();
    }

    // ---------- Proyectos ----------
    private async Task AgregarMembresiaAsync()
    {
        if (sel is null || nuevoProyecto == 0) return;
        var s = sel; var pid = nuevoProyecto; var rid = nuevoRol; var vence = nuevoVence;
        await Cambiar(async () =>
        {
            await ConDb(async (db, sp) =>
            {
                var inv = sp.GetRequiredService<ServicioInvitaciones>();
                await inv.AsignarAsync(s.Id, pid, rid, vence?.ToUniversalTime(), Actual.Id);
            });
            Permisos.Invalidar();
            Registrar("Acceso concedido", $"{s.Email} → {NombreProyecto(pid)} ({roles.FirstOrDefault(r => r.Id == rid)?.Nombre})");
        }, "Acceso concedido.");
        nuevoProyecto = 0; nuevoVence = null;
        await RecargarSeleccionAsync();
    }

    private async Task GuardarMembresiaAsync(Membresia m)
    {
        if (sel is null) return;
        var s = sel;
        await Cambiar(async () =>
        {
            await ConDb(async (db, _) =>
            {
                var fila = await db.Miembros.FirstOrDefaultAsync(x => x.Id == m.Id) ?? throw new OperacionAdminException("La asignación ya no existe.");
                fila.RolSigId = m.RolId; fila.Activo = m.Activo;
                fila.Vence = m.Vence is { } v ? DateTime.SpecifyKind(v.Date, DateTimeKind.Local).ToUniversalTime() : null;
                fila.AsignadoPor = Actual.Id;
                await db.SaveChangesAsync();
            });
            Permisos.Invalidar();
            Registrar("Acceso modificado", $"{s.Email} · {NombreProyecto(m.ProyectoId)} · rol {roles.FirstOrDefault(r => r.Id == m.RolId)?.Nombre} · {(m.Activo ? "activo" : "inactivo")} · vence {Dia(m.Vence)}");
        }, "Acceso actualizado.");
    }

    private async Task QuitarMembresiaAsync(Membresia m)
    {
        if (sel is null) return;
        var s = sel;
        await Cambiar(async () =>
        {
            await ConDb(async (db, _) =>
            {
                var fila = await db.Miembros.Include(x => x.Ajustes).FirstOrDefaultAsync(x => x.Id == m.Id);
                if (fila is null) return;
                db.PermisosMiembro.RemoveRange(fila.Ajustes);
                db.Miembros.Remove(fila);
                await db.SaveChangesAsync();
            });
            Permisos.Invalidar();
            Registrar("Acceso retirado", $"{s.Email} · {NombreProyecto(m.ProyectoId)}");
        }, "Acceso retirado del proyecto.");
        if (ajustes?.Miembro.Id == m.Id) ajustes = null;
        await RecargarSeleccionAsync();
    }

    // ---------- Ajustes por miembro (concede/niega sobre el rol) ----------
    private IEnumerable<CatalogoModulos.Def> Modulos()
    {
        var empresa = ajustes is not null && proyectos.FirstOrDefault(p => p.Id == ajustes.Miembro.ProyectoId)?.EsEmpresa == true;
        return CatalogoModulos.Lista.Where(m => !empresa || m.EnEmpresa || m.Ambito != AmbitoModulo.Proyecto);
    }

    private async Task AbrirAjustes(Membresia m)
    {
        var (bases, valores) = await ConDb(async (db, _) =>
        {
            var b = await db.PermisosRol.AsNoTracking().Where(p => p.RolSigId == m.RolId).ToDictionaryAsync(p => p.Modulo, p => p.Acciones);
            var v = await db.PermisosMiembro.AsNoTracking().Where(p => p.MiembroProyectoId == m.Id).ToDictionaryAsync(p => p.Modulo, p => (p.Conceder, p.Negar));
            return (b, v);
        });
        ajustes = new EdicionAjustes
        {
            Miembro = m,
            Base = new Dictionary<string, Accion>(bases, StringComparer.OrdinalIgnoreCase),
            Valores = new Dictionary<string, (Accion, Accion)>(valores, StringComparer.OrdinalIgnoreCase),
        };
    }

    private string EstadoAjuste(string modulo, Accion a)
    {
        if (ajustes is null) return "";
        var (c, n) = ajustes.Valores.GetValueOrDefault(modulo);
        if ((n & a) != 0) return "niega";
        if ((c & a) != 0) return "concede";
        return (ajustes.Base.GetValueOrDefault(modulo) & a) != 0 ? "hereda-si" : "hereda-no";
    }

    private static string SimboloAjuste(string e) => e switch { "niega" => "✕", "concede" => "＋", "hereda-si" => "✓", _ => "·" };
    private static string TituloAjuste(string e, string accion) => e switch
    {
        "niega" => $"{accion}: negado a este usuario",
        "concede" => $"{accion}: concedido a este usuario",
        "hereda-si" => $"{accion}: lo tiene por su rol",
        _ => $"{accion}: no lo tiene por su rol",
    };

    private void Ciclar(string modulo, Accion a)
    {
        if (ajustes is null) return;
        var (c, n) = ajustes.Valores.GetValueOrDefault(modulo);
        var tieneBase = (ajustes.Base.GetValueOrDefault(modulo) & a) != 0;
        // hereda → (concede si el rol no lo da) → niega → hereda
        if ((n & a) != 0) { n &= ~a; }
        else if ((c & a) != 0) { c &= ~a; n |= a; }
        else if (!tieneBase) { c |= a; }
        else { n |= a; }
        // Quitar "Ver" niega todo lo demás en la práctica; conceder algo sin "Ver" no sirve: se concede Ver también.
        if ((c & ~Accion.Ver) != 0 && (ajustes.Base.GetValueOrDefault(modulo) & Accion.Ver) == 0) c |= Accion.Ver;
        ajustes.Valores[modulo] = (c, n);
    }

    private void LimpiarAjustes() { ajustes?.Valores.Clear(); }

    private async Task GuardarAjustesAsync()
    {
        if (sel is null || ajustes is null) return;
        var s = sel; var aj = ajustes;
        await Cambiar(async () =>
        {
            await ConDb(async (db, _) =>
            {
                var actuales = await db.PermisosMiembro.Where(p => p.MiembroProyectoId == aj.Miembro.Id).ToListAsync();
                db.PermisosMiembro.RemoveRange(actuales);
                foreach (var (mod, (c, n)) in aj.Valores.Where(v => v.Value.Conceder != 0 || v.Value.Negar != 0))
                    db.PermisosMiembro.Add(new PermisoMiembro { MiembroProyectoId = aj.Miembro.Id, Modulo = mod, Conceder = c, Negar = n });
                await db.SaveChangesAsync();
            });
            Permisos.Invalidar();
            Registrar("Permisos de usuario modificados", $"{s.Email} · {NombreProyecto(aj.Miembro.ProyectoId)} · " +
                string.Join("; ", aj.Valores.Where(v => v.Value.Conceder != 0 || v.Value.Negar != 0)
                    .Select(v => $"{v.Key}: +[{Acciones.Texto(v.Value.Conceder)}] −[{Acciones.Texto(v.Value.Negar)}]")));
        }, "Permisos guardados.");
        await RecargarSeleccionAsync();
    }

    // ---------- Seguridad ----------
    private Task AlternarActivoAsync() => OperarUsuario(async (um, u) =>
    {
        if (u.Id == Actual.Id) throw new OperacionAdminException("No puede desactivar su propia cuenta.");
        if (u.Activo && await um.IsInRoleAsync(u, RolesFabrica.AdministradorGlobal)
            && (await um.GetUsersInRoleAsync(RolesFabrica.AdministradorGlobal)).Count(a => a.Activo && a.Email != ArranqueSeguridad.EmailDesarrollo) <= 1)
            throw new OperacionAdminException("Debe quedar al menos un administrador activo.");
        u.Activo = !u.Activo;
        await um.UpdateAsync(u);
        await um.UpdateSecurityStampAsync(u);
        return (u.Activo ? "Cuenta activada" : "Cuenta desactivada", u.Activo ? "Cuenta activada." : "Cuenta desactivada: sus sesiones se cierran en unos minutos.");
    });

    private Task DesbloquearAsync() => OperarUsuario(async (um, u) =>
    {
        await um.SetLockoutEndDateAsync(u, null);
        await um.ResetAccessFailedCountAsync(u);
        return ("Cuenta desbloqueada", "Cuenta desbloqueada.");
    });

    private Task RestablecerMfaAsync() => OperarUsuario(async (um, u) =>
    {
        await um.SetTwoFactorEnabledAsync(u, false);
        await um.ResetAuthenticatorKeyAsync(u);
        await um.UpdateSecurityStampAsync(u);
        return ("Verificación en 2 pasos restablecida", "Verificación en 2 pasos restablecida: el usuario debe activarla de nuevo en Mi cuenta.");
    });

    private Task CerrarSesionesAsync() => OperarUsuario(async (um, u) =>
    {
        await um.UpdateSecurityStampAsync(u);
        return ("Sesiones cerradas por el administrador", "Sesiones cerradas (se cierran en los próximos 5 minutos).");
    });

    private Task EnviarRestablecerAsync() => OperarUsuario(async (um, u) =>
    {
        if (!u.Activo) throw new OperacionAdminException("La cuenta está inactiva.");
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await um.GeneratePasswordResetTokenAsync(u)));
        using var scope = Scopes.CreateScope();
        var inv = scope.ServiceProvider.GetRequiredService<ServicioInvitaciones>();
        var enlace = $"{inv.UrlBase(Nav.BaseUri)}/cuenta/restablecer?u={Uri.EscapeDataString(u.Id)}&t={token}";
        var ok = await Correo.EnviarAsync(u.Email!, "Restablecer contraseña de SGC Web", EnviadorCorreo.Plantilla("Restablecer contraseña",
            "<p>El administrador de SGC Web le envió un enlace para crear una contraseña nueva. Sirve una vez y vence en 2 horas.</p>",
            "Crear contraseña nueva", enlace));
        if (!ok) throw new OperacionAdminException("El correo no salió. Revise la configuración en Administración › Sistema.");
        return ("Enlace de contraseña enviado", "Enlace enviado al correo del usuario.");
    });

    private void VerComo()
    {
        if (sel is null || !PuedeModificar || Actual.Id is null) return;
        var token = TokensVerComo.Crear(Actual.Id, sel.Id);
        Nav.NavigateTo("/cuenta/ver-como?t=" + Uri.EscapeDataString(token), forceLoad: true);
    }

    private async Task AnonimizarAsync()
    {
        if (sel is null || !string.Equals(confirmarAnonimizar?.Trim(), sel.Email, StringComparison.OrdinalIgnoreCase)) return;
        var s = sel;
        await Cambiar(async () =>
        {
            if (s.Id == Actual.Id) throw new OperacionAdminException("No puede anonimizar su propia cuenta.");
            await ConDb(async (db, sp) =>
            {
                var um = Usuarios(sp);
                var u = await um.FindByIdAsync(s.Id) ?? throw new OperacionAdminException("El usuario ya no existe.");
                if (await um.IsInRoleAsync(u, RolesFabrica.AdministradorGlobal) &&
                    (await um.GetUsersInRoleAsync(RolesFabrica.AdministradorGlobal)).Count(a => a.Activo && a.Email != ArranqueSeguridad.EmailDesarrollo) <= 1)
                    throw new OperacionAdminException("Debe quedar al menos un administrador activo.");
                var alias = $"anonimo-{u.Id[..8]}@anonimo.invalid";
                foreach (var r in await um.GetRolesAsync(u)) await um.RemoveFromRoleAsync(u, r);
                if (await um.HasPasswordAsync(u)) await um.RemovePasswordAsync(u);
                await um.SetTwoFactorEnabledAsync(u, false);
                await um.ResetAuthenticatorKeyAsync(u);
                u.UserName = alias; u.Email = alias; u.NormalizedEmail = alias.ToUpperInvariant(); u.NormalizedUserName = alias.ToUpperInvariant();
                u.NombreCompleto = "Usuario anonimizado"; u.Cargo = null; u.Empresa = null; u.PhoneNumber = null;
                u.Area = null; u.Profesion = null; u.MatriculaProfesional = null; u.TelefonoFijo = null; u.CorreoAlterno = null;
                u.Ciudad = null; u.JefeInmediato = null; u.FotoVersion = null;
                u.Activo = false; u.Anonimizado = true; u.ProyectoActivoId = null;
                await um.UpdateAsync(u);
                await um.UpdateSecurityStampAsync(u);
                var miembros = await db.Miembros.Include(m => m.Ajustes).Where(m => m.UsuarioId == u.Id).ToListAsync();
                db.PermisosMiembro.RemoveRange(miembros.SelectMany(m => m.Ajustes));
                db.Miembros.RemoveRange(miembros);
                db.VinculosTelegram.RemoveRange(await db.VinculosTelegram.Where(v => v.UsuarioId == u.Id).ToListAsync());
                await db.SaveChangesAsync();
                // La auditoría se conserva sin el correo (queda el identificador interno, ya sin datos personales detrás).
                await db.Auditoria.Where(a => a.UsuarioId == u.Id).ExecuteUpdateAsync(x => x.SetProperty(a => a.Email, (string?)null));
                await db.Invitaciones.Where(i => i.UsuarioId == u.Id || i.Email == s.Email)
                    .ExecuteUpdateAsync(x => x.SetProperty(i => i.Email, alias).SetProperty(i => i.Nombre, (string?)null));
                await db.SolicitudesAcceso.Where(i => i.Email == s.Email).ExecuteDeleteAsync();
                await db.FotosUsuario.Where(f => f.UsuarioId == u.Id).ExecuteDeleteAsync();
                await db.PreferenciasNotificacion.Where(f => f.UsuarioId == u.Id).ExecuteDeleteAsync();
                await db.PreferenciasUsuario.Where(f => f.UsuarioId == u.Id).ExecuteDeleteAsync();   // guardan rutas y títulos
                // Bitácora de avisos: se conserva el envío sin el correo ni el contenido.
                await db.NotificacionesEnviadas.Where(n => n.UsuarioId == u.Id || n.Email == s.Email)
                    .ExecuteUpdateAsync(x => x.SetProperty(n => n.Email, (string?)null).SetProperty(n => n.Html, (string?)null));
            });
            Permisos.Invalidar();
            Registrar("Datos personales suprimidos", "Usuario " + s.Id[..8]);
        }, "Datos personales suprimidos.");
        confirmarAnonimizar = null;
        await RecargarSeleccionAsync();
    }

    private async Task OperarUsuario(Func<UserManager<UsuarioSig>, UsuarioSig, Task<(string Evento, string Mensaje)>> f)
    {
        if (sel is null) return;
        var s = sel;
        string? msg = null;
        var ok = await Cambiar(async () =>
        {
            await ConDb(async (db, sp) =>
            {
                var um = Usuarios(sp);
                var u = await um.FindByIdAsync(s.Id) ?? throw new OperacionAdminException("El usuario ya no existe.");
                var (evento, m) = await f(um, u);
                Registrar(evento, u.Email);
                msg = m;
            });
            Permisos.Invalidar();
        });
        if (ok && msg is not null) Exito(msg);
        await RecargarSeleccionAsync();
    }

    private static string? Limpio(string? s) { s = s?.Trim(); return string.IsNullOrEmpty(s) ? null : s; }
}
