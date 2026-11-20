using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SigWeb.Components.Layout;
using SigWeb.Data.Planes;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Planes de acción: mismas columnas que la página; adjuntos en las mismas carpetas que su ventanita
// (CarpetasVista): una automática por acción ("Corrección 1", "Acción correctiva 1"…) con los anexos de
// sus productos y las carpetas con nombre del plan. Grupo = tipo de hallazgo.
public sealed class FuentePlanesAccion : IFuenteDossier
{
    public string Clave => "planes-accion";
    public string Nombre => "Planes de acción";
    public string Icono => "plan-accion";
    public const string ClaseAcciones = "Anexos de acciones", ClaseCarpetas = "Carpetas del plan";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var planes = await db.PlanesAccion.AsNoTracking().Where(p => !p.Eliminado).OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).ToListAsync(ct);
        var porId = planes.ToDictionary(p => p.Id);
        var idsAuditorias = planes.Select(p => p.AuditoriaId).OfType<int>().Distinct().ToList();
        var auditorias = await db.Auditorias.AsNoTracking().Where(a => !a.Eliminado && idsAuditorias.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Codigo, ct);
        string? Auditoria(PlanAccion? p) => p?.AuditoriaId is int id ? auditorias.GetValueOrDefault(id) : null;
        var acciones = await db.AccionesPlan.AsNoTracking().Where(a => !a.Eliminado).ToListAsync(ct);
        var accionesDe = acciones.GroupBy(a => a.PlanAccionId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Orden).ThenBy(a => a.Id).ToList());
        var localizaciones = await db.Catalogos.AsNoTracking().Where(c => c.Tipo == "Localización" && c.Activo).ToListAsync(ct);
        var torres = await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct);
        var componentes = await db.Componentes.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var tiros = await db.Tiros.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct);
        var lugares = (await db.LugaresPlan.AsNoTracking().ToListAsync(ct)).GroupBy(l => l.PlanAccionId).ToDictionary(g => g.Key, g => g.ToList());
        var cargas = await db.CargasPlanesAccion.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.NombreArchivo + (string.IsNullOrWhiteSpace(c.Hoja) ? "" : " › " + c.Hoja), ct);
        var versiones = await db.VersionesMatrizRiesgos.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(v => v.Id, ct);
        var riesgos = (await db.Riesgos.IgnoreQueryFilters().AsNoTracking()
                .Select(r => new { r.Id, r.Codigo, r.VersionMatrizRiesgosId, r.Eliminado }).ToListAsync(ct))
            .ToDictionary(r => r.Id, r => $"{r.Codigo} ({(versiones.TryGetValue(r.VersionMatrizRiesgosId, out var v) ? v.Codigo : "?")})"
                + (r.Eliminado || (versiones.TryGetValue(r.VersionMatrizRiesgosId, out var v2) && v2.Eliminado) ? " · en Papelera" : ""));
        var riesgosDe = PlanesAccionServicio.RiesgosDePlanes(db, planes);

        PlanAccion? P(int id) => porId.GetValueOrDefault(id);
        List<AccionPlan> A(int id) => accionesDe.TryGetValue(id, out var l) ? l : new();
        var resumen = new Dictionary<int, string>();   // texto de la columna Adjuntos (se llena con las carpetas)
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => P(id)?.Codigo),
            new ColumnaNativa("Fecha", "Fecha", TipoCampoTabla.Fecha, id => P(id)?.Fecha),
            new ColumnaNativa("Origen", "Origen", TipoCampoTabla.Texto, id => P(id)?.OrigenHallazgo),
            new ColumnaNativa("Auditoria", "Auditoría", TipoCampoTabla.Texto, id => Auditoria(P(id))),
            new ColumnaNativa("Tipo", "Tipo de hallazgo", TipoCampoTabla.Texto, id => P(id)?.TipoHallazgo),
            new ColumnaNativa("Descripcion", "Descripción del hallazgo", TipoCampoTabla.Texto, id => P(id)?.DescripcionHallazgo),
            new ColumnaNativa("Procesos", "Procesos", TipoCampoTabla.Texto, id => P(id)?.Proceso),
            new ColumnaNativa("Lugares", "Lugares", TipoCampoTabla.Texto, id => lugares.TryGetValue(id, out var l)
                ? string.Join("\n", l.Select(x => SelectorLugares.Texto(x, torres, componentes, localizaciones, tiros))) : null),
            new ColumnaNativa("Riesgo", "Riesgos", TipoCampoTabla.Texto, id => riesgosDe.TryGetValue(id, out var l)
                ? string.Join("\n", l.Select(r => riesgos.GetValueOrDefault(r) ?? "").Where(x => x.Length > 0)) : null),
            new ColumnaNativa("Acciones", "Acciones", TipoCampoTabla.Texto, id => string.Join("\n", A(id).Select(a => $"[{EscalaAccion.Nombre(a.Estado)}] {a.Descripcion}"))),
            new ColumnaNativa("Estado", "Estado del plan", TipoCampoTabla.Texto, id => EscalaAccion.Nombre(EscalaAccion.EstadoPlan(A(id)))),
            new ColumnaNativa("Adjuntos", "Adjuntos", TipoCampoTabla.Texto, id => resumen.GetValueOrDefault(id)),
            new ColumnaNativa("Analisis", "Análisis de causa", TipoCampoTabla.Texto, id => P(id)?.AnalisisCausa),
            new ColumnaNativa("Reporto", "Reportó", TipoCampoTabla.Texto, id => P(id) is { } p
                ? string.Join(" · ", new[] { p.ReportoNombre, p.ReportoCargo, p.ReportoEmpresa }.Where(x => !string.IsNullOrWhiteSpace(x))) : null),
            new ColumnaNativa("Carga", "Carga", TipoCampoTabla.Texto, id => P(id)?.CargaPlanesAccionId is int c && cargas.TryGetValue(c, out var n) ? n : null, visiblePorDefecto: false),
        };

        // Adjuntos (CarpetasVista de la página)
        var anexos = (await db.ArchivosAccionPlan.AsNoTracking().Where(x => !x.Eliminado).ToListAsync(ct))
            .GroupBy(x => x.AccionPlanId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FechaRegistro).ThenByDescending(x => x.Id).ToList());
        var carpetas = (await db.CarpetasPlan.AsNoTracking().Where(c => !c.Eliminado).ToListAsync(ct))
            .GroupBy(c => c.PlanAccionId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList());
        var archivosCarpeta = (await db.ArchivosCarpetaPlan.AsNoTracking().Where(a => !a.Eliminado).ToListAsync(ct))
            .GroupBy(a => a.CarpetaPlanId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FechaCarga).ThenByDescending(a => a.Id).ToList());

        // Campos "Archivos adjuntos" del formulario (carpeta = paso donde está) y soportes de la verificación de eficacia.
        var camposArch = new List<CampoTabla>();
        var archCampo = new Dictionary<(int, int), List<ArchivoCampoFormulario>>();
        var nombresCampo = new Dictionary<int, string>();
        try
        {
            camposArch = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == ImportadorPlanesAccion.Tabla && !c.Eliminado && c.Tipo == TipoCampoTabla.Archivos).OrderBy(c => c.Orden).ToListAsync(ct);
            var idsC = camposArch.Select(c => c.Id).ToList();
            archCampo = (await db.ArchivosCampoFormulario.AsNoTracking().Where(a => idsC.Contains(a.CampoTablaId)).ToListAsync(ct))
                .GroupBy(a => (a.CampoTablaId, a.FilaId)).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FechaCarga).ToList());
            nombresCampo = await SigWeb.Data.Formularios.ArchivosCampo.NombresCarpetaAsync(db, "planes-accion", camposArch);
        }
        catch { }   // sin la migración aplicada
        var eficacia = (await db.ArchivosEficaciaPlan.AsNoTracking().ToListAsync(ct))
            .GroupBy(a => a.PlanAccionId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FechaRegistro).ToList());

        var productos = (await db.ProductosAccion.AsNoTracking().OrderBy(p => p.Orden).ThenBy(p => p.Id).ToListAsync(ct))
            .ToLookup(p => p.AccionPlanId);
        var seguimientos = (await db.SeguimientosAccion.AsNoTracking().Where(s => !s.Eliminado).OrderBy(s => s.Fecha).ToListAsync(ct))
            .ToLookup(s => s.AccionPlanId);
        var pasos = (await db.PasosNoAplica.AsNoTracking().Where(p => p.Formulario == "planes-accion").ToListAsync(ct))
            .ToLookup(p => p.FilaId);
        var elementos = new List<ElementoDossier>();
        foreach (var p in planes)
        {
            var el = new ElementoDossier { Id = p.Id, Grupo = p.TipoHallazgo };
            if (SvgParaDescarga(p.AnalisisCausaSvg) is { } svg)
                el.ArchivosDeTabla.Add(("Análisis de causa.svg", _ => System.Text.Encoding.UTF8.GetBytes(svg)));
            var cuenta = new Dictionary<TipoAccionPlan, int>();
            var lineas = new List<string>();
            foreach (var a in A(p.Id))
            {
                cuenta[a.Tipo] = cuenta.TryGetValue(a.Tipo, out var n) ? n + 1 : 1;
                var carpeta = $"{EscalaAccion.Nombre(a.Tipo)} {cuenta[a.Tipo]}";
                if (!anexos.TryGetValue(a.Id, out var l) || l.Count == 0) continue;
                foreach (var x in l) el.Archivos.Add(new ArchivoDossier(ClaseAcciones, x.NombreOriginal, x.RutaArchivo, Carpeta: carpeta));
                lineas.Add($"{carpeta} ({l.Count})");
            }
            if (carpetas.TryGetValue(p.Id, out var cs))
                foreach (var c in cs)
                {
                    if (!archivosCarpeta.TryGetValue(c.Id, out var l) || l.Count == 0) continue;
                    foreach (var x in l) el.Archivos.Add(new ArchivoDossier(ClaseCarpetas, x.NombreOriginal, x.RutaArchivo, Carpeta: c.Nombre));
                    lineas.Add($"{c.Nombre} ({l.Count})");
                }
            foreach (var c in camposArch)
            {
                if (!archCampo.TryGetValue((c.Id, p.Id), out var l) || l.Count == 0) continue;
                var nombre = nombresCampo.GetValueOrDefault(c.Id, c.Nombre);
                foreach (var x in l) el.Archivos.Add(new ArchivoDossier(ClaseCarpetas, x.NombreOriginal, x.RutaArchivo, Carpeta: nombre));
                lineas.Add($"{nombre} ({l.Count})");
            }
            if (eficacia.TryGetValue(p.Id, out var ef) && ef.Count > 0)
            {
                foreach (var x in ef) el.Archivos.Add(new ArchivoDossier(ClaseCarpetas, x.NombreOriginal, x.RutaArchivo, Carpeta: "Soportes de la verificación"));
                lineas.Add($"Soportes de la verificación ({ef.Count})");
            }
            resumen[p.Id] = string.Join("\n", lineas);
            elementos.Add(el);
        }
        var motor = await MotorTabla.CargarLecturaAsync(db, ImportadorPlanesAccion.Tabla, nativas, planes.Select(p => (p.Id, p.Codigo)));

        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = elementos, ColumnaNombre = "Codigo", NombreGrupo = "Tipo de hallazgo",
            Clases = new() { ClaseAcciones, ClaseCarpetas }
        };
        var camposFicha = motor.Ordenados.Where(c => c.Origen != OrigenCampoTabla.Nativo).ToArray();
        fuente.Hojas.Add(new("Ficha del plan",
            new[] { "Código", "Fecha", "Origen", "Auditoría", "Tipo de hallazgo", "Descripción del hallazgo", "Procesos", "Lugares", "Riesgos",
                "Reportó", "Cargo", "Empresa", "Análisis de causa", "Pasos N/A", "Criterio de eficacia",
                "Verificación programada", "Fecha de verificación", "Verificó", "Evidencia", "Eficaz" }
                .Concat(camposFicha.Select(c => c.Nombre)).ToArray(),
            planes.Select(p => new FilaDossier(p.Id, new object?[] { p.Codigo, p.Fecha, p.OrigenHallazgo, Auditoria(p), p.TipoHallazgo,
                p.DescripcionHallazgo, p.Proceso, fuente.Texto(p.Id, "Lugares"), fuente.Texto(p.Id, "Riesgo"),
                p.ReportoNombre, p.ReportoCargo, p.ReportoEmpresa, p.AnalisisCausa, string.Join("\n", pasos[p.Id].Select(x => x.Paso)),
                p.CriterioEficacia, p.FechaProgramadaEficacia, p.FechaVerificacionEficacia, p.VerificoEficacia, p.EvidenciaEficacia, p.Eficaz }
                .Concat(camposFicha.Select(c => (object?)motor.Texto(p.Id, c, out _))).ToArray()))));
        fuente.Hojas.Add(new("Acciones propuestas",
            ["Plan", "Orden", "Tipo", "Descripción", "Fecha", "Responsable", "Estado", "Productos"],
            planes.SelectMany(p => A(p.Id).Select(a => new FilaDossier(p.Id, p.Codigo, a.Orden, EscalaAccion.Nombre(a.Tipo),
                a.Descripcion, a.Fecha, a.Responsable, EscalaAccion.Nombre(a.Estado),
                string.Join("\n", productos[a.Id].Select(x => x.Descripcion).DefaultIfEmpty(a.Producto)))))));
        fuente.Hojas.Add(new("Seguimientos de acciones",
            ["Plan", "Acción", "Fecha", "Responsable", "Observación", "Estado anterior", "Estado nuevo"],
            planes.SelectMany(p => A(p.Id).SelectMany(a => seguimientos[a.Id].Select(x => new FilaDossier(p.Id,
                p.Codigo, a.Descripcion, x.Fecha, x.Responsable, x.Observacion, EscalaAccion.Nombre(x.EstadoAnterior), EscalaAccion.Nombre(x.EstadoNuevo)))))));
        return fuente;
    }
    private static readonly HashSet<string> ElementosSvg = new(StringComparer.Ordinal)
    {
        "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "tspan", "title",
        "defs", "linearGradient", "radialGradient", "stop", "filter", "feDropShadow", "feGaussianBlur", "feMerge", "feMergeNode"
    };

    // El editor incrusta la imagen en un img. Un SVG descargado se abre como documento y requiere validación adicional.
    private static string? SvgParaDescarga(string? svg)
    {
        if (string.IsNullOrWhiteSpace(svg) || svg.Length > AnalisisCausa.MaxBytes) return null;
        try
        {
            using var entrada = new StringReader(svg);
            using var lector = XmlReader.Create(entrada, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = AnalisisCausa.MaxBytes,
                IgnoreProcessingInstructions = true
            });
            var raiz = XElement.Load(lector);
            XNamespace ns = "http://www.w3.org/2000/svg";
            if (raiz.Name != ns + "svg") return null;
            foreach (var nodo in raiz.DescendantsAndSelf())
            {
                if (nodo.Name.Namespace != ns || !ElementosSvg.Contains(nodo.Name.LocalName)) return null;
                foreach (var a in nodo.Attributes().Where(a => !a.IsNamespaceDeclaration))
                {
                    var nombre = a.Name.LocalName;
                    if (a.Name.Namespace != XNamespace.None || nombre.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                        || nombre is "href" or "src" or "style") return null;
                    if (a.Value.Contains("url", StringComparison.OrdinalIgnoreCase)
                        && !System.Text.RegularExpressions.Regex.IsMatch(a.Value, @"^url\(#[a-zA-Z0-9_-]+\)$")) return null;
                }
            }
            return raiz.ToString(SaveOptions.DisableFormatting);
        }
        catch (XmlException) { return null; }
    }
}
