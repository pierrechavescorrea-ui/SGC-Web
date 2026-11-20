using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Formularios;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Archivos de los campos "Archivos adjuntos" de los formularios (diseñador de formularios). Restaurar o borrar
// recalcula el número de archivos del campo (su valor de celda).
public class ProveedorPapeleraArchivosCampoFormulario : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;

    public ProveedorPapeleraArchivosCampoFormulario(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Adjuntos de formularios";

    public List<RegistroPapelera> Listar()
    {
        var l = _db.ArchivosCampoFormulario.IgnoreQueryFilters().AsNoTracking().Where(a => a.Eliminado)
            .OrderByDescending(a => a.FechaEliminacion).ToList();
        var ids = l.Select(a => a.CampoTablaId).Distinct().ToList();
        var campos = _db.CamposTabla.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Nombre, c.Tabla }).ToDictionary(c => c.Id);
        return l.Select(a =>
        {
            var c = campos.GetValueOrDefault(a.CampoTablaId);
            var form = c is null ? null : RegistroFormularios.Todos.FirstOrDefault(f => f.Tabla == c.Tabla)?.Nombre;
            return new RegistroPapelera
            {
                Modulo = Modulo, Id = a.Id, Titulo = a.NombreOriginal,
                Subtitulo = c is null ? $"Registro #{a.FilaId}" : $"{form ?? c.Tabla} · {c.Nombre} · registro #{a.FilaId}",
                FechaEliminacion = a.FechaEliminacion
            };
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var a = _db.ArchivosCampoFormulario.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (a is not null)
        {
            a.Eliminado = false; a.FechaEliminacion = null;
            _db.SaveChanges();
            ArchivosCampo.RecontarAsync(_db, a.CampoTablaId, a.FilaId).GetAwaiter().GetResult();
        }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var a = _db.ArchivosCampoFormulario.IgnoreQueryFilters().FirstOrDefault(x => x.Id == id);
        if (a is not null)
        {
            ProveedorPapeleraAdjuntosMaterial.BorrarFisico(_env, a.RutaArchivo);
            _db.ArchivosCampoFormulario.Remove(a);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.ArchivosCampoFormulario.IgnoreQueryFilters().Where(a => a.Eliminado && a.FechaEliminacion != null && a.FechaEliminacion < limite).Select(a => a.Id).ToList())
            EliminarDefinitivo(id);
    }
}
