using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Data.Indicadores;
using SigWeb.Data.Auditorias;
using SigWeb.Data.Planes;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

public sealed class FuenteIndicadores : IFuenteDossier
{
    public string Clave => "indicadores";
    public string Nombre => "Indicadores";
    public string Icono => "indicadores";
    public const string Soportes = "Soportes";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var indicadores = await db.Indicadores.AsNoTracking().Where(i => !i.Eliminado).OrderBy(i => i.Orden).ThenBy(i => i.Id).ToListAsync(ct);
        var mediciones = await db.MedicionesIndicador.AsNoTracking().Where(m => !m.Eliminado).ToListAsync(ct);
        var porMes = mediciones.GroupBy(m => (m.IndicadorId, m.Anio, m.Mes))
            .ToDictionary(g => g.Key, g => g.MaxBy(m => m.Id)!);
        var contexto = ContextoIndicadores.Cargar(db);
        var porId = indicadores.ToDictionary(i => i.Id);
        Indicador? I(int id) => porId.GetValueOrDefault(id);
        var nativas = new List<ColumnaNativa>
        {
            new("Nombre", "Indicador", TipoCampoTabla.Texto, id => I(id)?.Nombre),
            new("Formula", "Fórmula", TipoCampoTabla.Texto, id => I(id)?.Formula),
            new("Objetivo", "Objetivo", TipoCampoTabla.Texto, id => I(id)?.Objetivo),
            new("Meta", "Meta", TipoCampoTabla.Texto, id => I(id) is { Meta: decimal m } i ? $"{i.OperadorMeta} {m:0.##}" : null),
            new("Unidad", "Unidad", TipoCampoTabla.Texto, id => I(id)?.Unidad),
            new("Frecuencia", "Frecuencia", TipoCampoTabla.Texto, id => I(id) is { } i ? i.FrecuenciaTexto ?? i.Frecuencia.ToString() : null),
            new("Responsable", "Responsable", TipoCampoTabla.Texto, id => I(id)?.Responsable),
            new("FuenteInformacion", "Fuente de información", TipoCampoTabla.Texto, id => I(id)?.FuenteInformacion),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:indicadores", nativas, indicadores.Select(i => (i.Id, i.Nombre)));

        var medPorId = mediciones.ToDictionary(m => m.Id);
        var elementos = indicadores.ToDictionary(i => i.Id, i => new ElementoDossier { Id = i.Id, Grupo = i.TipoIndicador });
        foreach (var a in await db.ArchivosMedicionIndicador.AsNoTracking().Where(a => !a.Eliminado).OrderBy(a => a.Id).ToListAsync(ct))
            if (medPorId.TryGetValue(a.MedicionIndicadorId, out var m) && elementos.TryGetValue(m.IndicadorId, out var el))
                el.Archivos.Add(new ArchivoDossier(Soportes, a.NombreOriginal, a.RutaArchivo, Carpeta: $"{m.Anio}-{m.Mes:00}"));
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = indicadores.Select(i => elementos[i.Id]).ToList(), ColumnaNombre = "Nombre",
            NombreGrupo = "Tipo", Clases = new() { Soportes }
        };
        var anios = mediciones.Where(m => porId.ContainsKey(m.IndicadorId)).Select(m => m.Anio)
            .Append(DateTime.Today.Year).Distinct().OrderByDescending(a => a);
        foreach (var anio in anios)
        {
            ct.ThrowIfCancellationRequested();
            var filas = new List<FilaDossier>();
            foreach (var ind in indicadores)
                for (var mes = 1; mes <= 12; mes++)
                {
                    var m = porMes.GetValueOrDefault((ind.Id, anio, mes));
                    var calculado = CalculoIndicadores.Calculado(ind.Fuente);
                    var enVivo = calculado && m?.Congelado != true && CalculoIndicadores.Mide(ind, anio, mes);
                    var resultado = enVivo ? CalculoIndicadores.Calcular(ind, anio, mes, contexto, m?.BaseCalculo) : null;
                    var na = enVivo ? resultado?.NoAplica == true || resultado is { Valor: null } : m?.NoAplica == true;
                    var valor = enVivo ? resultado?.Valor : m?.Valor;
                    var estado = m?.Congelado == true ? "Congelado" : enVivo ? "En vivo" : m is null ? "" : "Digitado";
                    filas.Add(new(ind.Id, ind.Nombre, new DateTime(anio, mes, 1).ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("es-CO")),
                        ind.Meta is decimal meta ? $"{ind.OperadorMeta} {CalculoIndicadores.Formato(ind, meta)}" : null,
                        na ? "N/A" : valor is not null ? CalculoIndicadores.Formato(ind, valor) : null,
                        enVivo ? resultado?.Numerador : m?.Numerador, enVivo ? resultado?.Denominador : m?.Denominador,
                        estado, m?.FechaCongelado, enVivo ? resultado?.Detalle : m?.Detalle, m?.Observaciones));
                }
            fuente.Hojas.Add(new($"Indicadores {anio}", ["Indicador", "Mes", "Meta", "Valor", "Numerador", "Denominador",
                "Registro", "Fecha de congelación", "Detalle", "Observaciones"], filas));
        }
        return fuente;
    }
}

