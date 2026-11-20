using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Organigrama: cargos de la versión vigente (o de la más reciente si no hay vigente). Sin adjuntos. Grupo = área.
public sealed class FuenteOrganigrama : IFuenteDossier
{
    public string Clave => "organigrama";
    public string Nombre => "Organigrama";
    public string Icono => "organigrama";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var versiones = await db.VersionesOrganigrama.AsNoTracking().Where(v => !v.Eliminado).ToListAsync(ct);
        var ver = versiones.FirstOrDefault(v => v.Estado == EstadoOrganigrama.Vigente) ?? versiones.OrderByDescending(v => v.Numero).FirstOrDefault();
        var cargos = ver is null ? new() : await db.CargosOrganigrama.AsNoTracking()
            .Where(c => c.VersionOrganigramaId == ver.Id && !c.Eliminado).OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync(ct);
        var porId = cargos.ToDictionary(c => c.Id);
        CargoOrganigrama? C(int id) => porId.GetValueOrDefault(id);
        var nativas = new[]
        {
            new ColumnaNativa("Cargo", "Cargo", TipoCampoTabla.Texto, id => C(id)?.Nombre),
            new ColumnaNativa("Ocupante", "Ocupante", TipoCampoTabla.Texto, id => C(id)?.Ocupante),
            new ColumnaNativa("Jefe", "Jefe inmediato", TipoCampoTabla.Texto, id => C(id)?.JefeId is int j ? C(j)?.Nombre : null),
            new ColumnaNativa("Area", "Área", TipoCampoTabla.Texto, id => C(id)?.Area),
            new ColumnaNativa("Plazas", "Plazas", TipoCampoTabla.Numero, id => C(id)?.Plazas),
            new ColumnaNativa("Staff", "Apoyo (staff)", TipoCampoTabla.Texto, id => C(id) is { } c ? (c.Staff ? "Sí" : "No") : null),
            new ColumnaNativa("Version", "Versión", TipoCampoTabla.Texto, id => ver is null ? null : $"v{ver.Numero} · {ver.Estado}"),
            new ColumnaNativa("Supuesto", "Supuesto", TipoCampoTabla.Texto, id => C(id)?.Supuesto, visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:organigrama", nativas, cargos.Select(c => (c.Id, c.Nombre)));
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = cargos.Select(c => new ElementoDossier { Id = c.Id, Grupo = c.Area }).ToList(),
            ColumnaNombre = "Cargo", NombreGrupo = "Área"
        };
        var versionPorId = versiones.ToDictionary(v => v.Id);
        var idsVersion = versionPorId.Keys.ToList();
        var historicos = await db.CargosOrganigrama.AsNoTracking()
            .Where(c => !c.Eliminado && idsVersion.Contains(c.VersionOrganigramaId)).OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync(ct);
        var porClave = historicos.ToLookup(c => c.Clave);
        var historicoPorId = historicos.ToDictionary(c => c.Id);
        fuente.Hojas.Add(new("Versiones del organigrama",
            ["Versión", "Título", "Alcance", "Estado", "Vigente desde", "Vigente hasta", "Motivo", "Elaboró", "Revisó", "Aprobó",
                "Cargo", "Ocupante", "Jefe", "?rea", "Plazas", "Apoyo", "Notas"],
            cargos.SelectMany(c => porClave[c.Clave].Select(h =>
            {
                var v = versionPorId[h.VersionOrganigramaId];
                return new FilaDossier(c.Id, v.Numero, v.Titulo, v.Alcance, v.Estado, v.VigenteDesde, v.VigenteHasta, v.MotivoCambio,
                    v.Elaboro, v.Reviso, v.Aprobo, h.Nombre, h.Ocupante, h.JefeId is int j ? historicoPorId.GetValueOrDefault(j)?.Nombre : null,
                    h.Area, h.Plazas, h.Staff, h.Notas);
            }))));
        return fuente;
    }
}

