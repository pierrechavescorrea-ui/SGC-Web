using System.ComponentModel.DataAnnotations;

namespace SigWeb.Data.CreadorModulos;

// ---------- Base central (SeguridadDbContext): la definición, igual para todos los proyectos ----------

public enum EstadoModuloCreado { Borrador = 0, Publicado = 1, Archivado = 2, Eliminado = 3 }

// Módulo creado por el administrador. La app lo pinta con DefinicionJson (la versión publicada vigente); BorradorJson es lo que
// el diseñador está cambiando y no afecta a nadie hasta Publicar. Clave = identidad (ruta /mod/{clave}, permisos "mod-{clave}",
// tabla de Campos "mod:{clave}"): no cambia nunca después de creado.
public class ModuloCreado
{
    public int Id { get; set; }
    [MaxLength(40)] public string Clave { get; set; } = "";
    [MaxLength(80)] public string Nombre { get; set; } = "";
    [MaxLength(300)] public string? Descripcion { get; set; }
    // Clave de la galería (GaleriaIconosModulo) o de EncabezadoPagina.Trazos.
    [MaxLength(40)] public string Icono { get; set; } = "inspeccion";
    // Capítulo del menú (CatalogoModulos: "Contexto y liderazgo", "Planificación", "Apoyo", "Operación", "Evaluación y mejora", "Configuración").
    [MaxLength(40)] public string Grupo { get; set; } = "Operación";
    // Dónde existe: proyectos, la Empresa o ambos (Seguridad.AmbitoModulo).
    public Seguridad.AmbitoModulo Ambito { get; set; } = Seguridad.AmbitoModulo.Proyecto;
    public EstadoModuloCreado Estado { get; set; } = EstadoModuloCreado.Borrador;
    public int Version { get; set; }               // versión publicada vigente (0 = nunca publicado)
    public string? DefinicionJson { get; set; }    // publicada vigente
    public string? BorradorJson { get; set; }      // en diseño (null = igual a la publicada)
    public int Orden { get; set; }                 // orden dentro de su grupo del menú
    [MaxLength(450)] public string? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? PublicadoEn { get; set; }
    public List<VersionModuloCreado> Versiones { get; set; } = new();
}

// Historia: una fila por publicación (para ver qué cambió y volver a una versión anterior).
public class VersionModuloCreado
{
    public int Id { get; set; }
    public int ModuloCreadoId { get; set; }
    public ModuloCreado? Modulo { get; set; }
    public int Version { get; set; }
    public string Json { get; set; } = "";
    [MaxLength(80)] public string Nombre { get; set; } = "";
    [MaxLength(500)] public string? Nota { get; set; }
    [MaxLength(200)] public string? PublicadoPor { get; set; }
    public DateTime PublicadoEn { get; set; } = DateTime.UtcNow;
}

// ---------- Base de cada proyecto (AppDbContext): los registros ----------

// Registro de un módulo creado. Los valores de sus campos van en CampoTabla/ValorCampoTabla de la tabla "mod:{clave}"
// (FilaId = Id): así el módulo hereda columnas, fórmulas, formatos condicionales, Excel y Dossier de toda la app.
public class RegistroModulo
{
    public int Id { get; set; }
    [MaxLength(40)] public string Modulo { get; set; } = "";
    public int Consecutivo { get; set; }
    [MaxLength(40)] public string Codigo { get; set; } = "";
    [MaxLength(80)] public string? Estado { get; set; }
    // Copia del campo que titula el registro (para listas, vínculos, buscador y Papelera sin leer los valores).
    [MaxLength(300)] public string? Titulo { get; set; }
    // Pasos marcados "No aplica" (claves separadas por ';').
    public string? PasosNoAplica { get; set; }
    public int VersionDefinicion { get; set; }
    [MaxLength(200)] public string? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    [MaxLength(200)] public string? ModificadoPor { get; set; }
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;
    // Campo para el filtro por proyecto futuro (regla de la app).
    [MaxLength(60)] public string? Proyecto { get; set; }

    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Registro de otro módulo de la app enlazado desde un campo Vínculo (fuente del Dossier + Id de su fila).
// Etiqueta = texto que se vio al elegirlo (se refresca al abrir; si el destino se borra, queda el texto).
public class VinculoRegistroModulo
{
    public int Id { get; set; }
    public int RegistroModuloId { get; set; }
    public RegistroModulo? Registro { get; set; }
    [MaxLength(20)] public string Campo { get; set; } = "";
    [MaxLength(60)] public string Fuente { get; set; } = "";
    public int ElementoId { get; set; }
    [MaxLength(300)] public string Etiqueta { get; set; } = "";
    public int Orden { get; set; }
}

// Adjunto de un campo Archivos (uploads/modulos/{clave}/, nombre físico Guid).
public class ArchivoRegistroModulo
{
    public int Id { get; set; }
    public int RegistroModuloId { get; set; }
    public RegistroModulo? Registro { get; set; }
    [MaxLength(20)] public string Campo { get; set; } = "";
    [MaxLength(260)] public string Nombre { get; set; } = "";
    [MaxLength(400)] public string Ruta { get; set; } = "";
    public long Tamano { get; set; }
    [MaxLength(200)] public string? SubidoPor { get; set; }
    public DateTime SubidoEn { get; set; } = DateTime.UtcNow;

    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
