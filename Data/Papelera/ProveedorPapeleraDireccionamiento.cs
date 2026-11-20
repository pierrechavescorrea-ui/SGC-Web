using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera del Direccionamiento estratégico. El registro solo guarda su clasificación: sus archivos
// son los de su documento del Listado maestro, que no se toca (sigue en Documentos).
public class ProveedorPapeleraDireccionamiento : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraDireccionamiento(AppDbContext db) { _db = db; }

    public string Modulo => "Direccionamiento estratégico";

    public List<RegistroPapelera> Listar() =>
        _db.DocumentosDireccionamiento.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.Titulo, Subtitulo = x.Tipo, FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var x = _db.DocumentosDireccionamiento.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.DocumentosDireccionamiento.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { _db.DocumentosDireccionamiento.Remove(x); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.DocumentosDireccionamiento.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
