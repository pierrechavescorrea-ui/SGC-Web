using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Costos;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

internal sealed class ReferenciasCostos
{
    public required IReadOnlyList<ItemPago> Items { get; init; }
    public required IReadOnlyList<ElementoTrazado> Torres { get; init; }
    public required IReadOnlyList<Componente> Patas { get; init; }
    public required IReadOnlyList<Tiro> Tiros { get; init; }
    public required HashSet<string> Cuadrillas { get; init; }

    internal static async Task<ReferenciasCostos> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var catalogo = await db.Catalogos.AsNoTracking().Where(c => c.Activo && c.Tipo == InstantaneaCostos.CatalogoCuadrilla)
            .Select(c => c.Nombre).ToListAsync(ct);
        return new()
        {
            Items = await db.ItemsPago.AsNoTracking().ToListAsync(ct),
            Torres = await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct),
            Patas = (await db.Componentes.AsNoTracking().Where(p => !p.Eliminado).ToListAsync(ct))
                .Where(ServicioCantidadesPrevistas.EsPata).ToList(),
            Tiros = await db.Tiros.AsNoTracking().Where(t => !t.Eliminado).ToListAsync(ct),
            Cuadrillas = catalogo.Select(PlantillaCostos.Norm).Where(c => c.Length > 0).ToHashSet()
        };
    }

    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ListasAsync(AppDbContext db, CancellationToken ct)
    {
        var r = await CargarAsync(db, ct);
        var codigos = r.Torres.ToDictionary(t => t.Id, t => t.Codigo);
        var actividades = await db.ActividadesObra.AsNoTracking().Where(a => a.Activo).Select(a => a.Nombre).ToListAsync(ct);
        static IReadOnlyList<string> Lista(IEnumerable<string> valores) =>
            valores.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        return new Dictionary<string, IReadOnlyList<string>>
        {
            ["items-pago"] = Lista(r.Items.Where(i => !i.EsCapitulo).Select(i => i.Codigo)),
            ["capitulos-pago"] = Lista(r.Items.Where(i => i.EsCapitulo).Select(i => i.Codigo)),
            ["torres-costo"] = Lista(codigos.Values),
            ["patas-costo"] = Lista(r.Patas.Where(p => codigos.ContainsKey(p.ElementoTrazadoId))
                .Select(p => EtiquetaPata(codigos[p.ElementoTrazadoId], p.Nombre))),
            ["tiros-costo"] = Lista(r.Tiros.Select(t => t.Nombre)),
            ["cuadrillas"] = Lista(r.Cuadrillas),
            ["actividades-obra"] = Lista(actividades)
        };
    }

    public static string EtiquetaPata(string torre, string pata) => $"{torre} · {pata}";

    public int? PataId(int torreId, string codigoTorre, string texto)
    {
        var prefijo = PlantillaCostos.Norm(codigoTorre) + " · ";
        var valor = PlantillaCostos.Norm(texto);
        if (valor.StartsWith(prefijo, StringComparison.Ordinal)) valor = valor[prefijo.Length..].Trim();
        var coincidencias = Patas.Where(p => p.ElementoTrazadoId == torreId && PlantillaCostos.Norm(p.Nombre) == valor)
            .Select(p => p.Id).Take(2).ToArray();
        return coincidencias.Length == 1 ? coincidencias[0] : null;
    }
}

