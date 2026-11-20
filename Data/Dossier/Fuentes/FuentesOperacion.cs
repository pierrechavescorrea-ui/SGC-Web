using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Modelos3D;
using SigWeb.Data.Pie;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Matriz PIE: % de cumplimiento y pendientes por torre (misma regla que la Matriz PIE, estado de hoy). La documentación
// de cada parámetro sale en la fuente Torres (hoja de vida).
public sealed class FuenteMatrizPie : IFuenteDossier
{
    public string Clave => "matriz-pie";
    public string Nombre => "Matriz PIE";
    public string Icono => "matriz";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var torres = CumplimientoPie.PorTorre(db);
        var porId = torres.ToDictionary(t => t.LocalizacionId);
        CumplimientoPie.TorrePie? T(int id) => porId.GetValueOrDefault(id);
        var nativas = new[]
        {
            new ColumnaNativa("Torre", "Torre", TipoCampoTabla.Texto, id => T(id)?.Nombre),
            new ColumnaNativa("Pct", "Cumplimiento %", TipoCampoTabla.Numero, id => T(id)?.Pct is decimal p ? Math.Round(p, 1) : null),
            new ColumnaNativa("Aplican", "Parámetros que aplican", TipoCampoTabla.Numero, id => T(id)?.Aplican),
            new ColumnaNativa("Pendientes", "Pendientes", TipoCampoTabla.Numero, id => T(id)?.Pendientes),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:matriz-pie", nativas, torres.Select(t => (t.LocalizacionId, t.Nombre)));
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = torres.Select(t => new ElementoDossier { Id = t.LocalizacionId }).ToList(), ColumnaNombre = "Torre"
        };
        var actividades = await db.Actividades.AsNoTracking().Where(a => a.Activo && a.EnMatrizPie).ToDictionaryAsync(a => a.Id, ct);
        var idsActividades = actividades.Keys.ToList();
        var parametros = await db.ParametrosActividad.AsNoTracking().Where(p => p.Activo && idsActividades.Contains(p.ActividadId))
            .ToDictionaryAsync(p => p.Id, ct);
        var idsParametros = parametros.Keys.ToList();
        var estados = await db.EstadosParametro.AsNoTracking().Where(e => e.Activo).ToDictionaryAsync(e => e.Id, ct);
        var idsTorres = torres.Where(t => t.ElementoTrazadoId.HasValue).Select(t => t.ElementoTrazadoId!.Value).Distinct().ToList();
        var componentes = await db.Componentes.AsNoTracking().Where(c => !c.Eliminado && idsTorres.Contains(c.ElementoTrazadoId))
            .ToDictionaryAsync(c => c.Id, ct);
        var idsComponentes = componentes.Keys.ToList();
        var registros = (await db.RegistrosParametro.AsNoTracking()
            .Where(r => idsComponentes.Contains(r.ComponenteId) && idsParametros.Contains(r.ParametroActividadId)).ToListAsync(ct))
            .ToLookup(r => componentes[r.ComponenteId].ElementoTrazadoId);
        var tiros = await db.Tiros.AsNoTracking().Where(t => !t.Eliminado).ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);
        var torreTiro = await db.ElementosTrazado.AsNoTracking().Where(e => !e.Eliminado && idsTorres.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.TiroId, ct);
        var idsTiros = torreTiro.Values.OfType<int>().Where(tiros.ContainsKey).Distinct().ToList();
        var registrosTiro = (await db.RegistrosParametroTiro.AsNoTracking()
            .Where(r => idsTiros.Contains(r.TiroId) && idsParametros.Contains(r.ParametroActividadId)).ToListAsync(ct)).ToLookup(r => r.TiroId);
        FilaDossier Fila(CumplimientoPie.TorrePie t, string componente, int parametro, bool aplica, int? estado,
            DateTime? fecha, DateTime? facturado, string? observaciones)
        {
            var p = parametros[parametro];
            var e = estado is int id ? estados.GetValueOrDefault(id) : null;
            return new FilaDossier(t.LocalizacionId, t.Nombre, componente, actividades[p.ActividadId].Nombre, p.Codigo, p.Nombre,
                aplica ? e?.Nombre ?? "Sin estado" : "N/A", aplica ? e?.Porcentaje ?? 0M : null, fecha, facturado, observaciones);
        }
        var filas = torres.Where(t => t.ElementoTrazadoId != null).SelectMany(t =>
        {
            var id = t.ElementoTrazadoId!.Value;
            var detalle = registros[id].Select(r => Fila(t, componentes[r.ComponenteId].Nombre, r.ParametroActividadId,
                r.Completado, r.EstadoParametroId, r.Fecha, r.FechaFacturado, r.Observaciones));
            return torreTiro.GetValueOrDefault(id) is int tiro && tiros.TryGetValue(tiro, out var nombre)
                ? detalle.Concat(registrosTiro[tiro].Select(r => Fila(t, nombre, r.ParametroActividadId,
                    r.Completado, r.EstadoParametroId, r.Fecha, r.FechaFacturado, r.Observaciones)))
                : detalle;
        });
        fuente.Hojas.Add(new("Detalle de parámetros",
            ["Torre", "Componente o tiro", "Actividad", "Código", "Parámetro", "Estado", "Cumplimiento %", "Fecha", "Facturado", "Observaciones"], filas));
        return fuente;
    }
}

