using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Riesgos;

// Reglas de la matriz de riesgos: una sola versión vigente, revisiones que
// nacen como copia de la anterior, y seguimientos que siempre se registran
// contra la fila del riesgo de la versión en la que se hicieron (por eso
// una revisión nueva nunca borra ni mueve la trazabilidad anterior).
public class MatrizRiesgosServicio
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    public MatrizRiesgosServicio(AppDbContext db, IServiceScopeFactory scopes) { _db = db; _scopes = scopes; }

    public VersionMatrizRiesgos? Vigente() =>
        _db.VersionesMatrizRiesgos.AsNoTracking()
            .FirstOrDefault(v => v.Estado == EstadoVersionMatriz.Vigente);

    public List<VersionMatrizRiesgos> Versiones() =>
        _db.VersionesMatrizRiesgos.AsNoTracking()
            .OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).ToList();

    // Marca una versión como vigente y pasa la anterior a histórica.
    public async Task MarcarVigenteAsync(int versionId)
    {
        var todas = await _db.VersionesMatrizRiesgos.ToListAsync();
        foreach (var v in todas)
        {
            if (v.Id == versionId) v.Estado = EstadoVersionMatriz.Vigente;
            else if (v.Estado == EstadoVersionMatriz.Vigente) v.Estado = EstadoVersionMatriz.Historica;
        }
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    // Crea una revisión nueva copiando los riesgos y sus alcances desde
    // "desdeVersionId" (normalmente la vigente). Los seguimientos y
    // hallazgos NO se copian: quedan amarrados a la versión en la que se
    // registraron, que es lo que conserva la trazabilidad histórica.
    public async Task<VersionMatrizRiesgos> CrearVersionAsync(VersionMatrizRiesgos datos, int? desdeVersionId)
    {
        var nueva = new VersionMatrizRiesgos
        {
            Codigo = (datos.Codigo ?? "").Trim(),
            Fecha = datos.Fecha,
            ElaboradoPor = (datos.ElaboradoPor ?? "").Trim(),
            RevisadoPor = (datos.RevisadoPor ?? "").Trim(),
            AprobadoPor = (datos.AprobadoPor ?? "").Trim(),
            EstadoDocumento = (datos.EstadoDocumento ?? "").Trim(),
            Etapa = (datos.Etapa ?? "").Trim(),
            HojaOrigen = (datos.HojaOrigen ?? "").Trim(),
            DescripcionCambios = (datos.DescripcionCambios ?? "").Trim(),
            Estado = EstadoVersionMatriz.Borrador,
            VersionAnteriorId = desdeVersionId
        };
        _db.VersionesMatrizRiesgos.Add(nueva);
        await _db.SaveChangesAsync();

        if (desdeVersionId is int origen)
        {
            var riesgos = await _db.Riesgos.AsNoTracking()
                .Where(r => r.VersionMatrizRiesgosId == origen)
                .OrderBy(r => r.Orden).ToListAsync();

            var mapa = new Dictionary<int, Riesgo>();
            foreach (var r in riesgos)
            {
                var copia = new Riesgo
                {
                    VersionMatrizRiesgosId = nueva.Id,
                    Codigo = r.Codigo,
                    Orden = r.Orden,
                    Tipo = r.Tipo,
                    EstadoActual = r.EstadoActual,
                    Categoria = r.Categoria,
                    Area = r.Area,
                    Causa = r.Causa,
                    Descripcion = r.Descripcion,
                    Consecuencia = r.Consecuencia,
                    Probabilidad = r.Probabilidad,
                    Impacto = r.Impacto,
                    Disparadores = r.Disparadores,
                    Estrategia = r.Estrategia,
                    PlanRespuesta = r.PlanRespuesta,
                    ResponsablePlan = r.ResponsablePlan,
                    EstadoPlan = r.EstadoPlan,
                    ComoSeHaceSeguimiento = r.ComoSeHaceSeguimiento,
                    RiesgoAnteriorId = r.Id
                };
                _db.Riesgos.Add(copia);
                mapa[r.Id] = copia;
            }
            await _db.SaveChangesAsync();

            var idsOrigen = riesgos.Select(r => r.Id).ToList();
            var elementos = await _db.ElementosRiesgo.AsNoTracking().Where(e => idsOrigen.Contains(e.RiesgoId)).ToListAsync();
            foreach (var e in elementos)
            {
                if (!mapa.TryGetValue(e.RiesgoId, out var destino)) continue;
                _db.ElementosRiesgo.Add(new ElementoRiesgo { RiesgoId = destino.Id, Tipo = e.Tipo, Orden = e.Orden, Texto = e.Texto });
            }
            var alcances = await _db.AlcancesRiesgo.AsNoTracking().Where(a => idsOrigen.Contains(a.RiesgoId)).ToListAsync();
            foreach (var a in alcances)
            {
                if (!mapa.TryGetValue(a.RiesgoId, out var destino)) continue;
                _db.AlcancesRiesgo.Add(new AlcanceRiesgo
                {
                    RiesgoId = destino.Id,
                    ElementoTrazadoId = a.ElementoTrazadoId,
                    ComponenteId = a.ComponenteId,
                    ItemCatalogoId = a.ItemCatalogoId,
                    TiroId = a.TiroId
                });
            }
            // Columnas digitadas de la lista ("Campos"): se copian con la fila.
            var camposRiesgos = await _db.CamposTabla.AsNoTracking().Where(c => c.Tabla == "riesgos").Select(c => c.Id).ToListAsync();
            var valores = await _db.ValoresCampoTabla.AsNoTracking()
                .Where(x => camposRiesgos.Contains(x.CampoTablaId) && idsOrigen.Contains(x.FilaId)).ToListAsync();
            foreach (var x in valores)
            {
                if (!mapa.TryGetValue(x.FilaId, out var destino)) continue;
                _db.ValoresCampoTabla.Add(new ValorCampoTabla { CampoTablaId = x.CampoTablaId, FilaId = destino.Id, Valor = x.Valor, Formula = x.Formula });
            }
            await _db.SaveChangesAsync();
        }

        _db.ChangeTracker.Clear();
        return nueva;
    }

    // ---------- Control de cambios (como Organigrama y Partes interesadas) ----------
    // Solo el borrador se edita. Editar sobre la vigente abre el borrador que
    // ya exista o crea uno copiándola: todos los cambios se acumulan ahí y
    // solo al aprobarlo nace una revisión nueva (la anterior pasa a histórica).
    public VersionMatrizRiesgos? Borrador() =>
        _db.VersionesMatrizRiesgos.AsNoTracking()
            .Where(v => v.Estado == EstadoVersionMatriz.Borrador)
            .OrderByDescending(v => v.Id).FirstOrDefault();

    public async Task<(VersionMatrizRiesgos Version, bool Creado)> AbrirBorradorAsync(int? desdeVersionId)
    {
        if (Borrador() is VersionMatrizRiesgos b) return (b, false);
        var origen = desdeVersionId is int id
            ? await _db.VersionesMatrizRiesgos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id)
            : Vigente();
        var codigos = await _db.VersionesMatrizRiesgos.AsNoTracking().Select(v => v.Codigo).ToListAsync();
        var codigo = "Rev. " + DateTime.Today.Year;
        for (var n = codigos.Count + 1; n < 200; n++)
        {
            var c = "Rev. " + n.ToString("00");
            if (!codigos.Contains(c, StringComparer.OrdinalIgnoreCase)) { codigo = c; break; }
        }
        var nueva = await CrearVersionAsync(new VersionMatrizRiesgos
        {
            Codigo = codigo,
            Fecha = DateTime.Today,
            Etapa = origen?.Etapa ?? "",
            HojaOrigen = ""
        }, origen?.Id);
        return (nueva, true);
    }

    // Un borrador copiado de otra revisión que se abrió y se dejó sin cambios se descarta: abrir la edición y salir
    // sin tocar nada no deja revisiones vacías. No se descarta si algo ya depende de sus riesgos (seguimientos,
    // planes, hallazgos) o si cambió el número de riesgos, sus alcances o su orden.
    public async Task<bool> DescartarBorradorVacioAsync(int borradorId)
    {
        var v = await _db.VersionesMatrizRiesgos.FirstOrDefaultAsync(x => x.Id == borradorId);
        if (v is null || v.Estado != EstadoVersionMatriz.Borrador || v.VersionAnteriorId is not int anterior) return false;
        if (Comparar(anterior, borradorId).Count > 0) return false;
        var rb = await _db.Riesgos.IgnoreQueryFilters().Where(r => r.VersionMatrizRiesgosId == borradorId).ToListAsync();
        var ra = await _db.Riesgos.AsNoTracking().Where(r => r.VersionMatrizRiesgosId == anterior).Select(r => new { r.Codigo, r.Orden }).ToListAsync();
        if (rb.Any(r => r.Eliminado) || rb.Count != ra.Count) return false;
        var ordenA = ra.GroupBy(r => r.Codigo).ToDictionary(g => g.Key, g => g.First().Orden);
        if (rb.Any(r => !ordenA.TryGetValue(r.Codigo, out var o) || o != r.Orden)) return false;
        var ids = rb.Select(r => r.Id).ToList();
        var idsA = await _db.Riesgos.AsNoTracking().Where(r => r.VersionMatrizRiesgosId == anterior).Select(r => r.Id).ToListAsync();
        if (await _db.AlcancesRiesgo.CountAsync(a => ids.Contains(a.RiesgoId)) != await _db.AlcancesRiesgo.CountAsync(a => idsA.Contains(a.RiesgoId))) return false;
        if (await _db.SeguimientosRiesgo.IgnoreQueryFilters().AnyAsync(s => ids.Contains(s.RiesgoId))) return false;
        if (await _db.PlanesAccion.IgnoreQueryFilters().AnyAsync(p => p.RiesgoId != null && ids.Contains(p.RiesgoId.Value))) return false;
        if (await _db.RiesgosPlan.AnyAsync(r => ids.Contains(r.RiesgoId))) return false;
        if (await _db.Riesgos.IgnoreQueryFilters().AnyAsync(r => r.RiesgoAnteriorId != null && ids.Contains(r.RiesgoAnteriorId.Value))) return false;
        if (await _db.VersionesMatrizRiesgos.IgnoreQueryFilters().AnyAsync(x => x.VersionAnteriorId == borradorId)) return false;
        _db.ElementosRiesgo.RemoveRange(await _db.ElementosRiesgo.Where(e => ids.Contains(e.RiesgoId)).ToListAsync());
        _db.AlcancesRiesgo.RemoveRange(await _db.AlcancesRiesgo.Where(a => ids.Contains(a.RiesgoId)).ToListAsync());
        _db.Riesgos.RemoveRange(rb);
        _db.VersionesMatrizRiesgos.Remove(v);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return true;
    }

    // Aprueba el borrador: guarda las firmas y el control de cambios y lo deja vigente.
    public async Task PublicarAsync(int versionId, VersionMatrizRiesgos datos)
    {
        var v = await _db.VersionesMatrizRiesgos.FirstAsync(x => x.Id == versionId);
        if (v.Estado != EstadoVersionMatriz.Borrador) throw new InvalidOperationException("Solo se aprueba un borrador.");
        v.Codigo = (datos.Codigo ?? "").Trim();
        v.Fecha = datos.Fecha;
        v.Etapa = (datos.Etapa ?? "").Trim();
        v.ElaboradoPor = (datos.ElaboradoPor ?? "").Trim();
        v.RevisadoPor = (datos.RevisadoPor ?? "").Trim();
        v.AprobadoPor = (datos.AprobadoPor ?? "").Trim();
        v.EstadoDocumento = (datos.EstadoDocumento ?? "").Trim();
        v.DescripcionCambios = (datos.DescripcionCambios ?? "").Trim();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        await MarcarVigenteAsync(versionId);
    }

    public enum TipoCambio { Nuevo, Modificado, Retirado }
    public sealed record CampoCambiado(string Campo, string Antes, string Despues);
    public sealed record CambioRiesgo(string Codigo, TipoCambio Tipo, string Descripcion, List<CampoCambiado> Campos);

    public static string NombreTipo(TipoCambio t) => t switch
    {
        TipoCambio.Nuevo => "Nuevos",
        TipoCambio.Retirado => "Retirados",
        _ => "Modificados"
    };

    // Compara dos revisiones riesgo por riesgo (el Código los identifica entre versiones).
    public List<CambioRiesgo> Comparar(int versionA, int versionB)
    {
        var ids = new[] { versionA, versionB };
        var filas = _db.Riesgos.AsNoTracking().Where(r => ids.Contains(r.VersionMatrizRiesgosId)).ToList();
        var idsR = filas.Select(r => r.Id).ToList();
        var els = _db.ElementosRiesgo.AsNoTracking().Where(e => idsR.Contains(e.RiesgoId)).ToList()
            .GroupBy(e => e.RiesgoId).ToDictionary(g => g.Key, g => g.ToList());
        Dictionary<string, Riesgo> Mapa(int v) => filas.Where(r => r.VersionMatrizRiesgosId == v)
            .GroupBy(r => r.Codigo.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var a = Mapa(versionA);
        var b = Mapa(versionB);

        var res = new List<CambioRiesgo>();
        foreach (var (cod, rb) in b)
        {
            if (!a.TryGetValue(cod, out var ra)) { res.Add(new(cod, TipoCambio.Nuevo, rb.Descripcion, new())); continue; }
            var va = Valores(ra, els.GetValueOrDefault(ra.Id));
            var vb = Valores(rb, els.GetValueOrDefault(rb.Id));
            var campos = va.Where(kv => !string.Equals(kv.Value, vb[kv.Key], StringComparison.Ordinal))
                .Select(kv => new CampoCambiado(kv.Key, kv.Value, vb[kv.Key])).ToList();
            if (campos.Count > 0) res.Add(new(cod, TipoCambio.Modificado, rb.Descripcion, campos));
        }
        foreach (var (cod, ra) in a)
            if (!b.ContainsKey(cod)) res.Add(new(cod, TipoCambio.Retirado, ra.Descripcion, new()));
        return res.OrderBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, string> Valores(Riesgo r, List<ElementoRiesgo>? els)
    {
        // Las listas (causas, consecuencias…) salen de sus elementos; si no tiene, del texto del Excel.
        string Lista(TipoElementoRiesgo t, string texto)
        {
            var l = els?.Where(e => e.Tipo == t).OrderBy(e => e.Orden).Select(e => e.Texto.Trim()).ToList();
            return l is { Count: > 0 } ? string.Join("\n", l) : (texto ?? "").Trim();
        }
        return new Dictionary<string, string>
        {
            ["Tipo"] = r.Tipo.ToString(),
            ["Estado"] = (r.EstadoActual ?? "").Trim(),
            ["Categoría"] = (r.Categoria ?? "").Trim(),
            ["Área"] = (r.Area ?? "").Trim(),
            ["Riesgo / oportunidad"] = (r.Descripcion ?? "").Trim(),
            [ElementosRiesgo.Nombre(TipoElementoRiesgo.Causa)] = Lista(TipoElementoRiesgo.Causa, r.Causa),
            [ElementosRiesgo.Nombre(TipoElementoRiesgo.Consecuencia)] = Lista(TipoElementoRiesgo.Consecuencia, r.Consecuencia),
            ["Probabilidad"] = EscalaRiesgo.Nombre(r.Probabilidad),
            ["Impacto"] = EscalaRiesgo.Nombre(r.Impacto),
            ["Nivel"] = EscalaRiesgo.Nombre(r.Nivel),
            [ElementosRiesgo.Nombre(TipoElementoRiesgo.Disparador)] = Lista(TipoElementoRiesgo.Disparador, r.Disparadores),
            ["Estrategia"] = (r.Estrategia ?? "").Trim(),
            [ElementosRiesgo.Nombre(TipoElementoRiesgo.PlanRespuesta)] = Lista(TipoElementoRiesgo.PlanRespuesta, r.PlanRespuesta),
            ["Responsable del plan"] = (r.ResponsablePlan ?? "").Trim(),
            ["Estado del plan"] = (r.EstadoPlan ?? "").Trim(),
            [ElementosRiesgo.Nombre(TipoElementoRiesgo.Seguimiento)] = Lista(TipoElementoRiesgo.Seguimiento, r.ComoSeHaceSeguimiento),
        };
    }

    // Siguiente código de riesgo libre dentro de una versión (R-01, R-02…).
    public string SiguienteCodigo(int versionId)
    {
        var usados = _db.Riesgos.AsNoTracking()
            .Where(r => r.VersionMatrizRiesgosId == versionId)
            .Select(r => r.Codigo).ToList();
        for (var n = 1; n < 1000; n++)
        {
            var c = "R-" + n.ToString("00");
            if (!usados.Contains(c, StringComparer.OrdinalIgnoreCase)) return c;
        }
        return "R-" + Guid.NewGuid().ToString("N")[..4];
    }

    // Cadena completa de un riesgo a lo largo de las versiones (el mismo
    // código), de la más nueva a la más vieja.
    public List<Riesgo> Historial(string codigo) =>
        (from r in _db.Riesgos.AsNoTracking()
         join v in _db.VersionesMatrizRiesgos.AsNoTracking() on r.VersionMatrizRiesgosId equals v.Id
         where r.Codigo == codigo
         orderby v.Fecha descending, v.Id descending
         select r).ToList();

    // El historial operativo pertenece al código, aunque cada seguimiento conserva su fila de origen.
    public List<SeguimientoRiesgo> Seguimientos(string codigo) =>
        (from s in _db.SeguimientosRiesgo.AsNoTracking()
         join r in _db.Riesgos.AsNoTracking() on s.RiesgoId equals r.Id
         join v in _db.VersionesMatrizRiesgos.AsNoTracking() on r.VersionMatrizRiesgosId equals v.Id
         where r.Codigo == codigo
         orderby s.Fecha descending, s.Id descending
         select s).ToList();

    // Lo mismo para una lista de riesgos (matriz, ronda): cada seguimiento queda con el RiesgoId del
    // riesgo de la lista que tiene su código, para contar "último seguimiento" y hallazgos abiertos
    // aunque se hayan registrado en una revisión anterior. Solo para leer (no se guardan).
    public List<SeguimientoRiesgo> SeguimientosPorCodigo(IEnumerable<Riesgo> riesgos)
    {
        var idPorCodigo = riesgos.GroupBy(r => r.Codigo).ToDictionary(g => g.Key, g => g.First().Id);
        var codigos = idPorCodigo.Keys.ToList();
        var filas = (from s in _db.SeguimientosRiesgo.AsNoTracking()
                     join r in _db.Riesgos.AsNoTracking() on s.RiesgoId equals r.Id
                     join v in _db.VersionesMatrizRiesgos.AsNoTracking() on r.VersionMatrizRiesgosId equals v.Id
                     where codigos.Contains(r.Codigo)
                     select new { S = s, r.Codigo }).ToList();
        foreach (var f in filas) f.S.RiesgoId = idPorCodigo[f.Codigo];
        return filas.Select(f => f.S).ToList();
    }

    // Hallazgos que corresponden a una torre: los registrados sobre ella, sobre
    // cualquiera de sus patas o elementos, sobre una localización del catálogo
    // amarrada a ella o sobre el tiro al que pertenece (un hallazgo puede tener
    // varios lugares: LugarHallazgo).
    //
    // Usa su propio DbContext: la hoja de vida lo llama en segundo plano
    // mientras la página sigue consultando el suyo, y dos operaciones a la vez
    // sobre el mismo DbContext tumban el circuito ("A second operation…").
    public async Task<List<HallazgoTorre>> HallazgosDeTorreAsync(int elementoTrazadoId)
    {
        using var scope = _scopes.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var torre = await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(t => t.Id == elementoTrazadoId);
        if (torre is null) return new();
        var comps = await _db.Componentes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.ElementoTrazadoId == elementoTrazadoId).ToDictionaryAsync(c => c.Id, c => c.Nombre);
        var idsComp = comps.Keys.ToList();
        var locs = await _db.Catalogos.AsNoTracking()
            .Where(i => i.ElementoTrazadoId == elementoTrazadoId).ToDictionaryAsync(i => i.Id, i => i.Nombre);
        var idsLoc = locs.Keys.ToList();
        var tiroId = torre.TiroId;
        var tiroNombre = tiroId is int t0 ? (await _db.Tiros.AsNoTracking().FirstOrDefaultAsync(t => t.Id == t0))?.Nombre : null;

        var lugares = await _db.LugaresHallazgo.AsNoTracking()
            .Where(l => l.ElementoTrazadoId == elementoTrazadoId
                     || (l.ComponenteId != null && idsComp.Contains(l.ComponenteId.Value))
                     || (l.ItemCatalogoId != null && idsLoc.Contains(l.ItemCatalogoId.Value))
                     || (tiroId != null && l.TiroId == tiroId))
            .ToListAsync();
        var idsH = lugares.Select(l => l.HallazgoRiesgoId).Distinct().ToList();

        var q = from h in _db.HallazgosRiesgo.AsNoTracking()
                join s in _db.SeguimientosRiesgo.AsNoTracking() on h.SeguimientoRiesgoId equals s.Id
                join r in _db.Riesgos.AsNoTracking() on s.RiesgoId equals r.Id
                join v in _db.VersionesMatrizRiesgos.AsNoTracking() on r.VersionMatrizRiesgosId equals v.Id
                where idsH.Contains(h.Id)
                orderby s.Fecha descending, h.Id descending
                select new HallazgoTorre
                {
                    Hallazgo = h,
                    SeguimientoId = s.Id,
                    FechaSeguimiento = s.Fecha,
                    ResponsableSeguimiento = s.Responsable,
                    ObservacionSeguimiento = s.Observacion,
                    Resultado = s.Resultado,
                    RiesgoId = r.Id,
                    CodigoRiesgo = r.Codigo,
                    DescripcionRiesgo = r.Descripcion,
                    TipoRiesgo = r.Tipo,
                    Probabilidad = r.Probabilidad,
                    Impacto = r.Impacto,
                    VersionCodigo = v.Codigo,
                    VersionFecha = v.Fecha
                };
        var lista = await q.ToListAsync();

        var idsSeg = lista.Select(x => x.SeguimientoId).Distinct().ToList();
        var soportes = await _db.ArchivosRiesgo.AsNoTracking()
            .Where(a => a.SeguimientoRiesgoId != null && idsSeg.Contains(a.SeguimientoRiesgoId.Value)).ToListAsync();
        var planes = await _db.PlanesAccion.AsNoTracking()
            .Where(p => p.SeguimientoRiesgoId != null && idsSeg.Contains(p.SeguimientoRiesgoId.Value)).ToListAsync();
        var idsPlan = planes.Select(p => p.Id).ToList();
        var acciones = await _db.AccionesPlan.AsNoTracking().Where(a => idsPlan.Contains(a.PlanAccionId)).ToListAsync();

        string Texto(LugarHallazgo l) =>
            l.ElementoTrazadoId is not null ? "Torre"
            : l.ComponenteId is int c ? (comps.TryGetValue(c, out var n) ? n : "Elemento")
            : l.ItemCatalogoId is int i ? (locs.TryGetValue(i, out var nl) ? nl : "Localización")
            : $"Tiro {tiroNombre}";

        foreach (var x in lista)
        {
            x.DondeTexto = string.Join(", ", lugares.Where(l => l.HallazgoRiesgoId == x.Hallazgo.Id).Select(Texto).Distinct());
            x.Archivos = soportes.Where(a => a.SeguimientoRiesgoId == x.SeguimientoId).ToList();
            x.Planes = planes.Where(p => p.SeguimientoRiesgoId == x.SeguimientoId)
                .Select(p => (p, EscalaAccion.EstadoPlan(acciones.Where(a => a.PlanAccionId == p.Id)))).ToList();
        }

        // Riesgo materializado por medio de un plan de acción: un plan que nace de un
        // seguimiento a un riesgo, o asociado a riesgos (RiesgoPlan / PlanAccion.RiesgoId), y ubicado en
        // esta torre, sus elementos, sus localizaciones o su tiro (LugarPlan) = ese riesgo se materializó
        // aquí, aunque el seguimiento no tenga hallazgo con ese lugar. Se deduce al leer (sin escribir
        // nada), así cubre también los planes ya guardados. PorPlan = true; Hallazgo es sintético (Id < 0).
        var lugaresPlan = await _db.LugaresPlan.AsNoTracking()
            .Where(l => l.ElementoTrazadoId == elementoTrazadoId
                     || (l.ComponenteId != null && idsComp.Contains(l.ComponenteId.Value))
                     || (l.ItemCatalogoId != null && idsLoc.Contains(l.ItemCatalogoId.Value))
                     || (tiroId != null && l.TiroId == tiroId))
            .ToListAsync();
        if (lugaresPlan.Count > 0)
        {
            var idsPl = lugaresPlan.Select(l => l.PlanAccionId).Distinct().ToList();
            var planesLugar = await _db.PlanesAccion.AsNoTracking().Where(p => idsPl.Contains(p.Id)).ToListAsync();
            var riesgosPlan = await _db.RiesgosPlan.AsNoTracking().Where(rp => idsPl.Contains(rp.PlanAccionId)).ToListAsync();
            var idsSegPl = planesLugar.Where(p => p.SeguimientoRiesgoId != null).Select(p => p.SeguimientoRiesgoId!.Value).Distinct().ToList();
            var segs = await _db.SeguimientosRiesgo.AsNoTracking().Where(s => idsSegPl.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            List<int> RiesgosDe(PlanAccion p) => new[] { p.SeguimientoRiesgoId is int si && segs.TryGetValue(si, out var sg) ? sg.RiesgoId : (int?)null, p.RiesgoId }
                .Where(r => r is not null).Select(r => r!.Value)
                .Concat(riesgosPlan.Where(rp => rp.PlanAccionId == p.Id).OrderBy(rp => rp.Orden).Select(rp => rp.RiesgoId))
                .Distinct().ToList();
            var idsR = planesLugar.SelectMany(RiesgosDe).Distinct().ToList();
            var riesgos = await _db.Riesgos.AsNoTracking().Where(r => idsR.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
            var idsV = riesgos.Values.Select(r => r.VersionMatrizRiesgosId).Distinct().ToList();
            var versiones = await _db.VersionesMatrizRiesgos.AsNoTracking().Where(v => idsV.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
            var accionesPl = await _db.AccionesPlan.AsNoTracking().Where(a => idsPl.Contains(a.PlanAccionId) && !a.Eliminado).ToListAsync();
            var soportesPl = await _db.ArchivosRiesgo.AsNoTracking()
                .Where(a => a.SeguimientoRiesgoId != null && idsSegPl.Contains(a.SeguimientoRiesgoId.Value)).ToListAsync();

            string TextoPlan(LugarPlan l) =>
                l.ElementoTrazadoId is not null ? "Torre"
                : l.ComponenteId is int c ? (comps.TryGetValue(c, out var n) ? n : "Elemento")
                : l.ItemCatalogoId is int i ? (locs.TryGetValue(i, out var nl) ? nl : "Localización")
                : $"Tiro {tiroNombre}";

            foreach (var p in planesLugar.OrderByDescending(p => p.Fecha))
            {
                var seg = p.SeguimientoRiesgoId is int si && segs.TryGetValue(si, out var sg) ? sg : null;
                var estado = EscalaAccion.EstadoPlan(accionesPl.Where(a => a.PlanAccionId == p.Id));
                var k = 0;
                foreach (var rid in RiesgosDe(p))
                {
                    k++;
                    if (!riesgos.TryGetValue(rid, out var r) || !versiones.TryGetValue(r.VersionMatrizRiesgosId, out var v)) continue;
                    // Ya está como hallazgo del mismo seguimiento y riesgo (por código) en esta torre: no repetir.
                    if (lista.Any(x => !x.PorPlan && x.CodigoRiesgo == r.Codigo && (seg is null || x.SeguimientoId == seg.Id))) continue;
                    if (lista.Any(x => x.PorPlan && x.CodigoRiesgo == r.Codigo && x.Planes.Any(pp => pp.Plan.Id == p.Id))) continue;
                    lista.Add(new HallazgoTorre
                    {
                        Hallazgo = new HallazgoRiesgo
                        {
                            Id = -(p.Id * 100 + k),
                            SeguimientoRiesgoId = seg?.Id ?? 0,
                            Descripcion = p.DescripcionHallazgo,
                            Estado = estado switch { EstadoAccionPlan.Cerrada => EstadoHallazgo.Cerrado, EstadoAccionPlan.EnEjecucion => EstadoHallazgo.EnTratamiento, _ => EstadoHallazgo.Abierto },
                        },
                        PorPlan = true,
                        SeguimientoId = seg?.Id ?? 0,
                        FechaSeguimiento = seg?.Fecha ?? p.Fecha,
                        ResponsableSeguimiento = seg?.Responsable ?? "",
                        ObservacionSeguimiento = seg?.Observacion ?? "",
                        Resultado = seg?.Resultado ?? ResultadoSeguimiento.Materializado,
                        RiesgoId = r.Id,
                        CodigoRiesgo = r.Codigo,
                        DescripcionRiesgo = r.Descripcion,
                        TipoRiesgo = r.Tipo,
                        Probabilidad = r.Probabilidad,
                        Impacto = r.Impacto,
                        VersionCodigo = v.Codigo,
                        VersionFecha = v.Fecha,
                        DondeTexto = string.Join(", ", lugaresPlan.Where(l => l.PlanAccionId == p.Id).Select(TextoPlan).Distinct()),
                        Archivos = seg is null ? new() : soportesPl.Where(a => a.SeguimientoRiesgoId == seg.Id).ToList(),
                        Planes = new() { (p, estado) },
                    });
                }
            }
            lista = lista.OrderByDescending(x => x.FechaSeguimiento).ThenByDescending(x => x.Hallazgo.Id).ToList();
        }
        return lista;
    }

    public class HallazgoTorre
    {
        public HallazgoRiesgo Hallazgo { get; set; } = default!;
        // true = no hay hallazgo con este lugar: el riesgo se ubica aquí por un plan de acción ligado a él (ver arriba).
        public bool PorPlan { get; set; }
        public int SeguimientoId { get; set; }
        public ResultadoSeguimiento Resultado { get; set; }
        public ProbabilidadRiesgo Probabilidad { get; set; }
        public ImpactoRiesgo Impacto { get; set; }
        public List<(PlanAccion Plan, EstadoAccionPlan Estado)> Planes { get; set; } = new();
        public DateTime FechaSeguimiento { get; set; }
        public string ResponsableSeguimiento { get; set; } = "";
        public string ObservacionSeguimiento { get; set; } = "";
        public int RiesgoId { get; set; }
        public string CodigoRiesgo { get; set; } = "";
        public string DescripcionRiesgo { get; set; } = "";
        public TipoRiesgo TipoRiesgo { get; set; }
        public string VersionCodigo { get; set; } = "";
        public DateTime VersionFecha { get; set; }
        public string DondeTexto { get; set; } = "";
        public List<ArchivoRiesgo> Archivos { get; set; } = new();
    }
}
