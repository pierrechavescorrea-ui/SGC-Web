using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SigWeb.Data.Seguridad;

// Base CENTRAL (SeguridadDbContext): usuarios, proyectos, roles, permisos, invitaciones, auditoría y
// vínculos de Telegram. Los datos de cada proyecto siguen en SU propia base (AppDbContext, mismo esquema
// de siempre): el aislamiento entre proyectos lo da la base de datos, no un filtro en cada consulta.

// Usuario de SigWeb (ASP.NET Core Identity). El correo es el nombre de usuario.
public class UsuarioSig : IdentityUser
{
    [MaxLength(150)] public string NombreCompleto { get; set; } = "";
    [MaxLength(120)] public string? Cargo { get; set; }
    [MaxLength(150)] public string? Empresa { get; set; }
    // Ficha de contacto (Mi cuenta). El celular va en PhoneNumber (Identity).
    [MaxLength(120)] public string? Area { get; set; }                 // área o proceso
    [MaxLength(120)] public string? Profesion { get; set; }
    [MaxLength(60)] public string? MatriculaProfesional { get; set; }  // tarjeta o matrícula profesional
    [MaxLength(40)] public string? TelefonoFijo { get; set; }          // fijo y extensión
    [MaxLength(256)] public string? CorreoAlterno { get; set; }
    [MaxLength(120)] public string? Ciudad { get; set; }               // ciudad o sede
    [MaxLength(150)] public string? JefeInmediato { get; set; }
    // Foto de perfil: los bytes van en FotosUsuario; esta marca (ticks de la última carga) sirve de versión en la URL.
    public long? FotoVersion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoIngreso { get; set; }
    // Proyecto que el usuario tiene abierto (se lleva en la cookie como claim "sig:proyecto").
    public int? ProyectoActivoId { get; set; }
    // Ley 1581 de 2012 / Decreto 1377 de 2013: autorización previa, expresa e informada.
    public DateTime? AutorizoDatosEn { get; set; }
    [MaxLength(20)] public string? VersionPolitica { get; set; }
    // Cuenta anonimizada (supresión de datos sin romper la auditoría).
    public bool Anonimizado { get; set; }
}

public enum EstadoProyecto { Activo = 0, Suspendido = 1, Archivado = 2, Eliminado = 3 }

public class Proyecto
{
    public int Id { get; set; }
    [MaxLength(30)] public string Codigo { get; set; } = "";
    [MaxLength(200)] public string Nombre { get; set; } = "";
    [MaxLength(200)] public string? Cliente { get; set; }
    [MaxLength(1000)] public string? Descripcion { get; set; }
    [MaxLength(200)] public string? Ubicacion { get; set; }
    // Nombre de la base PostgreSQL del proyecto (mismo servidor y credenciales que DefaultConnection).
    [MaxLength(63)] public string BaseDatos { get; set; } = "";
    // Clave original de ConnectionStrings:Proyecto_<CODIGO>, estable tras renombrar el proyecto.
    [MaxLength(30)] public string? CodigoConexion { get; set; }
    public EstadoProyecto Estado { get; set; } = EstadoProyecto.Activo;
    public EstadoProyecto? EstadoAntesEliminacion { get; set; }
    // Espacio de la empresa: módulos de nivel corporativo (Direccionamiento estratégico, Certificaciones…).
    public bool EsEmpresa { get; set; }
    // Proyecto piloto (la base que existía antes del modo multiproyecto): recibe los datos de ejemplo.
    public bool EsPiloto { get; set; }
    [MaxLength(9)] public string? Color { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public List<ModuloProyecto> Modulos { get; set; } = new();
}

public enum FuenteModulo { Propio = 0, Empresa = 1 }

// Qué módulos tiene cada proyecto y de dónde salen sus datos. Sin fila = valor por defecto del catálogo.
public class ModuloProyecto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto? Proyecto { get; set; }
    [MaxLength(60)] public string Modulo { get; set; } = "";
    public bool Habilitado { get; set; } = true;
    public FuenteModulo Fuente { get; set; } = FuenteModulo.Propio;
}

// Rol = plantilla de permisos por módulo (matriz configurable). Los de fábrica no se borran.
public class RolSig
{
    public int Id { get; set; }
    [MaxLength(80)] public string Nombre { get; set; } = "";
    [MaxLength(400)] public string? Descripcion { get; set; }
    public bool DeFabrica { get; set; }
    [MaxLength(40)] public string? Clave { get; set; }
    public int Orden { get; set; }
    public List<PermisoRol> Permisos { get; set; } = new();
}

