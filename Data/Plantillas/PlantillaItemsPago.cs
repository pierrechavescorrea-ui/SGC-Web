using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

public sealed class PlantillaItemsPago : PlantillaCostos
{
    public override string Clave => Items;
    public override string Nombre => "Formulario de precios";
    public override int Orden => 40;
    protected override HojaPlantilla Hoja { get; } = new("ItemsPago", "Capítulos e ítems del contrato. Los padres se identifican por código.", new[]
    {
        Col("codigo", "Código", obligatoria: true), Col("descripcion", "Descripción", obligatoria: true),
        Col("capitulo", "Es capítulo", TipoColumna.SiNo), Ref("padre", "Capítulo", "capitulos-pago"),
        Col("unidad", "Unidad"), Col("cantidad", "Cantidad contratada", TipoColumna.Decimal),
        Col("precio", "Precio base", TipoColumna.Decimal), Col("anio", "Año base", TipoColumna.Entero),
        Col("aiu", "AIU %", TipoColumna.Decimal), EnumCol<AmbitoItemPago>("ambito", "Ámbito"),
        Col("oh", "Overhead", TipoColumna.SiNo), Col("sos", "Sostenimiento", TipoColumna.SiNo),
        Col("imp", "Impuestos", TipoColumna.SiNo), Ref("cuadrilla", "Cuadrilla", "cuadrillas"),
        Ref("actividad", "Actividad de obra", "actividades-obra"),
        Col("costo", "Costo unitario previsto", TipoColumna.Decimal), Col("orden", "Orden", TipoColumna.Entero)
    });

