using SigWeb.Models;

namespace SigWeb.Data.Costos;

// PDT (programa de trabajo) y costo por actividad (fase 3). En memoria sobre la instantánea.
public sealed partial class InstantaneaCostos
{
    // ---------- PDT ----------
    public ProgramaTrabajo? ProgramaVigente => Programas.Where(p => p.Vigente).OrderByDescending(p => p.Version).FirstOrDefault()
                                             ?? Programas.OrderByDescending(p => p.Version).FirstOrDefault();
    public ProgramaTrabajo? LineaBase => Programas.Where(p => p.LineaBase).OrderBy(p => p.Version).FirstOrDefault();

    public List<ActividadPrograma> ActividadesDe(int programaId) =>
        ActividadesPrograma.Where(a => a.ProgramaTrabajoId == programaId).OrderBy(a => a.Orden).ToList();

    // Fracción programada a una fecha: lineal entre Inicio y Fin (supuesto: Project no da la curva por tarea).
    public static decimal FraccionProgramada(ActividadPrograma a, DateTime fecha)
    {
        if (fecha.Date < a.Inicio.Date) return 0m;
        if (fecha.Date >= a.Fin.Date) return 1m;
        return (decimal)Dias(a.Inicio, fecha) / Dias(a.Inicio, a.Fin);
    }

    // Valor programado acumulado de una versión a una fecha (para la curva S).
    public decimal ValorProgramado(int programaId, DateTime fecha) =>
        ActividadesPrograma.Where(a => a.ProgramaTrabajoId == programaId && !a.EsResumen && a.ValorPlaneado is not null)
            .Sum(a => a.ValorPlaneado!.Value * FraccionProgramada(a, fecha));

    // Cantidad ejecutada de una tarea: registros explícitamente ligados a ella, o del mismo ítem y lugar.
    public decimal EjecutadaActividad(ActividadPrograma a, DateTime corte)
    {
        var ligados = Registros.Where(r => r.ActividadProgramaId == a.Id && r.Fecha.Date <= corte.Date).ToList();
        if (ligados.Count > 0) return ligados.Sum(r => r.Cantidad);
        if (a.ItemPagoId is not int item) return 0m;
        return Registros.Where(r => r.ItemPagoId == item && r.Fecha.Date <= corte.Date
                                    && (a.ElementoTrazadoId is null || r.ElementoTrazadoId == a.ElementoTrazadoId)
                                    && (a.TiroId is null || r.TiroId == a.TiroId)).Sum(r => r.Cantidad);
    }

    public sealed record SeguimientoTarea(ActividadPrograma A, decimal Programado, decimal? Real, decimal Ejecutada, DateTime? PrimerRegistro,
        DateTime? FinBase, int? DesvioFin, string Estado);

    public List<SeguimientoTarea> SeguimientoPrograma(int programaId, DateTime corte)
    {
        var baseUid = LineaBase is { } lb && lb.Id != programaId
            ? ActividadesDe(lb.Id).Where(a => a.Uid is not null).GroupBy(a => a.Uid!.Value).ToDictionary(g => g.Key, g => g.First())
            : new Dictionary<int, ActividadPrograma>();
        var l = new List<SeguimientoTarea>();
        foreach (var a in ActividadesDe(programaId))
        {
            var prog = FraccionProgramada(a, corte);
            var ejec = a.EsResumen ? 0m : EjecutadaActividad(a, corte);
            // Real: de las cantidades registradas; sin cantidad programada, el % que reporta Project (referencia).
            decimal? real = a.Cantidad > 0 ? Math.Min(1m, ejec / a.Cantidad) : a.EsResumen ? null : a.AvanceProject;
            DateTime? primero = a.ItemPagoId is int it
                ? Registros.Where(r => r.ItemPagoId == it && (a.ElementoTrazadoId is null || r.ElementoTrazadoId == a.ElementoTrazadoId) && r.Fecha.Date <= corte.Date)
                    .Select(r => (DateTime?)r.Fecha).Min()
                : null;
            var fb = a.Uid is int u && baseUid.TryGetValue(u, out var b) ? b.Fin : (DateTime?)null;
            var estado = a.EsResumen ? ""
                : real >= 0.999m ? "Terminada"
                : real is decimal r0 && r0 + 0.05m < prog ? "Atrasada"
                : real is null && ejec == 0 && prog > 0 ? "Sin registro"
                : real is decimal r1 && r1 > 0 && a.Cantidad <= 0 ? "En curso"
                : ejec > 0 ? "En curso"
                : prog > 0 ? "Atrasada" : "Por iniciar";
            l.Add(new SeguimientoTarea(a, prog, real, ejec, primero, fb, fb is DateTime f ? (a.Fin.Date - f.Date).Days : null, estado));
        }
        return l;
    }

    // ---------- Costo por actividad ----------
    // Costo de cada día por cuadrilla (directo + su parte de administración del día) repartido entre
    // los ítems que la cuadrilla produjo ese día, según el criterio de Ajustes; las asignaciones
    // manuales (AsignacionCostoActividad) mandan y el resto se reparte entre los demás ítems. El costo
    // de un día sin producción de la cuadrilla queda "sin asignar" (ociosidad).
    public sealed record CostoItem(int ItemId, decimal Ingreso, decimal Costo, decimal Indirectos, decimal Manual)
    {
        public decimal Margen => Ingreso - Costo - Indirectos;
    }

