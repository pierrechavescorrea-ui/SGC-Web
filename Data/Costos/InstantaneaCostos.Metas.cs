using SigWeb.Models;

namespace SigWeb.Data.Costos;

// Metas, seguimiento de meta y control por tiro (fase 2). Todo en memoria sobre la instantánea.
public sealed partial class InstantaneaCostos
{
    // ---------- Metas ----------
    private ILookup<(int, DateTime), MetaProduccionDia>? _metaPorDia;
    private HashSet<int>? _metasConDetalle;

    // Versión vigente de la meta para un día: la de mayor versión entre las vigentes que lo cubren.
    public MetaPeriodo? MetaDe(DateTime dia) =>
        Metas.Where(m => m.Vigente && m.Desde.Date <= dia.Date && m.Hasta.Date >= dia.Date)
            .OrderByDescending(m => m.Version).ThenByDescending(m => m.Id).FirstOrDefault();

    // Periodos (Desde) con su versión vigente, del más reciente al más antiguo.
    public List<MetaPeriodo> PeriodosMeta() =>
        Metas.GroupBy(m => m.Desde.Date)
            .Select(g => g.Where(m => m.Vigente).OrderByDescending(m => m.Version).FirstOrDefault() ?? g.OrderByDescending(m => m.Version).First())
            .OrderByDescending(m => m.Desde).ToList();

    // Meta de producción ($) de un rango: Σ cantidad meta × precio con AIU, usando para cada día la
    // versión vigente que lo cubre. Si la versión no tiene detalle por ítem, reparte MetaProduccionTotal
    // por igual entre sus días (solo para el total, sin cuadrilla).
    public decimal MetaProduccion(DateTime desde, DateTime hasta, DateTime corte, string? cuadrilla = null)
    {
        decimal total = 0;
        for (var d = desde.Date; d <= hasta.Date; d = d.AddDays(1))
        {
            if (MetaDe(d) is not { } m) continue;
            _metaPorDia ??= MetasDia.ToLookup(x => (x.MetaPeriodoId, x.Fecha.Date));
            _metasConDetalle ??= MetasDia.Select(x => x.MetaPeriodoId).ToHashSet();
            if (!_metasConDetalle.Contains(m.Id))
            {
                if (cuadrilla is null && m.MetaProduccionTotal is decimal t) total += t / Dias(m.Desde, m.Hasta);
                continue;
            }
            foreach (var x in _metaPorDia[(m.Id, d)].Where(x => (cuadrilla is null || Norm(x.Cuadrilla) == cuadrilla)))
                if (ItemPorId.TryGetValue(x.ItemPagoId, out var it)) total += x.Cantidad * PrecioConAiu(it, AnioValor(d, corte));
        }
        return total;
    }

    public decimal FacturadoEntre(DateTime desde, DateTime hasta)
    {
        var ids = Actas.Where(a => a.Fecha.Date >= desde.Date && a.Fecha.Date <= hasta.Date).Select(a => a.Id).ToHashSet();
        return LineasActa.Where(l => ids.Contains(l.ActaFacturacionId)).Sum(l => l.Valor);
    }

    // Semanas del seguimiento: de InicioSemana1 en bloques de 7 días, recortadas al periodo.
    public static List<(DateTime Desde, DateTime Hasta)> Semanas(MetaPeriodo m)
    {
        var l = new List<(DateTime, DateTime)>();
        var ini = m.InicioSemana1 == default ? m.Desde.Date : m.InicioSemana1.Date;
        for (var d = ini; d <= m.Hasta.Date; d = d.AddDays(7))
        {
            var hasta = d.AddDays(6) < m.Hasta.Date ? d.AddDays(6) : m.Hasta.Date;
            if (hasta < m.Desde.Date) continue;
            l.Add((d < m.Desde.Date ? m.Desde.Date : d, hasta));      // como el Excel: la semana se corta al periodo
        }
        return l;
    }

    // ---------- Seguimiento de meta ----------
    public sealed record FilaSeguimiento(string Cuadrilla, decimal Meta, decimal Produccion, decimal CostoDirecto, decimal Administracion,
        decimal Indirectos, bool EsAdministracion, bool SinReparto)
    {
        public decimal Costo => CostoDirecto + Administracion + Indirectos;
        public decimal Margen => Produccion - Costo;
        public decimal? Cumplimiento => Meta > 0 ? Produccion / Meta : null;
    }

    public HashSet<string> CuadrillasSinReparto() =>
        (Config.CuadrillasSinRepartoAdmin ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Norm).ToHashSet();

