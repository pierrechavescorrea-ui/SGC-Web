using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Vistas;

namespace SigWeb.Data.Papelera;

// Vistas configurables de los registros (Catálogos › Vistas).
public class ProveedorPapeleraVistasRegistro : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraVistasRegistro(AppDbContext db) { _db = db; }

    public string Modulo => "Vistas de registros";

    public List<RegistroPapelera> Listar() =>
        _db.VistasRegistro.IgnoreQueryFilters().AsNoTracking().Where(v => v.Eliminado).OrderByDescending(v => v.FechaEliminacion).ToList()
            .Select(v => new RegistroPapelera
            {
                Modulo = Modulo, Id = v.Id, Titulo = v.Nombre,
                Subtitulo = VistasModelo.Modulos.FirstOrDefault(m => m.Modulo == v.Modulo) is { } m ? $"{m.Nombre} · {m.Vista}" : v.Modulo,
                FechaEliminacion = v.FechaEliminacion
            }).ToList();

    public void Restaurar(int id)
    {
        var v = _db.VistasRegistro.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (v is not null) { v.Eliminado = false; v.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var v = _db.VistasRegistro.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (v is not null) { _db.VistasRegistro.Remove(v); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.VistasRegistro.IgnoreQueryFilters().Where(v => v.Eliminado && v.FechaEliminacion != null && v.FechaEliminacion < limite).Select(v => v.Id).ToList())
            EliminarDefinitivo(id);
    }
}
