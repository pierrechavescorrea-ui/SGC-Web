using System.Text.Json;
using System.Text.RegularExpressions;

namespace SigWeb.Data.Formularios;

// ---------- Lo que declara el código (EstructuraFormulario.Json) ----------
// Pasos (SeccionFormulario / BloqueFormulario) y campos (CampoFormulario) en el orden en que el formulario los pinta.
public class Estructura
{
    public List<SeccionE> Secciones { get; set; } = new();
    public List<CampoE> Campos { get; set; } = new();
}

public class SeccionE
{
    public string Clave { get; set; } = "";
    public string Titulo { get; set; } = "";
    public bool Bloque { get; set; }          // bloque compuesto (Acciones propuestas, Eficacia…): se mueve/renombra entero
    public bool Libre { get; set; }           // paso numerado con contenido propio (SinFila): sus campos se renombran/ocultan, no se reubican
}

public class CampoE
{
    public string Clave { get; set; } = "";   // "{sección}|{etiqueta}" o la Clave explícita del CampoFormulario
    public string Seccion { get; set; } = "";
    public string Etiqueta { get; set; } = "";
    public int Ancho { get; set; } = 12;      // columnas (1–12) que ocupa en el código (col-md-N)
    public int Alto { get; set; } = 1;
    public bool Obligatorio { get; set; }
    public bool Destacado { get; set; }
    public bool Fijo { get; set; }            // el código lo marca como no ocultable (lógica de la app)
    public string? Catalogo { get; set; }     // tipo de catálogo de sus opciones (para "Gestionar opciones")
    public bool Ocultable => !Obligatorio && !Destacado && !Fijo;
}

// ---------- Lo que decide el usuario (DisenoFormulario.Json) ----------
public class Diseno
{
    public bool Original { get; set; }        // versión "Restablecer": el formulario vuelve a ser el del código
    public List<SeccionD> Secciones { get; set; } = new();
    public List<CampoD> Campos { get; set; } = new();
}

public class SeccionD
{
    public string Clave { get; set; } = "";
    public string? Titulo { get; set; }       // null = el del código
    public int Orden { get; set; }
    public bool Nueva { get; set; }           // paso creado por el usuario (solo lleva campos nuevos)
    // Módulo que lee este paso (p. ej. "costos-no-calidad"): no se quita ni se pierde al restablecer; se renombra.
    // La Clave del paso es la identidad que usa el módulo.
    public string? Modulo { get; set; }
}

public class CampoD
{
    public string Clave { get; set; } = "";   // nativo = CampoE.Clave; nuevo = "c:{CampoTablaId}"
    public string Seccion { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; } = 6;
    public int H { get; set; } = 1;
    public string? Etiqueta { get; set; }     // null = la del código (o el nombre del CampoTabla)
    public bool Oculto { get; set; }
    public int? CampoTablaId { get; set; }    // campo nuevo
    public bool Obligatorio { get; set; }     // solo campos nuevos
}

// ---------- Disposición resultante (lo que pinta el formulario y el boceto) ----------
public sealed record Ubicacion(int X, int Y, int W, int H, string? Etiqueta, bool Oculto);

public static class DisenoModelo
{
    public const int Columnas = 12;
    private static readonly JsonSerializerOptions Opc = new() { PropertyNameCaseInsensitive = true };

