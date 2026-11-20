using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de la vitrina de Certificaciones. El certificado solo guarda sus datos: sus archivos
// son los de su documento del Listado maestro, que no se toca (sigue en Documentos).
public class ProveedorPapeleraCertificaciones : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraCertificaciones(AppDbContext db) { _db = db; }

    public string Modulo => "Certificaciones";

    public List<RegistroPapelera> Listar() =>
        _db.Certificaciones.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.Norma,
                Subtitulo = string.Join(" · ", new[] { x.Sistema, x.Numero, x.Organismo }.Where(t => !string.IsNullOrWhiteSpace(t))),
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var x = _db.Certificaciones.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.Certificaciones.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { _db.Certificaciones.Remove(x); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.Certificaciones.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
