using System.Data;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Costos;

public static class ServicioCantidadesPrevistas
{
    public readonly record struct Clave(int TorreId, int? PataId, int ItemId);

    public sealed record Captura(Clave Clave, decimal Cantidad);

    public static bool EsPata(Componente componente) =>
        !componente.Eliminado && !componente.EsHojaDeVida &&
        componente.Nombre.Trim().StartsWith("Pata ", StringComparison.OrdinalIgnoreCase);

    // Con una transacción del llamador, este conserva la responsabilidad de confirmar o revertir.
    public static async Task GuardarAsync(AppDbContext db, IReadOnlyCollection<Captura> capturas,
        IReadOnlyCollection<int> eliminarIds, CancellationToken ct = default)
    {
        if (capturas.Count == 0 && eliminarIds.Count == 0) return;

        await using var transaccion = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        await ValidarAsync(db, capturas, ct);

        var torres = capturas.Select(c => c.Clave.TorreId).Distinct().ToArray();
        var items = capturas.Select(c => c.Clave.ItemId).Distinct().ToArray();
        var claves = capturas.Select(c => c.Clave).ToHashSet();
        var filas = await db.CantidadesTorre
            .Where(x => eliminarIds.Contains(x.Id) || (torres.Contains(x.ElementoTrazadoId) && items.Contains(x.ItemPagoId)))
            .ToListAsync(ct);

        var porClave = filas.Where(x => !eliminarIds.Contains(x.Id))
            .GroupBy(x => new Clave(x.ElementoTrazadoId, x.ComponenteId, x.ItemPagoId))
            .Where(g => claves.Contains(g.Key))
            .ToDictionary(g => g.Key, g => g.ToList());
        if (porClave.Values.Any(g => g.Count > 1))
            throw new InvalidOperationException("Hay cantidades duplicadas en la base. Marque para eliminar las filas sobrantes antes de corregir la cantidad.");

        db.CantidadesTorre.RemoveRange(filas.Where(x => eliminarIds.Contains(x.Id)));
        foreach (var captura in capturas)
        {
            var clave = captura.Clave;
            if (!porClave.TryGetValue(clave, out var existentes))
            {
                db.CantidadesTorre.Add(new CantidadTorre
                {
                    ElementoTrazadoId = clave.TorreId, ComponenteId = clave.PataId,
                    ItemPagoId = clave.ItemId, CantidadPrevista = captura.Cantidad
                });
            }
            else
            {
                existentes[0].CantidadPrevista = captura.Cantidad;
            }
        }
        await db.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
    }

    public static async Task ValidarAsync(AppDbContext db, IReadOnlyCollection<Captura> capturas,
        CancellationToken ct = default)
    {
        if (capturas.Count == 0) return;

        var torres = capturas.Select(c => c.Clave.TorreId).Distinct().ToArray();
        var items = capturas.Select(c => c.Clave.ItemId).Distinct().ToArray();
        var patas = capturas.Select(c => c.Clave.PataId).OfType<int>().Distinct().ToArray();
        var torresVivas = (await db.ElementosTrazado.AsNoTracking().Where(t => !t.Eliminado && torres.Contains(t.Id))
            .Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var itemsHoja = await db.ItemsPago.AsNoTracking().Where(i => !i.EsCapitulo && items.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Ambito, ct);
        var patasVivas = (await db.Componentes.AsNoTracking().Where(p => patas.Contains(p.Id) && !p.Eliminado)
            .ToListAsync(ct)).Where(EsPata).ToDictionary(p => p.Id, p => p.ElementoTrazadoId);

        if (capturas.Select(c => c.Clave).Distinct().Count() != capturas.Count)
            throw new InvalidOperationException("Repitió una combinación de torre, pata e ítem entre los cambios. Deje una sola fila por combinación.");

        foreach (var captura in capturas)
        {
            var clave = captura.Clave;
            if (!torresVivas.Contains(clave.TorreId))
                throw new InvalidOperationException("Seleccione una torre existente que no esté en la Papelera.");
            if (!itemsHoja.TryGetValue(clave.ItemId, out var ambito))
                throw new InvalidOperationException("Seleccione un ítem de pago existente que no sea un capítulo.");
            if (clave.PataId is int pataId &&
                (ambito != AmbitoItemPago.Pata || patasVivas.GetValueOrDefault(pataId) != clave.TorreId))
                throw new InvalidOperationException("La pata debe estar activa, pertenecer a la torre y usarse en un ítem de ámbito Pata.");
            if (captura.Cantidad < 0 || captura.Cantidad >= 100_000_000_000_000m || decimal.Round(captura.Cantidad, 4) != captura.Cantidad)
                throw new InvalidOperationException("La cantidad debe ser no negativa, con hasta 14 dígitos enteros y 4 decimales.");
        }
    }
}

