using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Costos;

// Todo el módulo Costos y presupuestos en memoria: se carga una vez (CargarAsync) y las vistas
// calculan contra una fecha de corte. Mismas reglas del libro "INFORME DIARIO"; las diferencias con el Excel son correcciones documentadas.
public sealed partial class InstantaneaCostos
{
    public ConfiguracionCostos Config { get; private set; } = new();
    public List<ItemPago> Items { get; private set; } = new();
    public Dictionary<int, ItemPago> ItemPorId { get; private set; } = new();
    public List<AjustePrecio> Ajustes { get; private set; } = new();
    public List<ComponenteAjustePrecio> Componentes { get; private set; } = new();
    public List<PrecioManual> PreciosManuales { get; private set; } = new();
    public List<ModificacionContrato> Modificaciones { get; private set; } = new();
    public List<LineaModificacionContrato> LineasModificacion { get; private set; } = new();
    public List<CantidadTorre> CantidadesTorre { get; private set; } = new();
    public List<RegistroCantidad> Registros { get; private set; } = new();
    public List<ActaFacturacion> Actas { get; private set; } = new();
    public List<LineaActaFacturacion> LineasActa { get; private set; } = new();
    public List<CodigoPresupuesto> Codigos { get; private set; } = new();
    public List<ValorPresupuesto> ValoresPresupuesto { get; private set; } = new();
    public List<PorcentajeIndirecto> Indirectos { get; private set; } = new();
    public List<CostoOrden> Costos { get; private set; } = new();
    public List<Persona> Personas { get; private set; } = new();
    public List<MetaPeriodo> Metas { get; private set; } = new();
    public List<MetaProduccionDia> MetasDia { get; private set; } = new();
    public List<AsignacionCostoActividad> AsignacionesManual { get; private set; } = new();
    public List<ProgramaTrabajo> Programas { get; private set; } = new();
    public List<ActividadPrograma> ActividadesPrograma { get; private set; } = new();
    public List<CambioPrograma> CambiosPrograma { get; private set; } = new();
    public List<EstadoTorreManual> EstadosManuales { get; private set; } = new();
    public List<ItemCatalogo> EstadosCatalogo { get; private set; } = new();   // catálogo "Estado de torre" (orden de creación)
    public List<ElementoTrazado> Torres { get; private set; } = new();
    public Dictionary<int, ElementoTrazado> TorrePorId { get; private set; } = new();
    public List<Tiro> Tiros { get; private set; } = new();
    public List<ActividadObra> ActividadesObra { get; private set; } = new();
    public List<string> Cuadrillas { get; private set; } = new();

    // Caché de precios (ítem, año) → precio sin AIU.
    private readonly Dictionary<(int, int), decimal?> _precio = new();

