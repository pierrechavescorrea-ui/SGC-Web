using Microsoft.EntityFrameworkCore;
using SigWeb.Data.CostosNoCalidad;
using SigWeb.Data.Tablas;
using SigWeb.Data.Ensayos;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Costos de no calidad: un elemento por plan de acción con costo (paso ligado del formulario de Planes), con una columna por rubro
// y las dimensiones marcadas «Resumen». Sin adjuntos propios (los del plan salen en la fuente de Planes de acción).
public sealed class FuenteCostosNoCalidad : IFuenteDossier
{
    public string Clave => "costos-no-calidad";
    public string Nombre => "Costos de no calidad";
    public string Icono => "costos-no-calidad";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var inst = await ServicioCostosNoCalidad.CargarAsync(db);
        var planes = inst.Planes.Where(p => p.Total != 0).OrderBy(p => p.Fecha).ThenBy(p => p.Codigo).ToList();
        var porId = planes.ToDictionary(p => p.Id);
        PlanCosto? P(int id) => porId.GetValueOrDefault(id);
        var nativas = new List<ColumnaNativa>
        {
            new("Codigo", "Código", TipoCampoTabla.Texto, id => P(id)?.Codigo),
            new("Descripcion", "Descripción", TipoCampoTabla.Texto, id => P(id)?.Descripcion),
            new("Fecha", "Fecha", TipoCampoTabla.Fecha, id => P(id)?.Fecha),
            new("Origen", "Origen", TipoCampoTabla.Texto, id => P(id)?.Origen),
            new("Tipo", "Tipo de hallazgo", TipoCampoTabla.Texto, id => P(id)?.Tipo),
            new("Proceso", "Proceso", TipoCampoTabla.Texto, id => P(id)?.Proceso),
            new("Estado", "Estado", TipoCampoTabla.Texto, id => P(id)?.Estado.ToString()),
            new("Total", "Costo total", TipoCampoTabla.Numero, id => P(id)?.Total),
        };
        var i = 0;
        foreach (var rubro in inst.Rubros.ToList())
            nativas.Add(new($"Rubro{i++}", rubro, TipoCampoTabla.Numero, id => P(id) is { } p ? inst.ValorRubro(p, rubro) : null));
        foreach (var d in inst.Dimensiones)
            nativas.Add(new($"Dim{d.Id}", d.Nombre, TipoCampoTabla.Texto, id => P(id) is { } p && p.Dimensiones.TryGetValue(d.Id, out var v) ? v : null));

        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:costos-no-calidad", nativas, planes.Select(p => (p.Id, p.Codigo)));
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, ColumnaNombre = "Codigo", NombreGrupo = "Origen", Clases = new(),
            Elementos = planes.Select(p => new ElementoDossier { Id = p.Id, Grupo = string.IsNullOrWhiteSpace(p.Origen) ? null : p.Origen }).ToList(),
        };
        fuente.Hojas.Add(new("Conceptos de no calidad", ["Plan", "Grupo", "Rubro", "Concepto", "Costo"],
            planes.SelectMany(p => inst.Campos.Where(c => p.Valores.ContainsKey(c.Id)).Select(c =>
                new FilaDossier(p.Id, p.Codigo, c.Grupo, c.Rubro, c.Nombre, p.Valores[c.Id])))));
        return fuente;
    }
}

