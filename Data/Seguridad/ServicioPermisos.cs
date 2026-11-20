using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Calcula los permisos efectivos de un usuario en un proyecto:
//   rol global AdministradorSGC → todo, en todos los proyectos;
//   rol global AuditorSGC → Ver + Descargar en todos los proyectos;
//   miembro activo y vigente → permisos del rol en cada módulo, más lo concedido y menos lo negado al miembro;
//   módulo deshabilitado en el proyecto → nada; módulo compartido desde la Empresa → solo Ver/Descargar.
// Caché de 2 minutos por (usuario, proyecto); cualquier cambio de seguridad la limpia (Invalidar).
public sealed class ServicioPermisos
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ServicioProyectos _proyectos;
    private readonly ConcurrentDictionary<(string, int), (PermisosEfectivos P, DateTime Hasta)> _cache = new();
    private readonly ConcurrentDictionary<string, (Datos D, DateTime Hasta)> _usuarios = new();
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);

    public ServicioPermisos(IServiceScopeFactory scopes, ServicioProyectos proyectos) { _scopes = scopes; _proyectos = proyectos; }

    public event Action? Cambio;

    public void Invalidar()
    {
        _cache.Clear(); _usuarios.Clear();
        Cambio?.Invoke();
    }

    private sealed record Membresia(int ProyectoId, int RolId, string Rol, bool Activo, DateTime? Vence,
        Dictionary<string, (Accion Conceder, Accion Negar)> Ajustes);
    private sealed record Datos(bool Activo, bool Admin, bool Auditor, bool Mfa, List<Membresia> Membresias, Dictionary<int, Dictionary<string, Accion>> Roles);

    private Datos Cargar(string usuarioId)
    {
        if (_usuarios.TryGetValue(usuarioId, out var c) && c.Hasta > DateTime.UtcNow) return c.D;
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
        var u = db.Users.AsNoTracking().Where(x => x.Id == usuarioId).Select(x => new { x.Activo, x.LockoutEnd, x.TwoFactorEnabled }).FirstOrDefault();
        var roles = (from ur in db.UserRoles.AsNoTracking()
                     join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                     where ur.UserId == usuarioId
                     select r.Name).ToList();
        var miembros = db.Miembros.AsNoTracking().Where(m => m.UsuarioId == usuarioId)
            .Select(m => new { m.ProyectoId, m.RolSigId, Rol = m.Rol!.Nombre, m.Activo, m.Vence,
                Ajustes = m.Ajustes.Select(a => new { a.Modulo, a.Conceder, a.Negar }).ToList() }).ToList();
        var rolIds = miembros.Select(m => m.RolSigId).Distinct().ToList();
        var permisosRol = db.PermisosRol.AsNoTracking().Where(p => rolIds.Contains(p.RolSigId)).ToList()
            .GroupBy(p => p.RolSigId).ToDictionary(g => g.Key, g => g.ToDictionary(p => p.Modulo, p => p.Acciones, StringComparer.OrdinalIgnoreCase));
        var bloqueado = u?.LockoutEnd is { } le && le > DateTimeOffset.UtcNow;
        var d = new Datos(u is not null && u.Activo && !bloqueado,
            roles.Contains(RolesFabrica.AdministradorGlobal), roles.Contains(RolesFabrica.AuditorGlobal), u?.TwoFactorEnabled == true,
            miembros.Select(m => new Membresia(m.ProyectoId, m.RolSigId, m.Rol, m.Activo, m.Vence,
                m.Ajustes.ToDictionary(a => a.Modulo, a => (a.Conceder, a.Negar), StringComparer.OrdinalIgnoreCase))).ToList(),
            permisosRol);
        _usuarios[usuarioId] = (d, DateTime.UtcNow + Vigencia);
        return d;
    }

    // Tiene la verificación en dos pasos activa (para exigirla a los administradores).
    public bool TieneMfa(string? usuarioId) => usuarioId is not null && Cargar(usuarioId).Mfa;

    public bool EsAdministradorGlobal(string? usuarioId) => usuarioId is not null && Cargar(usuarioId) is { Activo: true, Admin: true };

    // Proyectos a los que el usuario puede entrar (activos; los suspendidos solo para el administrador global).
    public IReadOnlyList<Proyecto> ProyectosDe(string usuarioId)
    {
        var d = Cargar(usuarioId);
        if (!d.Activo) return Array.Empty<Proyecto>();
        var hoy = DateTime.UtcNow;
        return _proyectos.Todos().Where(p => p.Estado != EstadoProyecto.Archivado || d.Admin)
            .Where(p => d.Admin || (p.Estado == EstadoProyecto.Activo && (d.Auditor ||
                d.Membresias.Any(m => m.ProyectoId == p.Id && m.Activo && (m.Vence is null || m.Vence > hoy)))))
            .ToList();
    }

    public PermisosEfectivos De(string? usuarioId, int proyectoId)
    {
        if (string.IsNullOrEmpty(usuarioId)) return PermisosEfectivos.Ninguno;
        if (_cache.TryGetValue((usuarioId, proyectoId), out var c) && c.Hasta > DateTime.UtcNow) return c.P;
        var p = Calcular(usuarioId, proyectoId);
        _cache[(usuarioId, proyectoId)] = (p, DateTime.UtcNow + Vigencia);
        return p;
    }

    private PermisosEfectivos Calcular(string usuarioId, int proyectoId)
    {
        var d = Cargar(usuarioId);
        var proyecto = _proyectos.Proyecto(proyectoId);
        if (!d.Activo || proyecto is null) return PermisosEfectivos.Ninguno;
        if (!d.Admin && proyecto.Estado != EstadoProyecto.Activo) return PermisosEfectivos.Ninguno;

        var res = new Dictionary<string, Accion>(StringComparer.OrdinalIgnoreCase);
        string? rol = null;
        var miembro = false;
        if (d.Admin)
        {
            foreach (var m in CatalogoModulos.Lista) res[m.Clave] = Accion.Todas;
            rol = "Administrador SGC"; miembro = true;
        }
        else
        {
            var hoy = DateTime.UtcNow;
            var mem = d.Membresias.FirstOrDefault(m => m.ProyectoId == proyectoId && m.Activo && (m.Vence is null || m.Vence > hoy));
            if (mem is not null)
            {
                miembro = true; rol = mem.Rol;
                d.Roles.TryGetValue(mem.RolId, out var permisos);
                foreach (var m in CatalogoModulos.Lista)
                {
                    var a = permisos is not null && permisos.TryGetValue(m.Clave, out var x) ? x : Accion.Ninguna;
                    if (mem.Ajustes.TryGetValue(m.Clave, out var aj)) a = (a | aj.Conceder) & ~aj.Negar;
                    res[m.Clave] = a;
                }
            }
            if (d.Auditor)
            {
                miembro = true; rol ??= "Auditor SGC";
                foreach (var m in CatalogoModulos.Lista)
                    res[m.Clave] = (res.TryGetValue(m.Clave, out var a) ? a : Accion.Ninguna) | (m.Configuracion ? Accion.Ninguna : Accion.Ver | Accion.Descargar);
            }
        }

        // Módulos del proyecto: deshabilitado = nada; compartido desde la Empresa = solo lectura aquí.
        foreach (var m in CatalogoModulos.Lista)
        {
            var (habilitado, fuente) = _proyectos.Modulo(proyectoId, m.Clave);
            if (!habilitado) res[m.Clave] = Accion.Ninguna;
            else if (fuente == FuenteModulo.Empresa) res[m.Clave] &= Accion.Ver | Accion.Descargar;
        }
        // Inicio: quien entra al proyecto ve su panel.
        if (miembro) res["inicio"] |= Accion.Ver;
        // Lo que no se puede ver no se puede hacer.
        foreach (var k in res.Keys.ToList()) if (!res[k].HasFlag(Accion.Ver)) res[k] = Accion.Ninguna;
        return new PermisosEfectivos(res, d.Admin, d.Auditor, rol, miembro);
    }
}