// Auditoría interna: auditorías de los programas con sus hallazgos; adjuntos = anexos de cada hallazgo (carpeta = código
// del hallazgo). Grupo = programa.
public sealed class FuenteAuditorias : IFuenteDossier
{
    public string Clave => "auditoria-interna";
    public string Nombre => "Auditoría interna";
    public string Icono => "auditoria-interna";
    public const string Anexos = "Anexos de hallazgos";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var programas = await db.ProgramasAuditoria.AsNoTracking().Where(p => !p.Eliminado).ToDictionaryAsync(p => p.Id, ct);
        var auditorias = (await db.Auditorias.AsNoTracking().Where(a => !a.Eliminado).ToListAsync(ct))
            .Where(a => programas.ContainsKey(a.ProgramaAuditoriaId)).OrderByDescending(a => programas[a.ProgramaAuditoriaId].Anio).ThenBy(a => a.Codigo).ToList();
        var hallazgos = await db.HallazgosAuditoria.AsNoTracking().Where(h => !h.Eliminado).ToListAsync(ct);
        var halPorAud = hallazgos.GroupBy(h => h.AuditoriaId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.Codigo).ToList());
        var porId = auditorias.ToDictionary(a => a.Id);
        Auditoria? A(int id) => porId.GetValueOrDefault(id);
        List<HallazgoAuditoria> H(int id) => halPorAud.TryGetValue(id, out var l) ? l : new();
        string Programa(Auditoria a) => programas[a.ProgramaAuditoriaId] is var p ? $"{p.Anio} · {p.Titulo}" : "";
        var nativas = new[]
        {
            new ColumnaNativa("Codigo", "Código", TipoCampoTabla.Texto, id => A(id)?.Codigo),
            new ColumnaNativa("Programa", "Programa", TipoCampoTabla.Texto, id => A(id) is { } a ? Programa(a) : null),
            new ColumnaNativa("Tipo", "Tipo", TipoCampoTabla.Texto, id => A(id)?.Tipo),
            new ColumnaNativa("Mes", "Mes", TipoCampoTabla.Texto, id => A(id) is { } a ? EscalaAuditoria.Mes(a.Mes) : null),
            new ColumnaNativa("FechaInicio", "Inicio", TipoCampoTabla.Fecha, id => A(id)?.FechaInicio),
            new ColumnaNativa("FechaFin", "Fin", TipoCampoTabla.Fecha, id => A(id)?.FechaFin),
            new ColumnaNativa("Procesos", "Procesos", TipoCampoTabla.Texto, id => A(id)?.Procesos),
            new ColumnaNativa("Alcance", "Alcance", TipoCampoTabla.Texto, id => A(id)?.Alcance),
            new ColumnaNativa("AuditorLider", "Auditor líder", TipoCampoTabla.Texto, id => A(id)?.AuditorLider),
            new ColumnaNativa("Estado", "Estado", TipoCampoTabla.Texto, id => A(id) is { } a ? EscalaAuditoria.Nombre(a.Estado) : null),
            new ColumnaNativa("Hallazgos", "Hallazgos", TipoCampoTabla.Texto, id => string.Join("\n",
                H(id).Select(h => $"{h.Codigo} · {h.Tipo}{(string.IsNullOrWhiteSpace(h.Grado) ? "" : " " + h.Grado)} · {h.Descripcion}"))),
            new ColumnaNativa("Conclusiones", "Conclusiones", TipoCampoTabla.Texto, id => A(id)?.Conclusiones, visiblePorDefecto: false),
        };
        var motor = await MotorTabla.CargarLecturaAsync(db, "dossier:auditoria-interna", nativas, auditorias.Select(a => (a.Id, a.Codigo)));

        var idsAud = porId.Keys.ToList();
        var hallazgosPorId = hallazgos.Where(h => porId.ContainsKey(h.AuditoriaId)).ToDictionary(h => h.Id);
        var idsHallazgos = hallazgosPorId.Keys.ToList();
        var relaciones = await db.HallazgosPlan.AsNoTracking().Where(r => idsHallazgos.Contains(r.HallazgoAuditoriaId)).ToListAsync(ct);
        var idsPlanLigado = relaciones.Select(r => r.PlanAccionId).Distinct().ToList();
        var planes = await db.PlanesAccion.AsNoTracking().Where(p => !p.Eliminado &&
            ((p.AuditoriaId != null && idsAud.Contains(p.AuditoriaId.Value)) || idsPlanLigado.Contains(p.Id)))
            .OrderBy(p => p.Codigo).ToDictionaryAsync(p => p.Id, ct);
        var idsPlan = planes.Keys.ToList();
        var acciones = (await db.AccionesPlan.AsNoTracking().Where(a => !a.Eliminado && idsPlan.Contains(a.PlanAccionId))
            .OrderBy(a => a.Orden).ThenBy(a => a.Id).ToListAsync(ct)).ToLookup(a => a.PlanAccionId);
        var estados = planes.ToDictionary(x => x.Key, x => EscalaAccion.EstadoPlan(acciones[x.Key]));
        var soportes = (await db.ArchivosEficaciaPlan.AsNoTracking().Where(x => !x.Eliminado && idsPlan.Contains(x.PlanAccionId))
            .OrderBy(x => x.Id).ToListAsync(ct)).ToLookup(x => x.PlanAccionId);
        var cambios = (await db.CambiosAuditoria.AsNoTracking().Where(c => idsAud.Contains(c.AuditoriaId))
            .OrderBy(c => c.Fecha).ToListAsync(ct)).ToLookup(c => c.AuditoriaId);
        var opciones = OpcionesAuditoria.Cargar(db);
        string Lugar(int? torre, int? componente, int? item, int? tiro) => opciones.TextoLugar(new LugarPlan
        {
            ElementoTrazadoId = torre, ComponenteId = componente, ItemCatalogoId = item, TiroId = tiro
        });
        var lugaresAud = (await db.LugaresAuditoria.AsNoTracking().Where(l => idsAud.Contains(l.AuditoriaId)).ToListAsync(ct))
            .ToLookup(l => l.AuditoriaId, l => Lugar(l.ElementoTrazadoId, l.ComponenteId, l.ItemCatalogoId, l.TiroId));
        var lugaresHall = (await db.LugaresHallazgoAuditoria.AsNoTracking().Where(l => idsHallazgos.Contains(l.HallazgoAuditoriaId)).ToListAsync(ct))
            .ToLookup(l => l.HallazgoAuditoriaId, l => Lugar(l.ElementoTrazadoId, l.ComponenteId, l.ItemCatalogoId, l.TiroId));
        var anexos = (await db.ArchivosHallazgoAuditoria.AsNoTracking().Where(x => !x.Eliminado && idsHallazgos.Contains(x.HallazgoAuditoriaId))
            .OrderBy(x => x.Id).ToListAsync(ct)).ToLookup(x => x.HallazgoAuditoriaId);
        var relPorHallazgo = relaciones.Where(r => planes.ContainsKey(r.PlanAccionId)).ToLookup(r => r.HallazgoAuditoriaId, r => r.PlanAccionId);
        var planesDirectos = planes.Values.ToLookup(p => p.AuditoriaId, p => p.Id);
        var elementos = auditorias.ToDictionary(a => a.Id, a => new ElementoDossier { Id = a.Id, Grupo = Programa(a) });
        var informes = new Dictionary<int, InformeAuditoria>();
        foreach (var a in auditorias)
        {
            var hs = H(a.Id);
            var hallazgosPorPlan = hs.SelectMany(h => relPorHallazgo[h.Id].Select(id => (Id: id, h.Codigo)))
                .ToLookup(x => x.Id, x => x.Codigo);
            var ids = hallazgosPorPlan.Select(g => g.Key).Concat(planesDirectos[a.Id]).Distinct().OrderBy(id => planes[id].Codigo);
            var informe = new InformeAuditoria
            {
                Auditoria = a,
                Programa = programas[a.ProgramaAuditoriaId],
                Lugares = lugaresAud[a.Id].ToList(),
                Cambios = cambios[a.Id].ToList(),
                Hallazgos = hs.Select(h => new InformeAuditoria.HallazgoInforme
                {
                    H = h,
                    Estado = ServicioAuditorias.EstadoDe(h, relPorHallazgo[h.Id].Select(id =>
                        new ServicioAuditorias.PlanRef(id, planes[id].Codigo, estados[id], planes[id].Eficaz, planes[id].FechaProgramadaEficacia)).ToList()),
                    Lugares = lugaresHall[h.Id].ToList(),
                    Fotos = anexos[h.Id].Where(x => AlmacenAnexos.EsImagen(x.RutaArchivo)).ToList(),
                    Anexos = anexos[h.Id].Where(x => !AlmacenAnexos.EsImagen(x.RutaArchivo)).ToList(),
                    Planes = relPorHallazgo[h.Id].ToList()
                }).ToList(),
                Planes = ids.Select(id => new InformeAuditoria.PlanInforme
                {
                    P = planes[id],
                    Estado = estados[id],
                    Eficacia = EscalaEficacia.De(planes[id].Eficaz, planes[id].FechaProgramadaEficacia, estados[id]),
                    Acciones = acciones[id].ToList(),
                    Soportes = soportes[id].ToList(),
                    Hallazgos = hallazgosPorPlan[id].Order().ToList()
                }).ToList()
            };
            informes[a.Id] = informe;
            var el = elementos[a.Id];
            foreach (var h in hs)
                foreach (var x in anexos[h.Id])
                    el.Archivos.Add(new ArchivoDossier(Anexos, x.NombreOriginal, x.RutaArchivo, Carpeta: h.Codigo));
            el.ArchivosDeTabla.Add((ExportadorInformeAuditoria.NombreArchivo(informe, "docx"),
                raiz => ExportadorInformeAuditoria.Word(informe, raiz)));
        }
        var fuente = new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = auditorias.Select(a => elementos[a.Id]).ToList(), ColumnaNombre = "Codigo",
            NombreGrupo = "Programa", Clases = new() { Anexos }
        };
        fuente.Hojas.Add(new("Programas de auditoría",
            ["Auditoría", "Año", "Programa", "Proyecto", "Normas", "Objetivos", "Responsable", "Recursos", "Estado", "Aprobó", "Fecha de aprobación"],
            auditorias.Select(a =>
            {
                var p = programas[a.ProgramaAuditoriaId];
                return new FilaDossier(a.Id, a.Codigo, p.Anio, p.Titulo, p.Proyecto, p.Normas, p.Objetivos, p.Responsable,
                    p.Recursos, p.Estado, p.Aprobo, p.FechaAprobacion);
            })));
        fuente.Hojas.Add(new("Hallazgos de auditoría",
            ["Auditoría", "Código", "Fecha", "Tipo", "Grado", "Normas", "Numerales", "Documento", "Requisito", "Evidencia",
                "Declaración", "Procesos", "Auditor", "Estado", "Lugares", "Planes ligados"],
            auditorias.SelectMany(a => informes[a.Id].Hallazgos.Select(x =>
            {
                var h = x.H;
                return new FilaDossier(a.Id, a.Codigo, h.Codigo, h.Fecha, h.Tipo, h.Grado, h.Normas, h.Numerales, h.Documento,
                    h.Requisito, h.Evidencia, h.Descripcion, h.Procesos, h.Auditor, EscalaAuditoria.Nombre(x.Estado),
                    string.Join("\n", x.Lugares), string.Join("\n", x.Planes.Select(id => planes[id].Codigo)));
            }))));
        fuente.Hojas.Add(new("Planes de auditoría",
            ["Auditoría", "Plan", "Hallazgos", "Descripción", "Estado", "Eficacia"],
            auditorias.SelectMany(a => informes[a.Id].Planes.Select(x => new FilaDossier(a.Id, a.Codigo, x.P.Codigo,
                string.Join("\n", x.Hallazgos), x.P.DescripcionHallazgo, EscalaAccion.Nombre(x.Estado), EscalaEficacia.Nombre(x.Eficacia))))));
        return fuente;
    }
}
