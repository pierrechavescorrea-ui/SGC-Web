using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Costos;
using SigWeb.Models;
using ClaveCantidad = SigWeb.Data.Costos.ServicioCantidadesPrevistas.Clave;

namespace SigWeb.Data.Plantillas;

public sealed class PlantillaCantidadesPrevistas : PlantillaCostos
{
    public override string Clave => Previstas;
    public override string Nombre => "Cantidades previstas";
    public override int Orden => 60;
    protected override HojaPlantilla Hoja { get; } = new("CantidadesPrevistas", "Cantidades previstas por torre, pata opcional e ítem.", new[]
    {
        Ref("torre", "Torre", "torres-costo", true), Ref("pata", "Pata", "patas-costo"),
        Ref("item", "Ítem", "items-pago", true), Col("cantidad", "Prevista", TipoColumna.Decimal, true),
        Col("cimentacion", "Cimentación"), Col("tipo", "Tipo torre"), Col("cuerpo", "Cuerpo"), Col("nota", "Nota")
    });

    private static ClaveCantidad Identidad(CantidadTorre c) => new(c.ElementoTrazadoId, c.ComponenteId, c.ItemPagoId);

    public override async Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct)
    {
        var r = await ReferenciasCostos.CargarAsync(db, ct);
        var torres = r.Torres.ToDictionary(t => t.Id, t => t.Codigo);
        var items = r.Items.Where(i => !i.EsCapitulo).ToDictionary(i => i.Id, i => i.Codigo);
        var patas = r.Patas.ToDictionary(p => p.Id, p => p.Nombre);
        return (await db.CantidadesTorre.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct))
            .Where(c => torres.ContainsKey(c.ElementoTrazadoId) && items.ContainsKey(c.ItemPagoId) &&
                (c.ComponenteId is null || patas.ContainsKey(c.ComponenteId.Value)))
            .Select(c => new Dictionary<string, object?>
            {
                ["torre"] = torres[c.ElementoTrazadoId],
                ["pata"] = c.ComponenteId is int pata ? ReferenciasCostos.EtiquetaPata(torres[c.ElementoTrazadoId], patas[pata]) : null,
                ["item"] = items[c.ItemPagoId], ["cantidad"] = c.CantidadPrevista,
                ["cimentacion"] = c.Cimentacion, ["tipo"] = c.TipoTorre, ["cuerpo"] = c.Cuerpo, ["nota"] = c.Nota
            }).ToList();
    }

    public override async Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct)
    {
        var plan = NuevoPlan();
        var r = await ReferenciasCostos.CargarAsync(ctx.Db, ct);
        var actuales = (await ctx.Db.CantidadesTorre.AsNoTracking().ToListAsync(ct)).ToLookup(Identidad);
        var vistos = new HashSet<ClaveCantidad>();
        var capturas = new List<ServicioCantidadesPrevistas.Captura>();
        foreach (var f in Filas(hojas))
        {
            var codigoTorre = f.Texto("torre")!;
            var codigoItem = f.Texto("item")!;
            int? torre = IdPorCodigo(r.Torres, t => t.Codigo, t => t.Id, codigoTorre);
            int? item = IdPorCodigo(r.Items.Where(i => !i.EsCapitulo), i => i.Codigo, i => i.Id, codigoItem);
            if (torre is null) Error(plan, f, "torre", "Torre inexistente o ambigua.");
            if (item is null) Error(plan, f, "item", "Ítem hoja inexistente o ambiguo.");
            if (torre is null || item is null) continue;
            int? pata = null;
            if (f.Texto("pata") is string nombrePata)
            {
                pata = r.PataId(torre.Value, codigoTorre, nombrePata);
                if (pata is null || r.Items.Single(i => i.Id == item).Ambito != AmbitoItemPago.Pata)
                {
                    Error(plan, f, "pata", "La pata debe pertenecer a la torre y el ítem debe tener ámbito Pata.");
                    continue;
                }
            }
            if (Decimal(plan, f, "cantidad") is not decimal cantidad) continue;
            var clave = new ClaveCantidad(torre.Value, pata, item.Value);
            if (!vistos.Add(clave) || actuales[clave].Count() > 1)
            {
                Error(plan, f, "item", "Combinación torre, pata e ítem repetida o ambigua en el proyecto.");
                continue;
            }
            var anterior = actuales[clave].SingleOrDefault();
            var nuevo = Copiar(ctx.Db, anterior);
            nuevo.ElementoTrazadoId = clave.TorreId;
            nuevo.ItemPagoId = clave.ItemId;
            nuevo.ComponenteId = clave.PataId;
            nuevo.CantidadPrevista = cantidad;
            nuevo.Cimentacion = f.Texto("cimentacion") ?? nuevo.Cimentacion;
            nuevo.TipoTorre = f.Texto("tipo") ?? nuevo.TipoTorre;
            nuevo.Cuerpo = f.Texto("cuerpo") ?? nuevo.Cuerpo;
            nuevo.Nota = f.Texto("nota") ?? nuevo.Nota;
            Planear(plan, f, $"{codigoTorre} · {f.Texto("pata") ?? "Sin pata"} · {codigoItem}", nuevo, anterior, anterior?.Id, ctx.Db);
            capturas.Add(new(clave, cantidad));
        }
        try { await ServicioCantidadesPrevistas.ValidarAsync(ctx.Db, capturas, ct); }
        catch (InvalidOperationException ex)
        {
            for (var i = 0; i < capturas.Count; i++)
            {
                try { await ServicioCantidadesPrevistas.ValidarAsync(ctx.Db, new[] { capturas[i] }, ct); }
                catch (InvalidOperationException fila)
                { plan.Errores.Add(new(Hoja.Nombre, plan.Filas[i].NumeroFila, "Prevista", fila.Message)); }
            }
            if (plan.Errores.Count == 0) plan.Errores.Add(new(Hoja.Nombre, 0, "Prevista", ex.Message));
        }
        return plan;
    }

    public override async Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var cambios = Modificadas(plan).Select(f => (Cambio<CantidadTorre>)f.Datos!).ToList();
        await ServicioCantidadesPrevistas.ValidarAsync(ctx.Db,
            cambios.Select(c => new ServicioCantidadesPrevistas.Captura(Identidad(c.Nuevo), c.Nuevo.CantidadPrevista)).ToList(), ct);
        var actuales = (await ctx.Db.CantidadesTorre.ToListAsync(ct)).ToLookup(Identidad);
        foreach (var cambio in cambios) Integrar(ctx, cambio, Unico(actuales[Identidad(cambio.Nuevo)]));
    }
}

