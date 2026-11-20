using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Documentos;
using SigWeb.Data.Riesgos;
using SigWeb.Models;

namespace SigWeb.Data.Catalogos;

// Renombrar una opción de un catálogo (Configuración › Catálogos) = los registros que ya la usan toman el nombre nuevo.
// Los valores se guardan como texto (no como Id), así que sin esto quedarían huérfanos (sin color, fuera de filtros).
// Cubre: campos de lista de cualquier tabla/formulario ligados a ese catálogo (CampoTabla.CatalogoTipo) y las propiedades
// del código que se llenan con un catálogo conocido (Referencias). Propiedad nueva ligada a un catálogo = una línea aquí.
public static class PropagadorCatalogo
{
    private sealed record Referencia(string Catalogo, Func<AppDbContext, string, string, Task<int>> Aplicar, string Donde, string Campo = "");

    private static readonly Referencia[] Referencias =
    {
        new(SembradorCatalogosRiesgo.OrigenHallazgo, (db, a, b) => db.PlanesAccion.IgnoreQueryFilters().Where(x => x.OrigenHallazgo == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.OrigenHallazgo, b)), "Planes de acción", "Origen del hallazgo"),
        new(SembradorCatalogosRiesgo.TipoHallazgo, (db, a, b) => db.PlanesAccion.IgnoreQueryFilters().Where(x => x.TipoHallazgo == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.TipoHallazgo, b)), "Planes de acción", "Tipo de hallazgo"),
        new(SembradorCatalogosRiesgo.ProcesoPlan, (db, a, b) => EnListaAsync(db.PlanesAccion.IgnoreQueryFilters().Where(x => x.Proceso.Contains(a)), x => x.Proceso, (x, v) => x.Proceso = v, db, a, b), "Planes de acción", "Procesos"),
        new(SembradorCatalogosRiesgo.EstadoActual, (db, a, b) => db.Riesgos.IgnoreQueryFilters().Where(x => x.EstadoActual == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.EstadoActual, b)), "Riesgos", "Estado actual"),
        new(SembradorCatalogosRiesgo.Categoria, (db, a, b) => db.Riesgos.IgnoreQueryFilters().Where(x => x.Categoria == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.Categoria, b)), "Riesgos", "Categoría"),
        new(SembradorCatalogosRiesgo.Area, (db, a, b) => db.Riesgos.IgnoreQueryFilters().Where(x => x.Area == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.Area, b)), "Riesgos", "Área"),
        new(SembradorCatalogosRiesgo.Estrategia, (db, a, b) => db.Riesgos.IgnoreQueryFilters().Where(x => x.Estrategia == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estrategia, b)), "Riesgos", "Estrategia de respuesta"),
        new(SembradorCatalogosRiesgo.EstadoPlan, (db, a, b) => db.Riesgos.IgnoreQueryFilters().Where(x => x.EstadoPlan == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.EstadoPlan, b)), "Riesgos", "Estado del plan"),
        new(MigracionDocumentos.CatTipo, (db, a, b) => db.DocumentosMaestro.IgnoreQueryFilters().Where(x => x.TipoDocumental == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.TipoDocumental, b)), "Documentos", "Tipo documental"),
        new(MigracionDocumentos.CatEstado, (db, a, b) => db.DocumentosMaestro.IgnoreQueryFilters().Where(x => x.Estado == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, b)), "Documentos", "Estado"),
        new(SigWeb.Data.Costos.EstadoTorres.Catalogo, (db, a, b) => db.EstadosTorreManual.IgnoreQueryFilters().Where(x => x.Estado == a).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, b)), "Costos", "Estado de las torres"),
    };

    // Dónde usa el código cada catálogo (columna "Usado en" de Catálogos): (catálogo, "Módulo · Campo").
    public static IEnumerable<(string Catalogo, string Uso)> Usos() =>
        Referencias.Select(r => (r.Catalogo, string.IsNullOrEmpty(r.Campo) ? r.Donde : $"{r.Donde} · {r.Campo}"));

    // Devuelve un resumen ("Planes de acción: 4, Especialidad: 2") para avisar al usuario.
    public static async Task<string> RenombrarAsync(AppDbContext db, string catalogo, string viejo, string nuevo)
    {
        viejo = viejo.Trim(); nuevo = nuevo.Trim();
        if (viejo.Length == 0 || string.Equals(viejo, nuevo, StringComparison.Ordinal)) return "";
        var partes = new List<string>();

        foreach (var r in Referencias.Where(r => string.Equals(r.Catalogo, catalogo, StringComparison.OrdinalIgnoreCase)))
        {
            var n = await r.Aplicar(db, viejo, nuevo);
            if (n > 0) partes.Add($"{r.Donde}: {n}");
        }

        // Campos de lista (columnas de las tablas y campos nuevos de los formularios) ligados a este catálogo.
        var campos = await db.CamposTabla.AsNoTracking().Where(c => c.CatalogoTipo == catalogo && !c.Eliminado).Select(c => new { c.Id, c.Nombre }).ToListAsync();
        foreach (var c in campos)
        {
            var n = await EnListaAsync(db.ValoresCampoTabla.Where(v => v.CampoTablaId == c.Id && v.Valor != null && v.Valor.Contains(viejo)),
                v => v.Valor ?? "", (v, t) => v.Valor = t, db, viejo, nuevo);
            if (n > 0) partes.Add($"{c.Nombre}: {n}");
        }
        db.ChangeTracker.Clear();
        return string.Join(", ", partes);
    }

    // Valor único o lista separada por coma/punto y coma ("A, B"): reemplaza solo el elemento exacto.
    private static async Task<int> EnListaAsync<T>(IQueryable<T> candidatos, Func<T, string> leer, Action<T, string> poner, AppDbContext db, string viejo, string nuevo) where T : class
    {
        int n = 0;
        foreach (var x in await candidatos.ToListAsync())
        {
            var texto = leer(x);
            var sep = texto.Contains(';') ? ";" : ",";
            var items = texto.Split(sep[0]).Select(s => s.Trim()).ToList();
            if (!items.Any(i => string.Equals(i, viejo, StringComparison.Ordinal))) continue;
            poner(x, string.Join(sep + " ", items.Select(i => string.Equals(i, viejo, StringComparison.Ordinal) ? nuevo : i)));
            n++;
        }
        if (n > 0) await db.SaveChangesAsync();
        return n;
    }
}
