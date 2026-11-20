using System.ComponentModel.DataAnnotations;

namespace SigWeb.Data.Seguridad;

// Foto de perfil (base central). Se guarda en la base y no en wwwroot: así sirve a varias instancias del servidor
// y no queda pública por URL (se entrega por /cuenta/foto/{id} solo con sesión).
public class FotoUsuario
{
    [Key, MaxLength(450)] public string UsuarioId { get; set; } = "";
    [MaxLength(40)] public string Tipo { get; set; } = "image/jpeg";
    public byte[] Datos { get; set; } = Array.Empty<byte>();
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}

public enum FrecuenciaNotificacion { Diaria = 0, Semanal = 1, Mensual = 2 }

[Flags]
public enum DestinoNotificacion
{
    Ninguno = 0,
    Responsables = 1,      // la persona escrita como responsable del registro (se busca por nombre o correo entre los usuarios)
    QuienesEditan = 2,     // miembros del proyecto que pueden editar el módulo
    Rol = 4,               // miembros del proyecto con un rol
    Usuarios = 8,          // usuarios elegidos
    Correos = 16,          // correos escritos a mano (personas sin cuenta)
}

// Regla de un aviso automático por proyecto: qué se revisa, cada cuánto, a qué hora y a quién se le envía.
public class ReglaNotificacion
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    [MaxLength(40)] public string Tipo { get; set; } = "";          // clave del CatalogoNotificaciones
    public bool Activa { get; set; }
    public FrecuenciaNotificacion Frecuencia { get; set; } = FrecuenciaNotificacion.Semanal;
    public int DiaSemana { get; set; } = 1;                         // 0 = domingo … 6 = sábado (semanal)
    public int DiaMes { get; set; } = 1;                            // 1–28 (mensual)
    public int Hora { get; set; } = 7;                              // hora local (America/Bogota por defecto)
    public DestinoNotificacion Destino { get; set; } = DestinoNotificacion.Responsables | DestinoNotificacion.QuienesEditan;
    public int? RolSigId { get; set; }
    [MaxLength(4000)] public string? UsuariosIds { get; set; }      // separados por ;
    [MaxLength(2000)] public string? Correos { get; set; }          // separados por ; o salto de línea
    public bool SoloNuevos { get; set; }                            // no repetir lo ya avisado a esa persona
    public DateTime? UltimaEjecucion { get; set; }                  // UTC
    [MaxLength(450)] public string? ModificadoPor { get; set; }
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;
}

public enum EstadoEnvio { Pendiente = 0, Enviado = 1, Fallido = 2, Omitido = 3 }

// Bitácora y cola de envíos (reintentos, «solo nuevos» y auditoría). Guarda el correo del destinatario porque puede no
// tener cuenta; anonimizar un usuario lo borra también aquí.
public class NotificacionEnviada
{
    public long Id { get; set; }
    public int? ProyectoId { get; set; }
    [MaxLength(40)] public string Tipo { get; set; } = "";          // clave del catálogo o "manual"
    [MaxLength(450)] public string? UsuarioId { get; set; }
    [MaxLength(256)] public string? Email { get; set; }
    [MaxLength(300)] public string Asunto { get; set; } = "";
    public string? Html { get; set; }                               // solo mientras está pendiente (se borra al enviar)
    [MaxLength(8000)] public string? Claves { get; set; }           // claves de los elementos avisados (para «solo nuevos»)
    public int Elementos { get; set; }
    public EstadoEnvio Estado { get; set; }
    public int Intentos { get; set; }
    [MaxLength(500)] public string? Error { get; set; }
    public DateTime Creado { get; set; } = DateTime.UtcNow;
    public DateTime? Enviado { get; set; }
    [MaxLength(450)] public string? EnviadoPor { get; set; }        // null = automático
}

// Lo que cada persona no quiere recibir (por omisión recibe todo lo que le corresponde).
public class PreferenciaNotificacion
{
    public int Id { get; set; }
    [MaxLength(450)] public string UsuarioId { get; set; } = "";
    [MaxLength(40)] public string Tipo { get; set; } = "";          // clave del catálogo, o "manual"
    public bool Recibir { get; set; } = true;
}
