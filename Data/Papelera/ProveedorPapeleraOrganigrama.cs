using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera del Organigrama: versiones completas (con sus cargos) y cargos quitados de un borrador.
public class ProveedorPapeleraVersionesOrganigrama : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraVersionesOrganigrama(AppDbContext db) { _db = db; }

    public string Modulo => "Organigrama · versiones";

    public List<RegistroPapelera> Listar() =>
        _db.VersionesOrganigrama.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = $"v{x.Numero} · {x.Estado}",
                Subtitulo = string.Join(" · ", new[] { x.Alcance, x.MotivoCambio }.Where(t => !string.IsNullOrWhiteSpace(t))),
                FechaEliminacion = x.FechaEliminacion
            }).ToList();

    public List<AvisoRestauracion> Revisar(int id)
    {
        var v = _db.VersionesOrganigrama.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        var r = new List<AvisoRestauracion>();
        if (v is null) return r;
        if (v.Estado == EstadoOrganigrama.Vigente && _db.VersionesOrganigrama.Any(x => x.Estado == EstadoOrganigrama.Vigente))
            r.Add(new AvisoRestauracion { Mensaje = $"Ya hay otra versión vigente: la v{v.Numero} vuelve como Obsoleta.", Menus = { "Organigrama" } });
        if (v.Estado == EstadoOrganigrama.Borrador && _db.VersionesOrganigrama.Any(x => x.Estado == EstadoOrganigrama.Borrador))
            r.Add(new AvisoRestauracion { Mensaje = $"Ya hay un borrador: la v{v.Numero} vuelve como Obsoleta (solo consulta).", Menus = { "Organigrama" } });
        return r;
    }

    public void Restaurar(int id)
    {
        var v = _db.VersionesOrganigrama.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (v is null) return;
        if (v.Estado != EstadoOrganigrama.Obsoleta && _db.VersionesOrganigrama.Any(x => x.Estado == v.Estado && x.Id != id))
            v.Estado = EstadoOrganigrama.Obsoleta;
        v.Eliminado = false; v.FechaEliminacion = null;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void EliminarDefinitivo(int id)
    {
        var v = _db.VersionesOrganigrama.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (v is not null)
        {
            // Otras versiones copiadas de esta quedan sin base (SetNull); los cargos se sueltan entre sí y se borran.
            _db.CargosOrganigrama.IgnoreQueryFilters().Where(c => c.VersionOrganigramaId == id).ExecuteUpdate(s => s.SetProperty(c => c.JefeId, (int?)null));
            _db.CargosOrganigrama.IgnoreQueryFilters().Where(c => c.VersionOrganigramaId == id).ExecuteDelete();
            _db.VersionesOrganigrama.IgnoreQueryFilters().Where(x => x.VersionBaseId == id).ExecuteUpdate(s => s.SetProperty(x => x.VersionBaseId, (int?)null));
            _db.VersionesOrganigrama.Remove(v);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.VersionesOrganigrama.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}

public class ProveedorPapeleraCargosOrganigrama : IProveedorPapelera, IValidaRestauracion
{
    private readonly AppDbContext _db;
    private const int DiasRetencion = 30;
    public ProveedorPapeleraCargosOrganigrama(AppDbContext db) { _db = db; }

    public string Modulo => "Organigrama · cargos";

    // Solo los cargos cuya versión no está en la Papelera (si no, vuelven con ella).
    public List<RegistroPapelera> Listar()
    {
        var versiones = _db.VersionesOrganigrama.AsNoTracking().Select(v => new { v.Id, V = v.Numero }).ToDictionary(x => x.Id, x => x.V);
        return _db.CargosOrganigrama.IgnoreQueryFilters().AsNoTracking().Where(x => x.Eliminado)
            .OrderByDescending(x => x.FechaEliminacion).ToList()
            .Where(x => versiones.ContainsKey(x.VersionOrganigramaId))
            .Select(x => new RegistroPapelera
            {
                Modulo = Modulo, Id = x.Id, Titulo = x.Nombre,
                Subtitulo = string.Join(" · ", new[] { $"v{versiones[x.VersionOrganigramaId]}", x.Ocupante, x.Area }.Where(t => !string.IsNullOrWhiteSpace(t))),
                FechaEliminacion = x.FechaEliminacion
            }).ToList();
    }

    public List<AvisoRestauracion> Revisar(int id)
    {
        var r = new List<AvisoRestauracion>();
        var c = _db.CargosOrganigrama.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == id);
        if (c is null) return r;
        var v = _db.VersionesOrganigrama.AsNoTracking().FirstOrDefault(x => x.Id == c.VersionOrganigramaId);
        if (v is not null && v.Estado != EstadoOrganigrama.Borrador)
        {
            var borrador = _db.VersionesOrganigrama.AsNoTracking().FirstOrDefault(x => x.Estado == EstadoOrganigrama.Borrador);
            r.Add(borrador is null
                ? new AvisoRestauracion { Nivel = "grave", Mensaje = $"La v{v.Numero} ya está publicada y no hay borrador.", Solucion = "Cree un borrador en Organigrama (Editar) y restaure de nuevo.", Menus = { "Organigrama" } }
                : new AvisoRestauracion { Mensaje = $"La v{v.Numero} ya está publicada: el cargo vuelve al borrador v{borrador.Numero}.", Menus = { "Organigrama" } });
        }
        return r;
    }

    public void Restaurar(int id)
    {
        var c = _db.CargosOrganigrama.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is null) return;
        var v = _db.VersionesOrganigrama.AsNoTracking().First(x => x.Id == c.VersionOrganigramaId);
        if (v.Estado == EstadoOrganigrama.Borrador)
        {
            c.Eliminado = false; c.FechaEliminacion = null;
            c.JefeId = JefeVivo(c.JefeId);
        }
        else
        {
            // Versión publicada: no se toca; el cargo se copia al borrador (mismo Clave, jefe por Clave).
            var borrador = _db.VersionesOrganigrama.AsNoTracking().FirstOrDefault(x => x.Estado == EstadoOrganigrama.Borrador)
                ?? throw new InvalidOperationException($"La v{v.Numero} ya está publicada y no hay borrador del organigrama.");
            if (_db.CargosOrganigrama.Any(x => x.VersionOrganigramaId == borrador.Id && x.Clave == c.Clave))
                throw new InvalidOperationException($"«{c.Nombre}» ya está en el borrador v{borrador.Numero}.");
            int? jefe = null;
            if (c.JefeId is int j && _db.CargosOrganigrama.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == j) is { } jo)
                jefe = _db.CargosOrganigrama.Where(x => x.VersionOrganigramaId == borrador.Id && x.Clave == jo.Clave).Select(x => (int?)x.Id).FirstOrDefault();
            _db.CargosOrganigrama.Add(new CargoOrganigrama
            {
                VersionOrganigramaId = borrador.Id, Clave = c.Clave, JefeId = jefe, Nombre = c.Nombre, Ocupante = c.Ocupante, Area = c.Area,
                Plazas = c.Plazas, Staff = c.Staff && jefe is not null, Orden = c.Orden, Notas = c.Notas, Supuesto = c.Supuesto
            });
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    // Primer jefe hacia arriba que no esté en la Papelera (null = queda como raíz).
    private int? JefeVivo(int? jefeId)
    {
        var vistos = new HashSet<int>();
        while (jefeId is int j && vistos.Add(j))
        {
            var p = _db.CargosOrganigrama.IgnoreQueryFilters().AsNoTracking().FirstOrDefault(x => x.Id == j);
            if (p is null) return null;
            if (!p.Eliminado) return p.Id;
            jefeId = p.JefeId;
        }
        return null;
    }

    public void EliminarDefinitivo(int id)
    {
        var c = _db.CargosOrganigrama.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
        if (c is not null)
        {
            // Quien lo tenía de jefe (vivo o en la Papelera) pasa al jefe de este.
            _db.CargosOrganigrama.IgnoreQueryFilters().Where(x => x.JefeId == id).ExecuteUpdate(s => s.SetProperty(x => x.JefeId, c.JefeId));
            _db.CargosOrganigrama.Remove(c);
            _db.SaveChanges();
        }
        _db.ChangeTracker.Clear();
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        foreach (var id in _db.CargosOrganigrama.IgnoreQueryFilters().Where(x => x.Eliminado && x.FechaEliminacion != null && x.FechaEliminacion < limite).Select(x => x.Id).ToList())
            EliminarDefinitivo(id);
    }
}