// Avance de obra: informes diarios (F-054) con sus fotos. Grupo = mes.
public sealed class FuenteAvanceObra : IFuenteDossier
{
    public string Clave => "avance-obra";
    public string Nombre => "Informes diarios de obra";
    public string Icono => "avance-obra";
    public const string Fotos = "Fotografías";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var informes = await db.InformesDiarios.AsNoTracking().Where(i => !i.Eliminado).OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id)
            .Select(i => new { i.Id, i.Fecha, i.ClimaManana, i.ClimaTarde, i.HorasLluvia, i.ElaboradoPor, i.RevisadoPor, i.AprobadoPor, i.Observaciones })
            .ToListAsync(ct);
        var registros = (await db.RegistrosAvance.AsNoTracking().Where(r => !r.Eliminado).GroupBy(r => r.InformeDiarioId)
            .Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.N);
        var registrosTiro = (await db.RegistrosAvanceTiro.AsNoTracking().Where(r => !r.Eliminado).GroupBy(r => r.InformeDiarioId)
            .Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.N);
        var porId = informes.ToDictionary(i => i.Id);
        var nativas = new[]
        {
            new ColumnaNativa("Fecha", "Fecha", TipoCampoTabla.Fecha, id => porId.GetValueOrDefault(id)?.Fecha),
            new ColumnaNativa("Registros", "Registros de torres", TipoCampoTabla.Numero, id => registros.GetValueOrDefault(id)),
            new ColumnaNativa("RegistrosTiro", "Registros de tendido", TipoCampoTabla.Numero, id => registrosTiro.GetValueOrDefault(id)),
            new ColumnaNativa("ClimaManana", "Clima mañana", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.ClimaManana),
            new ColumnaNativa("ClimaTarde", "Clima tarde", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.ClimaTarde),
            new ColumnaNativa("HorasLluvia", "Horas de lluvia", TipoCampoTabla.Numero, id => porId.GetValueOrDefault(id)?.HorasLluvia),
            new ColumnaNativa("Elaboro", "Elaboró", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.ElaboradoPor),
            new ColumnaNativa("Reviso", "Revisó", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.RevisadoPor),
            new ColumnaNativa("Aprobo", "Aprobó", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.AprobadoPor),
            new ColumnaNativa("Observaciones", "Observaciones", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Observaciones),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:avance-obra", nativas, informes.Select(i => (i.Id, i.Fecha.ToString("yyyy-MM-dd"))));
        var elementos = informes.ToDictionary(i => i.Id, i => new ElementoDossier { Id = i.Id, Grupo = i.Fecha.ToString("yyyy-MM") });
        foreach (var f in await db.FotosAvance.AsNoTracking().Where(f => !f.Eliminado).OrderBy(f => f.Id)
            .Select(f => new { f.InformeDiarioId, f.Nombre, f.Ruta }).ToListAsync(ct))
            if (elementos.TryGetValue(f.InformeDiarioId, out var el)) el.Archivos.Add(new ArchivoDossier(Fotos, f.Nombre, f.Ruta));
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = informes.Select(i => elementos[i.Id]).ToList(), ColumnaNombre = "Fecha",
            NombreGrupo = "Mes", Clases = new() { Fotos }
        };
        var ids = porId.Keys.ToList();
        var detalleTorres = await (
            from r in db.RegistrosAvance.AsNoTracking()
            join t in db.ElementosTrazado on r.ElementoTrazadoId equals t.Id
            join a in db.ActividadesObra on r.ActividadObraId equals a.Id
            join c in db.Componentes on r.ComponenteId equals (int?)c.Id into componentes
            from pata in componentes.DefaultIfEmpty()
            where ids.Contains(r.InformeDiarioId) && !r.Eliminado && !t.Eliminado && !a.Eliminado
                && (r.ComponenteId == null || (pata != null && !pata.Eliminado))
            orderby r.Fecha descending, t.Codigo, a.Orden, r.Id
            select new { r.InformeDiarioId, r.Fecha, t.Codigo, Pata = pata == null ? null : pata.Nombre, a.Nombre, a.Unidad,
                r.Cantidad, r.AvanceAcumulado, r.NoAplica, r.Programado, r.Observaciones }).ToListAsync(ct);
        fuente.Hojas.Add(new("Avance por torre",
            ["Informe diario", "Fecha", "Torre", "Pata", "Actividad", "Unidad", "Cantidad del día", "Avance acumulado %", "No aplica", "Programado", "Observaciones"],
            detalleTorres.Select(r => new FilaDossier(r.InformeDiarioId, porId[r.InformeDiarioId].Fecha, r.Fecha, r.Codigo,
                r.Pata, r.Nombre, r.Unidad, r.Cantidad, r.AvanceAcumulado, r.NoAplica, r.Programado, r.Observaciones))));
        var detalleTiros = await (
            from r in db.RegistrosAvanceTiro.AsNoTracking()
            join t in db.Tiros on r.TiroId equals t.Id
            join a in db.ActividadesObra on r.ActividadObraId equals a.Id
            where ids.Contains(r.InformeDiarioId) && !r.Eliminado && !t.Eliminado && !a.Eliminado
            orderby r.Fecha descending, t.Orden, a.Orden, r.Id
            select new { r.InformeDiarioId, r.Fecha, Tiro = t.Nombre, Actividad = a.Nombre, a.Unidad,
                r.Cantidad, r.AvanceAcumulado, r.NoAplica, r.Observaciones }).ToListAsync(ct);
        fuente.Hojas.Add(new("Avance de tendido",
            ["Informe diario", "Fecha", "Tiro", "Actividad", "Unidad", "Cantidad del día", "Avance acumulado %", "No aplica", "Observaciones"],
            detalleTiros.Select(r => new FilaDossier(r.InformeDiarioId, porId[r.InformeDiarioId].Fecha, r.Fecha, r.Tiro,
                r.Actividad, r.Unidad, r.Cantidad, r.AvanceAcumulado, r.NoAplica, r.Observaciones))));
        return fuente;
    }
}

