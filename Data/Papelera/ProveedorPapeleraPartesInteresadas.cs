using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de Partes interesadas: versiones completas (con sus partes), partes quitadas de un borrador y seguimientos.
public class ProveedorPapeleraVersionesPartes : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraVersionesPartes(AppDbContext db) { _db = db; }

    public string Modulo => "Partes interesadas · versiones";

    public List<RegistroPapelera> Listar() =>
        _db.VersionesPartesInteresadas.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = $"v{x.Numero} · {x.Estado}",
                Subtitulo = string.Join(" · ", new[] { x.Codigo, x.MotivoCambio }.Where(t => !string.IsNullOrWhiteSpace(t))),
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public List<AvisoRestauracion> Revisar(int id)
    {
        var v = _db.VersionesPartesInteresadas.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        var r = new List<AvisoRestauracion>();
        if (v is null) return r;
        if (v.Estado == EstadoVersionPartes.Vigente && _db.VersionesPartesInteresadas.Any(x => x.Estado == EstadoVersionPartes.Vigente))
            r.Add(new AvisoRestauracion { Mensaje = $"Ya hay otra versión vigente: la v{v.Numero} vuelve como Obsoleta.", Menus = { "Partes interesadas" } });
        if (v.Estado == EstadoVersionPartes.Borrador && _db.VersionesPartesInteresadas.Any(x => x.Estado == EstadoVersionPartes.Borrador))
            r.Add(new AvisoRestauracion { Mensaje = $"Ya hay un borrador: la v{v.Numero} vuelve como Obsoleta (solo consulta).", Menus = { "Partes interesadas" } });
        return r;
    }

    public void Restaurar(int id)
    {
        var v = _db.VersionesPartesInteresadas.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (v is null) return;
        if (v.Estado != EstadoVersionPartes.Obsoleta && _db.VersionesPartesInteresadas.Any(x => x.Estado == v.Estado && x.Id != id))
            v.Estado = EstadoVersionPartes.Obsoleta;
        v.Eliminado = false; v.FechaEliminacion = null;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var v = _db.VersionesPartesInteresadas.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (v is not null)
        {
            // Los seguimientos quedan (van por Clave); solo pierden la fila de esta versión (SetNull).
            var ids = _db.PartesInteresadas.IgnoreQueryFilters().Where(p => p.VersionPartesInteresadasId == id).Select(p => p.Id).ToList();
            _db.SeguimientosParteInteresada.IgnoreQueryFilters().Where(s => s.ParteInteresadaId != null && ids.Contains(s.ParteInteresadaId.Value))
                .ExecuteUpdate(s => s.SetProperty(x => x.ParteInteresadaId, (int?)null));
            _db.PartesInteresadas.IgnoreQueryFilters().Where(p => p.VersionPartesInteresadasId == id).ExecuteDelete();
            _db.VersionesPartesInteresadas.IgnoreQueryFilters().Where(x => x.VersionBaseId == id).ExecuteUpdate(s => s.SetProperty(x => x.VersionBaseId, (int?)null));
            _db.VersionesPartesInteresadas.Remove(v);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.VersionesPartesInteresadas.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraPartes : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraPartes(AppDbContext db) { _db = db; }

    public string Modulo => "Partes interesadas · partes";

    // Solo las partes cuya versión no está en la Papelera (si no, vuelven con ella).
    public List<RegistroPapelera> Listar()
    {
        var versiones = _db.VersionesPartesInteresadas.AsNoTracking().Select(v => new { v.Id, V = v.Numero }).ToDictionary(x => x.Id, x => x.V);
        return _db.PartesInteresadas.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Where(x => versiones.ContainsKey(x.VersionPartesInteresadasId))
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.Nombre, Subtitulo = $"v{versiones[x.VersionPartesInteresadasId]}",
                FechaEliminacion = x.FechaEliminacion
            }).ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var r = new List<AvisoRestauracion>();
        var p = _db.PartesInteresadas.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (p is null) return r;
        var v = _db.VersionesPartesInteresadas.AsNoTracking().FirstOrDefault(x => x.Id == p.VersionPartesInteresadasId);
        if (v is not null && v.Estado != EstadoVersionPartes.Borrador)
        {
            var borrador = _db.VersionesPartesInteresadas.AsNoTracking().FirstOrDefault(x => x.Estado == EstadoVersionPartes.Borrador);
            r.Add(borrador is null
                ? new AvisoRestauracion { Nivel = "grave", Mensaje = $"La v{v.Numero} ya está publicada y no hay borrador.", Solucion = "Cree un borrador en Partes interesadas (Editar) y restaure de nuevo.", Menus = { "Partes interesadas" } }
                : new AvisoRestauracion { Mensaje = $"La v{v.Numero} ya está publicada: la parte vuelve al borrador v{borrador.Numero}.", Menus = { "Partes interesadas" } });
        }
        return r;
    }

    public void Restaurar(int id)
    {
        var p = _db.PartesInteresadas.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (p is null) return;
        var v = _db.VersionesPartesInteresadas.AsNoTracking().First(x => x.Id == p.VersionPartesInteresadasId);
        if (v.Estado == EstadoVersionPartes.Borrador) { p.Eliminado = false; p.FechaEliminacion = null; }
        else
        {
            var borrador = _db.VersionesPartesInteresadas.AsNoTracking().FirstOrDefault(x => x.Estado == EstadoVersionPartes.Borrador)
                ?? throw new InvalidOperationException($"La v{v.Numero} ya está publicada y no hay borrador de la matriz.");
            if (_db.PartesInteresadas.Any(x => x.VersionPartesInteresadasId == borrador.Id && x.Clave == p.Clave))
                throw new InvalidOperationException($"«{p.Nombre}» ya está en el borrador v{borrador.Numero}.");
            _db.Entry(p).State = EntityState.Detached;
            p.Id = 0; p.VersionPartesInteresadasId = borrador.Id; p.Eliminado = false; p.FechaEliminacion = null;
            _db.PartesInteresadas.Add(p);
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var p = _db.PartesInteresadas.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (p is not null) { _db.PartesInteresadas.Remove(p); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.PartesInteresadas.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraSeguimientosParte : IProveedorPapelera
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraSeguimientosParte(AppDbContext db) { _db = db; }

    public string Modulo => "Partes interesadas · seguimientos";

    public List<RegistroPapelera> Listar()
    {
        var segs = _db.SeguimientosParteInteresada.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado).OrderByDescending(x => x.FechaEliminacion).ToList();
        var claves = segs.Select(s => s.ParteClave).Distinct().ToList();
        var nombres = _db.PartesInteresadas.IgnoreQueryFilters().AsNoTracking().Where(p => claves.Contains(p.Clave))
            .OrderByDescending(p => p.Id).ToList().GroupBy(p => p.Clave).ToDictionary(g => g.Key, g => g.First().Nombre);
        return segs.Select(x => new RegistroPapelera
        {
            Modulo = Modulo, Id = x.Id, Titulo = $"{(nombres.TryGetValue(x.ParteClave, out var n) ? n : "Parte interesada")} · {x.Fecha:dd/MM/yyyy}",
            Subtitulo = string.Join(" · ", new[] { x.Resultado, x.Observacion }.Where(t => !string.IsNullOrWhiteSpace(t))),
            FechaEliminacion = x.FechaEliminacion
        }).ToList();
    }

    public void Restaurar(int id)
    {
        var s = _db.SeguimientosParteInteresada.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (s is null) return;
        s.Eliminado = false; s.FechaEliminacion = null;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        // Los planes de acción que nacieron de él quedan (SetNull): son evidencia de gestión.
        var s = _db.SeguimientosParteInteresada.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (s is not null) { _db.SeguimientosParteInteresada.Remove(s); _db.SaveChanges(); }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.SeguimientosParteInteresada.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