public class PermisoRol
{
    public int Id { get; set; }
    public int RolSigId { get; set; }
    public RolSig? Rol { get; set; }
    [MaxLength(60)] public string Modulo { get; set; } = "";
    public Accion Acciones { get; set; }
}

// Usuario en un proyecto con un rol; el acceso puede vencer (interventoría, cliente, auditor externo).
public class MiembroProyecto
{
    public int Id { get; set; }
    [MaxLength(450)] public string UsuarioId { get; set; } = "";
    public UsuarioSig? Usuario { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto? Proyecto { get; set; }
    public int RolSigId { get; set; }
    public RolSig? Rol { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? Vence { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? AsignadoPor { get; set; }
    public List<PermisoMiembro> Ajustes { get; set; } = new();
}

// Excepciones al rol para un miembro: concede o niega acciones en un módulo.
public class PermisoMiembro
{
    public int Id { get; set; }
    public int MiembroProyectoId { get; set; }
    public MiembroProyecto? Miembro { get; set; }
    [MaxLength(60)] public string Modulo { get; set; } = "";
    public Accion Conceder { get; set; }
    public Accion Negar { get; set; }
}

public enum EstadoInvitacion { Pendiente = 0, Aceptada = 1, Revocada = 2, Vencida = 3 }

// Registro solo por invitación: el administrador la crea, llega al correo y el enlace sirve una vez.
// Se guarda el hash SHA-256 del token, nunca el token.
public class Invitacion
{
    public int Id { get; set; }
    [MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(150)] public string? Nombre { get; set; }
    public int? ProyectoId { get; set; }
    public int? RolSigId { get; set; }
    public bool AdministradorGlobal { get; set; }
    public DateTime? AccesoVence { get; set; }
    [MaxLength(88)] public string TokenHash { get; set; } = "";
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
    public DateTime Vence { get; set; }
    [MaxLength(450)] public string? CreadaPor { get; set; }
    public EstadoInvitacion Estado { get; set; } = EstadoInvitacion.Pendiente;
    public DateTime? AceptadaEn { get; set; }
    [MaxLength(450)] public string? UsuarioId { get; set; }
    [MaxLength(1000)] public string? Mensaje { get; set; }
    public int Envios { get; set; }
}

public enum EstadoSolicitud { Pendiente = 0, Aprobada = 1, Rechazada = 2 }

// "Solicitar acceso" desde el login: no crea cuenta; el administrador decide si invita.
public class SolicitudAcceso
{
    public int Id { get; set; }
    [MaxLength(256)] public string Email { get; set; } = "";
    [MaxLength(150)] public string Nombre { get; set; } = "";
    [MaxLength(150)] public string? Empresa { get; set; }
    [MaxLength(1000)] public string? Motivo { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public EstadoSolicitud Estado { get; set; }
    [MaxLength(450)] public string? RevisadaPor { get; set; }
    public DateTime? RevisadaEn { get; set; }
}

// Auditoría de accesos y de cambios de seguridad. Sin contraseñas, tokens ni códigos.
public class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? UsuarioId { get; set; }
    [MaxLength(256)] public string? Email { get; set; }
    [MaxLength(60)] public string Evento { get; set; } = "";
    public bool Exito { get; set; } = true;
    public int? ProyectoId { get; set; }
    [MaxLength(64)] public string? Ip { get; set; }
    [MaxLength(200)] public string? Navegador { get; set; }
    [MaxLength(1000)] public string? Detalle { get; set; }
}

// Chat de Telegram → proyecto que consulta y (si se vinculó desde "Mi cuenta" o lo asignó el administrador)
// usuario de SigWeb cuyos permisos aplica. Sin usuario = chat vinculado con código de un proyecto (como antes).
public class VinculoTelegram
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    [MaxLength(450)] public string? UsuarioId { get; set; }
    [MaxLength(150)] public string? Nombre { get; set; }
    public UsuarioSig? Usuario { get; set; }
    public int ProyectoId { get; set; }
    [MaxLength(150)] public string? UsuarioTelegram { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime Vinculado { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoUso { get; set; }
}