    public override async Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct)
    {
        var items = await db.ItemsPago.AsNoTracking().OrderBy(i => i.Orden).ThenBy(i => i.Codigo).ToListAsync(ct);
        var codigos = items.ToDictionary(i => i.Id, i => i.Codigo);
        var actividades = await db.ActividadesObra.AsNoTracking().Where(a => a.Activo).ToDictionaryAsync(a => a.Id, a => a.Nombre, ct);
        return items.Select(i => new Dictionary<string, object?>
        {
            ["codigo"] = i.Codigo, ["descripcion"] = i.Descripcion, ["capitulo"] = i.EsCapitulo,
            ["padre"] = i.CapituloId is int padre ? codigos.GetValueOrDefault(padre) : null,
            ["unidad"] = i.Unidad, ["cantidad"] = i.CantidadContratada, ["precio"] = i.PrecioBase,
            ["anio"] = i.AnioBase, ["aiu"] = i.AiuPct * 100, ["ambito"] = i.Ambito.ToString(),
            ["oh"] = i.AplicaOverhead, ["sos"] = i.AplicaSostenimiento, ["imp"] = i.AplicaImpuestos,
            ["cuadrilla"] = i.CuadrillaPorDefecto, ["actividad"] = i.ActividadObraId is int a ? actividades.GetValueOrDefault(a) : null,
            ["costo"] = i.CostoUnitarioPresupuestado, ["orden"] = i.Orden
        }).ToList();
    }

    public override async Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct)
    {
        var plan = NuevoPlan();
        var activos = await ctx.Db.ItemsPago.AsNoTracking().ToListAsync(ct);
        var porCodigo = activos.ToLookup(i => Norm(i.Codigo));
        var codigos = activos.ToDictionary(i => i.Id, i => Norm(i.Codigo));
        var actividades = await ctx.Db.ActividadesObra.AsNoTracking().Where(a => a.Activo).ToListAsync(ct);
        var referencias = await ReferenciasCostos.CargarAsync(ctx.Db, ct);
        var recibidas = new Dictionary<string, (FilaLeida Fila, ItemPago Nuevo, ItemPago? Anterior, string? Padre)>();
        var vistos = new HashSet<string>();
        foreach (var f in Filas(hojas))
        {
            string codigo = Norm(f.Texto("codigo"));
            if (!vistos.Add(codigo) || porCodigo[codigo].Count() > 1)
            {
                Error(plan, f, "codigo", "Código repetido en la hoja o ambiguo en el proyecto.");
                continue;
            }
            var actual = porCodigo[codigo].SingleOrDefault();
            var nuevo = Copiar(ctx.Db, actual);
            var errores = plan.Errores.Count;
            nuevo.Codigo = actual?.Codigo ?? codigo;
            nuevo.Descripcion = f.Texto("descripcion") ?? nuevo.Descripcion;
            nuevo.EsCapitulo = f.SiNo("capitulo") ?? nuevo.EsCapitulo;
            nuevo.Unidad = f.Texto("unidad") ?? nuevo.Unidad;
            nuevo.CantidadContratada = Decimal(plan, f, "cantidad") ?? nuevo.CantidadContratada;
            nuevo.PrecioBase = Decimal(plan, f, "precio") ?? nuevo.PrecioBase;
            nuevo.CostoUnitarioPresupuestado = Decimal(plan, f, "costo") ?? nuevo.CostoUnitarioPresupuestado;
            if (Decimal(plan, f, "aiu") is decimal aiu)
            {
                if (aiu > 100) Error(plan, f, "aiu", "Use un porcentaje de 0 a 100.");
                else nuevo.AiuPct = aiu / 100;
            }
            if (f.Tiene("anio"))
            {
                if (f.Entero("anio") is int anio && anio is >= 1 and <= 9999) nuevo.AnioBase = anio;
                else Error(plan, f, "anio", "Año fuera de rango.");
            }
            if (f.Tiene("orden"))
            {
                if (f.Entero("orden") is int orden && orden >= 0) nuevo.Orden = orden;
                else Error(plan, f, "orden", "El orden debe ser un entero no negativo.");
            }
            if (Enum.TryParse<AmbitoItemPago>(f.Texto("ambito"), true, out var ambito)) nuevo.Ambito = ambito;
            nuevo.AplicaOverhead = f.SiNo("oh") ?? nuevo.AplicaOverhead;
            nuevo.AplicaSostenimiento = f.SiNo("sos") ?? nuevo.AplicaSostenimiento;
            nuevo.AplicaImpuestos = f.SiNo("imp") ?? nuevo.AplicaImpuestos;
            if (f.Texto("cuadrilla") is string cuadrilla)
            {
                if (!referencias.Cuadrillas.Contains(Norm(cuadrilla))) Error(plan, f, "cuadrilla", "La cuadrilla no existe.");
                else nuevo.CuadrillaPorDefecto = Norm(cuadrilla);
            }
            if (f.Texto("actividad") is string actividad)
            {
                var id = IdPorCodigo(actividades, a => a.Nombre, a => a.Id, actividad);
                if (id is null) Error(plan, f, "actividad", "La actividad no existe o el nombre es ambiguo.");
                else nuevo.ActividadObraId = id;
            }
            if (plan.Errores.Count == errores)
                recibidas[codigo] = (f, nuevo, actual, f.Texto("padre") is string padre ? Norm(padre) : null);
        }

        var efectivos = activos.Where(i => porCodigo[Norm(i.Codigo)].Count() == 1)
            .ToDictionary(i => Norm(i.Codigo), i => (i.EsCapitulo, Padre: i.CapituloId is int id ? codigos.GetValueOrDefault(id) : null));
        foreach (var (codigo, r) in recibidas)
            efectivos[codigo] = (r.Nuevo.EsCapitulo, r.Padre ?? efectivos.GetValueOrDefault(codigo).Padre);

        foreach (var (codigo, r) in recibidas)
        {
            var visitados = new HashSet<string> { codigo };
            var padre = efectivos[codigo].Padre;
            while (padre is not null)
            {
                if (!efectivos.TryGetValue(padre, out var p) || !p.EsCapitulo || !visitados.Add(padre))
                {
                    Error(plan, r.Fila, "padre", $"Capítulo «{padre}» inexistente, ambiguo, no es capítulo o generaría un ciclo.");
                    break;
                }
                padre = p.Padre;
            }
            if (!r.Nuevo.EsCapitulo && efectivos.Values.Any(x => x.Padre == codigo))
                Error(plan, r.Fila, "capitulo", "El código tiene hijos y debe seguir siendo capítulo.");
            var padreAnterior = r.Anterior?.CapituloId is int id ? codigos.GetValueOrDefault(id) : null;
            Planear(plan, r.Fila, codigo, r.Nuevo, r.Anterior, r.Anterior?.Id, ctx.Db, r.Padre,
                r.Padre is not null && r.Padre != padreAnterior);
        }
        return plan;
    }

    public override async Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var items = await ctx.Db.ItemsPago.ToListAsync(ct);
        var porCodigo = items.ToLookup(i => Norm(i.Codigo));
        var cambios = new List<(ItemPago Item, string Padre)>();
        var nuevas = new Dictionary<string, ItemPago>();
        var tocadas = new List<ItemPago>();
        foreach (var fila in Modificadas(plan))
        {
            var cambio = (Cambio<ItemPago>)fila.Datos!;
            var entidad = Integrar(ctx, cambio, Unico(porCodigo[fila.Codigo]));
            tocadas.Add(entidad);
            if (cambio.Id is null) nuevas.Add(fila.Codigo, entidad);
            if (cambio.Padre is string padre) cambios.Add((entidad, padre));
        }
        await ctx.Db.SaveChangesAsync(ct);
        foreach (var (item, codigoPadre) in cambios)
        {
            var padre = nuevas.GetValueOrDefault(codigoPadre) ?? Unico(porCodigo[codigoPadre]);
            if (padre is null || !padre.EsCapitulo) throw new InvalidOperationException("El capítulo cambió desde la vista previa.");
            item.CapituloId = padre.Id;
        }

        var todos = items.Concat(nuevas.Values).ToDictionary(i => i.Id);
        var padres = todos.Values.Select(i => i.CapituloId).OfType<int>().ToHashSet();
        foreach (var item in tocadas)
        {
            if (!item.EsCapitulo && padres.Contains(item.Id))
                throw new InvalidOperationException("El ítem tiene hijos y debe seguir siendo capítulo.");
            var visitados = new HashSet<int> { item.Id };
            for (var id = item.CapituloId; id is int padreId;)
            {
                if (!todos.TryGetValue(padreId, out var padre) || !padre.EsCapitulo || !visitados.Add(padreId))
                    throw new InvalidOperationException("La jerarquía de capítulos cambió desde la vista previa.");
                id = padre.CapituloId;
            }
        }
    }
}

