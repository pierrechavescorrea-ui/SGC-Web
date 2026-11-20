using Microsoft.EntityFrameworkCore;
using SigWeb.Data.CreadorModulos;

namespace SigWeb.Data.Papelera;

public sealed class ProveedorPapeleraModulosCreados(AppDbContext db, IWebHostEnvironment env, RegistroModulosCreados modulos) : IProveedorPapelera
{
    public string Modulo => "Registros de módulos creados";

    public List<RegistroPapelera> Listar() => db.RegistrosModulo.IgnoreQueryFilters().AsNoTracking()
        .Where(r => r.Eliminado).OrderByDescending(r => r.FechaEliminacion)
        .Select(r => new { r.Id, r.Modulo, r.Codigo, r.Titulo, r.FechaEliminacion }).ToList()
        .Select(r => new RegistroPapelera
        {
            Modulo = Modulo, Id = r.Id,
            Titulo = $"{modulos.De(r.Modulo)?.Nombre ?? r.Modulo} · {r.Codigo} {r.Titulo}".Trim(),
            FechaEliminacion = r.FechaEliminacion
        }).ToList();

    public void Restaurar(int id)
    {
        var r = db.RegistrosModulo.IgnoreQueryFilters().FirstOrDefault(r => r.Id == id && r.Eliminado);
        if (r is not null)
        {
            // Solo vuelven con él los adjuntos eliminados en la misma operación.
            foreach (var a in db.ArchivosRegistroModulo.IgnoreQueryFilters()
                .Where(a => a.RegistroModuloId == id && a.Eliminado && a.FechaEliminacion == r.FechaEliminacion))
            { a.Eliminado = false; a.FechaEliminacion = null; }
            r.Eliminado = false; r.FechaEliminacion = null;
            db.SaveChanges();
        }
        db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var r = db.RegistrosModulo.IgnoreQueryFilters().FirstOrDefault(r => r.Id == id && r.Eliminado);
        if (r is not null)
        {
            var tabla = "mod:" + r.Modulo;
            var archivos = db.ArchivosRegistroModulo.IgnoreQueryFilters().Where(a => a.RegistroModuloId == id).ToList();
            foreach (var a in archivos) ProveedorPapeleraArchivosModulosCreados.BorrarFisico(env, a.Ruta);
            db.ValoresCampoTabla.RemoveRange(db.ValoresCampoTabla.IgnoreQueryFilters().Where(v => v.FilaId == id
                && db.CamposTabla.IgnoreQueryFilters().Any(c => c.Id == v.CampoTablaId && c.Tabla == tabla)));
            db.VinculosRegistroModulo.RemoveRange(db.VinculosRegistroModulo.IgnoreQueryFilters().Where(v => v.RegistroModuloId == id));
            db.ArchivosRegistroModulo.RemoveRange(archivos);
            db.RegistrosModulo.Remove(r);
            db.SaveChanges();
        }
        db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-30);
        foreach (var id in db.RegistrosModulo.IgnoreQueryFilters().Where(r => r.Eliminado && r.FechaEliminacion < limite)
            .Select(r => r.Id).ToList()) EliminarDefinitivo(id);
    }
}

public sealed class ProveedorPapeleraArchivosModulosCreados(AppDbContext db, IWebHostEnvironment env, RegistroModulosCreados modulos)
    : IProveedorPapelera, IValidaRestauracion
{
    public string Modulo => "Adjuntos de módulos creados";

    public List<RegistroPapelera> Listar()
    {
        var filas = (from a in db.ArchivosRegistroModulo.IgnoreQueryFilters().AsNoTracking()
                     where a.Eliminado
                     join r in db.RegistrosModulo.IgnoreQueryFilters().AsNoTracking() on a.RegistroModuloId equals r.Id into registros
                     from r in registros.DefaultIfEmpty()
                     orderby a.FechaEliminacion descending
                     select new { a.Id, a.Nombre, a.FechaEliminacion, a.RegistroModuloId,
                         Clave = r == null ? null : r.Modulo, Codigo = r == null ? null : r.Codigo, Titulo = r == null ? null : r.Titulo }).ToList();
        return filas.Select(a => new RegistroPapelera
        {
            Modulo = Modulo, Id = a.Id, Titulo = a.Nombre,
            Subtitulo = a.Clave is null ? $"Registro #{a.RegistroModuloId}"
                : $"{modulos.De(a.Clave)?.Nombre ?? a.Clave} · {a.Codigo} {a.Titulo}".Trim(),
            FechaEliminacion = a.FechaEliminacion
        }).ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var padre = db.ArchivosRegistroModulo.IgnoreQueryFilters().AsNoTracking().Where(a => a.Id == id)
            .Select(a => (int?)a.RegistroModuloId).FirstOrDefault();
        if (padre is null || db.RegistrosModulo.AsNoTracking().Any(r => r.Id == padre && !r.Eliminado)) return [];
        return [new AvisoRestauracion
        {
            Nivel = "grave", Mensaje = "El registro de este adjunto está en la Papelera o ya no existe.",
            Solucion = "Restaura primero el registro antes de recuperar su adjunto.", Menus = ["Módulos creados"]
        }];
    }

    public void Restaurar(int id)
    {
        var a = db.ArchivosRegistroModulo.IgnoreQueryFilters().FirstOrDefault(a => a.Id == id && a.Eliminado);
        if (a is not null)
        {
            if (!db.RegistrosModulo.Any(r => r.Id == a.RegistroModuloId && !r.Eliminado))
                throw new InvalidOperationException("Restaura primero el registro de este adjunto.");
            a.Eliminado = false; a.FechaEliminacion = null;
            db.SaveChanges();
        }
        db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var a = db.ArchivosRegistroModulo.IgnoreQueryFilters().FirstOrDefault(a => a.Id == id && a.Eliminado);
        if (a is not null)
        {
            BorrarFisico(env, a.Ruta);
            db.ArchivosRegistroModulo.Remove(a);
            db.SaveChanges();
        }
        db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-30);
        foreach (var id in db.ArchivosRegistroModulo.IgnoreQueryFilters().Where(a => a.Eliminado && a.FechaEliminacion < limite)
            .Select(a => a.Id).ToList()) EliminarDefinitivo(id);
    }

    internal static void BorrarFisico(IWebHostEnvironment env, string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return;
        // Mismo límite por carpeta y normalización absoluta que los archivos de hojas de vida.
        var raiz = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads", "modulos")) + Path.DirectorySeparatorChar;
        var fisica = Path.GetFullPath(Path.Combine(env.WebRootPath, ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (fisica.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) && File.Exists(fisica)) File.Delete(fisica);
    }
}
