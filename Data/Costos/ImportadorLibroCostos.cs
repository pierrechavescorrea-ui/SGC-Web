using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.AvanceObra;
using SigWeb.Data.Documentos;
using SigWeb.Models;

namespace SigWeb.Data.Costos;

// Importa el libro "INFORME DIARIO" (Formulario de precios, Presupuesto, Costos, Personal, Avance
// diario, DETALLE DE LAS TORRES, DETALLE MONTAJE, Meta produccion e Informe). BD, Instructivo,
// Seguimiento Meta y C. CONTROL no se importan (se calculan).
// Primero Analizar (lee y arma el plan, no escribe) y luego Aplicar (escribe).
// Reimportar: ítems, códigos y personas se actualizan por código/cédula; costos y registros que
// vinieron del libro van a la Papelera y entran los nuevos; cantidades previstas se reemplazan.
public sealed class PlanImportacionCostos
{
    public List<string> Hojas { get; } = new();
    public List<string> Avisos { get; } = new();
    public ConfiguracionCostos? Config;
    public List<ItemLeido> Items { get; } = new();
    public List<(int Anio, string Nombre, List<(string Nombre, decimal Peso, decimal Indice)> Comps)> Ajustes { get; } = new();
    public List<(string Codigo, string Nombre, GrupoPresupuesto Grupo, decimal Inicial, decimal Actual)> Codigos { get; } = new();
    public List<(ConceptoIndirecto Concepto, decimal Pct)> Indirectos { get; } = new();
    public List<CostoLeido> Costos { get; } = new();
    public List<PersonaLeida> Personas { get; } = new();
    public List<CantidadLeida> Previstas { get; } = new();
    public List<RegistroLeido> Registros { get; } = new();
    public List<(string Acta, string Item, decimal Cantidad, decimal Valor, DateTime Fecha)> Actas { get; } = new();
    public List<(string Cuadrilla, string Item, DateTime Fecha, decimal Cantidad)> Metas { get; } = new();
    public DateTime? InicioSemana1;
    public List<string> TorresSinCasar { get; } = new();
    public List<(string Item, decimal Detalle, decimal Avance)> Diferencias { get; } = new();
    public DateTime? InicioDetalle;

    public sealed record ItemLeido(string Codigo, string Descripcion, string? Unidad, bool EsCapitulo, string? Capitulo, decimal Cantidad,
        decimal? PrecioBase, int AnioBase, decimal Aiu, List<(int Anio, decimal Precio)> Manuales, int Orden);
    public sealed record CostoLeido(string? Orden, string? Codigo, string? Proveedor, string? Concepto, decimal Valor, DateTime? Desde, DateTime? Hasta, string? Cuadrilla, bool Pf1);
    public sealed record PersonaLeida(string? Codigo, string? Cargo, string? Cedula, string Nombre, DateTime? Ingreso, DateTime? Retiro, decimal Salario, decimal Auxilio, string? Cuadrilla);
    public sealed record CantidadLeida(int Torre, string Item, decimal Cantidad, string? Cimentacion, string? Tipo, string? Cuerpo);
    public sealed record RegistroLeido(DateTime Fecha, string Item, int? Torre, decimal Cantidad, string? Cuadrilla, string? Descripcion, string Origen);
}

public static class ImportadorLibroCostos
{
    public const string OrigenAvance = "Libro: Avance diario";
    public const string OrigenDetalle = "Libro: DETALLE DE LAS TORRES";
    public const string OrigenMontaje = "Libro: DETALLE MONTAJE";

    public static PlanImportacionCostos Analizar(AppDbContext db, Stream xlsx)
    {
        var plan = new PlanImportacionCostos();
        using var wb = SigWeb.Data.Excel.LibroExcel.Abrir(xlsx);
        IXLWorksheet? Hoja(string n) => wb.Worksheets.FirstOrDefault(w => ImportadorF054.Norm(w.Name) == ImportadorF054.Norm(n));

        if (Hoja("Informe") is { } inf) LeerInforme(inf, Hoja("Presupuesto"), plan);
        if (Hoja("Formulario de precios") is { } fp) LeerPrecios(fp, plan);
        if (Hoja("Presupuesto") is { } pr) LeerPresupuesto(pr, plan);
        if (Hoja("Costos") is { } co) LeerCostos(co, plan);
        if (Hoja("Personal") is { } pe) LeerPersonal(pe, plan);

        var torres = new CasadorTorres(db);
        var cuadrillaItem = new Dictionary<string, string>();
        var avance = Hoja("Avance diario") is { } av ? LeerAvance(av, plan, cuadrillaItem) : new List<PlanImportacionCostos.RegistroLeido>();
        var detalle = new List<PlanImportacionCostos.RegistroLeido>();
        if (Hoja("DETALLE DE LAS TORRES") is { } dt) detalle.AddRange(LeerDetalle(dt, plan, torres, cuadrillaItem, false));
        if (Hoja("DETALLE MONTAJE") is { } dm) detalle.AddRange(LeerDetalle(dm, plan, torres, cuadrillaItem, true));

        // Un solo avance: lo de los DETALLE (por torre) manda desde su primera fecha para sus ítems;
        // del Avance diario entra lo demás (otros ítems o fechas anteriores), sin torre.
        var cubiertos = detalle.Select(r => r.Item).ToHashSet();
        var desde = plan.InicioDetalle ?? DateTime.MaxValue;
        foreach (var it in cubiertos.OrderBy(x => x))
        {
            var d = detalle.Where(r => r.Item == it).Sum(r => r.Cantidad);
            var a = avance.Where(r => r.Item == it && r.Fecha >= desde).Sum(r => r.Cantidad);
            if (Math.Abs(d - a) > 0.005m) plan.Diferencias.Add((it, d, a));
        }
        plan.Registros.AddRange(detalle);
        plan.Registros.AddRange(avance.Where(r => !(cubiertos.Contains(r.Item) && r.Fecha >= desde)));
        plan.TorresSinCasar.AddRange(torres.SinCasar.Distinct().OrderBy(x => x));

        if (Hoja("Meta produccion") is { } mp) LeerMeta(mp, plan);
        if (Hoja("Seguimiento Meta") is { } sm && Fecha(sm.Cell(2, 2)) is DateTime s1) plan.InicioSemana1 = s1;
        return plan;
    }

