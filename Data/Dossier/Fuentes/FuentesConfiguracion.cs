using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Actividades (Configuración): actividades con sus parámetros. Grupo = ámbito.
public sealed class FuenteActividades : IFuenteDossier
{
    public string Clave => "actividades";
    public string Nombre => "Actividades";
    public string Icono => "actividades";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var acts = await db.Actividades.AsNoTracking().Where(a => !a.Eliminado).OrderBy(a => a.Orden).ThenBy(a => a.Id).ToListAsync(ct);
        var pars = (await db.ParametrosActividad.AsNoTracking().Where(p => !p.Eliminado && p.Activo).OrderBy(p => p.Orden).ThenBy(p => p.Id).ToListAsync(ct))
            .GroupBy(p => p.ActividadId).ToDictionary(g => g.Key, g => g.ToList());
        var porId = acts.ToDictionary(a => a.Id);
        Actividad? A(int id) => porId.GetValueOrDefault(id);
        var nativas = new[]
        {
            new ColumnaNativa("Nombre", "Actividad", TipoCampoTabla.Texto, id => A(id)?.Nombre),
            new ColumnaNativa("Ambito", "Ámbito", TipoCampoTabla.Texto, id => A(id)?.Ambito.ToString()),
            new ColumnaNativa("EnMatrizPie", "En Matriz PIE", TipoCampoTabla.Texto, id => A(id) is { } a ? (a.EnMatrizPie ? "Sí" : "No") : null),
            new ColumnaNativa("Activo", "Activa", TipoCampoTabla.Texto, id => A(id) is { } a ? (a.Activo ? "Sí" : "No") : null),
            new ColumnaNativa("Parametros", "Parámetros", TipoCampoTabla.Texto, id => pars.TryGetValue(id, out var l)
                ? string.Join("\n", l.Select(p => string.IsNullOrWhiteSpace(p.Codigo) ? p.Nombre : $"{p.Codigo} {p.Nombre}")) : null),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:actividades", nativas, acts.Select(a => (a.Id, a.Nombre)));
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = acts.Select(a => new ElementoDossier { Id = a.Id, Grupo = a.Ambito.ToString() }).ToList(),
            ColumnaNombre = "Nombre", NombreGrupo = "Ámbito"
        };
    }
}

// Tiros (Configuración).
public sealed class FuenteTiros : IFuenteDossier
{
    public string Clave => "tiros";
    public string Nombre => "Tiros";
    public string Icono => "linea3d";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var tiros = await db.Tiros.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Orden).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.Nombre, t.Orden, Torres = db.ElementosTrazado.Count(e => !e.Eliminado && e.TiroId == t.Id) })
            .ToListAsync(ct);
        var porId = tiros.ToDictionary(t => t.Id);
        var nativas = new[]
        {
            new ColumnaNativa("Nombre", "Tiro", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Nombre),
            new ColumnaNativa("Orden", "Orden", TipoCampoTabla.Numero, id => porId.GetValueOrDefault(id)?.Orden),
            new ColumnaNativa("Torres", "Torres asignadas", TipoCampoTabla.Numero, id => porId.GetValueOrDefault(id)?.Torres),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:tiros", nativas, tiros.Select(t => (t.Id, t.Nombre)));
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = tiros.Select(t => new ElementoDossier { Id = t.Id }).ToList(), ColumnaNombre = "Nombre"
        };
    }
}

// Catálogos (Configuración): ítems activos de todos los catálogos. Grupo = catálogo.
public sealed class FuenteCatalogos : IFuenteDossier
{
    public string Clave => "catalogos";
    public string Nombre => "Catálogos";
    public string Icono => "catalogos";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var items = await db.Catalogos.AsNoTracking().Where(i => i.Activo).OrderBy(i => i.Tipo).ThenBy(i => i.Nombre).ToListAsync(ct);
        var porId = items.ToDictionary(i => i.Id);
        var nativas = new[]
        {
            new ColumnaNativa("Nombre", "Nombre", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Nombre),
            new ColumnaNativa("Tipo", "Catálogo", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Tipo),
            new ColumnaNativa("Descripcion", "Descripción", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Descripcion),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:catalogos", nativas, items.Select(i => (i.Id, i.Nombre)));
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = items.Select(i => new ElementoDossier { Id = i.Id, Grupo = i.Tipo }).ToList(),
            ColumnaNombre = "Nombre", NombreGrupo = "Catálogo"
        };
    }
}
