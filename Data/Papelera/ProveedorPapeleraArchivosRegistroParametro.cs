using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera para los adjuntos (PDF, fotos) de un parámetro/checklist de
// una torre. Antes se borraban de una vez al hacer clic en "Quitar";
// ahora se piden confirmación y solo se marcan Eliminado = true — el
// archivo físico solo se borra desde acá (o al vencer el plazo).
public class ProveedorPapeleraArchivosRegistroParametro : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraArchivosRegistroParametro(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public string Modulo => "Adjuntos de checklist";

    // Una sola consulta con los datos de etiqueta (antes: toda la tabla de registros + una consulta por archivo).
    public List<RegistroPapelera> Listar()
    {
        var filas = (from a in _db.ArchivosRegistroParametro.AsNoTracking()
                     where a.Eliminado
                     join r in _db.RegistrosParametro.AsNoTracking() on a.RegistroParametroId equals r.Id into rj
                     from r in rj.DefaultIfEmpty()
                     join c in _db.Componentes.IgnoreQueryFilters().AsNoTracking() on r.ComponenteId equals c.Id into cj
                     from c in cj.DefaultIfEmpty()
                     join t in _db.ElementosTrazado.IgnoreQueryFilters().AsNoTracking() on c.ElementoTrazadoId equals t.Id into tj
                     from t in tj.DefaultIfEmpty()
                     join p in _db.ParametrosActividad.IgnoreQueryFilters().AsNoTracking() on r.ParametroActividadId equals p.Id into pj
                     from p in pj.DefaultIfEmpty()
                     orderby a.FechaEliminacion descending
                     select new
                     {
                         a.Id, a.NombreOriginal, a.FechaEliminacion,
                         HayRegistro = r != null,
                         Componente = c != null ? c.Nombre : null,
                         Torre = t != null ? t.Codigo : null,
                         Parametro = p != null ? p.Nombre : null
                     }).ToList();

        return filas.Select(a => new RegistroPapelera
        {
            Modulo = Modulo,
            Id = a.Id,
            Titulo = a.NombreOriginal,
            Subtitulo = !a.HayRegistro ? "" : $"{(a.Torre is null ? "" : $"Torre {a.Torre} — ")}{a.Componente} · {a.Parametro}",
            FechaEliminacion = a.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var a = _db.ArchivosRegistroParametro.Find(id);
        if (a is not null)
        {
            a.Eliminado = false;
            a.FechaEliminacion = null;
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var a = _db.ArchivosRegistroParametro.Find(id);
        if (a is not null)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosRegistroParametro.Remove(a);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.ArchivosRegistroParametro
            .Where(a => a.Eliminado && a.FechaEliminacion != null && a.FechaEliminacion < limite)
            .ToList();

        if (vencidos.Count == 0) return;

        foreach (var a in vencidos)
        {
            BorrarArchivoFisico(a.RutaArchivo);
            _db.ArchivosRegistroParametro.Remove(a);
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private void BorrarArchivoFisico(string? rutaWeb)
    {
        if (string.IsNullOrEmpty(rutaWeb)) return;
        try
        {
            var ruta = Path.Combine(_env.WebRootPath, rutaWeb.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch
        {
            // Archivo huérfano en disco, no crítico.
        }
    }
}