    // ---------- Aplicar ----------
    public static async Task AplicarAsync(AppDbContext db, PlanImportacionCostos plan)
    {
        var ahora = DateTime.Now;

        if (plan.Config is { } cfgLeida)
        {
            var cfg = await db.ConfiguracionCostos.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (cfg is null) db.ConfiguracionCostos.Add(cfg = new ConfiguracionCostos());
            cfg.Cliente = cfgLeida.Cliente ?? cfg.Cliente; cfg.Contrato = cfgLeida.Contrato ?? cfg.Contrato;
            cfg.Interventor = cfgLeida.Interventor ?? cfg.Interventor; cfg.Objeto = cfgLeida.Objeto ?? cfg.Objeto;
            cfg.Inicio = cfgLeida.Inicio ?? cfg.Inicio; cfg.Fin = cfgLeida.Fin ?? cfg.Fin; cfg.ValorContrato = cfgLeida.ValorContrato ?? cfg.ValorContrato;
        }

        // Cuadrillas al catálogo.
        var cuadrillas = plan.Registros.Select(r => r.Cuadrilla).Concat(plan.Costos.Select(c => c.Cuadrilla)).Concat(plan.Personas.Select(p => p.Cuadrilla))
            .Concat(plan.Metas.Select(m => m.Cuadrilla)).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => InstantaneaCostos.Norm(c)).Distinct().ToList();
        var yaCat = await db.Catalogos.Where(c => c.Tipo == InstantaneaCostos.CatalogoCuadrilla).Select(c => c.Nombre).ToListAsync();
        foreach (var c in cuadrillas.Where(c => !yaCat.Any(y => InstantaneaCostos.Norm(y) == c)))
            db.Catalogos.Add(new ItemCatalogo { Tipo = InstantaneaCostos.CatalogoCuadrilla, Nombre = c });

        // Ítems (por código).
        var items = await db.ItemsPago.IgnoreQueryFilters().ToListAsync();
        var porCodigo = items.Where(i => !i.Eliminado).GroupBy(i => i.Codigo).ToDictionary(g => g.Key, g => g.First());
        foreach (var l in plan.Items)
        {
            if (!porCodigo.TryGetValue(l.Codigo, out var it)) { it = new ItemPago { Codigo = l.Codigo }; db.ItemsPago.Add(it); porCodigo[l.Codigo] = it; }
            it.Descripcion = l.Descripcion; it.Unidad = l.Unidad; it.EsCapitulo = l.EsCapitulo; it.CantidadContratada = l.Cantidad;
            it.PrecioBase = l.PrecioBase; it.AnioBase = l.AnioBase; it.AiuPct = l.Aiu; it.Orden = l.Orden;
            if (it.Id == 0)
            {
                it.Ambito = AmbitoItemPago.Global;
                it.CuadrillaPorDefecto = plan.Registros.Where(r => r.Item == l.Codigo && r.Cuadrilla != null).GroupBy(r => r.Cuadrilla).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
            }
        }
        // Ámbito Torre para los ítems que traen detalle por torre (solo si estaba Global).
        foreach (var cod in plan.Previstas.Select(p => p.Item).Concat(plan.Registros.Where(r => r.Torre != null).Select(r => r.Item)).Distinct())
            if (porCodigo.TryGetValue(cod, out var it) && it.Ambito == AmbitoItemPago.Global) it.Ambito = AmbitoItemPago.Torre;
        await db.SaveChangesAsync();
        // Capítulos.
        foreach (var l in plan.Items.Where(l => l.Capitulo != null))
            if (porCodigo.TryGetValue(l.Codigo, out var it) && porCodigo.TryGetValue(l.Capitulo!, out var cap) && cap.Id != it.Id) it.CapituloId = cap.Id;
        // Precios manuales del libro (reemplazan los del mismo año).
        var ids = plan.Items.Select(l => porCodigo[l.Codigo].Id).ToHashSet();
        var manPrevios = await db.PreciosManuales.Where(p => ids.Contains(p.ItemPagoId)).ToListAsync();
        foreach (var l in plan.Items)
            foreach (var (anio, precio) in l.Manuales)
            {
                var id = porCodigo[l.Codigo].Id;
                var m = manPrevios.FirstOrDefault(p => p.ItemPagoId == id && p.Anio == anio);
                if (m is null) db.PreciosManuales.Add(new PrecioManual { ItemPagoId = id, Anio = anio, Precio = precio, Motivo = "Valor fijo en el libro" });
                else m.Precio = precio;
            }
        // Ajustes por año (si el año ya existe, se reemplazan sus componentes).
        foreach (var (anio, nombre, comps) in plan.Ajustes)
        {
            var a = await db.AjustesPrecio.FirstOrDefaultAsync(x => x.Anio == anio);
            if (a is null) { a = new AjustePrecio { Anio = anio, Nombre = nombre, Soporte = "Fórmula del Formulario de precios" }; db.AjustesPrecio.Add(a); await db.SaveChangesAsync(); }
            db.ComponentesAjustePrecio.RemoveRange(db.ComponentesAjustePrecio.Where(c => c.AjustePrecioId == a.Id));
            foreach (var c in comps) db.ComponentesAjustePrecio.Add(new ComponenteAjustePrecio { AjustePrecioId = a.Id, Nombre = c.Nombre, Peso = c.Peso, Indice = c.Indice });
        }
        await db.SaveChangesAsync();

