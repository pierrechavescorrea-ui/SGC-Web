using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera del módulo Auditoría interna. Programa → auditorías → hallazgos se marcan con la misma
// marca de tiempo: restaurar el de arriba trae lo que se fue con él. El borrado definitivo va en
// cascada por la base (lugares, historial, anexos y vínculos con planes; los planes quedan).
public class ProveedorPapeleraProgramasAuditoria : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraProgramasAuditoria(AppDbContext db) { _db = db; }

    public string Modulo => "Programas de auditoría";

    public List<RegistroPapelera> Listar() =>
        _db.ProgramasAuditoria.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera { Modulo = Modulo, Id = x.Id, Titulo = $"{x.Anio} · {x.Titulo}", Subtitulo = x.Proyecto ?? x.Estado, FechaEliminacion = x.FechaEliminacion }).ToList();

    public void Restaurar(int id)
    {
        var p = _db.ProgramasAuditoria.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (p is null) return;
        var marca = p.FechaEliminacion;
        p.Eliminado = false; p.FechaEliminacion = null;
        var auds = _db.Auditorias.IgnoreQueryFilters().Where(a => a.ProgramaAuditoriaId == id && a.Eliminado && a.FechaEliminacion == marca).ToList();
        foreach (var a in auds) { a.Eliminado = false; a.FechaEliminacion = null; }
        var idsA = auds.Select(a => a.Id).ToList();
        foreach (var h in _db.HallazgosAuditoria.IgnoreQueryFilters().Where(h => idsA.Contains(h.AuditoriaId) && h.Eliminado && h.FechaEliminacion == marca).ToList())
        { h.Eliminado = false; h.FechaEliminacion = null; }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var p = _db.ProgramasAuditoria.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (p is not null) { _db.ProgramasAuditoria.Remove(p); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.ProgramasAuditoria.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraAuditorias : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraAuditorias(AppDbContext db) { _db = db; }

    public string Modulo => "Auditorías";

    // Las que se fueron con su programa se restauran desde el programa.
    public List<RegistroPapelera> Listar()
    {
        var progBorrados = _db.ProgramasAuditoria.IgnoreQueryFilters().AsNoTracking().Where(p => p.Eliminado).Select(p => p.Id).ToHashSet();
        return _db.Auditorias.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Where(x => !progBorrados.Contains(x.ProgramaAuditoriaId))
            .Select(x => new RegistroPapelera { Modulo = Modulo, Id = x.Id, Titulo = x.Codigo, Subtitulo = string.Join(" · ", new[] { x.Tipo, EscalaAuditoria.Mes(x.Mes) }.Where(t => !string.IsNullOrWhiteSpace(t))), FechaEliminacion = x.FechaEliminacion }).ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var a = _db.Auditorias.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (a is null) return new();
        var prog = _db.ProgramasAuditoria.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(p => p.Id == a.ProgramaAuditoriaId);
        if (prog is { Eliminado: true })
            return new() { new AvisoRestauracion { Nivel = "grave", Mensaje = $"Su programa ({prog.Anio} · {prog.Titulo}) está en la Papelera.", Solucion = "Restaura primero el programa.", Menus = new() { "Auditoría interna" } } };
        return new();
    }

    public void Restaurar(int id)
    {
        var a = _db.Auditorias.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (a is null) return;
        var marca = a.FechaEliminacion;
        a.Eliminado = false; a.FechaEliminacion = null;
        foreach (var h in _db.HallazgosAuditoria.IgnoreQueryFilters().Where(h => h.AuditoriaId == id && h.Eliminado && h.FechaEliminacion == marca).ToList())
        { h.Eliminado = false; h.FechaEliminacion = null; }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var a = _db.Auditorias.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (a is not null) { _db.Auditorias.Remove(a); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.Auditorias.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraHallazgosAuditoria : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraHallazgosAuditoria(AppDbContext db) { _db = db; }

    public string Modulo => "Hallazgos de auditoría";

    public List<RegistroPapelera> Listar()
    {
        var audBorradas = _db.Auditorias.IgnoreQueryFilters().AsNoTracking().Where(a => a.Eliminado).Select(a => a.Id).ToHashSet();
        return _db.HallazgosAuditoria.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Where(x => !audBorradas.Contains(x.AuditoriaId))
            .Select(x => new RegistroPapelera { Modulo = Modulo, Id = x.Id, Titulo = $"{x.Codigo} · {x.Tipo}", Subtitulo = x.Descripcion, FechaEliminacion = x.FechaEliminacion }).ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var h = _db.HallazgosAuditoria.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (h is null) return new();
        var a = _db.Auditorias.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == h.AuditoriaId);
        if (a is { Eliminado: true })
            return new() { new AvisoRestauracion { Nivel = "grave", Mensaje = $"Su auditoría ({a.Codigo}) está en la Papelera.", Solucion = "Restaura primero la auditoría.", Menus = new() { "Auditoría interna" } } };
        return new();
    }

    public void Restaurar(int id)
    {
        var h = _db.HallazgosAuditoria.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (h is not null) { h.Eliminado = false; h.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var h = _db.HallazgosAuditoria.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (h is not null) { _db.HallazgosAuditoria.Remove(h); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.HallazgosAuditoria.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraAnexosHallazgo : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraAnexosHallazgo(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Anexos de hallazgos de auditoría";

    public List<RegistroPapelera> Listar()
    {
        var hall = _db.HallazgosAuditoria.IgnoreQueryFilters().AsNoTracking().Select(h => new { h.Id, V = h.Codigo }).ToDictionary(x => x.Id, x => x.V);
        return _db.ArchivosHallazgoAuditoria.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera { Modulo = Modulo, Id = x.Id, Titulo = x.NombreOriginal, Subtitulo = hall.TryGetValue(x.HallazgoAuditoriaId, out var c) ? c : "", FechaEliminacion = x.FechaEliminacion }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.ArchivosHallazgoAuditoria.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ArchivosHallazgoAuditoria.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        try
        {
            var ruta = Path.Combine(_env.WebRootPath, x.RutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch { }
        _db.ArchivosHallazgoAuditoria.Remove(x);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.ArchivosHallazgoAuditoria.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

// Soportes de la verificación de eficacia de un plan de acción.
public class ProveedorPapeleraAnexosEficacia : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraAnexosEficacia(AppDbContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public string Modulo => "Soportes de eficacia de planes";

    public List<RegistroPapelera> Listar()
    {
        var planes = _db.PlanesAccion.IgnoreQueryFilters().AsNoTracking().Select(p => new { p.Id, V = p.Codigo }).ToDictionary(x => x.Id, x => x.V);
        return _db.ArchivosEficaciaPlan.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera { Modulo = Modulo, Id = x.Id, Titulo = x.NombreOriginal, Subtitulo = planes.TryGetValue(x.PlanAccionId, out var c) ? c : "", FechaEliminacion = x.FechaEliminacion }).ToList();
    }

    public void Restaurar(int id)
    {
        var x = _db.ArchivosEficaciaPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is not null) { x.Eliminado = false; x.FechaEliminacion = null; _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var x = _db.ArchivosEficaciaPlan.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (x is null) return;
        try
        {
            var ruta = Path.Combine(_env.WebRootPath, x.RutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch { }
        _db.ArchivosEficaciaPlan.Remove(x);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.ArchivosEficaciaPlan.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
