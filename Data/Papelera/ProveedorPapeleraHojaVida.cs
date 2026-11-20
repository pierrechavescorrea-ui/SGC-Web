using Microsoft.EntityFrameworkCore;
using SigWeb.Models;
namespace SigWeb.Data.Papelera;

public class ProveedorPapeleraCarpetasHojaVida(AppDbContext db, IWebHostEnvironment env) : IProveedorPapelera
{
    public string Modulo => "Carpetas globales de hojas de vida";
    public List<RegistroPapelera> Listar() => db.CarpetasHojaVida.IgnoreQueryFilters().AsNoTracking().Where(c => c.Eliminado)
        .OrderByDescending(c => c.FechaEliminacion).Select(c => new RegistroPapelera { Modulo = Modulo, Id = c.Id, Titulo = c.Nombre, Subtitulo = c.ClaveElemento == null ? "Todas las torres" : "Elemento: " + c.ClaveElemento + " · Todas las torres", FechaEliminacion = c.FechaEliminacion }).ToList();
    public void Restaurar(int id)
    {
        var c = db.CarpetasHojaVida.IgnoreQueryFilters().FirstOrDefault(c => c.Id == id);
        if (c is null) return;
        if (db.CarpetasHojaVida.Any(x => x.Id != id && x.NombreNormalizado == c.NombreNormalizado && x.ClaveElemento == c.ClaveElemento))
            throw new InvalidOperationException("Ya existe una carpeta activa con ese nombre. Renómbrala antes de restaurar.");
        c.Eliminado = false; c.FechaEliminacion = null;
        db.SaveChanges(); db.ChangeTracker.Clear();
    }
    public void EliminarDefinitivo(int id)
    {
        var c = db.CarpetasHojaVida.IgnoreQueryFilters().FirstOrDefault(c => c.Id == id && c.Eliminado);
        if (c is null) return;
        var archivos = db.ArchivosCarpetaHojaVida.IgnoreQueryFilters().Where(a => a.CarpetaHojaVidaId == id).ToList();
        db.ArchivosCarpetaHojaVida.RemoveRange(archivos); db.CarpetasHojaVida.Remove(c); db.SaveChanges();
        foreach (var a in archivos) ProveedorPapeleraArchivosHojaVida.BorrarFisico(env, a.Ruta);
        db.ChangeTracker.Clear();
    }
    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-30);
        foreach (var id in db.CarpetasHojaVida.IgnoreQueryFilters().Where(c => c.Eliminado && c.FechaEliminacion < limite).Select(c => c.Id).ToList()) EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraArchivosHojaVida(AppDbContext db, IWebHostEnvironment env) : IProveedorPapelera
{
    public string Modulo => "Archivos de hojas de vida";
    public List<RegistroPapelera> Listar() => db.ArchivosCarpetaHojaVida.IgnoreQueryFilters().AsNoTracking().Where(a => a.Eliminado)
        .OrderByDescending(a => a.FechaEliminacion).Select(a => new RegistroPapelera { Modulo = Modulo, Id = a.Id, Titulo = a.NombreOriginal, Subtitulo = a.Carpeta.Nombre + " · Torre #" + a.ElementoTrazadoId + (a.ComponenteId == null ? "" : " · Elemento #" + a.ComponenteId), FechaEliminacion = a.FechaEliminacion }).ToList();
    public void Restaurar(int id)
    {
        var a = db.ArchivosCarpetaHojaVida.IgnoreQueryFilters().FirstOrDefault(a => a.Id == id);
        if (a is null) return;
        if (!db.CarpetasHojaVida.Any(c => c.Id == a.CarpetaHojaVidaId && c.Activo)) throw new InvalidOperationException("Restaura primero la carpeta de este archivo.");
        if (a.ComponenteId is int compId && !db.Componentes.Any(c => c.Id == compId && !c.Eliminado)) throw new InvalidOperationException("Restaura primero el elemento de este archivo.");
        a.Eliminado = false; a.FechaEliminacion = null; db.SaveChanges(); db.ChangeTracker.Clear();
    }
    public void EliminarDefinitivo(int id)
    {
        var a = db.ArchivosCarpetaHojaVida.IgnoreQueryFilters().FirstOrDefault(a => a.Id == id && a.Eliminado);
        if (a is null) return;
        db.ArchivosCarpetaHojaVida.Remove(a); db.SaveChanges(); BorrarFisico(env, a.Ruta); db.ChangeTracker.Clear();
    }
    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-30);
        foreach (var id in db.ArchivosCarpetaHojaVida.IgnoreQueryFilters().Where(a => a.Eliminado && a.FechaEliminacion < limite).Select(a => a.Id).ToList()) EliminarDefinitivo(id);
    }
    internal static void BorrarFisico(IWebHostEnvironment env, string ruta)
    {
        var raiz = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads", "hojavida")) + Path.DirectorySeparatorChar;
        var fisica = Path.GetFullPath(Path.Combine(env.WebRootPath, ruta.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (fisica.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) && File.Exists(fisica)) File.Delete(fisica);
    }
}
