using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Modelos3D;

namespace SigWeb.Data.Papelera;

// Papelera de los modelos 3D cargados desde la app (una fila por versión, con sus archivos).
public class ProveedorPapeleraModelosIfc : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraModelosIfc(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Modelos 3D cargados";

    public List<RegistroPapelera> Listar()
    {
        try
        {
            return _db.ModelosIfcCargados.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList()
                .Select(x => new RegistroPapelera
                {
                    Modulo = Modulo, Id = x.Id, Titulo = $"{x.Codigo} v{x.Version}",
                    Subtitulo = string.Join(" · ", new[] { x.Familia, x.NombreOriginalIfc }.Where(t => !string.IsNullOrWhiteSpace(t))),
                    FechaEliminacion = x.FechaEliminacion
                }).ToList();
        }
        catch { return new(); }   // sin migración aplicada
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var x = _db.ModelosIfcCargados.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(e => e.Id == id);
        if (x is null) return new();
        var todos = ModelosCargados.Todos(_db);
        var k = ConfigModelos3D.Norm(x.Codigo);
        var mayor = todos.Where(t => ConfigModelos3D.Norm(t.Codigo) == k).Select(t => t.Version).DefaultIfEmpty(0).Max();
        if (x.Version > mayor)
            return new() { new AvisoRestauracion { Nivel = "info", Mensaje = $"{x.Codigo} v{x.Version} vuelve a ser la versión vigente.", Solucion = "Si no es lo que quieres, elimina esta versión de nuevo desde Modelos 3D › Versiones.", Menus = new() { "Modelos 3D", "Avance de obra" } } };
        return new();
    }

    public void Restaurar(int id)
    {
        var x = _db.ModelosIfcCargados.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ModelosIfcCargados.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            foreach (var a in ModelosCargados.Archivos(x)) ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, a);
            var raiz = ModelosCargados.RaizDe(x);
            _db.AsignacionesPiezaModelo.RemoveRange(_db.AsignacionesPiezaModelo.Where(a => a.Modelo == raiz));
            _db.ModelosIfcCargados.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        List<int> ids;
        try { ids = _db.ModelosIfcCargados.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList(); }
        catch { return; }
        foreach (var id in ids) EliminarDefinitivo(id);
    }
}
