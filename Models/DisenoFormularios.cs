namespace SigWeb.Models;

// Diseñador de formularios (Configuración › Catálogos › Formularios, 2026-11-15).
// Cada formulario de registro tiene una clave fija (Data/Formularios/RegistroFormularios). Su diseño = pasos (secciones)
// con nombre y orden + posición/tamaño de cada campo en una rejilla de 12 columnas + campos nuevos (CampoTabla de su tabla).
// Cada vez que se guarda queda una versión nueva (historial; restaurar = guardar una copia). Sin diseño = el formulario de siempre.
public class DisenoFormulario
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;     // clave del formulario, p. ej. "planes-accion"
    public int Version { get; set; }
    public string Json { get; set; } = "{}";               // Data/Formularios/DisenoModelo.Diseno
    public bool Vigente { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
    public string? Motivo { get; set; }
}

// Archivo de un campo "Archivos adjuntos" de un formulario: CampoTabla (tipo Archivos) + registro (FilaId).
// En Planes de acción cada campo de adjuntos es una carpeta automática con el nombre del paso donde está.
public class ArchivoCampoFormulario
{
    public int Id { get; set; }
    public int CampoTablaId { get; set; }
    public int FilaId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;

    // ---------- Papelera ----------
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}

// Paso de un formulario marcado "N/A" (no aplica) en un registro concreto: formulario (clave) + registro (FilaId) + paso
// (clave del paso en el diseño). Sus campos se ven apagados y sus campos nuevos obligatorios dejan de exigirse.
// Configuración del registro: quitar la marca = borrado directo (no Papelera).
public class PasoNoAplica
{
    public int Id { get; set; }
    public string Formulario { get; set; } = string.Empty;
    public int FilaId { get; set; }
    public string Paso { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Now;
}

// Lo que el código declara en cada formulario (pasos y campos, en su orden), tal como se pintó la última vez.
// Lo escribe el propio formulario al abrirse; el diseñador lo usa para dibujar el boceto sin abrir la pantalla.
public class EstructuraFormulario
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Json { get; set; } = "{}";               // Data/Formularios/DisenoModelo.Estructura
    public DateTime Actualizado { get; set; } = DateTime.Now;
}
