using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Las referencias opcionales quedan en null al purgar; los archivos compartidos se conservan.
public class ProveedorPapeleraIconos : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly SigWeb.Data.Iconos.IconosApp _iconos;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraIconos(AppDbContext db, SigWeb.Data.Iconos.IconosApp iconos) { _db = db; _iconos = iconos; }

    public string Modulo => "Galería de íconos";

    public List<RegistroPapelera> Listar()
    {
        return _db.IconosCatalogo.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion)
            .ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo,
                Id = x.Id,
                Titulo = x.Nombre,
                Subtitulo = "Imagen o ícono",
                FechaEliminacion = x.FechaEliminacion
            })
            .ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.IconosCatalogo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            x.Eliminado = false;
            x.FechaEliminacion = null;

            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
        _iconos.Recargar();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.IconosCatalogo.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null)
        {
            LimpiarLogo(x);
            _db.IconosCatalogo.Remove(x);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
        _iconos.Recargar();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var vencidos = _db.IconosCatalogo.IgnoreQueryFilters()
            .Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite)
            .ToList();
        if (vencidos.Count == 0) return;
        foreach (var x in vencidos) LimpiarLogo(x);
        _db.IconosCatalogo.RemoveRange(vencidos);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        _iconos.Recargar();
    }
    private void LimpiarLogo(IconoCatalogo icono)
    {
        foreach (var config in _db.Configuracion.Where(c => c.LogoUrl == icono.RutaArchivo)) config.LogoUrl = null;
    }
}