        // Presupuesto.
        var codigos = await db.CodigosPresupuesto.ToListAsync();
        int orden = 0;
        foreach (var (cod, nom, grupo, ini, act) in plan.Codigos)
        {
            var c = codigos.FirstOrDefault(x => string.Equals(x.Codigo, cod, StringComparison.OrdinalIgnoreCase));
            if (c is null) { c = new CodigoPresupuesto { Codigo = cod }; db.CodigosPresupuesto.Add(c); codigos.Add(c); }
            c.Nombre = nom; c.Grupo = grupo; c.PresupuestoInicial = ini; c.Orden = ++orden;
            await db.SaveChangesAsync();
            var ultimo = await db.ValoresPresupuesto.Where(v => v.CodigoPresupuestoId == c.Id).OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).FirstOrDefaultAsync();
            if (ultimo is null || ultimo.Valor != act)
                db.ValoresPresupuesto.Add(new ValorPresupuesto { CodigoPresupuestoId = c.Id, Fecha = DateTime.Today, Valor = act, Motivo = "Importado del libro (EAC)" });
        }
        var inicio = plan.Config?.Inicio ?? new DateTime(2000, 1, 1);
        foreach (var (concepto, pct) in plan.Indirectos)
        {
            var p = await db.PorcentajesIndirectos.FirstOrDefaultAsync(x => x.Concepto == concepto && x.Desde == inicio);
            if (p is null) db.PorcentajesIndirectos.Add(new PorcentajeIndirecto { Concepto = concepto, Desde = inicio, Porcentaje = pct, Nota = "% combinado del libro" });
            else p.Porcentaje = pct;
        }
        await db.SaveChangesAsync();
        var codId = codigos.Where(c => c.Id > 0).GroupBy(c => c.Codigo.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().Id);
        int? Cod(string? c) => c is not null && codId.TryGetValue(c.Trim().ToUpperInvariant(), out var id) ? id : null;

        // Costos: los anteriores a la Papelera, entran los del libro.
        if (plan.Costos.Count > 0)
        {
            foreach (var c in await db.CostosOrden.ToListAsync()) { c.Eliminado = true; c.FechaEliminacion = ahora; }
            foreach (var c in plan.Costos)
                db.CostosOrden.Add(new CostoOrden
                {
                    NumeroOrden = c.Orden, CodigoPresupuestoId = Cod(c.Codigo), Proveedor = c.Proveedor, Concepto = c.Concepto, Valor = c.Valor,
                    Desde = c.Desde, Hasta = c.Hasta, Cuadrilla = c.Cuadrilla is null ? null : InstantaneaCostos.Norm(c.Cuadrilla), Pf1 = c.Pf1
                });
        }
        // Personal por cédula (o nombre).
        var personas = await db.Personas.ToListAsync();
        foreach (var p in plan.Personas)
        {
            var x = personas.FirstOrDefault(y => !string.IsNullOrWhiteSpace(p.Cedula) && y.Cedula == p.Cedula)
                ?? personas.FirstOrDefault(y => string.IsNullOrWhiteSpace(p.Cedula) && ImportadorF054.Norm(y.Nombre) == ImportadorF054.Norm(p.Nombre));
            if (x is null) { x = new Persona(); db.Personas.Add(x); personas.Add(x); }
            x.CodigoPresupuestoId = Cod(p.Codigo); x.Cargo = p.Cargo; x.Cedula = p.Cedula; x.Nombre = p.Nombre; x.Ingreso = p.Ingreso; x.Retiro = p.Retiro;
            x.Salario = p.Salario; x.Auxilio = p.Auxilio; x.Cuadrilla = p.Cuadrilla is null ? null : InstantaneaCostos.Norm(p.Cuadrilla);
        }
        await db.SaveChangesAsync();

        // Cantidades previstas por torre (se reemplazan las de los ítems leídos).
        var itemId = (await db.ItemsPago.Select(i => new { i.Id, i.Codigo }).ToListAsync()).GroupBy(i => i.Codigo).ToDictionary(g => g.Key, g => g.First().Id);
        if (plan.Previstas.Count > 0)
        {
            var itemsPrev = plan.Previstas.Select(p => itemId.GetValueOrDefault(p.Item)).ToHashSet();
            var torresPrev = plan.Previstas.Select(p => p.Torre).ToHashSet();
            db.CantidadesTorre.RemoveRange(db.CantidadesTorre.Where(c => itemsPrev.Contains(c.ItemPagoId) && torresPrev.Contains(c.ElementoTrazadoId)));
            foreach (var g in plan.Previstas.Where(p => itemId.ContainsKey(p.Item)).GroupBy(p => (p.Torre, p.Item)))
            {
                var f = g.First();
                db.CantidadesTorre.Add(new CantidadTorre
                {
                    ElementoTrazadoId = g.Key.Torre, ItemPagoId = itemId[g.Key.Item], CantidadPrevista = g.Sum(x => x.Cantidad),
                    Cimentacion = f.Cimentacion, TipoTorre = f.Tipo, Cuerpo = f.Cuerpo
                });
            }
        }
        // Registros: los que vinieron del libro antes van a la Papelera.
        if (plan.Registros.Count > 0)
        {
            foreach (var r in await db.RegistrosCantidad.Where(r => r.Origen != null && r.Origen.StartsWith("Libro:")).ToListAsync()) { r.Eliminado = true; r.FechaEliminacion = ahora; }
            foreach (var r in plan.Registros.Where(r => itemId.ContainsKey(r.Item)))
                db.RegistrosCantidad.Add(new RegistroCantidad
                {
                    Fecha = r.Fecha.Date, ItemPagoId = itemId[r.Item], ElementoTrazadoId = r.Torre, Cantidad = r.Cantidad,
                    Cuadrilla = r.Cuadrilla is null ? null : InstantaneaCostos.Norm(r.Cuadrilla), Descripcion = r.Descripcion, Origen = r.Origen, CreadoEn = ahora
                });
        }
        await db.SaveChangesAsync();

        // Actas (por número): sus líneas se reemplazan. Valor = cantidad × precio con AIU del año de la fila
        // (el libro lo calcula al precio vigente; sus valores en caché pueden venir vacíos).
        var precios = await InstantaneaCostos.CargarAsync(db);
        decimal ValorActa(string item, decimal cant, DateTime f) =>
            precios.Items.FirstOrDefault(i => i.Codigo == item) is { } ip ? cant * precios.PrecioConAiu(ip, precios.AnioValor(f, f)) : 0m;
        foreach (var g in plan.Actas.GroupBy(a => a.Acta))
        {
            var acta = await db.ActasFacturacion.FirstOrDefaultAsync(a => a.Numero == g.Key);
            if (acta is null) { acta = new ActaFacturacion { Numero = g.Key }; db.ActasFacturacion.Add(acta); }
            // Fecha del acta (el libro no la tiene): último día con avance de esa acta, sin pasar de hoy (supuesto).
            var fechas = g.Where(x => x.Fecha != DateTime.MinValue).Select(x => x.Fecha).ToList();
            var fin = fechas.Count > 0 ? fechas.Max() : DateTime.Today;
            acta.PeriodoDesde = fechas.Count > 0 ? fechas.Min() : null; acta.PeriodoHasta = fin;
            acta.Fecha = fin > DateTime.Today ? DateTime.Today : fin;
            acta.Observaciones ??= "Importada del libro: fecha = último día con avance de esa acta, sin pasar de la fecha de importación (supuesto)";
            await db.SaveChangesAsync();
            db.LineasActaFacturacion.RemoveRange(db.LineasActaFacturacion.Where(l => l.ActaFacturacionId == acta.Id));
            foreach (var l in g.Where(x => itemId.ContainsKey(x.Item)).GroupBy(x => x.Item))
                db.LineasActaFacturacion.Add(new LineaActaFacturacion { ActaFacturacionId = acta.Id, ItemPagoId = itemId[l.Key], Cantidad = l.Sum(x => x.Cantidad), Valor = Math.Round(l.Sum(x => ValorActa(x.Item, x.Cantidad, x.Fecha == DateTime.MinValue ? acta.Fecha : x.Fecha)), 2) });
        }
        await db.SaveChangesAsync();

        // Meta: una versión nueva del periodo.
        if (plan.Metas.Count > 0)
        {
            var desde = plan.Metas.Min(m => m.Fecha); var hasta = plan.Metas.Max(m => m.Fecha);
            desde = new DateTime(desde.Year, desde.Month, 1); hasta = desde.AddMonths(1).AddDays(-1);
            var previas = await db.MetasPeriodo.Where(m => m.Desde == desde).ToListAsync();
            foreach (var p in previas) p.Vigente = false;
            var meta = new MetaPeriodo
            {
                Desde = desde, Hasta = hasta, InicioSemana1 = plan.InicioSemana1 ?? desde, Version = previas.Count == 0 ? 1 : previas.Max(p => p.Version) + 1,
                Motivo = "Importada del libro", Vigente = true, MetaFacturacion = previas.OrderByDescending(p => p.Version).Select(p => p.MetaFacturacion).FirstOrDefault()
            };
            db.MetasPeriodo.Add(meta);
            await db.SaveChangesAsync();
            foreach (var m in plan.Metas.Where(m => itemId.ContainsKey(m.Item) && m.Cantidad != 0))
                db.MetasProduccionDia.Add(new MetaProduccionDia { MetaPeriodoId = meta.Id, Cuadrilla = InstantaneaCostos.Norm(m.Cuadrilla), ItemPagoId = itemId[m.Item], Fecha = m.Fecha, Cantidad = m.Cantidad });
            await db.SaveChangesAsync();
        }
        db.ChangeTracker.Clear();
    }

    // ---------- Lectura por hoja ----------
    private static void LeerInforme(IXLWorksheet h, IXLWorksheet? pres, PlanImportacionCostos plan)
    {
        plan.Hojas.Add("Informe (encabezado del contrato)");
        string? T(string dir) { var t = Texto(h.Cell(dir)).Trim(); return t.Length == 0 ? null : t; }
        plan.Config = new ConfiguracionCostos
        {
            Cliente = T("G8"), Contrato = T("Q8"), Interventor = T("G10"), Objeto = T("AI8"), Inicio = Fecha(h.Cell("BO8")), Fin = Fecha(h.Cell("BO10")),
            ValorContrato = pres is null ? null : Num(pres.Cell("D2"))
        };
    }

    private static readonly Regex RxPct = new(@"(?:(\d+(?:\.\d+)?)\s*\*\s*)?(\d+(?:\.\d+)?)%", RegexOptions.Compiled);

    private static void LeerPrecios(IXLWorksheet h, PlanImportacionCostos plan)
    {
        plan.Hojas.Add("Formulario de precios");
        var ultima = h.LastRowUsed()?.RowNumber() ?? 0;
        string? capNum = null, capTexto = null;
        var conteoAjuste = new Dictionary<(int, string), int>();
        var anios = new[] { (Col: 5, Anio: 2023), (Col: 6, Anio: 2024), (Col: 7, Anio: 2025), (Col: 8, Anio: 2026) };
        // Años de las columnas E–H por su encabezado ("UNITARIO 2024"…).
        for (int k = 0; k < anios.Length; k++)
            if (Regex.Match(Texto(h.Cell(1, anios[k].Col)), @"(20\d\d)") is { Success: true } m) anios[k] = (anios[k].Col, int.Parse(m.Groups[1].Value));
        int orden = 0;
        for (int f = 2; f <= ultima; f++)
        {
            var cod = Texto(h.Cell(f, 1)).Trim();
            var desc = Texto(h.Cell(f, 2)).Trim();
            if (cod.Length == 0 && desc.Length == 0) continue;
            var unidad = Texto(h.Cell(f, 3)).Trim();
            var esCap = unidad.Length == 0 && Num(h.Cell(f, 10)) is null;
            if (esCap)
            {
                var codCap = cod.Length > 0 ? cod : "CAP-" + desc.ToUpperInvariant();
                string? padre = cod.Contains('.') ? cod[..cod.LastIndexOf('.')] : (cod.Length > 0 ? null : null);
                if (cod.Length > 0 && !cod.Contains('.')) capNum = cod;
                if (cod.Length == 0) capTexto = codCap;
                plan.Items.Add(new(codCap, desc, null, true, padre, 0, null, 2023, 0, new(), ++orden));
                if (cod.Length > 0) capTexto = null;
                continue;
            }
            string? capitulo = cod.Contains('.') ? cod[..cod.LastIndexOf('.')] : capTexto ?? capNum;
            if (!cod.Contains('.') && capTexto is not null) capitulo = capTexto;
            if (cod.Length == 0) { plan.Avisos.Add($"Formulario fila {f}: ítem sin código ({desc}), omitido."); continue; }
            // Precio base = primer valor digitado de E–H; los siguientes digitados = precio manual.
            decimal? baseP = null; int anioBase = anios[0].Anio; var manuales = new List<(int, decimal)>();
            foreach (var (col, anio) in anios)
            {
                var c = h.Cell(f, col);
                if (c.HasFormula)
                {
                    var fm = c.FormulaA1;
                    if (baseP is not null) { var k = (anio, NormFormula(fm)); conteoAjuste[k] = conteoAjuste.GetValueOrDefault(k) + 1; }
                    continue;
                }
                if (Num(c) is not decimal v) continue;
                if (baseP is null) { baseP = v; anioBase = anio; }
                else manuales.Add((anio, v));
            }
            var aiu = 0m;
            var ci = h.Cell(f, 9);
            if (ci.HasFormula) { var m = RxPct.Match(ci.FormulaA1); if (m.Success) aiu = decimal.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 100m; }
            else if (Num(ci) is decimal ai && Num(h.Cell(f, 8)) is decimal hh && hh != 0) aiu = Math.Round(ai / hh, 6);
            plan.Items.Add(new(cod, desc, unidad, false, capitulo, Num(h.Cell(f, 4)) ?? 0m, baseP, anioBase, aiu, manuales, ++orden));
        }
        // El ajuste de cada año = la fórmula más usada en esa columna.
        foreach (var g in conteoAjuste.GroupBy(k => k.Key.Item1))
        {
            var fm = g.OrderByDescending(x => x.Value).First().Key.Item2;
            var comps = RxPct.Matches(fm).Select(m => (
                Nombre: "",
                Peso: m.Groups[1].Success ? decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 1m,
                Indice: decimal.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 100m - 1m)).ToList();
            if (comps.Count == 0) continue;
            var nombres = comps.Count == 1 ? new[] { "IPC" } : comps.Select((_, i) => $"Componente {i + 1}").ToArray();
            plan.Ajustes.Add((g.Key, comps.Count == 1 ? $"IPC {g.Key}" : $"Compuesto {g.Key}",
                comps.Select((c, i) => (nombres[i], c.Peso, c.Indice)).ToList()));
            var otros = g.Where(x => x.Key.Item2 != fm).Sum(x => x.Value);
            if (otros > 0) plan.Avisos.Add($"Precios {g.Key}: {otros} ítem(s) con otra fórmula; se aplicó la general ({fm}).");
        }
    }

    // "E3*109.28%" → "*109.28%" (sin referencia de celda) para comparar fórmulas entre filas.
    private static string NormFormula(string f) => Regex.Replace(f.Replace(" ", ""), @"^[A-Z]+\d+", "");

    private static void LeerPresupuesto(IXLWorksheet h, PlanImportacionCostos plan)
    {
        plan.Hojas.Add("Presupuesto");
        var grupo = GrupoPresupuesto.General;
        var ultima = Math.Min(h.LastRowUsed()?.RowNumber() ?? 0, 200);
        for (int f = 5; f <= ultima; f++)
        {
            var a = Texto(h.Cell(f, 1)).Trim();
            var b = Texto(h.Cell(f, 2)).Trim();
            var nb = ImportadorF054.Norm(b);
            if (nb.StartsWith("total costo") || nb.StartsWith("ebitda")) break;
            GrupoPresupuesto? g = nb switch
            {
                "general" => GrupoPresupuesto.General, "materiales" => GrupoPresupuesto.Materiales, "mano de obra" => GrupoPresupuesto.ManoDeObra,
                "equipo y herramientas" => GrupoPresupuesto.EquipoYHerramientas, "otros" => GrupoPresupuesto.Otros, "costos indirectos" => GrupoPresupuesto.Indirectos, _ => null
            };
            if (g is not null) { grupo = g.Value; continue; }
            if (nb.StartsWith("overhead")) { if (Num(h.Cell(f, 1)) is decimal p) plan.Indirectos.Add((ConceptoIndirecto.Overhead, p)); continue; }
            if (nb.StartsWith("sostenimiento")) { if (Num(h.Cell(f, 1)) is decimal p) plan.Indirectos.Add((ConceptoIndirecto.Sostenimiento, p)); continue; }
            if (b.Length == 0 || nb == "costos directos") continue;
            var sinCodigo = a.Length == 0 || a == "0";
            var cod = sinCodigo ? "SC" + f : a;
            if (sinCodigo) plan.Avisos.Add($"Presupuesto fila {f}: \"{b}\" sin código; quedó como {cod}.");
            plan.Codigos.Add((cod, b, grupo, Num(h.Cell(f, 3)) ?? 0m, Num(h.Cell(f, 4)) ?? 0m));
        }
        for (int f = 60; f <= ultima; f++)
            if (ImportadorF054.Norm(Texto(h.Cell(f, 2))).StartsWith("impuestos y financiacion") && Num(h.Cell(f, 1)) is decimal t)
                plan.Indirectos.Add((ConceptoIndirecto.ImpuestosFinanciacion, t));
    }

    private static void LeerCostos(IXLWorksheet h, PlanImportacionCostos plan)
    {
        plan.Hojas.Add("Costos");
        var ultima = h.LastRowUsed()?.RowNumber() ?? 0;
        int sinFecha = 0;
        for (int f = 2; f <= ultima; f++)
        {
            var valor = Num(h.Cell(f, 5));
            var desde = Fecha(h.Cell(f, 6)); var hasta = Fecha(h.Cell(f, 7));
            var cod = Texto(h.Cell(f, 2)).Trim();
            if (valor is null && cod.Length == 0) continue;
            if (desde is null || hasta is null) { sinFecha++; desde = hasta = null; }
            else if (hasta < desde) hasta = desde;
            var pf1 = ImportadorF054.Norm(Texto(h.Cell(f, 12))) == "si";
            string? N(int c) { var t = Texto(h.Cell(f, c)).Trim(); return t.Length == 0 ? null : t; }
            plan.Costos.Add(new(N(1), N(2), N(3), N(4), valor ?? 0m, desde, hasta, N(11), pf1));
        }
        var pf1Raros = Enumerable.Range(2, Math.Max(0, ultima - 1)).Select(f => Texto(h.Cell(f, 12)).Trim()).Where(t => t.Length > 0 && ImportadorF054.Norm(t) != "si").Distinct().ToList();
        if (pf1Raros.Count > 0) plan.Avisos.Add($"Costos, columna PF1 con valores distintos de \"SI\" (no se tomaron como PF1): {string.Join(", ", pf1Raros)}.");
        if (sinFecha > 0) plan.Avisos.Add($"Costos: {sinFecha} fila(s) sin DESDE/HASTA: cuentan como comprometido, no se cargan por día.");
    }

    private static void LeerPersonal(IXLWorksheet h, PlanImportacionCostos plan)
    {
        plan.Hojas.Add("Personal");
        var ultima = h.LastRowUsed()?.RowNumber() ?? 0;
        for (int f = 2; f <= ultima; f++)
        {
            var nombre = Regex.Replace(Texto(h.Cell(f, 4)), @"\s+", " ").Trim();
            if (nombre.Length == 0) continue;
            string? N(int c) { var t = Texto(h.Cell(f, c)).Trim(); return t.Length == 0 ? null : t; }
            plan.Personas.Add(new(N(1), N(2), N(3), nombre, Fecha(h.Cell(f, 5)), Fecha(h.Cell(f, 6)), Num(h.Cell(f, 7)) ?? 0m, Num(h.Cell(f, 8)) ?? 0m, N(11)));
        }
    }

    private static List<PlanImportacionCostos.RegistroLeido> LeerAvance(IXLWorksheet h, PlanImportacionCostos plan, Dictionary<string, string> cuadrillaItem)
    {
        plan.Hojas.Add("Avance diario");
        var res = new List<PlanImportacionCostos.RegistroLeido>();
        var ultima = h.LastRowUsed()?.RowNumber() ?? 0;
        int sinItem = 0, sinFecha = 0;
        var porItem = new Dictionary<string, Dictionary<string, int>>();
        for (int f = 2; f <= ultima; f++)
        {
            var item = Texto(h.Cell(f, 1)).Trim();
            var fecha = Fecha(h.Cell(f, 6));
            var cant = Num(h.Cell(f, 4));
            var acta = Texto(h.Cell(f, 7)).Trim();
            if (cant is null) continue;
            if (item.Length == 0) { sinItem++; continue; }
            // Sin fecha: no es avance ejecutado, pero si tiene acta sí cuenta como facturado (igual que el libro).
            if (fecha is null) { sinFecha++; if (acta.Length > 0) plan.Actas.Add((acta, item, cant.Value, 0m, DateTime.MinValue)); continue; }
            var cuad = Texto(h.Cell(f, 8)).Trim();
            var desc = Texto(h.Cell(f, 9)).Trim();
            res.Add(new(fecha.Value.Date, item, null, cant.Value, cuad.Length == 0 ? null : cuad, desc.Length == 0 ? null : desc, OrigenAvance));
            if (cuad.Length > 0)
            {
                if (!porItem.TryGetValue(item, out var d)) porItem[item] = d = new();
                var k = InstantaneaCostos.Norm(cuad); d[k] = d.GetValueOrDefault(k) + 1;
            }
            if (acta.Length > 0)
                plan.Actas.Add((acta, item, cant.Value, Num(h.Cell(f, 5)) ?? 0m, fecha.Value.Date));
        }
        foreach (var (it, d) in porItem) cuadrillaItem[it] = d.OrderByDescending(x => x.Value).First().Key;
        if (sinItem > 0) plan.Avisos.Add($"Avance diario: {sinItem} fila(s) con cantidad pero sin ítem, omitidas.");
        if (sinFecha > 0) plan.Avisos.Add($"Avance diario: {sinFecha} fila(s) sin fecha: no entran como avance; las que tienen acta sí entran a la facturación.");
        return res;
    }

    // DETALLE DE LAS TORRES: B torre, C cimentación, D actividad, E cantidad, F ítem, fechas desde G.
    // DETALLE MONTAJE: B torre, C tipo, D cuerpo, E actividad, F cantidad, G ítem, fechas desde H.
    private static List<PlanImportacionCostos.RegistroLeido> LeerDetalle(IXLWorksheet h, PlanImportacionCostos plan, CasadorTorres torres, Dictionary<string, string> cuadrillaItem, bool montaje)
    {
        plan.Hojas.Add(h.Name);
        var res = new List<PlanImportacionCostos.RegistroLeido>();
        int cCant = montaje ? 6 : 5, cItem = montaje ? 7 : 6, cIni = montaje ? 8 : 7;
        var ultCol = h.LastColumnUsed()?.ColumnNumber() ?? 0;
        var fechas = new Dictionary<int, DateTime>();
        for (int c = cIni; c <= ultCol; c++) if (Fecha(h.Cell(1, c)) is DateTime d) fechas[c] = d.Date;
        if (fechas.Count > 0) plan.InicioDetalle = plan.InicioDetalle is null ? fechas.Values.Min() : (fechas.Values.Min() < plan.InicioDetalle ? fechas.Values.Min() : plan.InicioDetalle);
        var ultima = h.LastRowUsed()?.RowNumber() ?? 0;
        for (int f = 2; f <= ultima; f++)
        {
            var nomTorre = Texto(h.Cell(f, 2)).Trim();
            var item = Texto(h.Cell(f, cItem)).Trim();
            if (nomTorre.Length == 0 || item.Length == 0 || ImportadorF054.Norm(nomTorre) == "torre") continue;
            if (torres.Casar(nomTorre) is not int torre) continue;
            var prevista = Num(h.Cell(f, cCant));
            if (prevista is decimal p && p > 0)
                plan.Previstas.Add(new(torre, item, p, montaje ? null : Texto(h.Cell(f, 3)).Trim(), montaje ? Texto(h.Cell(f, 3)).Trim() : null, montaje ? Texto(h.Cell(f, 4)).Trim() : null));
            var cuad = cuadrillaItem.GetValueOrDefault(item) ?? (montaje ? "MONTAJE" : "OBRA CIVIL");
            foreach (var (c, d) in fechas)
                if (Num(h.Cell(f, c)) is decimal q && q != 0)
                    res.Add(new(d, item, torre, q, cuad, null, montaje ? OrigenMontaje : OrigenDetalle));
        }
        return res;
    }

    private static void LeerMeta(IXLWorksheet h, PlanImportacionCostos plan)
    {
        plan.Hojas.Add("Meta produccion");
        var fechas = new Dictionary<int, DateTime>();
        for (int c = 4; c <= 40; c++)
        {
            if (Fecha(h.Cell(1, c)) is DateTime d) fechas[c] = d.Date;
            else if (fechas.Count > 0) break;
        }
        var ultima = h.LastRowUsed()?.RowNumber() ?? 0;
        for (int f = 2; f <= ultima; f++)
        {
            var cuad = Texto(h.Cell(f, 1)).Trim();
            var item = Texto(h.Cell(f, 2)).Trim();
            if (cuad.Length == 0 || item.Length == 0) continue;
            foreach (var (c, d) in fechas)
                if (Num(h.Cell(f, c)) is decimal q && q != 0) plan.Metas.Add((cuad, item, d, q));
        }
    }

    // ---------- Celdas ----------
    private static XLCellValue Valor(IXLCell c) => c.HasFormula ? c.CachedValue : c.Value;

    private static string Texto(IXLCell c)
    {
        try
        {
            var v = Valor(c);
            if (v.IsBlank || v.IsError) return "";
            if (v.IsNumber) return v.GetNumber().ToString(CultureInfo.InvariantCulture);
            if (v.IsDateTime) return v.GetDateTime().ToString("dd/MM/yyyy");
            return v.ToString(CultureInfo.InvariantCulture);
        }
        catch { return ""; }
    }

    private static decimal? Num(IXLCell c)
    {
        try
        {
            var v = Valor(c);
            if (v.IsNumber) { var d = v.GetNumber(); return double.IsFinite(d) && Math.Abs(d) < 1e15 ? (decimal)d : null; }
            if (v.IsText && decimal.TryParse(v.GetText().Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;
        }
        catch { }
        return null;
    }

    private static DateTime? Fecha(IXLCell c)
    {
        try
        {
            var v = Valor(c);
            if (v.IsDateTime) return v.GetDateTime();
            if (v.IsNumber) { var n = v.GetNumber(); return n > 20000 && n < 80000 ? DateTime.FromOADate(n) : null; }
            if (v.IsText) return FechaTexto.Parsear(v.GetText());
        }
        catch { }
        return null;
    }
}

// Torres del Trazado por nombre/código/alias (misma regla que el importador F-054).
public sealed class CasadorTorres
{
    private readonly Dictionary<string, int> _clave = new();
    private readonly Dictionary<string, List<int>> _raiz = new();
    public List<string> SinCasar { get; } = new();

    public CasadorTorres(AppDbContext db)
    {
        foreach (var e in db.ElementosTrazado.AsNoTracking().Where(e => !e.Eliminado).Select(e => new { e.Id, e.Codigo, e.Nombre }).ToList())
            foreach (var n in new[] { e.Nombre, e.Codigo }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            {
                var k = ImportadorF054.ClaveTorre(n!);
                if (k.Length == 0) continue;
                _clave.TryAdd(k, e.Id);
                var r = k.TrimEnd('N');
                if (!_raiz.TryGetValue(r, out var l)) _raiz[r] = l = new();
                if (!l.Contains(e.Id)) l.Add(e.Id);
            }
        foreach (var a in db.AliasTorres.AsNoTracking().Select(a => new { a.Texto, a.ElementoTrazadoId }).ToList())
            if (ImportadorF054.ClaveTorre(a.Texto) is { Length: > 0 } k) _clave.TryAdd(k, a.ElementoTrazadoId);
    }

    public int? Casar(string nombre)
    {
        var k = ImportadorF054.ClaveTorre(nombre);
        if (_clave.TryGetValue(k, out var id)) return id;
        if (_raiz.TryGetValue(k.TrimEnd('N'), out var ids) && ids.Count == 1) return ids[0];
        SinCasar.Add(nombre);
        return null;
    }
}
