using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

public sealed class PlantillaProduccion : PlantillaCostos
{
    public override string Clave => Produccion;
    public override string Nombre => "Producción";
    public override int Orden => 70;
    protected override HojaPlantilla Hoja { get; } = new("Produccion", "Avance diario. Identidad: fecha, ítem, torre, pata, tiro y cuadrilla.", new[]
    {
        Col("fecha", "Fecha", TipoColumna.Fecha, true), Ref("item", "Ítem", "items-pago", true),
        Ref("torre", "Torre", "torres-costo"), Ref("pata", "Pata", "patas-costo"), Ref("tiro", "Tiro", "tiros-costo"),
        Ref("cuadrilla", "Cuadrilla", "cuadrillas"), Col("cantidad", "Cantidad", TipoColumna.Decimal, true),
        Col("descripcion", "Descripción")
    });

    private readonly record struct Identidad(DateTime Fecha, int Item, int? Torre, int? Pata, int? Tiro, string Cuadrilla);
    private static Identidad ClaveDe(RegistroCantidad r) =>
        new(r.Fecha.Date, r.ItemPagoId, r.ElementoTrazadoId, r.ComponenteId, r.TiroId, Norm(r.Cuadrilla));

    public override async Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct)
    {
        var r = await ReferenciasCostos.CargarAsync(db, ct);
        var items = r.Items.Where(i => !i.EsCapitulo).ToDictionary(i => i.Id, i => i.Codigo);
        var torres = r.Torres.ToDictionary(t => t.Id, t => t.Codigo);
        var patas = r.Patas.ToDictionary(p => p.Id, p => p.Nombre);
        var tiros = r.Tiros.ToDictionary(t => t.Id, t => t.Nombre);
        return (await db.RegistrosCantidad.AsNoTracking().OrderBy(p => p.Fecha).ThenBy(p => p.Id).ToListAsync(ct))
            .Where(p => items.ContainsKey(p.ItemPagoId) &&
                (p.ElementoTrazadoId is null || torres.ContainsKey(p.ElementoTrazadoId.Value)) &&
                (p.TiroId is null || tiros.ContainsKey(p.TiroId.Value)) &&
                (p.ComponenteId is null || (p.ElementoTrazadoId.HasValue && patas.ContainsKey(p.ComponenteId.Value))))
            .Select(p => new Dictionary<string, object?>
            {
                ["fecha"] = p.Fecha.Date, ["item"] = items[p.ItemPagoId],
                ["torre"] = p.ElementoTrazadoId is int t ? torres[t] : null,
                ["pata"] = p.ComponenteId is int c ? ReferenciasCostos.EtiquetaPata(torres[p.ElementoTrazadoId!.Value], patas[c]) : null,
                ["tiro"] = p.TiroId is int tiro ? tiros[tiro] : null,
                ["cuadrilla"] = p.Cuadrilla, ["cantidad"] = p.Cantidad, ["descripcion"] = p.Descripcion
            }).ToList();
    }

    public override async Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct)
    {
        var plan = NuevoPlan();
        var r = await ReferenciasCostos.CargarAsync(ctx.Db, ct);
        var actuales = (await ctx.Db.RegistrosCantidad.AsNoTracking().ToListAsync(ct)).ToLookup(ClaveDe);
        var cuadrillas = r.Cuadrillas.Concat(actuales.Select(g => g.Key.Cuadrilla)).ToHashSet();
        var vistos = new HashSet<Identidad>();
        foreach (var f in Filas(hojas))
        {
            var errores = plan.Errores.Count;
            var codigoItem = f.Texto("item")!;
            int? itemId = IdPorCodigo(r.Items.Where(i => !i.EsCapitulo), i => i.Codigo, i => i.Id, codigoItem);
            if (itemId is null)
            {
                Error(plan, f, "item", "Ítem hoja inexistente o ambiguo.");
                continue;
            }
            var ambito = r.Items.Single(i => i.Id == itemId).Ambito;
            var codigoTorre = f.Texto("torre");
            var nombrePata = f.Texto("pata");
            var nombreTiro = f.Texto("tiro");
            int? torre = codigoTorre is null ? null : IdPorCodigo(r.Torres, t => t.Codigo, t => t.Id, codigoTorre);
            int? pata = torre.HasValue && nombrePata is not null ? r.PataId(torre.Value, codigoTorre!, nombrePata) : null;
            int? tiro = nombreTiro is null ? null : IdPorCodigo(r.Tiros, t => t.Nombre, t => t.Id, nombreTiro);
            bool requiereTorre = ambito is AmbitoItemPago.Torre or AmbitoItemPago.Pata;
            if (requiereTorre ? torre is null : codigoTorre is not null)
                Error(plan, f, "torre", requiereTorre ? "Seleccione una torre existente y no ambigua." : "El ámbito del ítem no admite torre.");
            if (nombrePata is not null && (ambito != AmbitoItemPago.Pata || pata is null))
                Error(plan, f, "pata", "La pata debe pertenecer a la torre y el ítem debe tener ámbito Pata.");
            if (ambito == AmbitoItemPago.Tiro ? tiro is null : nombreTiro is not null)
                Error(plan, f, "tiro", ambito == AmbitoItemPago.Tiro ? "Seleccione un tiro existente y no ambiguo." : "El ámbito del ítem no admite tiro.");

            var cuadrilla = Norm(f.Texto("cuadrilla"));
            if (cuadrilla.Length > 0 && !cuadrillas.Contains(cuadrilla))
                Error(plan, f, "cuadrilla", "La cuadrilla no existe en el proyecto.");
            if (f.Fecha("fecha") is not DateTime fecha || Decimal(plan, f, "cantidad", admitirNegativo: true) is not decimal cantidad ||
                plan.Errores.Count != errores) continue;

            var clave = new Identidad(fecha, itemId.Value, torre, pata, tiro, cuadrilla);
            if (!vistos.Add(clave) || actuales[clave].Count() > 1)
            {
                Error(plan, f, "item", "Identidad repetida en la hoja o varias capturas coincidentes en el proyecto.");
                continue;
            }
            var anterior = actuales[clave].SingleOrDefault();
            var nuevo = Copiar(ctx.Db, anterior);
            nuevo.Fecha = fecha;
            nuevo.ItemPagoId = itemId.Value;
            nuevo.ElementoTrazadoId = torre;
            nuevo.ComponenteId = pata;
            nuevo.TiroId = tiro;
            nuevo.Cuadrilla = anterior?.Cuadrilla ?? (cuadrilla.Length == 0 ? null : cuadrilla);
            nuevo.Cantidad = cantidad;
            nuevo.Descripcion = f.Texto("descripcion") ?? nuevo.Descripcion;
            if (anterior is null) nuevo.Origen = "Plantilla: Produccion";
            Planear(plan, f, $"{fecha:yyyy-MM-dd} · {codigoItem} · {codigoTorre ?? nombreTiro ?? "Global"} · {nombrePata} · {cuadrilla}",
                nuevo, anterior, anterior?.Id, ctx.Db);
        }
        return plan;
    }

    public override async Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var referencias = await ReferenciasCostos.CargarAsync(ctx.Db, ct);
        var items = referencias.Items.Where(i => !i.EsCapitulo).ToDictionary(i => i.Id, i => i.Ambito);
        var torres = referencias.Torres.Select(t => t.Id).ToHashSet();
        var patas = referencias.Patas.ToDictionary(p => p.Id, p => p.ElementoTrazadoId);
        var tiros = referencias.Tiros.Select(t => t.Id).ToHashSet();
        var actuales = (await ctx.Db.RegistrosCantidad.ToListAsync(ct)).ToLookup(ClaveDe);
        foreach (var f in Modificadas(plan))
        {
            var cambio = (Cambio<RegistroCantidad>)f.Datos!;
            var clave = ClaveDe(cambio.Nuevo);
            if (!items.TryGetValue(clave.Item, out var ambito) ||
                (ambito is AmbitoItemPago.Torre or AmbitoItemPago.Pata
                    ? clave.Torre is not int t || !torres.Contains(t)
                    : clave.Torre is not null) ||
                (clave.Pata is int p && (ambito != AmbitoItemPago.Pata || patas.GetValueOrDefault(p) != clave.Torre)) ||
                (ambito == AmbitoItemPago.Tiro ? clave.Tiro is not int tiro || !tiros.Contains(tiro) : clave.Tiro is not null))
                throw new InvalidOperationException("Cambió algún ítem o lugar desde la vista previa. Cargue el archivo de nuevo para validar.");

            Integrar(ctx, cambio, Unico(actuales[clave]));
        }
    }
}

