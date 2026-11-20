using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Formularios;

// Archivos de los campos "Archivos adjuntos" de los formularios (ArchivoCampoFormulario).
//   · El valor de celda del campo (ValorCampoTabla) es el número de archivos: la columna de la tabla, el Excel y el
//     Dossier muestran cuántos hay sin conocer los archivos.
//   · Carpeta de cada campo para las vistas de adjuntos (Planes de acción): el nombre del PASO donde está el campo en el
//     diseño vigente; si un paso tiene varios campos de adjuntos, "Paso · Campo"; si no está en el diseño, el nombre del campo.
public static class ArchivosCampo
{
    public const string CarpetaDisco = "formularios";

    public static async Task RecontarAsync(AppDbContext db, int campoId, int fila)
    {
        var n = await db.ArchivosCampoFormulario.CountAsync(a => a.CampoTablaId == campoId && a.FilaId == fila);
        var v = await db.ValoresCampoTabla.FirstOrDefaultAsync(x => x.CampoTablaId == campoId && x.FilaId == fila);
        if (v is null) { if (n == 0) return; v = new ValorCampoTabla { CampoTablaId = campoId, FilaId = fila }; db.ValoresCampoTabla.Add(v); }
        v.Valor = n == 0 ? null : n.ToString();
        await db.SaveChangesAsync();
    }

    // campo → nombre de carpeta, para los campos de adjuntos de un formulario.
    public static async Task<Dictionary<int, string>> NombresCarpetaAsync(ServicioFormularios servicio, string claveFormulario, IReadOnlyList<CampoTabla> campos)
    {
        if (campos.Count == 0) return new();
        return Nombres(await servicio.DisenoAsync(claveFormulario), await servicio.EstructuraAsync(claveFormulario) ?? new Estructura(), campos);
    }

    // Igual, leyendo el diseño vigente directo de la base (Dossier y otros procesos fuera de una página).
    public static async Task<Dictionary<int, string>> NombresCarpetaAsync(AppDbContext db, string claveFormulario, IReadOnlyList<CampoTabla> campos)
    {
        if (campos.Count == 0) return new();
        Diseno? d = null; Estructura? e = null;
        try
        {
            d = DisenoModelo.DeJson<Diseno>(await db.DisenosFormulario.AsNoTracking().Where(x => x.Clave == claveFormulario && x.Vigente)
                .OrderByDescending(x => x.Version).Select(x => x.Json).FirstOrDefaultAsync());
            if (d is { Original: true }) d = null;
            e = DisenoModelo.DeJson<Estructura>(await db.EstructurasFormulario.AsNoTracking().Where(x => x.Clave == claveFormulario).Select(x => x.Json).FirstOrDefaultAsync());
        }
        catch { }
        return Nombres(d, e ?? new Estructura(), campos);
    }

    private static Dictionary<int, string> Nombres(Diseno? d, Estructura e, IReadOnlyList<CampoTabla> campos)
    {
        var r = new Dictionary<int, string>();
        var pasos = DisenoModelo.Pasos(e, d).ToDictionary(p => p.Clave, p => p.Titulo);
        var pasoDe = new Dictionary<int, string?>();
        foreach (var c in campos)
        {
            var cd = d?.Campos.FirstOrDefault(x => x.CampoTablaId == c.Id && !x.Oculto);
            pasoDe[c.Id] = cd is not null && pasos.TryGetValue(cd.Seccion, out var t) ? t : null;
        }
        foreach (var c in campos)
        {
            var paso = pasoDe[c.Id];
            if (paso is null) { r[c.Id] = c.Nombre; continue; }
            var varios = pasoDe.Count(x => x.Value == paso) > 1;
            r[c.Id] = varios ? $"{Capitalizar(paso)} · {c.Nombre}" : Capitalizar(paso);
        }
        return r;
    }

    private static string Capitalizar(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
}
