using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Navegacion;
using SigWeb.Models;

namespace SigWeb.Data.Catalogos;

// Módulo de cada catálogo (DefinicionCatalogo.Modulo). Los que no lo tienen (null) se clasifican al arrancar por su
// entidad asociada y su nombre ("Riesgos — …", "Plan de acción — …"); si nada coincide quedan en General ("").
// Idempotente: solo toca los null, así lo que el usuario elija (incluido General) se respeta.
public static class ModulosCatalogo
{
    // Palabra clave (sin tildes, minúsculas) → ruta del módulo. El orden importa: la primera que aparezca gana.
    private static readonly (string Clave, string Ruta)[] Claves =
    {
        ("planes de accion", "planes-accion"), ("plan de accion", "planes-accion"),
        ("auditoria", "auditoria-interna"),
        ("riesgo", "riesgos"),
        ("listado maestro de documentos", "documentos"), ("tipo documental", "documentos"), ("estado del documento", "documentos"),
        ("partes interesadas", "partes-interesadas"),
        ("costos", "costos"), ("estado de torre", "costos"),
        ("comunicado", "comunicados"),
        ("indicador", "indicadores"),
        ("certificacion", "certificaciones"),
        ("organigrama", "organigrama"),
        ("avance de obra", "avance-obra"),
        ("materiales", "materiales"), ("material", "materiales"), ("proveedor", "materiales"),
        ("ensayo", "ensayos"), ("laboratorio", "ensayos"), ("criterio de aceptacion", "ensayos"),
        ("planos", "planos"),
        ("equipos", "equipos"),
        ("trazado", "trazado"), ("torre", "trazado"), ("localizacion", "trazado"),
    };

    public static string Deducir(string nombre, string? entidad)
    {
        // Primero lo que dice la entidad asociada antes del "—" (el módulo que la usa), luego el resto y el nombre.
        var e = entidad ?? "";
        var corte = e.IndexOfAny(new[] { '—', '–' });
        foreach (var texto in new[] { corte > 0 ? e[..corte] : e, e, nombre })
        {
            var t = Normal(texto);
            if (t.Length == 0) continue;
            foreach (var (clave, ruta) in Claves) if (t.Contains(clave)) return ruta;
            foreach (var m in MapaNavegacion.Modulos()) if (t.Contains(Normal(m.Nombre))) return m.Href;
        }
        return "";
    }

    public static void ClasificarPendientes(AppDbContext db)
    {
        var pendientes = db.DefinicionesCatalogo.IgnoreQueryFilters().Where(d => d.Modulo == null).ToList();
        if (pendientes.Count == 0) return;
        foreach (var d in pendientes) d.Modulo = Deducir(d.Nombre, d.EntidadAsociada);
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    public static string NombreModulo(string? ruta) =>
        string.IsNullOrEmpty(ruta) ? "General" : MapaNavegacion.De(ruta)?.Nombre ?? ruta;

    private static string Normal(string s)
    {
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString();
    }
}
