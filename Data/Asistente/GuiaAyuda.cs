using System.Text.RegularExpressions;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Asistente;

public sealed record PasoGuia(string Texto, string? Ruta, string? Control, bool Bloqueado = false, string? RutaPlantilla = null);
public sealed record GuiaAyuda(string SeccionId, string Titulo, IReadOnlyList<PasoGuia> Pasos)
{
    private static readonly Regex Peticion = new(
        @"\b(paso\s+a\s+paso|guia(?:me|rme)|guieme|me guia|muestrame como|muestreme como|mostrarme como|ensename|enseneme|ensenarme|llevame|lleveme|llevarme|acompaname|acompaneme)\b",
        RegexOptions.Compiled);
    private static readonly Regex Numerados = new(
        @"(?m)^[ \t]*\d+[.)][ \t]+([^\r\n]+(?:\r?\n[ \t]+(?!\d+[.)][ \t])\S[^\r\n]*)*)",
        RegexOptions.Compiled);
    private static readonly Regex Destacados = new(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);

    public static string? Tarea(string pregunta)
    {
        var normal = TextoAyuda.SinTildes(pregunta.ToLowerInvariant());
        return Peticion.IsMatch(normal) ? Peticion.Replace(normal, " ").Trim() : null;
    }

    public static GuiaAyuda? Buscar(ManualAyuda manual, UsuarioActual usuario, string tarea, string? ruta)
    {
        var candidatos = manual.Buscar(tarea, ruta, manual.Secciones.Count);
        var seccion = candidatos.FirstOrDefault(r => r.Puntaje >= 2.6 && r.Seccion.Titulo.StartsWith("Cómo ", StringComparison.OrdinalIgnoreCase)
            && Numerados.IsMatch(r.Seccion.Markdown))?.Seccion;
        return seccion == null ? null : Preparar(manual, seccion, usuario);
    }

    public static GuiaAyuda? Preparar(ManualAyuda manual, ManualAyuda.Seccion seccion, UsuarioActual usuario)
    {
        if (!seccion.Titulo.StartsWith("Cómo ", StringComparison.OrdinalIgnoreCase)) return null;
        var cap = seccion.Capitulo;
        var pasos = new List<PasoGuia>();
        foreach (Match numero in Numerados.Matches(seccion.Markdown))
        {
            var linea = numero.Groups[1].Value.Trim();
            var texto = Regex.Replace(linea.Replace("**", "").Replace("\u0060", ""), @"\s+", " ");
            var nombres = Destacados.Matches(linea).Select(m => m.Groups[1].Value).ToArray();
            var opcion = nombres.Select(nombre => (Nombre: nombre, Opcion: manual.ResolverOpcion(nombre, cap)))
                .Where(x => x.Opcion != null && (x.Opcion.Capitulo == cap || x.Nombre.Contains('›')))
                .Select(x => x.Opcion).FirstOrDefault(o => o?.Resaltar != null)
                ?? cap.Opciones.Where(o => o.Resaltar != null
                    && (texto.Contains("(" + o.Nombre + ")", StringComparison.OrdinalIgnoreCase)
                        || texto.Contains("(" + o.Resaltar.TrimStart('!') + ")", StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(o => o.Nombre.Length).FirstOrDefault();
            // Un enlace del manual puede señalar el botón que abre un formulario en vez del campo.
            var campo = nombres.Select(nombre => cap.Campos.FirstOrDefault(c =>
                    c.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(c => c != null);
            var nombreControl = campo?.Nombre ?? opcion?.Resaltar?.TrimStart('!');
            var rutaControl = campo?.Ruta ?? opcion?.Ruta;
            var plantilla = nombreControl == null ? pasos.LastOrDefault()?.RutaPlantilla
                : cap.Campos.FirstOrDefault(c => c.Ruta.Contains('{') && c.Nombre == nombreControl)?.Ruta
                    ?? cap.Opciones.FirstOrDefault(o => o.Ruta.Contains('{') && o.Resaltar == nombreControl)?.Ruta;
            if (rutaControl?.Contains('{') == true)
            {
                plantilla = rutaControl;
                rutaControl = null;
            }
            var destino = rutaControl ?? cap.RutaIr;
            var permiso = AccesoAyuda.Requisito(texto, destino) | (opcion?.Requiere ?? Accion.Ver);
            if (!AccesoAyuda.Permite(usuario, destino, permiso))
            {
                pasos.Add(new("Su rol no permite este paso. Solicite acceso al administrador " +
                    (destino?.StartsWith("/admin") == true ? "global." : "del proyecto."),null,null,true));
                break;
            }
            pasos.Add(new(texto, destino, nombreControl, RutaPlantilla: plantilla));
        }
        return pasos.Count == 0 ? null : new(seccion.Id, seccion.Titulo, pasos);
    }
}