// Modelos 3D: modelos IFC cargados (cada versión es un elemento; actual = la mayor de su código). Grupo = familia.
public sealed class FuenteModelos3D : IFuenteDossier
{
    public string Clave => "ifc";
    public string Nombre => "Modelos 3D";
    public string Icono => "ifc";
    public const string Ifc = "IFC", Json = "Modelo (JSON)", Informe = "Informe de validación", Miniatura = "Miniatura";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var modelos = await db.ModelosIfcCargados.AsNoTracking().Where(m => !m.Eliminado).OrderBy(m => m.Codigo).ThenByDescending(m => m.Version).ToListAsync(ct);
        var vigentes = ModelosCargados.Vigentes(modelos).Select(m => m.Id).ToHashSet();
        var porId = modelos.ToDictionary(m => m.Id);
        ModeloIfcCargado? M(int id) => porId.GetValueOrDefault(id);
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => M(id)?.Codigo),
            new ColumnaNativa("Version", "Versión", TipoCampoTabla.Numero, id => M(id)?.Version),
            new ColumnaNativa("Nombre", "Tipo en tabla de torres", TipoCampoTabla.Texto, id => M(id)?.Nombre),
            new ColumnaNativa("Familia", "Familia", TipoCampoTabla.Texto, id => M(id)?.Familia),
            new ColumnaNativa("Vigente", "Vigente", TipoCampoTabla.Texto, id => vigentes.Contains(id) ? "Sí" : "No"),
            new ColumnaNativa("Validaciones", "Validaciones", TipoCampoTabla.Numero, id => M(id)?.Validaciones),
            new ColumnaNativa("Supuestos", "Supuestos", TipoCampoTabla.Numero, id => M(id)?.Supuestos),
            new ColumnaNativa("Cambios", "Cambios", TipoCampoTabla.Texto, id => M(id)?.Cambios),
            new ColumnaNativa("FechaCarga", "Cargado", TipoCampoTabla.Fecha, id => M(id)?.FechaCarga),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:ifc", nativas, modelos.Select(m => (m.Id, $"{m.Codigo} v{m.Version}")));
        var elementos = modelos.Select(m =>
        {
            var el = new ElementoDossier { Id = m.Id, Grupo = m.Familia };
            var actual = vigentes.Contains(m.Id);
            void Add(string clase, string? ruta, string? nombre)
            {
                if (string.IsNullOrWhiteSpace(ruta)) return;
                el.Archivos.Add(new ArchivoDossier(clase, string.IsNullOrWhiteSpace(nombre) ? Path.GetFileName(ruta) : nombre, ruta, Actual: actual));
            }
            Add(Ifc, m.ArchivoIfc, m.NombreOriginalIfc);
            Add(Json, m.ArchivoJson, null);
            Add(Informe, m.ArchivoInforme, m.NombreOriginalInforme);
            Add(Miniatura, m.ArchivoMiniatura, null);
            return el;
        }).ToList();
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Codigo", NombreGrupo = "Familia",
            Clases = new() { Ifc, Json, Informe, Miniatura }, TieneVersiones = true
        };
    }
}
