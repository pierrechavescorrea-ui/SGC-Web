using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.CreadorModulos;

// Eliminar un módulo creado, en dos pasos (como la Papelera):
//   1) Eliminar → Estado = Eliminado: sale del menú y de los permisos en todos los proyectos; definición y registros intactos;
//      se restaura desde «Eliminados» en Administración › Creador de módulos.
//   2) Eliminar definitivamente → borra la definición, su historia y, en la base de CADA proyecto, sus registros, valores,
//      columnas, vínculos y adjuntos (también los archivos en uploads/modulos/{clave}). No se puede deshacer.
// Las bases de los proyectos se abren con ServicioProyectos.CrearContexto, cada una con su sesión de sistema.
public static class EliminacionModulos
{
    public sealed record ConteoProyecto(string Proyecto, int Registros, int Adjuntos);

    // Registros (incluida la Papelera) y adjuntos del módulo en cada proyecto. Un proyecto que no abre se informa con -1.
    public static async Task<List<ConteoProyecto>> ContarAsync(ServicioProyectos proyectos, string clave)
    {
        var l = new List<ConteoProyecto>();
        foreach (var pr in proyectos.Todos())
        {
            if (proyectos.Activo(pr.Id) is not { } p) continue;
            try
            {
                using var _ = AmbienteSig.Usar(new SesionSig { Proyecto = p, Sistema = true });
                await using var db = proyectos.CrearContexto(p);
                var ids = await db.RegistrosModulo.IgnoreQueryFilters().Where(r => r.Modulo == clave).Select(r => r.Id).ToListAsync();
                var adj = ids.Count == 0 ? 0 : await db.ArchivosRegistroModulo.IgnoreQueryFilters().CountAsync(a => ids.Contains(a.RegistroModuloId));
                if (ids.Count > 0 || adj > 0) l.Add(new ConteoProyecto(p.Nombre, ids.Count, adj));
            }
            catch { l.Add(new ConteoProyecto(p.Nombre, -1, 0)); }
        }
        return l;
    }

    public static async Task EliminarAsync(SeguridadDbContext db, int id)
    {
        var m = await db.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        if (m.Version == 0)
        {
            // Nunca publicado: no puede tener registros en ningún proyecto; se borra el diseño.
            db.ModulosCreados.Remove(m);
        }
        else
        {
            m.Estado = EstadoModuloCreado.Eliminado;
            m.ModificadoEn = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    public static async Task RestaurarAsync(SeguridadDbContext db, int id)
    {
        var m = await db.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        if (await db.ModulosCreados.AnyAsync(x => x.Id != id && x.Estado != EstadoModuloCreado.Eliminado && x.Nombre.ToLower() == m.Nombre.ToLower()))
            throw new InvalidOperationException($"Ya hay otro módulo llamado «{m.Nombre}»: renombre ese antes de restaurar este.");
        // Sin migración nueva: vuelve publicado si alguna vez se publicó (si estaba archivado, se archiva de nuevo a mano).
        m.Estado = m.DefinicionJson is null ? EstadoModuloCreado.Borrador : EstadoModuloCreado.Publicado;
        m.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // Borrado definitivo (solo de un módulo ya en «Eliminados»). Devuelve un resumen. Si un proyecto falla, no se borra la
    // definición (para poder reintentar) y el error se informa.
    public static async Task<string> PurgarAsync(SeguridadDbContext central, ServicioProyectos proyectos, string webRoot, int id)
    {
        var m = await central.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        if (m.Estado != EstadoModuloCreado.Eliminado) throw new InvalidOperationException("Primero elimine el módulo (pasa a «Eliminados»); después puede borrarlo definitivamente.");
        var clave = m.Clave;
        var tabla = "mod:" + clave;
        int registros = 0, fallas = 0;
        foreach (var pr in proyectos.Todos())
        {
            if (proyectos.Activo(pr.Id) is not { } p) continue;
            try
            {
                using var _ = AmbienteSig.Usar(new SesionSig { Proyecto = p, Sistema = true });
                await using var db = proyectos.CrearContexto(p);
                await using var tx = await db.Database.BeginTransactionAsync();
                var ids = await db.RegistrosModulo.IgnoreQueryFilters().Where(r => r.Modulo == clave).Select(r => r.Id).ToListAsync();
                var campos = await db.CamposTabla.IgnoreQueryFilters().Where(c => c.Tabla == tabla).Select(c => c.Id).ToListAsync();
                if (campos.Count > 0)
                {
                    await db.ValoresCampoTabla.Where(v => campos.Contains(v.CampoTablaId)).ExecuteDeleteAsync();
                    await db.FormatosCondicionales.Where(f => campos.Contains(f.CampoTablaId)).ExecuteDeleteAsync();
                    await db.CamposTabla.IgnoreQueryFilters().Where(c => c.Tabla == tabla).ExecuteDeleteAsync();
                }
                if (ids.Count > 0)
                {
                    await db.VinculosRegistroModulo.IgnoreQueryFilters().Where(v => ids.Contains(v.RegistroModuloId)).ExecuteDeleteAsync();
                    await db.ArchivosRegistroModulo.IgnoreQueryFilters().Where(a => ids.Contains(a.RegistroModuloId)).ExecuteDeleteAsync();
                    await db.RegistrosModulo.IgnoreQueryFilters().Where(r => r.Modulo == clave).ExecuteDeleteAsync();
                }
                await tx.CommitAsync();
                registros += ids.Count;
            }
            catch { fallas++; }
        }
        if (fallas > 0)
            throw new InvalidOperationException($"No se pudo limpiar {fallas} proyecto(s); el módulo sigue en «Eliminados» para reintentar.");

        // Archivos físicos (uploads/modulos/{clave}); la ruta se valida dentro de la carpeta de módulos.
        try
        {
            var raiz = Path.GetFullPath(Path.Combine(webRoot, "uploads", "modulos"));
            var carpeta = Path.GetFullPath(Path.Combine(raiz, clave));
            if (carpeta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(carpeta))
                Directory.Delete(carpeta, recursive: true);
        }
        catch { /* archivos huérfanos: sin consecuencias para la app */ }

        central.ModulosCreados.Remove(m);   // la historia (VersionesModuloCreado) se borra en cascada
        await central.SaveChangesAsync();
        return $"«{m.Nombre}» se borró definitivamente ({registros} registro(s) en todos los proyectos).";
    }
}