    public sealed record ResultadoCostoActividad(List<CostoItem> Items, Dictionary<string, decimal> SinAsignar,
        Dictionary<(DateTime Dia, string Cuadrilla, int ItemId), decimal> Detalle);

    public ResultadoCostoActividad CostoPorActividad(DateTime desde, DateTime hasta, DateTime corte)
    {
        var fin = hasta.Date < corte.Date ? hasta.Date : corte.Date;
        var admin = Norm(Config.CuadrillaAdministracion);
        var sinReparto = CuadrillasSinReparto();
        var detalle = new Dictionary<(DateTime, string, int), decimal>();
        var sinAsignar = new Dictionary<string, decimal>();
        var ingreso = new Dictionary<int, decimal>();
        var indirectos = new Dictionary<int, decimal>();
        var manual = new Dictionary<int, decimal>();
        var regsDia = Registros.Where(r => r.Fecha.Date >= desde.Date && r.Fecha.Date <= fin).ToLookup(r => r.Fecha.Date);
        var manDia = AsignacionesManual.Where(m => m.Fecha.Date >= desde.Date && m.Fecha.Date <= fin).ToLookup(m => m.Fecha.Date);

        for (var d = desde.Date; d <= fin; d = d.AddDays(1))
        {
            var costo = new Dictionary<string, decimal>();
            foreach (var c in Costos) if (Vigente(c, d)) { var k = Norm(c.Cuadrilla); costo[k] = costo.GetValueOrDefault(k) + ValorDia(c); }
            foreach (var p in Personas) if (Activa(p, d)) { var k = Norm(p.Cuadrilla); costo[k] = costo.GetValueOrDefault(k) + ValorDia(p); }

            var regs = regsDia[d].ToList();
            var prodCuad = regs.GroupBy(r => Norm(r.Cuadrilla)).ToDictionary(g => g.Key, g => g.Sum(r => Valor(r, corte)));

            // Administración del día → cuadrillas con producción ese día (sin las de "sin reparto").
            var adm = costo.GetValueOrDefault(admin);
            costo.Remove(admin);
            var receptoras = prodCuad.Where(kv => kv.Key != admin && kv.Key != "" && !sinReparto.Contains(kv.Key) && kv.Value > 0).ToList();
            var baseAdm = receptoras.Sum(kv => kv.Value);
            if (adm != 0)
            {
                if (baseAdm > 0) foreach (var (k, v) in receptoras) costo[k] = costo.GetValueOrDefault(k) + adm * v / baseAdm;
                else sinAsignar[admin] = sinAsignar.GetValueOrDefault(admin) + adm;
            }

            foreach (var r in regs)
            {
                ingreso[r.ItemPagoId] = ingreso.GetValueOrDefault(r.ItemPagoId) + Valor(r, corte);
                var (oh, sos, tax) = IndirectosDe(new[] { r }, corte);
                indirectos[r.ItemPagoId] = indirectos.GetValueOrDefault(r.ItemPagoId) + oh + sos + tax;
            }

            foreach (var (cuad, total) in costo)
            {
                var propios = regs.Where(r => Norm(r.Cuadrilla) == cuad).GroupBy(r => r.ItemPagoId)
                    .Select(g => (Item: g.Key, Valor: g.Sum(r => Valor(r, corte)), Cant: g.Sum(r => r.Cantidad))).ToList();
                var fijos = manDia[d].Where(m => Norm(m.Cuadrilla) == cuad).GroupBy(m => m.ItemPagoId).ToDictionary(g => g.Key, g => g.Sum(m => m.Valor));
                var resto = total - fijos.Values.Sum();
                foreach (var (it, v) in fijos)
                {
                    detalle[(d, cuad, it)] = v;
                    manual[it] = manual.GetValueOrDefault(it) + v;
                }
                var libres = propios.Where(p => !fijos.ContainsKey(p.Item)).ToList();
                if (libres.Count == 0) { if (resto != 0) sinAsignar[cuad] = sinAsignar.GetValueOrDefault(cuad) + resto; continue; }
                var pesos = libres.Select(p => Config.CriterioReparto switch
                {
                    CriterioRepartoCosto.Cantidad => Math.Max(0, p.Cant),
                    CriterioRepartoCosto.PartesIguales => 1m,
                    _ => Math.Max(0, p.Valor)
                }).ToList();
                var suma = pesos.Sum();
                if (suma <= 0) { pesos = libres.Select(_ => 1m).ToList(); suma = pesos.Count; }
                for (var i = 0; i < libres.Count; i++)
                    detalle[(d, cuad, libres[i].Item)] = detalle.GetValueOrDefault((d, cuad, libres[i].Item)) + resto * pesos[i] / suma;
            }
        }

        var costoItem = detalle.GroupBy(kv => kv.Key.Item3).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));
        var ids = ingreso.Keys.Union(costoItem.Keys).ToList();
        var lista = ids.Select(i => new CostoItem(i, ingreso.GetValueOrDefault(i), costoItem.GetValueOrDefault(i), indirectos.GetValueOrDefault(i), manual.GetValueOrDefault(i)))
            .OrderBy(c => ItemPorId.GetValueOrDefault(c.ItemId)?.Orden).ToList();
        return new ResultadoCostoActividad(lista, sinAsignar, detalle);
    }
}
