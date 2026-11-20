using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Riesgos: riesgos de la matriz vigente (o de la revisión más reciente) con su último seguimiento; adjuntos = soportes
// de seguimientos y hallazgos (carpeta = seguimiento). Grupo = categoría.
public sealed class FuenteRiesgos : IFuenteDossier
{
    public string Clave => "riesgos";
    public string Nombre => "Riesgos";
    public string Icono => "riesgos";
    public const string Soportes = "Soportes de seguimiento";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var versiones = await db.VersionesMatrizRiesgos.AsNoTracking().Where(v => !v.Eliminado).ToListAsync(ct);
        var ver = versiones.FirstOrDefault(v => v.Estado == EstadoVersionMatriz.Vigente) ?? versiones.OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).FirstOrDefault();
        var versionesPorId = versiones.ToDictionary(v => v.Id);
        var versionIds = versionesPorId.Keys.ToList();
        var historicos = await db.Riesgos.AsNoTracking().Where(r => versionIds.Contains(r.VersionMatrizRiesgosId) && !r.Eliminado)
            .OrderBy(r => r.Orden).ThenBy(r => r.Id).ToListAsync(ct);
        var riesgos = historicos.Where(r => r.VersionMatrizRiesgosId == ver?.Id).ToList();
        var riesgoPorId = historicos.ToDictionary(r => r.Id);
        var ids = riesgoPorId.Keys.ToList();
        var seguimientos = await db.SeguimientosRiesgo.AsNoTracking().Where(s => ids.Contains(s.RiesgoId) && !s.Eliminado)
            .OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).ToListAsync(ct);
        var segPorCodigo = seguimientos.ToLookup(s => riesgoPorId[s.RiesgoId].Codigo, StringComparer.OrdinalIgnoreCase);
        var ultimo = seguimientos.GroupBy(s => s.RiesgoId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).First());
        var segPorId = seguimientos.ToDictionary(s => s.Id);
        var idsSeg = segPorId.Keys.ToList();
        var hallazgos = await db.HallazgosRiesgo.AsNoTracking().Where(h => !h.Eliminado && idsSeg.Contains(h.SeguimientoRiesgoId))
            .ToDictionaryAsync(h => h.Id, ct);
        var hallazgosPorSeg = hallazgos.Values.ToLookup(h => h.SeguimientoRiesgoId);
        var vigentesPorCodigo = riesgos.ToLookup(r => r.Codigo, StringComparer.OrdinalIgnoreCase);
        var porId = riesgos.ToDictionary(r => r.Id);
        Riesgo? R(int id) => porId.GetValueOrDefault(id);
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => R(id)?.Codigo),
            new ColumnaNativa("Tipo", "Tipo", TipoCampoTabla.Texto, id => R(id)?.Tipo.ToString()),
            new ColumnaNativa("Categoria", "Categoría", TipoCampoTabla.Texto, id => R(id)?.Categoria),
            new ColumnaNativa("Area", "Área", TipoCampoTabla.Texto, id => R(id)?.Area),
            new ColumnaNativa("Descripcion", "Descripción", TipoCampoTabla.Texto, id => R(id)?.Descripcion),
            new ColumnaNativa("Causa", "Causas", TipoCampoTabla.Texto, id => R(id)?.Causa),
            new ColumnaNativa("Consecuencia", "Consecuencias", TipoCampoTabla.Texto, id => R(id)?.Consecuencia),
            new ColumnaNativa("Probabilidad", "Probabilidad", TipoCampoTabla.Texto, id => R(id)?.Probabilidad.ToString()),
            new ColumnaNativa("Impacto", "Impacto", TipoCampoTabla.Texto, id => R(id)?.Impacto.ToString()),
            new ColumnaNativa("Nivel", "Nivel", TipoCampoTabla.Texto, id => R(id)?.Nivel.ToString()),
            new ColumnaNativa("Estrategia", "Estrategia", TipoCampoTabla.Texto, id => R(id)?.Estrategia),
            new ColumnaNativa("PlanRespuesta", "Plan de respuesta", TipoCampoTabla.Texto, id => R(id)?.PlanRespuesta),
            new ColumnaNativa("Responsable", "Responsable", TipoCampoTabla.Texto, id => R(id)?.ResponsablePlan),
            new ColumnaNativa("EstadoActual", "Estado actual", TipoCampoTabla.Texto, id => R(id)?.EstadoActual),
            new ColumnaNativa("UltimoSeguimiento", "Último seguimiento", TipoCampoTabla.Fecha, id => ultimo.GetValueOrDefault(id)?.Fecha),
            new ColumnaNativa("Resultado", "Resultado", TipoCampoTabla.Texto, id => ultimo.GetValueOrDefault(id)?.Resultado.ToString()),
            new ColumnaNativa("Version", "Revisión de la matriz", TipoCampoTabla.Texto, id => ver?.Codigo, visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:riesgos", nativas, riesgos.Select(r => (r.Id, r.Codigo)));

        var elementos = riesgos.ToDictionary(r => r.Id, r => new ElementoDossier { Id = r.Id, Grupo = r.Categoria });
        foreach (var a in await db.ArchivosRiesgo.AsNoTracking().Where(a => !a.Eliminado).OrderBy(a => a.Id).ToListAsync(ct))
        {
            var sid = a.SeguimientoRiesgoId ?? (a.HallazgoRiesgoId is int h && hallazgos.TryGetValue(h, out var hallazgo) ? hallazgo.SeguimientoRiesgoId : (int?)null);
            if (sid is not int si || !segPorId.TryGetValue(si, out var seg)) continue;
            var r = riesgoPorId[seg.RiesgoId];
            foreach (var vigente in vigentesPorCodigo[r.Codigo])
                elementos[vigente.Id].Archivos.Add(new ArchivoDossier(Soportes, a.NombreOriginal, a.RutaArchivo,
                    Carpeta: $"{versionesPorId[r.VersionMatrizRiesgosId].Codigo}/Seguimiento {seg.Fecha:yyyy-MM-dd}"));
        }
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = riesgos.Select(r => elementos[r.Id]).ToList(), ColumnaNombre = "Codigo",
            NombreGrupo = "Categoría", Clases = new() { Soportes }
        };
        fuente.Hojas.Add(new("Seguimientos de riesgos",
            ["Código", "Revisión", "Fecha", "Responsable", "Resultado", "Observación", "Probabilidad", "Impacto", "Hallazgos"],
            riesgos.SelectMany(r => segPorCodigo[r.Codigo].Select(x => new FilaDossier(r.Id, r.Codigo,
                versionesPorId[riesgoPorId[x.RiesgoId].VersionMatrizRiesgosId].Codigo, x.Fecha, x.Responsable,
                x.Resultado.ToString(), x.Observacion, x.Probabilidad?.ToString(), x.Impacto?.ToString(),
                string.Join("\n", hallazgosPorSeg[x.Id].Select(h => $"{h.Descripcion} ? {h.Accion} ? {h.Estado}")))))));
        var porCodigo = historicos.ToLookup(r => r.Codigo, StringComparer.OrdinalIgnoreCase);
        fuente.Hojas.Add(new("Revisiones de riesgos",
            ["Código", "Revisión", "Fecha", "Estado", "Cambios", "Elaboró", "Revisó", "Aprobó", "Descripción", "Plan de respuesta"],
            riesgos.SelectMany(r => porCodigo[r.Codigo].Select(h =>
            {
                var v = versionesPorId[h.VersionMatrizRiesgosId];
                return new FilaDossier(r.Id, r.Codigo, v.Codigo, v.Fecha, v.Estado.ToString(), v.DescripcionCambios,
                    v.ElaboradoPor, v.RevisadoPor, v.AprobadoPor, h.Descripcion, h.PlanRespuesta);
            }))));
        return fuente;
    }
}