// Resultados de ensayo: un elemento por resultado (todos los tipos), con sus valores en texto y los archivos adjuntos a sus campos.
// Complementa «Informes de ensayo» (un elemento por informe de laboratorio). Los campos tipo Fórmula no se guardan: no salen aquí.
public sealed class FuenteResultadosEnsayo : IFuenteDossier
{
    public string Clave => "resultados-ensayo";
    public string Nombre => "Resultados de ensayo";
    public string Icono => "ensayos";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var tipos = await db.TiposEnsayo.AsNoTracking().Where(t => !t.Eliminado).ToDictionaryAsync(t => t.Id, ct);
        var campos = await db.CamposEnsayo.AsNoTracking().OrderBy(c => c.Orden).ThenBy(c => c.Etiqueta)
            .ToDictionaryAsync(c => c.Id, ct);
        var res = (await db.ResultadosEnsayo.AsNoTracking().Where(r => !r.Eliminado)
                .Select(r => new { r.Id, r.TipoEnsayoId, r.ElementoTrazadoId, r.ElementoTexto, r.FechaEjecucion, r.InformeId, r.Localizacion, r.Origen, r.DisenoMezclaCodigo })
                .ToListAsync(ct))
            .Where(r => tipos.ContainsKey(r.TipoEnsayoId))
            .OrderBy(r => tipos[r.TipoEnsayoId].Nombre).ThenBy(r => r.FechaEjecucion ?? DateTime.MaxValue).ThenBy(r => r.Id).ToList();
        var porId = res.ToDictionary(r => r.Id);
        var torres = await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).ToDictionaryAsync(t => t.Id, t => t.Codigo, ct);
        var informes = await db.Informes.AsNoTracking().Select(x => new { x.Id, x.Numero }).ToDictionaryAsync(x => x.Id, x => x.Numero, ct);
        var ids = porId.Keys.ToHashSet();
        var valores = (await db.ValoresCampoEnsayo.AsNoTracking()
                .Select(v => new { v.ResultadoEnsayoId, v.CampoEnsayoId, v.Valor, v.AdjuntoEnsayoId }).ToListAsync(ct))
            .Where(v => ids.Contains(v.ResultadoEnsayoId))
            .GroupBy(v => v.ResultadoEnsayoId).ToDictionary(g => g.Key, g => g.ToList());
        var adjIds = valores.Values.SelectMany(l => l).Where(v => v.AdjuntoEnsayoId != null).Select(v => v.AdjuntoEnsayoId!.Value).Distinct().ToList();
        var adjuntos = await db.AdjuntosEnsayo.AsNoTracking().Where(a => adjIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        string Torre(int id) => porId.TryGetValue(id, out var r)
            ? (r.ElementoTrazadoId is int t && torres.TryGetValue(t, out var c) ? c : r.ElementoTexto) : "";
        string Valores(int id) => valores.TryGetValue(id, out var l)
            ? string.Join("; ", l.Where(v => campos.ContainsKey(v.CampoEnsayoId) && !string.IsNullOrWhiteSpace(v.Valor))
                .Select(v => $"{campos[v.CampoEnsayoId].Etiqueta}: {v.Valor}{(string.IsNullOrWhiteSpace(campos[v.CampoEnsayoId].Unidad) ? "" : " " + campos[v.CampoEnsayoId].Unidad)}"))
            : "";
        var nativas = new[]
        {
            new ColumnaNativa("Tipo", "Tipo de ensayo", TipoCampoTabla.Texto, id => porId.TryGetValue(id, out var r) ? tipos[r.TipoEnsayoId].Nombre : null),
            new ColumnaNativa("Torre", "Torre / elemento", TipoCampoTabla.Texto, id => Torre(id)),
            new ColumnaNativa("Localizacion", "Localización", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Localizacion),
            new ColumnaNativa("Fecha", "Fecha de ejecución", TipoCampoTabla.Fecha, id => porId.GetValueOrDefault(id)?.FechaEjecucion),
            new ColumnaNativa("Informe", "Informe", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.InformeId is int i && informes.TryGetValue(i, out var n) ? n : null),
            new ColumnaNativa("Valores", "Valores", TipoCampoTabla.Texto, id => Valores(id)),
            new ColumnaNativa("Origen", "Origen", TipoCampoTabla.Texto, id => porId.GetValueOrDefault(id)?.Origen, visiblePorDefecto: false),
            new ColumnaNativa("Nombre", "Resultado", TipoCampoTabla.Texto, id => porId.TryGetValue(id, out var r)
                ? $"{tipos[r.TipoEnsayoId].Nombre} {Torre(id)} {r.FechaEjecucion:yyyy-MM-dd} #{r.Id}".Trim() : null, visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:resultados-ensayo", nativas, res.Select(r => (r.Id, $"{tipos[r.TipoEnsayoId].Nombre} #{r.Id}")));
        var elementos = res.Select(r =>
        {
            var el = new ElementoDossier { Id = r.Id, Grupo = tipos[r.TipoEnsayoId].Nombre };
            if (valores.TryGetValue(r.Id, out var l))
                foreach (var v in l)
                    if (v.AdjuntoEnsayoId is int a && adjuntos.TryGetValue(a, out var adj))
                        el.Archivos.Add(new ArchivoDossier("Adjunto", adj.NombreOriginal, adj.Ruta));
            return el;
        }).ToList();
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Nombre", NombreGrupo = "Tipo de ensayo",
            Clases = new() { "Adjunto" },
        };
        var camposPorTipo = campos.Values.Where(c => c.Activo).ToLookup(c => c.TipoEnsayoId);
        string? Informe(int id) => porId[id].InformeId is int i ? informes.GetValueOrDefault(i) : null;
        static TipoCampoTabla TipoDe(TipoDatoCampoEnsayo tipo) => tipo switch
        {
            TipoDatoCampoEnsayo.Numero => TipoCampoTabla.Numero,
            TipoDatoCampoEnsayo.Fecha => TipoCampoTabla.Fecha,
            TipoDatoCampoEnsayo.Formula => TipoCampoTabla.Auto,
            _ => TipoCampoTabla.Texto
        };
        foreach (var grupo in res.GroupBy(r => r.TipoEnsayoId))
        {
            var tipo = tipos[grupo.Key];
            var plantilla = camposPorTipo[tipo.Id].ToList();
            var datos = grupo.ToDictionary(r => r.Id, r =>
            {
                var valoresFila = new Dictionary<string, string?>();
                if (valores.TryGetValue(r.Id, out var guardados))
                    foreach (var v in guardados)
                        if (campos.TryGetValue(v.CampoEnsayoId, out var c))
                            valoresFila[c.Clave] = v.Valor;
                foreach (var campo in plantilla.Where(c => c.TipoDato == TipoDatoCampoEnsayo.Formula && !string.IsNullOrWhiteSpace(c.Formula)))
                {
                    try { valoresFila[campo.Clave] = EvaluadorFormulas.Evaluar(campo.Formula!, valoresFila); }
                    catch { valoresFila[campo.Clave] = "#ERROR"; }
                }
                return valoresFila;
            });
            string? TorreOLocalizacion(int id) => porId[id].ElementoTrazadoId is int t && torres.TryGetValue(t, out var codigo)
                ? codigo : tipo.UsaTorre ? null : porId[id].Localizacion;
            var columnas = new List<ColumnaNativa>
            {
                new("Torre", tipo.UsaTorre ? "Torre" : "Localización", TipoCampoTabla.Texto, id => TorreOLocalizacion(id)),
                new("Elemento", "Elemento", TipoCampoTabla.Texto, id => porId[id].ElementoTexto)
            };
            foreach (var c in plantilla)
            {
                var tipoCampo = TipoDe(c.TipoDato);
                var nombre = string.IsNullOrEmpty(c.Unidad) ? c.Etiqueta : $"{c.Etiqueta} ({c.Unidad})";
                columnas.Add(new(c.Clave, nombre, tipoCampo,
                    id => MotorTabla.Tipar(new CampoTabla { Tipo = tipoCampo }, datos[id].GetValueOrDefault(c.Clave)), true, "Excel"));
            }
            columnas.Add(new("Informe", "Informe", TipoCampoTabla.Texto, id => Informe(id)));
            columnas.Add(tipo.UsaTorre
                ? new("DisenoMezcla", "Diseño mezcla", TipoCampoTabla.Texto, id => porId[id].DisenoMezclaCodigo)
                : new("FechaEjecucion", "Fecha ejecución", TipoCampoTabla.Fecha, id => porId[id].FechaEjecucion));
            var motorTipo = await MotorTabla.CargarLecturaAsync(db, $"ensayos:{tipo.Id}", columnas,
                grupo.Select(r => (r.Id, $"{TorreOLocalizacion(r.Id)} ? {r.ElementoTexto} ? #{r.Id}")));
            fuente.Hojas.Add(new(tipo.Nombre, [], grupo.Select(r => new FilaDossier(r.Id)), motorTipo));
        }
        return fuente;
    }
}
