using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Formularios;

// Campos asociados a las opciones de una lista (2026-11-16).
//   · Nombre de todo campo asociado = lo que escribe el usuario + " - " + la opción: "Valor - Consultoría".
//   · "Aplicar a todas las opciones": el campo queda como PLANTILLA de la lista (CampoTabla con OpcionPadre = "*", oculto en la
//     tabla). Cada opción tiene su propio campo real ("Valor - Humanos", "Valor - Equipos"…: columna, Excel, totales) y las opciones
//     que se agreguen después al catálogo reciben el suyo al sincronizar (al abrir el formulario o el diseñador).
//     En el formulario la plantilla se pinta como un bloque con una casilla por cada opción elegida en la lista.
public static class CamposPorOpcion
{
    public const string Todas = "*";
    public const string Separador = " - ";

    public static bool EsPlantilla(CampoTabla c) => c.CampoPadreId is not null && c.OpcionPadre == Todas;
    public static bool EsAsociado(CampoTabla c) => c.CampoPadreId is not null && !string.IsNullOrWhiteSpace(c.OpcionPadre) && c.OpcionPadre != Todas;

    public static string NombreDe(string baseNombre, string opcion) => $"{baseNombre.Trim()}{Separador}{opcion.Trim()}";

    // Nombre que escribió el usuario: sin " - opción" (ni el sufijo antiguo " (opción)").
    public static string Base(CampoTabla c)
    {
        if (!EsAsociado(c)) return c.Nombre;
        var op = c.OpcionPadre!.Trim();
        foreach (var suf in new[] { Separador + op, $" ({op})" })
            if (c.Nombre.EndsWith(suf, StringComparison.OrdinalIgnoreCase) && c.Nombre.Length > suf.Length) return c.Nombre[..^suf.Length].Trim();
        return c.Nombre;
    }

    // Campos asociados que reparte una plantilla (mismo padre, mismo nombre base), en el orden de las opciones.
    public static List<CampoTabla> HijosDe(CampoTabla plantilla, IEnumerable<CampoTabla> campos) =>
        campos.Where(c => EsAsociado(c) && c.CampoPadreId == plantilla.CampoPadreId && !c.Eliminado
                          && string.Equals(Base(c), plantilla.Nombre, StringComparison.OrdinalIgnoreCase)).ToList();

    public static CampoTabla? PlantillaDe(CampoTabla hijo, IEnumerable<CampoTabla> campos) =>
        !EsAsociado(hijo) ? null : campos.FirstOrDefault(p => EsPlantilla(p) && !p.Eliminado && p.CampoPadreId == hijo.CampoPadreId
                                                              && string.Equals(p.Nombre, Base(hijo), StringComparison.OrdinalIgnoreCase));

    public static async Task<List<string>> OpcionesAsync(AppDbContext db, CampoTabla padre)
    {
        if (!string.IsNullOrWhiteSpace(padre.CatalogoTipo))
            return await db.Catalogos.AsNoTracking().Where(i => i.Tipo == padre.CatalogoTipo && i.Activo).OrderBy(i => i.Nombre).Select(i => i.Nombre).ToListAsync();
        return (padre.Opciones ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
    }

    // Deja la tabla al día: nombres "Base - Opción" en todos los campos asociados y un campo por opción en cada plantilla.
    // Devuelve cuántos campos creó o renombró.
    public static async Task<int> SincronizarAsync(AppDbContext db, string tabla)
    {
        var campos = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == tabla && !c.Eliminado).ToListAsync();
        var asociados = campos.Where(EsAsociado).ToList();
        var plantillas = campos.Where(EsPlantilla).ToList();
        if (asociados.Count == 0 && plantillas.Count == 0) return 0;
        var motor = await MotorTabla.CargarLecturaAsync(db, tabla, Enumerable.Empty<ColumnaNativa>(), Enumerable.Empty<(int, string)>());
        int n = 0;
        // 1) Nombres: lo escrito + " - " + la opción.
        foreach (var c in asociados)
        {
            var nuevo = NombreDe(Base(c), c.OpcionPadre!);
            if (string.Equals(c.Nombre, nuevo, StringComparison.Ordinal) || campos.Any(o => o.Id != c.Id && string.Equals(o.Nombre, nuevo, StringComparison.OrdinalIgnoreCase))) continue;
            await motor.GuardarCampoAsync(db, Copia(c, nuevo));
            c.Nombre = nuevo; n++;
        }
        // 2) Plantillas: un campo por opción (también las agregadas después al catálogo).
        foreach (var p in plantillas)
        {
            var padre = campos.FirstOrDefault(c => c.Id == p.CampoPadreId);
            if (padre is null) continue;
            var hijos = HijosDe(p, campos);
            foreach (var op in await OpcionesAsync(db, padre))
            {
                if (hijos.Any(h => string.Equals(h.OpcionPadre?.Trim(), op, StringComparison.OrdinalIgnoreCase))) continue;
                var id = await motor.GuardarCampoAsync(db, new CampoTabla { Nombre = NombreDe(p.Nombre, op), Tipo = p.Tipo, Decimales = p.Decimales });
                var ent = await db.CamposTabla.FirstAsync(c => c.Id == id);
                ent.CampoPadreId = padre.Id; ent.OpcionPadre = op; ent.EnResumen = p.EnResumen;
                await db.SaveChangesAsync();
                db.Entry(ent).State = EntityState.Detached;
                campos.Add(ent); n++;
            }
        }
        return n;
    }

    private static CampoTabla Copia(CampoTabla c, string nombre) => new()
    {
        Id = c.Id, Nombre = nombre, Tipo = c.Tipo, Calculado = c.Calculado, Formula = c.Formula, CatalogoTipo = c.CatalogoTipo,
        Opciones = c.Opciones, Multiple = c.Multiple, Decimales = c.Decimales, Visible = c.Visible, Orden = c.Orden
    };
}