    public static async Task<InstantaneaCostos> CargarAsync(AppDbContext db)
    {
        var s = new InstantaneaCostos();
        s.Config = await db.ConfiguracionCostos.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync() ?? new ConfiguracionCostos();
        s.PrecioActual = s.Config.ValorizarPrecioActual;
        s.Items = await db.ItemsPago.AsNoTracking().OrderBy(i => i.Orden).ThenBy(i => i.Id).ToListAsync();
        s.ItemPorId = s.Items.ToDictionary(i => i.Id);
        s.Ajustes = await db.AjustesPrecio.AsNoTracking().OrderBy(a => a.Anio).ToListAsync();
        s.Componentes = await db.ComponentesAjustePrecio.AsNoTracking().ToListAsync();
        s.PreciosManuales = await db.PreciosManuales.AsNoTracking().ToListAsync();
        s.Modificaciones = await db.ModificacionesContrato.AsNoTracking().OrderByDescending(m => m.Fecha).ToListAsync();
        s.LineasModificacion = await db.LineasModificacionContrato.AsNoTracking().ToListAsync();
        s.CantidadesTorre = await db.CantidadesTorre.AsNoTracking().ToListAsync();
        s.Registros = await db.RegistrosCantidad.AsNoTracking().OrderBy(r => r.Fecha).ToListAsync();
        s.Actas = await db.ActasFacturacion.AsNoTracking().OrderBy(a => a.Fecha).ToListAsync();
        s.LineasActa = await db.LineasActaFacturacion.AsNoTracking().ToListAsync();
        s.Codigos = await db.CodigosPresupuesto.AsNoTracking().OrderBy(c => c.Grupo).ThenBy(c => c.Orden).ToListAsync();
        s.ValoresPresupuesto = await db.ValoresPresupuesto.AsNoTracking().OrderBy(v => v.Fecha).ThenBy(v => v.Id).ToListAsync();
        s.Indirectos = await db.PorcentajesIndirectos.AsNoTracking().OrderBy(p => p.Desde).ToListAsync();
        s.Costos = await db.CostosOrden.AsNoTracking().ToListAsync();
        s.Personas = await db.Personas.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();
        s.Metas = await db.MetasPeriodo.AsNoTracking().OrderBy(m => m.Desde).ToListAsync();
        s.MetasDia = await db.MetasProduccionDia.AsNoTracking().ToListAsync();
        s.AsignacionesManual = await db.AsignacionesCostoActividad.AsNoTracking().ToListAsync();
        s.Programas = await db.ProgramasTrabajo.AsNoTracking().OrderBy(p => p.Version).ToListAsync();
        s.ActividadesPrograma = await db.ActividadesPrograma.AsNoTracking().ToListAsync();
        s.CambiosPrograma = await db.CambiosPrograma.AsNoTracking().OrderByDescending(c => c.Fecha).ToListAsync();
        s.EstadosManuales = await db.EstadosTorreManual.AsNoTracking().ToListAsync();
        s.EstadosCatalogo = EstadoTorres.Ordenar(await db.Catalogos.AsNoTracking().Where(c => c.Tipo == EstadoTorres.Catalogo && c.Activo).ToListAsync(), s.Config.OrdenEstadosTorre);
        s.Torres = (await db.ElementosTrazado.AsNoTracking().ToListAsync())
            .OrderBy(t => NumeroTorre(t.Nombre ?? t.Codigo)).ThenBy(t => t.Nombre ?? t.Codigo, StringComparer.OrdinalIgnoreCase).ToList();
        s.TorrePorId = s.Torres.ToDictionary(t => t.Id);
        s.Tiros = await db.Tiros.AsNoTracking().OrderBy(t => t.Orden).ThenBy(t => t.Nombre).ToListAsync();
        s.ActividadesObra = await db.ActividadesObra.AsNoTracking().OrderBy(a => a.Orden).ToListAsync();
        var cat = await db.Catalogos.AsNoTracking().Where(c => c.Tipo == CatalogoCuadrilla).Select(c => c.Nombre).ToListAsync();
        s.Cuadrillas = cat
            .Concat(s.Registros.Select(r => r.Cuadrilla)).Concat(s.Costos.Select(c => c.Cuadrilla)).Concat(s.Personas.Select(p => p.Cuadrilla))
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => Norm(c!)).Distinct().OrderBy(c => c).ToList();
        return s;
    }

    // Orden natural de torres: el primer número del nombre (T19 antes que T100).
    private static int NumeroTorre(string? n)
    {
        var d = new string((n ?? "").SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(d, out var v) ? v : int.MaxValue;
    }

    public const string CatalogoCuadrilla = "Cuadrilla";

    // Cuadrilla sin espacios sobrantes y en mayúsculas ("MONTAJE " = "MONTAJE").
    public static string Norm(string? c) => (c ?? "").Trim().ToUpperInvariant();

    public string NombreTorre(int? id) => id is int i && TorrePorId.TryGetValue(i, out var t) ? (string.IsNullOrWhiteSpace(t.Nombre) ? t.Codigo : t.Nombre!) : "";

    // ---------- Precios ----------
    public decimal FactorAjuste(int anio)
    {
        var a = Ajustes.FirstOrDefault(x => x.Anio == anio);
        if (a is null) return 1m;
        var comps = Componentes.Where(c => c.AjustePrecioId == a.Id).ToList();
        if (comps.Count == 0) return 1m;
        return comps.Sum(c => c.Peso * (1m + c.Indice));
    }

    public int UltimoAnioPrecio => Math.Max(Ajustes.Count > 0 ? Ajustes.Max(a => a.Anio) : 0,
        PreciosManuales.Count > 0 ? PreciosManuales.Max(p => p.Anio) : 0);

    // Precio sin AIU del ítem en un año: manual de ese año, o el del año anterior × ajuste.
    public decimal? Precio(ItemPago it, int anio)
    {
        if (_precio.TryGetValue((it.Id, anio), out var c)) return c;
        decimal? r;
        var man = PreciosManuales.FirstOrDefault(p => p.ItemPagoId == it.Id && p.Anio == anio);
        if (man is not null) r = man.Precio;
        else if (anio <= it.AnioBase) r = it.PrecioBase ?? PreciosManuales.Where(p => p.ItemPagoId == it.Id && p.Anio < anio).OrderByDescending(p => p.Anio).Select(p => (decimal?)p.Precio).FirstOrDefault();
        else
        {
            var previo = Precio(it, anio - 1);
            r = previo is null ? null : previo * FactorAjuste(anio);
        }
        _precio[(it.Id, anio)] = r;
        return r;
    }

    public decimal PrecioConAiu(ItemPago it, int anio) => (Precio(it, anio) ?? 0m) * (1m + it.AiuPct);

    // Valorizar todo al precio del año de corte (como el Excel) o al del año de ejecución.
    // Arranca con el valor de Ajustes; la página lo cambia con su selector.
    public bool PrecioActual { get; set; }

    // Año con el que se valoriza lo ejecutado en una fecha.
    public int AnioValor(DateTime fecha, DateTime corte)
    {
        var anio = PrecioActual ? corte.Year : fecha.Year;
        var tope = UltimoAnioPrecio;
        return tope > 0 ? Math.Min(anio, Math.Max(tope, Items.Count > 0 ? Items.Min(i => i.AnioBase) : anio)) : anio;
    }

    public decimal Valor(RegistroCantidad r, DateTime corte) =>
        ItemPorId.TryGetValue(r.ItemPagoId, out var it) ? r.Cantidad * PrecioConAiu(it, AnioValor(r.Fecha, corte)) : 0m;

    // Cantidad contratada vigente: la contratada original modificada por la última línea de modificación.
    public decimal CantidadVigente(ItemPago it)
    {
        var mods = LineasModificacion.Where(l => l.ItemPagoId == it.Id && l.CantidadDespues is not null)
            .Join(Modificaciones, l => l.ModificacionContratoId, m => m.Id, (l, m) => new { l, m })
            .OrderByDescending(x => x.m.Fecha).ThenByDescending(x => x.l.Id).FirstOrDefault();
        return mods?.l.CantidadDespues ?? it.CantidadContratada;
    }

    public decimal ValorContrato(DateTime corte) => Config.ValorContrato ?? ValorFormulario(corte);

    // Σ cantidad vigente × precio con AIU de todos los ítems del Formulario de precios.
    public decimal ValorFormulario(DateTime corte) =>
        Items.Where(i => !i.EsCapitulo).Sum(i => CantidadVigente(i) * PrecioConAiu(i, AnioValor(corte, corte)));

    // ---------- Producción ----------
    public IEnumerable<RegistroCantidad> RegistrosHasta(DateTime corte) => Registros.Where(r => r.Fecha.Date <= corte.Date);

    public decimal ProduccionDia(DateTime dia, DateTime corte, string? cuadrilla = null) =>
        Registros.Where(r => r.Fecha.Date == dia.Date && (cuadrilla is null || Norm(r.Cuadrilla) == cuadrilla)).Sum(r => Valor(r, corte));

    public decimal ProduccionAcumulada(DateTime corte) => RegistrosHasta(corte).Sum(r => Valor(r, corte));

    public decimal CantidadEjecutada(int itemId, DateTime corte) => RegistrosHasta(corte).Where(r => r.ItemPagoId == itemId).Sum(r => r.Cantidad);

    public decimal Facturado(DateTime corte)
    {
        var ids = Actas.Where(a => a.Fecha.Date <= corte.Date).Select(a => a.Id).ToHashSet();
        return LineasActa.Where(l => ids.Contains(l.ActaFacturacionId)).Sum(l => l.Valor);
    }

    public decimal CantidadFacturada(int itemId, DateTime corte)
    {
        var ids = Actas.Where(a => a.Fecha.Date <= corte.Date).Select(a => a.Id).ToHashSet();
        return LineasActa.Where(l => l.ItemPagoId == itemId && ids.Contains(l.ActaFacturacionId)).Sum(l => l.Cantidad);
    }

    // ---------- Indirectos ----------
    public decimal Porcentaje(ConceptoIndirecto c, DateTime dia) =>
        Indirectos.Where(p => p.Concepto == c && p.Desde.Date <= dia.Date).OrderByDescending(p => p.Desde).Select(p => p.Porcentaje).FirstOrDefault();

    // Overhead, sostenimiento e impuestos de una lista de registros: % del día × valor, solo ítems marcados.
    public (decimal oh, decimal sos, decimal tax) IndirectosDe(IEnumerable<RegistroCantidad> regs, DateTime corte)
    {
        decimal oh = 0, sos = 0, tax = 0;
        foreach (var r in regs)
        {
            if (!ItemPorId.TryGetValue(r.ItemPagoId, out var it)) continue;
            var v = Valor(r, corte);
            if (it.AplicaOverhead) oh += v * Porcentaje(ConceptoIndirecto.Overhead, r.Fecha);
            if (it.AplicaSostenimiento) sos += v * Porcentaje(ConceptoIndirecto.Sostenimiento, r.Fecha);
            if (it.AplicaImpuestos) tax += v * Porcentaje(ConceptoIndirecto.ImpuestosFinanciacion, r.Fecha);
        }
        return (oh, sos, tax);
    }

    // ---------- Costos ----------
    public static int Dias(DateTime desde, DateTime hasta) => Math.Max(1, (hasta.Date - desde.Date).Days + 1);

    public static decimal ValorDia(CostoOrden c) => c.Desde is DateTime d && c.Hasta is DateTime h ? c.Valor / Dias(d, h) : 0m;

    public decimal ValorDia(Persona p)
    {
        var k = Config;
        var adicional = k.DivisorAdicional == 0 ? 0 : k.BaseAdicional / k.DivisorAdicional / 30m;
        return p.Salario / 30m * k.FactorPrestacional * (1m + k.RecargoA * k.RecargoB) + p.Auxilio / 30m + adicional;
    }

    public static bool Activa(Persona p, DateTime dia) =>
        p.Ingreso is DateTime i && i.Date <= dia.Date && (p.Retiro is null || p.Retiro.Value.Date >= dia.Date);

    public static bool Vigente(CostoOrden c, DateTime dia) => c.Desde is DateTime d && c.Hasta is DateTime h && d.Date <= dia.Date && h.Date >= dia.Date;

    // Cargado a la fecha: días transcurridos (desde → min(corte, hasta)) × valor día.
    public static decimal Cargado(CostoOrden c, DateTime corte) =>
        c.Desde is not DateTime d || c.Hasta is not DateTime h || corte.Date < d.Date ? 0m : ValorDia(c) * Dias(d, corte.Date < h.Date ? corte : h);

    public decimal Cargado(Persona p, DateTime corte)
    {
        if (p.Ingreso is not DateTime i || corte.Date < i.Date) return 0m;
        var fin = p.Retiro is DateTime r && r.Date < corte.Date ? r : corte;
        return fin.Date < i.Date ? 0m : ValorDia(p) * Dias(i, fin);
    }

    // Costo de un día (opcional: solo una cuadrilla / un código).
    public decimal CostoDia(DateTime dia, string? cuadrilla = null, int? codigoId = null) =>
        Costos.Where(c => Vigente(c, dia) && (cuadrilla is null || Norm(c.Cuadrilla) == cuadrilla) && (codigoId is null || c.CodigoPresupuestoId == codigoId)).Sum(ValorDia)
        + Personas.Where(p => Activa(p, dia) && (cuadrilla is null || Norm(p.Cuadrilla) == cuadrilla) && (codigoId is null || p.CodigoPresupuestoId == codigoId)).Sum(ValorDia);

    // PF1 del día = todo el personal activo + órdenes marcadas PF1 vigentes.
    public decimal Pf1Dia(DateTime dia) =>
        Personas.Where(p => Activa(p, dia)).Sum(ValorDia) + Costos.Where(c => c.Pf1 && Vigente(c, dia)).Sum(ValorDia);

    public decimal PresupuestoActual(CodigoPresupuesto c, DateTime corte) =>
        ValoresPresupuesto.Where(v => v.CodigoPresupuestoId == c.Id && v.Fecha.Date <= corte.Date).OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).Select(v => (decimal?)v.Valor).FirstOrDefault()
        ?? ValoresPresupuesto.Where(v => v.CodigoPresupuestoId == c.Id).OrderBy(v => v.Fecha).Select(v => (decimal?)v.Valor).FirstOrDefault()
        ?? c.PresupuestoInicial;

    // Disponible = actual − lo mayor entre comprometido y causado (lo causado sin orden también consume presupuesto).
    // Ev = valor ganado del código (actual × avance físico); Eac = pronóstico al cierre por rendimiento (actual / CPI).
    public sealed record FilaPresupuesto(CodigoPresupuesto? Codigo, string Nombre, GrupoPresupuesto? Grupo, decimal Inicial, decimal Actual,
        decimal Acumulado, decimal Comprometido, decimal Disponible, decimal CostoDia, bool EsTotal, decimal Ev = 0, decimal? Eac = null)
    {
        public decimal? Cpi => Acumulado > 0 ? Ev / Acumulado : null;
        public decimal? Vac => Eac is decimal e ? Actual - e : null;
    }

    public List<FilaPresupuesto> Presupuesto(DateTime corte)
    {
        var filas = new List<FilaPresupuesto>();
        var fis = AvanceFisico(corte);
        static decimal? Pronostico(decimal actual, decimal ev, decimal ac) => ac <= 0 ? null : ev <= 0 ? null : ac + (actual - ev) * ac / ev;
        foreach (var g in Enum.GetValues<GrupoPresupuesto>())
        {
            var cods = Codigos.Where(c => c.Grupo == g).ToList();
            if (cods.Count == 0) continue;
            var hijas = cods.Select(c =>
            {
                var acum = Costos.Where(x => x.CodigoPresupuestoId == c.Id).Sum(x => Cargado(x, corte)) + Personas.Where(p => p.CodigoPresupuestoId == c.Id).Sum(p => Cargado(p, corte));
                var comp = Comprometido(c, corte);
                var act = PresupuestoActual(c, corte);
                var ev = act * fis;
                return new FilaPresupuesto(c, c.Nombre, g, c.PresupuestoInicial, act, acum, comp, act - Math.Max(comp, acum), CostoDia(corte, null, c.Id), false,
                    ev, Pronostico(act, ev, acum));
            }).ToList();
            decimal tAct = hijas.Sum(h => h.Actual), tAc = hijas.Sum(h => h.Acumulado), tEv = hijas.Sum(h => h.Ev);
            filas.Add(new FilaPresupuesto(null, NombreGrupo(g), g, hijas.Sum(h => h.Inicial), tAct, tAc,
                hijas.Sum(h => h.Comprometido), hijas.Sum(h => h.Disponible), hijas.Sum(h => h.CostoDia), true, tEv, Pronostico(tAct, tEv, tAc)));
            filas.AddRange(hijas);
        }
        return filas;
    }

    public static string NombreGrupo(GrupoPresupuesto g) => g switch
    {
        GrupoPresupuesto.General => "General",
        GrupoPresupuesto.Materiales => "Materiales",
        GrupoPresupuesto.ManoDeObra => "Mano de obra",
        GrupoPresupuesto.EquipoYHerramientas => "Equipo y herramientas",
        GrupoPresupuesto.Otros => "Otros",
        _ => "Costos indirectos"
    };

    // Costo acumulado (órdenes + personal) al corte, sin indirectos sobre producción.
    public decimal CostoAcumulado(DateTime corte) =>
        Costos.Sum(c => Cargado(c, corte)) + Personas.Sum(p => Cargado(p, corte));

    // ---------- Resumen (Informe / Presupuesto) ----------
    public sealed record Resumen(decimal Ingreso, decimal CostoDirecto, decimal Overhead, decimal Sostenimiento, decimal Impuestos)
    {
        public decimal TotalCosto => CostoDirecto + Overhead + Sostenimiento;
        public decimal Ebitda => Ingreso - TotalCosto;
        public decimal TotalEgresos => TotalCosto + Impuestos;
        public decimal Utilidad => Ingreso - TotalEgresos;
    }

    public Resumen ResumenDia(DateTime dia, DateTime corte)
    {
        var regs = Registros.Where(r => r.Fecha.Date == dia.Date).ToList();
        var (oh, sos, tax) = IndirectosDe(regs, corte);
        return new Resumen(regs.Sum(r => Valor(r, corte)), CostoDia(dia), oh, sos, tax);
    }

    public Resumen ResumenAcumulado(DateTime corte)
    {
        var regs = RegistrosHasta(corte).ToList();
        var (oh, sos, tax) = IndirectosDe(regs, corte);
        return new Resumen(regs.Sum(r => Valor(r, corte)), CostoAcumulado(corte), oh, sos, tax);
    }

    // ---------- Detalle de torres ----------
    public sealed record CeldaDetalle(int TorreId, int ItemId, decimal Prevista, decimal Ejecutada)
    {
        public decimal? Pct => Prevista > 0 ? Ejecutada / Prevista : null;
    }

    public List<CeldaDetalle> Detalle(DateTime corte, Func<ItemPago, bool>? filtro = null)
    {
        var ejec = RegistrosHasta(corte).Where(r => r.ElementoTrazadoId is not null)
            .GroupBy(r => (r.ElementoTrazadoId!.Value, r.ItemPagoId)).ToDictionary(g => g.Key, g => g.Sum(r => r.Cantidad));
        var prev = CantidadesTorre.GroupBy(c => (c.ElementoTrazadoId, c.ItemPagoId)).ToDictionary(g => g.Key, g => g.Sum(c => c.CantidadPrevista));
        var claves = ejec.Keys.Union(prev.Keys)
            .Where(k => ItemPorId.TryGetValue(k.Item2, out var it) && (filtro is null || filtro(it)));
        return claves.Select(k => new CeldaDetalle(k.Item1, k.Item2, prev.GetValueOrDefault(k), ejec.GetValueOrDefault(k))).ToList();
    }

    // Σ % de una torre: ponderado por el valor previsto de cada ítem.
    public decimal? PctTorre(IEnumerable<CeldaDetalle> celdas, DateTime corte)
    {
        decimal num = 0, den = 0;
        foreach (var c in celdas)
        {
            if (c.Prevista <= 0 || !ItemPorId.TryGetValue(c.ItemId, out var it)) continue;
            var p = PrecioConAiu(it, AnioValor(corte, corte));
            var peso = p > 0 ? c.Prevista * p : c.Prevista;
            num += peso * Math.Min(1m, c.Ejecutada / c.Prevista);
            den += peso;
        }
        return den > 0 ? num / den : null;
    }

    public static bool EsMontaje(ItemPago it) => it.Codigo.StartsWith("6.", StringComparison.Ordinal);
}
