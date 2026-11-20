using SigWeb.Models;

namespace SigWeb.Data.Formularios;

// Formularios de registro que se pueden diseñar (Configuración › Catálogos › Formularios).
//   Clave   = identidad fija del formulario (la usa <DisenoFormulario Clave=…> / <FormularioSig Formulario=…>).
//   Ruta    = módulo del menú (MapaNavegacion: agrupa por capítulo, como el Dossier).
//   Tabla   = tabla de Campos (CampoTabla.Tabla) donde viven los campos nuevos: la misma de la lista del módulo cuando
//             la tiene (así el campo sale también como columna, en Excel y en el Dossier); si no, "form:{clave}".
//   Entidad = tipo que se guarda: al crear un registro nuevo, sus campos nuevos se escriben con el Id que le dé la base
//             (ValoresFormulario escucha SaveChanges del circuito; no hace falta tocar cada página).
// Formulario nuevo = una línea aquí + Formulario="clave" en su <FormularioSig> (o <DisenoFormulario> en el componente).
public static class RegistroFormularios
{
    // PintaCamposTabla = el formulario ya pinta todos los campos de su tabla (Materiales): un campo nuevo aparece solo,
    // no se agrega como campo aparte (se duplicaría).
    public sealed record Info(string Clave, string Nombre, string Ruta, string Tabla, Type Entidad, bool PintaCamposTabla = false);

    public static readonly IReadOnlyList<Info> Todos = new Info[]
    {
        new("direccionamiento", "Documento de direccionamiento", "direccionamiento", "form:direccionamiento", typeof(DocumentoDireccionamiento)),
        new("partes-interesadas", "Parte interesada", "partes-interesadas", "form:partes-interesadas", typeof(ParteInteresada)),
        new("seguimiento-parte", "Seguimiento de parte interesada", "partes-interesadas", "form:seguimiento-parte", typeof(SeguimientoParteInteresada)),
        new("riesgos", "Riesgo", "riesgos", "riesgos", typeof(Riesgo)),
        new("seguimiento-riesgo", "Seguimiento de riesgo", "riesgos", "form:seguimiento-riesgo", typeof(SeguimientoRiesgo)),
        new("certificaciones", "Certificado", "certificaciones", "form:certificaciones", typeof(Certificacion)),
        new("comunicados", "Comunicado", "comunicados", "comunicados", typeof(Comunicado)),
        new("consecutivos", "Consecutivo de comunicado", "comunicados", "form:consecutivos", typeof(ReservaConsecutivo)),
        new("documentos", "Documento", "documentos", "documentos", typeof(DocumentoMaestro)),
        new("informe-diario", "Informe diario de avance", "avance-obra", "form:informe-diario", typeof(InformeDiario)),
        new("materiales", "Material", "materiales", "materiales", typeof(Material), PintaCamposTabla: true),
        new("indicadores", "Indicador", "indicadores", "form:indicadores", typeof(Indicador)),
        new("programa-auditoria", "Programa de auditoría", "auditoria-interna", "form:programa-auditoria", typeof(ProgramaAuditoria)),
        new("auditorias", "Auditoría", "auditoria-interna", "form:auditorias", typeof(Auditoria)),
        new("hallazgos-auditoria", "Hallazgo de auditoría", "auditoria-interna", "form:hallazgos-auditoria", typeof(HallazgoAuditoria)),
        new("planes-accion", "Plan de acción", "planes-accion", "planes-accion", typeof(PlanAccion)),
    };

    private static readonly Dictionary<string, Info> PorClave = Todos.ToDictionary(f => f.Clave, StringComparer.OrdinalIgnoreCase);

    public static Info? De(string? clave) => clave is not null && PorClave.TryGetValue(clave, out var i) ? i : null;
}
