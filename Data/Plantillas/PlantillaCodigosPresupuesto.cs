using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

public sealed class PlantillaCodigosPresupuesto : PlantillaCostos
{
    public override string Clave => Presupuesto;
    public override string Nombre => "Códigos de presupuesto";
    public override int Orden => 50;
    protected override HojaPlantilla Hoja { get; } = new("CodigosPresupuesto", "Códigos, grupos y montos iniciales del presupuesto.", new[]
    {
        Col("codigo", "Código", obligatoria: true), Col("nombre", "Nombre", obligatoria: true),
        EnumCol<GrupoPresupuesto>("grupo", "Grupo", obligatoria: true),
        Col("inicial", "Presupuesto inicial", TipoColumna.Decimal, obligatoria: true), Col("orden", "Orden", TipoColumna.Entero)
    });

    public override async Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct) =>
        (await db.CodigosPresupuesto.AsNoTracking().OrderBy(c => c.Orden).ThenBy(c => c.Codigo).ToListAsync(ct))
        .Select(c => new Dictionary<string, object?>
        {
            ["codigo"] = c.Codigo, ["nombre"] = c.Nombre, ["grupo"] = c.Grupo.ToString(),
            ["inicial"] = c.PresupuestoInicial, ["orden"] = c.Orden
        }).ToList();

    public override async Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct)
    {
        var plan = NuevoPlan();
        var activos = (await ctx.Db.CodigosPresupuesto.AsNoTracking().ToListAsync(ct)).ToLookup(c => Norm(c.Codigo));
        var vistos = new HashSet<string>();
        foreach (var f in Filas(hojas))
        {
            var codigo = Norm(f.Texto("codigo"));
            if (!vistos.Add(codigo) || activos[codigo].Count() > 1)
            {
                Error(plan, f, "codigo", "Código repetido en la hoja o ambiguo en el proyecto.");
                continue;
            }
            var anterior = activos[codigo].SingleOrDefault();
            var nuevo = Copiar(ctx.Db, anterior);
            nuevo.Codigo = anterior?.Codigo ?? codigo;
            nuevo.Nombre = f.Texto("nombre") ?? nuevo.Nombre;
            if (Enum.TryParse<GrupoPresupuesto>(f.Texto("grupo"), true, out var grupo)) nuevo.Grupo = grupo;
            nuevo.PresupuestoInicial = Decimal(plan, f, "inicial", 2) ?? nuevo.PresupuestoInicial;
            if (f.Tiene("orden"))
            {
                if (f.Entero("orden") is int orden && orden >= 0) nuevo.Orden = orden;
                else Error(plan, f, "orden", "El orden debe ser un entero no negativo.");
            }
            Planear(plan, f, codigo, nuevo, anterior, anterior?.Id, ctx.Db);
        }
        return plan;
    }

    public override async Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var porCodigo = (await ctx.Db.CodigosPresupuesto.ToListAsync(ct)).ToLookup(c => Norm(c.Codigo));
        foreach (var f in Modificadas(plan))
            Integrar(ctx, (Cambio<CodigoPresupuesto>)f.Datos!, Unico(porCodigo[f.Codigo]));
    }
}

