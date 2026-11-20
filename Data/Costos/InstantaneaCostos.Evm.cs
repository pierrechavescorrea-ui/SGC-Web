using SigWeb.Models;

namespace SigWeb.Data.Costos;

// Valor ganado (EVM, PMI / AACE): mide el costo contra el avance FÍSICO, no contra el ingreso.
// - Avance físico = Σ cantidad ejecutada × precio del año base / Σ cantidad vigente × precio del año base
//   (precios constantes: el reajuste por IPC no infla el avance).
// - BAC = presupuesto de costo vigente (Σ "Actual" de los códigos de presupuesto).
// - EV (valor ganado) = BAC × avance físico; AC (costo real) = órdenes + personal cargados al corte.
// - PV (valor planeado) = BAC × avance programado del PDT vigente (Σ valor planeado × fracción programada).
// - CPI = EV / AC; SPI = EV / PV; EAC = AC + (BAC − EV) / CPI (el rendimiento de costo actual se mantiene);
//   ETC = EAC − AC; VAC = BAC − EAC; TCPI = (BAC − EV) / (BAC − AC).
// SUPUESTO: el avance físico del contrato representa el avance de todos los códigos de costo (incluidos los
// de administración, que dependen del tiempo); por grupo se usa el mismo avance físico.
public sealed partial class InstantaneaCostos
{
    public sealed record Evm(decimal Bac, decimal AvanceFisico, decimal? AvanceProgramado, decimal Ev, decimal? Pv, decimal Ac)
    {
        public decimal? Cpi => Ac > 0 ? Ev / Ac : null;
        public decimal? Spi => Pv is decimal p && p > 0 ? Ev / p : null;
        public decimal? Eac => Cpi is decimal c && c > 0 ? Ac + (Bac - Ev) / c : null;
        public decimal? Etc => Eac is decimal e ? e - Ac : null;
        public decimal? Vac => Eac is decimal e ? Bac - e : null;
        public decimal? Tcpi => Bac - Ac != 0 ? (Bac - Ev) / (Bac - Ac) : null;
        public decimal Cv => Ev - Ac;
        public decimal? Sv => Pv is decimal p ? Ev - p : null;
    }

    private int AnioBase(ItemPago it) => it.AnioBase > 0 ? it.AnioBase : (Items.Count > 0 ? Items.Min(i => i.AnioBase) : DateTime.Today.Year);

    // Avance físico ponderado a precios constantes (año base de cada ítem). Lo ejecutado de más no pasa del 100 % del ítem.
    public decimal AvanceFisico(DateTime corte, Func<ItemPago, bool>? filtro = null)
    {
        var ejec = RegistrosHasta(corte).GroupBy(r => r.ItemPagoId).ToDictionary(g => g.Key, g => g.Sum(r => r.Cantidad));
        decimal num = 0, den = 0;
        foreach (var it in Items.Where(i => !i.EsCapitulo && (filtro is null || filtro(i))))
        {
            var vig = CantidadVigente(it);
            var p = PrecioConAiu(it, AnioBase(it));
            if (vig <= 0 || p <= 0) continue;
            den += vig * p;
            num += Math.Min(vig, Math.Max(0, ejec.GetValueOrDefault(it.Id))) * p;
        }
        return den > 0 ? num / den : 0m;
    }

    // Avance programado (0–1) del PDT vigente a una fecha: Σ valor planeado × fracción programada / Σ valor planeado.
    public decimal? AvanceProgramado(DateTime fecha)
    {
        if (ProgramaVigente is not { } p) return null;
        var hojas = ActividadesPrograma.Where(a => a.ProgramaTrabajoId == p.Id && !a.EsResumen && a.ValorPlaneado > 0).ToList();
        var total = hojas.Sum(a => a.ValorPlaneado!.Value);
        return total > 0 ? hojas.Sum(a => a.ValorPlaneado!.Value * FraccionProgramada(a, fecha)) / total : null;
    }

    public decimal Bac(DateTime corte) => Codigos.Sum(c => PresupuestoActual(c, corte));

    public Evm ValorGanado(DateTime corte)
    {
        var bac = Bac(corte);
        var fis = AvanceFisico(corte);
        var prog = AvanceProgramado(corte);
        return new Evm(bac, fis, prog, bac * fis, prog is decimal x ? bac * x : null, CostoAcumulado(corte));
    }

    // El EVM solo es válido si el presupuesto de costo (BAC) cubre el mismo alcance que el contrato. Margen implícito =
    // 1 − BAC / valor del Formulario vigente; fuera de −10 %…50 % se avisa (umbral SUPUESTO, ajustable).
    public string? AvisoAlcance(DateTime corte)
    {
        var bac = Bac(corte); var formulario = ValorFormulario(corte);
        if (bac <= 0 || formulario <= 0) return bac <= 0 ? "Sin presupuesto de costo: el valor ganado no se puede calcular." : null;
        var margen = 1m - bac / formulario;
        return margen is < -0.10m or > 0.50m
            ? $"El presupuesto de costo ({FormatoCostos.Millones(bac)}) equivale al {FormatoCostos.Pct(bac / formulario, 0)} del Formulario de precios vigente ({FormatoCostos.Millones(formulario)}): si no cubren el mismo alcance, CPI y EAC no son comparables."
            : null;
    }

    // Comprometido de un código: órdenes (valor total) + personal (lo causado al corte; no tiene valor comprometido).
    public decimal Comprometido(CodigoPresupuesto c, DateTime corte) =>
        Costos.Where(x => x.CodigoPresupuestoId == c.Id).Sum(x => x.Valor) + Personas.Where(p => p.CodigoPresupuestoId == c.Id).Sum(p => Cargado(p, corte));
}