    // Por cuadrilla, en el rango (sin pasar del corte): meta, producción, costo directo (órdenes y
    // personal de la cuadrilla), parte de administración y overhead + sostenimiento + impuestos de su producción
    // (solo ítems marcados). La administración de cada día se reparte entre las cuadrillas que no
    // están en "sin reparto", en proporción a su meta del rango (si no hay meta, a su producción;
    // si tampoco, por partes iguales).
    public List<FilaSeguimiento> Seguimiento(DateTime desde, DateTime hasta, DateTime corte)
    {
        var fin = hasta.Date < corte.Date ? hasta.Date : corte.Date;
        var admin = Norm(Config.CuadrillaAdministracion);
        var sinReparto = CuadrillasSinReparto();

        var regs = Registros.Where(r => r.Fecha.Date >= desde.Date && r.Fecha.Date <= fin).ToList();
        var nombres = regs.Select(r => Norm(r.Cuadrilla))
            .Concat(Costos.Select(c => Norm(c.Cuadrilla))).Concat(Personas.Select(p => Norm(p.Cuadrilla)))
            .Concat(MetasDia.Select(m => Norm(m.Cuadrilla))).Concat(Cuadrillas)
            .Distinct().ToList();

        var meta = nombres.ToDictionary(c => c, c => MetaProduccion(desde, hasta, corte, c));
        var prod = nombres.ToDictionary(c => c, c => regs.Where(r => Norm(r.Cuadrilla) == c).Sum(r => Valor(r, corte)));
        var ind = nombres.ToDictionary(c => c, c => { var (oh, sos, tax) = IndirectosDe(regs.Where(r => Norm(r.Cuadrilla) == c), corte); return oh + sos + tax; });

        var directo = nombres.ToDictionary(c => c, _ => 0m);
        for (var d = desde.Date; d <= fin; d = d.AddDays(1))
        {
            foreach (var c in Costos) if (Vigente(c, d)) directo[Norm(c.Cuadrilla)] += ValorDia(c);
            foreach (var p in Personas) if (Activa(p, d)) directo[Norm(p.Cuadrilla)] += ValorDia(p);
        }

        var receptoras = nombres.Where(c => c != admin && c != "" && !sinReparto.Contains(c)).ToList();
        var baseReparto = receptoras.ToDictionary(c => c, c => meta[c]);
        if (baseReparto.Values.Sum() <= 0) baseReparto = receptoras.ToDictionary(c => c, c => Math.Max(0, prod[c]));
        if (baseReparto.Values.Sum() <= 0) baseReparto = receptoras.ToDictionary(c => c, _ => 1m);
        var sumaBase = baseReparto.Values.Sum();
        var costoAdmin = directo.GetValueOrDefault(admin);

        return nombres
            .Select(c => new FilaSeguimiento(c == "" ? "(sin cuadrilla)" : c, meta[c], prod[c], directo[c],
                c == admin ? -costoAdmin : (sumaBase > 0 && baseReparto.TryGetValue(c, out var b) ? costoAdmin * b / sumaBase : 0m),
                ind[c], c == admin, sinReparto.Contains(c)))
            .Where(f => f.Meta != 0 || f.Produccion != 0 || f.CostoDirecto != 0)
            .OrderBy(f => f.EsAdministracion).ThenBy(f => f.Cuadrilla)
            .ToList();
    }

    // ---------- Control por tiro (C. CONTROL) ----------
    public sealed record EstadoTorre(int TorreId, string Estado, decimal? PctMontaje, decimal? PctObra, string Clase);

    // Lo que dice Avance de obra (informe diario F-054) de una torre al corte; fracciones 0–1.
    public sealed record ObraTorre(decimal? Civil, decimal? Montaje, bool CivilIniciada, bool MontajeIniciado);

    // Estado de la torre: manda la obra física. Se toma lo que digan las cantidades (registros de este módulo) o
    // Avance de obra (F-054), lo que vaya más adelante; un registro en 0 % del F-054 ya cuenta como iniciada.
    // MONTADA = montaje de la estructura al 100 %; el estado manual solo se usa si no hay montaje registrado.
    public EstadoTorre Estado(int torreId, DateTime corte, List<CeldaDetalle> celdas, ObraTorre? obra = null)
    {
        var mont = celdas.Where(c => c.TorreId == torreId && ItemPorId.TryGetValue(c.ItemId, out var it) && EsMontaje(it)).ToList();
        var civil = celdas.Where(c => c.TorreId == torreId && ItemPorId.TryGetValue(c.ItemId, out var it) && !EsMontaje(it)).ToList();
        var pm = Mayor(mont.Count > 0 ? PctTorre(mont, corte) : null, obra?.Montaje);
        var po = Mayor(civil.Count > 0 ? PctTorre(civil, corte) : null, obra?.Civil);
        var manual = EstadosManuales.Where(e => e.ElementoTrazadoId == torreId && e.Desde.Date <= corte.Date)
            .OrderByDescending(e => e.Desde).ThenByDescending(e => e.Id).FirstOrDefault();

        // Estado manual de la vista Estado de las torres (catálogo): cuenta en la etapa que dice su nombre.
        var etapa = string.IsNullOrWhiteSpace(manual?.Estado) ? (EstadoTorres.Etapa?)null : EstadoTorres.EtapaDe(manual!.Estado, EstadosCatalogo);
        if (pm >= 0.999m) return new(torreId, "MONTADA", pm, po, "montada");
        if (etapa is EstadoTorres.Etapa em && EstadoTorres.Montada(em)) return new(torreId, manual!.Estado, pm, po, "montada");
        if (mont.Any(c => c.Ejecutada > 0) || obra?.MontajeIniciado == true || pm > 0)
            return new(torreId, $"MONTAJE {FormatoCostos.Pct(pm ?? 0, 0)}", pm, po, "montaje");
        if (etapa is EstadoTorres.Etapa e1)
            return new(torreId, manual!.Estado, pm, po, e1 switch
            {
                EstadoTorres.Etapa.MontajeProceso => "montaje",
                EstadoTorres.Etapa.CivilProceso or EstadoTorres.Etapa.CivilTerminado or EstadoTorres.Etapa.CambioCimentacion => "obra",
                _ => "manual"
            });
        if (civil.Any(c => c.Ejecutada > 0) || obra?.CivilIniciada == true || po > 0)
            return new(torreId, $"OBRA CIVIL {FormatoCostos.Pct(po ?? 0, 0)}", pm, po, "obra");
        return new(torreId, "SIN INICIAR", pm, po, "sin");
    }

    private static decimal? Mayor(decimal? a, decimal? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
}