// Partes interesadas: matriz de la versión vigente (o de la más reciente) con su último seguimiento. Grupo = clasificación.
public sealed class FuentePartesInteresadas : IFuenteDossier
{
    public string Clave => "partes-interesadas";
    public string Nombre => "Partes interesadas";
    public string Icono => "partes-interesadas";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var versiones = await db.VersionesPartesInteresadas.AsNoTracking().Where(v => !v.Eliminado).ToListAsync(ct);
        var ver = versiones.FirstOrDefault(v => v.Estado == EstadoVersionPartes.Vigente) ?? versiones.OrderByDescending(v => v.Numero).FirstOrDefault();
        var partes = ver is null ? new() : await db.PartesInteresadas.AsNoTracking()
            .Where(p => p.VersionPartesInteresadasId == ver.Id && !p.Eliminado).OrderBy(p => p.Orden).ThenBy(p => p.Id).ToListAsync(ct);
        var seguimientos = await db.SeguimientosParteInteresada.AsNoTracking().Where(s => !s.Eliminado)
            .OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).ToListAsync(ct);
        var ultimo = seguimientos.GroupBy(s => s.ParteClave).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).First());
        var porId = partes.ToDictionary(p => p.Id);
        ParteInteresada? P(int id) => porId.GetValueOrDefault(id);
        SeguimientoParteInteresada? S(int id) => P(id) is { } p ? ultimo.GetValueOrDefault(p.Clave) : null;
        var nativas = new[]
        {
            new ColumnaNativa("Parte", "Parte interesada", TipoCampoTabla.Texto, id => P(id)?.Nombre),
            new ColumnaNativa("Procesos", "Procesos", TipoCampoTabla.Texto, id => P(id)?.Procesos),
            new ColumnaNativa("Necesidades", "Necesidades", TipoCampoTabla.Texto, id => P(id)?.Necesidades),
            new ColumnaNativa("Expectativas", "Expectativas", TipoCampoTabla.Texto, id => P(id)?.Expectativas),
            new ColumnaNativa("AfectaSgc", "Cómo afecta al SGC", TipoCampoTabla.Texto, id => P(id)?.AfectaSgc),
            new ColumnaNativa("Imp", "IMP", TipoCampoTabla.Numero, id => P(id)?.Impacto),
            new ColumnaNativa("Inf", "INF", TipoCampoTabla.Numero, id => P(id)?.Influencia),
            new ColumnaNativa("Clasificacion", "Clasificación", TipoCampoTabla.Texto, id => P(id)?.Clasificacion),
            new ColumnaNativa("Estrategias", "Estrategias", TipoCampoTabla.Texto, id => P(id)?.Estrategias),
            new ColumnaNativa("Medicion", "Medición", TipoCampoTabla.Texto, id => P(id)?.Medicion),
            new ColumnaNativa("Periodicidad", "Periodicidad", TipoCampoTabla.Texto, id => P(id)?.Periodicidad),
            new ColumnaNativa("Meta", "Meta", TipoCampoTabla.Texto, id => P(id)?.Meta),
            new ColumnaNativa("UltimoSeguimiento", "Último seguimiento", TipoCampoTabla.Fecha, id => S(id)?.Fecha),
            new ColumnaNativa("Resultado", "Resultado del seguimiento", TipoCampoTabla.Texto, id => S(id)?.Resultado),
            new ColumnaNativa("Observaciones", "Observaciones", TipoCampoTabla.Texto, id => P(id)?.Observaciones, visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:partes-interesadas", nativas, partes.Select(p => (p.Id, p.Nombre)));
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = partes.Select(p => new ElementoDossier { Id = p.Id, Grupo = p.Clasificacion }).ToList(),
            ColumnaNombre = "Parte", NombreGrupo = "Clasificación"
        };
        var segPorClave = seguimientos.ToLookup(s => s.ParteClave);
        fuente.Hojas.Add(new("Seguimientos de partes",
            ["Parte interesada", "Fecha", "Responsable", "Resultado", "Cumplimiento", "IMP", "INF", "Observación", "Hallazgo"],
            partes.SelectMany(p => segPorClave[p.Clave].Select(x => new FilaDossier(p.Id, p.Nombre, x.Fecha, x.Responsable,
                x.Resultado, x.Cumplimiento.ToString(), x.Impacto, x.Influencia, x.Observacion, x.DescripcionHallazgo)))));
        var versionesPorId = versiones.ToDictionary(v => v.Id);
        var idsVersion = versionesPorId.Keys.ToList();
        var claves = partes.Select(p => p.Clave).Distinct().ToList();
        var historicas = (await db.PartesInteresadas.AsNoTracking()
            .Where(p => !p.Eliminado && idsVersion.Contains(p.VersionPartesInteresadasId) && claves.Contains(p.Clave)).ToListAsync(ct))
            .ToLookup(p => p.Clave);
        fuente.Hojas.Add(new("Versiones de partes",
            ["Parte interesada", "Versión", "Código", "Título", "Estado", "Vigente desde", "Vigente hasta", "Motivo", "Elaboró",
                "Revisó", "Aprobó", "Necesidades", "Expectativas", "Estrategias"],
            partes.SelectMany(p => historicas[p.Clave].Select(h =>
            {
                var v = versionesPorId[h.VersionPartesInteresadasId];
                return new FilaDossier(p.Id, h.Nombre, v.Numero, v.Codigo, v.Titulo, v.Estado, v.VigenteDesde, v.VigenteHasta,
                    v.MotivoCambio, v.Elaboro, v.Reviso, v.Aprobo, h.Necesidades, h.Expectativas, h.Estrategias);
            }))));
        return fuente;
    }
}
