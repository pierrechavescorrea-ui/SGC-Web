using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Base central (ConnectionStrings:Central; si falta, la de DefaultConnection con la base "<nombre>_central").
// Migraciones en Migrations/Seguridad: dotnet ef migrations add <Nombre> --context SeguridadDbContext -o Migrations/Seguridad
public class SeguridadDbContext : IdentityDbContext<UsuarioSig>, IDataProtectionKeyContext
{
    public SeguridadDbContext(DbContextOptions<SeguridadDbContext> options) : base(options) { }

    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<ModuloProyecto> ModulosProyecto => Set<ModuloProyecto>();
    public DbSet<RolSig> RolesSig => Set<RolSig>();
    public DbSet<PermisoRol> PermisosRol => Set<PermisoRol>();
    public DbSet<MiembroProyecto> Miembros => Set<MiembroProyecto>();
    public DbSet<PermisoMiembro> PermisosMiembro => Set<PermisoMiembro>();
    public DbSet<Invitacion> Invitaciones => Set<Invitacion>();
    public DbSet<SolicitudAcceso> SolicitudesAcceso => Set<SolicitudAcceso>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
    public DbSet<VinculoTelegram> VinculosTelegram => Set<VinculoTelegram>();
    public DbSet<FotoUsuario> FotosUsuario => Set<FotoUsuario>();
    public DbSet<ReglaNotificacion> ReglasNotificacion => Set<ReglaNotificacion>();
    public DbSet<NotificacionEnviada> NotificacionesEnviadas => Set<NotificacionEnviada>();
    public DbSet<PreferenciaNotificacion> PreferenciasNotificacion => Set<PreferenciaNotificacion>();
    public DbSet<PreferenciaUsuario> PreferenciasUsuario => Set<PreferenciaUsuario>();   // tema y opciones de vista por usuario
    // Creador de módulos: definiciones (iguales para todos los proyectos) y su historia de publicaciones.
    public DbSet<SigWeb.Data.CreadorModulos.ModuloCreado> ModulosCreados => Set<SigWeb.Data.CreadorModulos.ModuloCreado>();
    public DbSet<SigWeb.Data.CreadorModulos.VersionModuloCreado> VersionesModuloCreado => Set<SigWeb.Data.CreadorModulos.VersionModuloCreado>();
    // Llaves de Data Protection (cookies, tokens de Identity): compartidas por todas las instancias.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Proyecto>(e =>
        {
            e.HasQueryFilter(x => x.Estado != EstadoProyecto.Eliminado);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => x.BaseDatos).IsUnique();
        });
        b.Entity<ModuloProyecto>(e =>
        {
            e.HasQueryFilter(x => x.Proyecto!.Estado != EstadoProyecto.Eliminado);
            e.HasIndex(x => new { x.ProyectoId, x.Modulo }).IsUnique();
            e.HasOne(x => x.Proyecto).WithMany(p => p.Modulos).HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RolSig>(e => e.HasIndex(x => x.Nombre).IsUnique());
        b.Entity<SigWeb.Data.CreadorModulos.ModuloCreado>(e => e.HasIndex(x => x.Clave).IsUnique());
        b.Entity<SigWeb.Data.CreadorModulos.VersionModuloCreado>(e =>
        {
            e.HasOne(x => x.Modulo).WithMany(m => m.Versiones).HasForeignKey(x => x.ModuloCreadoId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ModuloCreadoId, x.Version }).IsUnique();
        });
        b.Entity<ReglaNotificacion>(e => e.HasIndex(x => new { x.ProyectoId, x.Tipo }).IsUnique());
        b.Entity<NotificacionEnviada>(e =>
        {
            e.HasIndex(x => x.Creado);
            e.HasIndex(x => new { x.Estado, x.Creado });
            e.HasIndex(x => new { x.ProyectoId, x.Tipo, x.Email });
        });
        b.Entity<PreferenciaNotificacion>(e => e.HasIndex(x => new { x.UsuarioId, x.Tipo }).IsUnique());
        b.Entity<PreferenciaUsuario>(e => e.HasIndex(x => new { x.UsuarioId, x.Clave }).IsUnique());
        b.Entity<PermisoRol>(e =>
        {
            e.HasIndex(x => new { x.RolSigId, x.Modulo }).IsUnique();
            e.HasOne(x => x.Rol).WithMany(r => r.Permisos).HasForeignKey(x => x.RolSigId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MiembroProyecto>(e =>
        {
            e.HasQueryFilter(x => x.Proyecto!.Estado != EstadoProyecto.Eliminado);
            e.HasIndex(x => new { x.UsuarioId, x.ProyectoId }).IsUnique();
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Proyecto).WithMany().HasForeignKey(x => x.ProyectoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolSigId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PermisoMiembro>(e =>
        {
            e.HasQueryFilter(x => x.Miembro!.Proyecto!.Estado != EstadoProyecto.Eliminado);
            e.HasIndex(x => new { x.MiembroProyectoId, x.Modulo }).IsUnique();
            e.HasOne(x => x.Miembro).WithMany(m => m.Ajustes).HasForeignKey(x => x.MiembroProyectoId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Invitacion>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.Email, x.Estado });
        });
        b.Entity<SolicitudAcceso>(e => e.HasIndex(x => new { x.Estado, x.Fecha }));
        b.Entity<RegistroAuditoria>(e =>
        {
            e.HasIndex(x => x.Fecha);
            e.HasIndex(x => new { x.UsuarioId, x.Fecha });
            e.HasIndex(x => new { x.Evento, x.Fecha });
        });
        b.Entity<VinculoTelegram>(e =>
        {
            e.HasIndex(x => x.ChatId).IsUnique();
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