    public static string AJson<T>(T v) => JsonSerializer.Serialize(v);
    public static T? DeJson<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Opc); } catch { return null; }
    }

    public static string ClaveCampo(string seccion, string etiqueta) => $"{seccion}|{Normal(etiqueta)}";
    public static string ClaveNuevo(int campoTablaId) => $"c:{campoTablaId}";
    public static string Normal(string? s) => Regex.Replace((s ?? "").Trim(), @"\s+", " ");

    // "col-md-4", "col-12", "col-sm-6 col-lg-3" → columnas en escritorio: manda md, luego lg, sm, xl y col-N.
    public static int AnchoDe(string? clases)
    {
        if (string.IsNullOrWhiteSpace(clases)) return Columnas;
        foreach (var bp in new[] { "md", "lg", "sm", "xl", "xxl" })
        {
            var m = Regex.Match(clases, $@"col-{bp}-(\d{{1,2}})");
            if (m.Success) return Math.Clamp(int.Parse(m.Groups[1].Value), 1, Columnas);
        }
        var n = Regex.Match(clases, @"col-(\d{1,2})");
        return n.Success ? Math.Clamp(int.Parse(n.Groups[1].Value), 1, Columnas) : Columnas;
    }

    // Pasos en el orden final: los del código (en su orden, salvo que el diseño los mueva) + los nuevos del usuario.
    public static List<(string Clave, string Titulo, bool Bloque, bool Nueva)> Pasos(Estructura e, Diseno? d)
    {
        var lista = new List<(string Clave, string Titulo, bool Bloque, bool Nueva, int Orden)>();
        int i = 0;
        foreach (var s in e.Secciones)
        {
            var sd = d?.Secciones.FirstOrDefault(x => x.Clave == s.Clave);
            lista.Add((s.Clave, string.IsNullOrWhiteSpace(sd?.Titulo) ? s.Titulo : sd!.Titulo!, s.Bloque, false, sd?.Orden ?? (i * 10)));
            i++;
        }
        if (d is not null)
            foreach (var sd in d.Secciones.Where(x => x.Nueva && !lista.Any(l => l.Clave == x.Clave)))
                lista.Add((sd.Clave, sd.Titulo ?? "Paso nuevo", false, true, sd.Orden));
        return lista.OrderBy(x => x.Orden).Select(x => (x.Clave, x.Titulo, x.Bloque, x.Nueva)).ToList();
    }

    // Ubicación de cada campo (nativo o nuevo) dentro de su paso. Los campos que el código declara y el diseño aún no
    // conoce (agregados después) entran en el primer hueco con su ancho del código: nunca desaparecen.
    // Cualquier campo (del código o nuevo) puede ir en cualquier paso con rejilla: el formulario diseñado es UNA rejilla
    // con todos los pasos, así el campo se ve donde diga el diseño aunque en el código esté declarado en otro paso.
    // Excepciones: un campo de un paso "libre" (contenido propio, SinFila) no sale de él, y nada entra a un paso libre o bloque.
    public static Dictionary<string, (string Seccion, Ubicacion U)> Disponer(Estructura e, Diseno? d)
    {
        var r = new Dictionary<string, (string, Ubicacion)>();
        var libres = e.Secciones.Where(s => s.Libre).Select(s => s.Clave).ToHashSet();
        var rejilla = e.Secciones.Where(s => !s.Bloque && !s.Libre).Select(s => s.Clave).ToHashSet();
        if (d is not null) foreach (var s in d.Secciones.Where(s => s.Nueva)) rejilla.Add(s.Clave);
        var ocupado = new Dictionary<string, List<(int X, int Y, int W, int H)>>();
        List<(int X, int Y, int W, int H)> Oc(string s) => ocupado.TryGetValue(s, out var l) ? l : ocupado[s] = new();

        if (d is not null)
        {
            foreach (var cd in d.Campos)
            {
                var nativo = e.Campos.FirstOrDefault(c => c.Clave == cd.Clave);
                if (nativo is null && cd.CampoTablaId is null) continue;              // campo del código que ya no existe
                var seccion = nativo is not null && (libres.Contains(nativo.Seccion) || !rejilla.Contains(cd.Seccion)) ? nativo.Seccion : cd.Seccion;
                var w = Math.Clamp(cd.W, 1, Columnas);
                var x = Math.Clamp(cd.X, 0, Columnas - w);
                var oculto = cd.Oculto && (nativo?.Ocultable ?? true);               // lo que la lógica necesita no se oculta
                r[cd.Clave] = (seccion, new Ubicacion(x, Math.Max(0, cd.Y), w, Math.Max(1, cd.H), cd.Etiqueta, oculto));
                if (!oculto) Oc(seccion).Add((x, Math.Max(0, cd.Y), w, Math.Max(1, cd.H)));
            }
        }
        // Lo que el diseño aún no conoce (campos agregados al código después): en su paso, primer hueco, ancho del código.
        foreach (var c in e.Campos.Where(c => !r.ContainsKey(c.Clave)))
        {
            var oc = Oc(c.Seccion);
            var (x, y) = Hueco(oc, c.Ancho, c.Alto);
            r[c.Clave] = (c.Seccion, new Ubicacion(x, y, c.Ancho, c.Alto, null, false));
            oc.Add((x, y, c.Ancho, c.Alto));
        }
        return r;
    }

    public static bool EsRejilla(Estructura e, Diseno? d, string paso) =>
        e.Secciones.FirstOrDefault(s => s.Clave == paso) is { } s ? !s.Bloque && !s.Libre
        : d?.Secciones.Any(x => x.Clave == paso && x.Nueva) == true;

    // Primer hueco libre (de arriba a abajo, izquierda a derecha) sin chocar con lo ya puesto.
    public static (int X, int Y) Hueco(List<(int X, int Y, int W, int H)> ocupado, int w, int h)
    {
        w = Math.Clamp(w, 1, Columnas);
        int fondo = ocupado.Count == 0 ? 0 : ocupado.Max(o => o.Y + o.H);
        // Primero después del último (flujo natural); si no cabe en esa fila, la siguiente.
        if (ocupado.Count > 0)
        {
            var ultimaFila = ocupado.Max(o => o.Y);
            var finFila = ocupado.Where(o => o.Y == ultimaFila).Max(o => o.X + o.W);
            if (finFila + w <= Columnas && !Choca(ocupado, finFila, ultimaFila, w, h)) return (finFila, ultimaFila);
        }
        for (int y = 0; y <= fondo; y++)
            for (int x = 0; x + w <= Columnas; x++)
                if (!Choca(ocupado, x, y, w, h) && y >= (ocupado.Count == 0 ? 0 : ocupado.Max(o => o.Y))) return (x, y);
        return (0, fondo);
    }

    private static bool Choca(List<(int X, int Y, int W, int H)> o, int x, int y, int w, int h) =>
        o.Any(p => x < p.X + p.W && p.X < x + w && y < p.Y + p.H && p.Y < y + h);
}