// Costos y presupuestos: formulario de precios (ítems de pago, sin capítulos). Grupo = capítulo.
public sealed class FuenteCostos : IFuenteDossier
{
    public string Clave => "costos";
    public string Nombre => "Costos y presupuestos";
    public string Icono => "costos";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var todos = await db.ItemsPago.AsNoTracking().Where(i => !i.Eliminado).OrderBy(i => i.Orden).ThenBy(i => i.Id).ToListAsync(ct);
        var capitulos = todos.Where(i => i.EsCapitulo).ToDictionary(i => i.Id, i => $"{i.Codigo} {i.Descripcion}".Trim());
        var items = todos.Where(i => !i.EsCapitulo).ToList();
        var porId = items.ToDictionary(i => i.Id);
        ItemPago? I(int id) => porId.GetValueOrDefault(id);
        string? Cap(ItemPago i) => i.CapituloId is int c ? capitulos.GetValueOrDefault(c) : null;
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Ítem", TipoCampoTabla.Texto, id => I(id)?.Codigo),
            new ColumnaNativa("Descripcion", "Descripción", TipoCampoTabla.Texto, id => I(id)?.Descripcion),
            new ColumnaNativa("Capitulo", "Capítulo", TipoCampoTabla.Texto, id => I(id) is { } i ? Cap(i) : null),
            new ColumnaNativa("Unidad", "Unidad", TipoCampoTabla.Texto, id => I(id)?.Unidad),
            new ColumnaNativa("Cantidad", "Cantidad contratada", TipoCampoTabla.Numero, id => I(id)?.CantidadContratada),
            new ColumnaNativa("PrecioBase", "Precio base", TipoCampoTabla.Numero, id => I(id)?.PrecioBase),
            new ColumnaNativa("AnioBase", "Año base", TipoCampoTabla.Numero, id => I(id)?.AnioBase),
            new ColumnaNativa("Aiu", "AIU %", TipoCampoTabla.Numero, id => I(id)?.AiuPct),
            new ColumnaNativa("Ambito", "Ámbito", TipoCampoTabla.Texto, id => I(id)?.Ambito.ToString(), visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:costos", nativas, items.Select(i => (i.Id, i.Codigo)));
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = items.Select(i => new ElementoDossier { Id = i.Id, Grupo = Cap(i) }).ToList(),
            ColumnaNombre = "Codigo", NombreGrupo = "Capítulo"
        };
    }
}
