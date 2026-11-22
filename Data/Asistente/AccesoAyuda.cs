using System.Text.RegularExpressions;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Asistente;

internal static class AccesoAyuda
{
    private static readonly (Regex Patron, Accion Accion)[] Reglas = new (string,Accion)[]
    {
        (@"\b(elimin|borr(?:ar|o|e)|quit|suprim|papelera)",Accion.Eliminar),
        (@"\b(congel|asign|desasign|habilit|deshabilit|activar|desactivar|aplicar|fusion|mover|edit|modific|guard|renombr|actualiz|cambi(?:ar|o)|reorden|unir|vincul)",Accion.Editar),
        (@"\b(agreg|anad|cre(?:ar|o)|nuev|adjunt|registrar|duplicar)",Accion.Crear),
        (@"\b(import|carg(?:ar|o)|subir)",Accion.Importar | Accion.Crear),
        (@"\b(export|descarg|bajar|imprim)",Accion.Descargar),
        (@"\b(aproba|publicar|verificar eficacia|cerrar plan)",Accion.Aprobar),
        (@"\b(comentar|registrar seguimiento|ronda de seguimiento)",Accion.Comentar),
        (@"\b(configurar campos|configurar columnas|formula)",Accion.ConfigurarCampos),
        (@"\b(disenar vistas|editar vistas)",Accion.EditarVistas),
        (@"\b(disenar formularios|editar formularios)",Accion.EditarFormularios),
        (@"\b(configuracion|ajustes|restaurar)",Accion.Administrar)
    }.Select(r => (new Regex(r.Item1,RegexOptions.Compiled),r.Item2)).ToArray();

    public static Accion Requisito(string texto, string? ruta = null)
    {
        var t = TextoAyuda.SinTildes(texto.ToLowerInvariant());
        var acciones = Accion.Ver;
        if (ruta != null)
        {
            var destinoLocal = ruta.ToLowerInvariant();
            if (destinoLocal.Contains("/nuevo") || destinoLocal.Contains("/nueva")) acciones |= Accion.Crear;
            if (destinoLocal.Contains("editar")) acciones |= Accion.Editar;
            if (destinoLocal.Contains("pestana=plantillas")) acciones |= Accion.Administrar;
        }
        foreach (var (patron,accion) in Reglas)
            if (patron.IsMatch(t)) acciones |= accion;
        var destino = ruta?.TrimStart('/');
        if (destino?.StartsWith("editor") == true)
            acciones |= destino.Contains("vistas") ? Accion.EditarVistas : Accion.EditarFormularios;
        if (t.Split(' ',StringSplitOptions.RemoveEmptyEntries).All(p => p is "pdf" or "excel" or "word")) acciones |= Accion.Descargar;
        return acciones;
    }

    public static Accion RequisitoControl(string nombre,string? control,string ruta)
    {
        var accion = Requisito(nombre + " " + control,ruta);
        if (accion != Accion.Ver || string.IsNullOrWhiteSpace(control)) return accion;
        var etiqueta = TextoAyuda.SinTildes(control.TrimStart('!').ToLowerInvariant());
        // Para un control sin acción reconocible se exige la matriz completa. Una etiqueta nueva
        // no debe habilitar por accidente un cambio de datos a un perfil de lectura.
        if (Regex.IsMatch(etiqueta,@"^(ver\b|mostrar\b|ocultar\b|filtrar\b|filtros?\b|buscar\b|consultar\b|abrir\b|mapa\b|pantalla completa\b|tablero\b|tabla\b|lista\b|graficos?\b|resultados\b|base de datos\b|versiones y control de cambios\b|hoja de vida\b)"))
            return Accion.Ver;
        return Accion.Todas;
    }

    public static string Modulo(string ruta) => ruta.Split('?','#')[0].Trim('/') == "bot-telegram" ? "telegram" : CatalogoModulos.ClaveDeRuta(ruta);

    public static bool Permite(UsuarioActual usuario, string? ruta, Accion accion)
    {
        if (ruta is null || !ruta.StartsWith('/') || ruta.StartsWith("//") || ruta.Contains('\\') || ruta.Contains('{')) return false;
        if (ruta.TrimStart('/').StartsWith("admin",StringComparison.OrdinalIgnoreCase)) return usuario.EsAdministrador;
        return usuario.Puede(Modulo(ruta),accion);
    }
}
