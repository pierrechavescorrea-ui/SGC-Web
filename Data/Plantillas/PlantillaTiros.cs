using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

// Adaptador de referencia (el más simple): Tiros de tendido. Identidad = Nombre del tiro.
// Copiar este patrón para cada módulo nuevo: Hojas → FilasActualesAsync → ValidarAsync (sin escribir) → AplicarAsync.
public class PlantillaTiros : IPlantillaModulo
{
    public string Clave => "tiros";
    public string Nombre => "Tiros";
    public int Orden => 20;                                    // después de Catálogos (10), antes de Torres (30)

    private const string Hoja = "Tiros";
    public IReadOnlyList<HojaPlantilla> Hojas { get; } = new[]
    {
        new HojaPlantilla(Hoja, "Tiros de tendido (conjuntos de torres). Las torres se asignan a su tiro en la hoja Torres.", new[]
        {
            new ColumnaPlantilla("nombre", "Tiro", Obligatoria: true, Ayuda: "Nombre o número del tiro; identifica la fila."),
            new ColumnaPlantilla("orden", "Orden", TipoColumna.Entero, Ayuda: "Orden en la línea (1, 2, 3…). Vacío = al final."),
        })
    };

    // Tiro no tiene filtro global de Papelera: se filtra a mano. Un tiro en la Papelera con el mismo nombre se revive.
    private sealed record DatosTiro(int? Id, string Nombre, int? Orden, bool Revivido = false);

    public async Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct) =>
        (await db.Tiros.AsNoTracking().Where(t => !t.Eliminado).OrderBy(t => t.Orden).ThenBy(t => t.Nombre).ToListAsync(ct))
            .Select(t => new Dictionary<string, object?> { ["nombre"] = t.Nombre, ["orden"] = t.Orden }).ToList();

    public async Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct)
    {
        var plan = new PlanModulo { Clave = Clave, Nombre = Nombre };
        if (!hojas.TryGetValue(Hoja, out var filas)) return plan;
        var existentes = (await ctx.Db.Tiros.AsNoTracking().ToListAsync(ct))
            .GroupBy(t => t.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Eliminado).First(), StringComparer.OrdinalIgnoreCase);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
        {
            var nombre = f.Texto("nombre")!;
            if (!vistos.Add(nombre)) { plan.Errores.Add(new(Hoja, f.NumeroFila, "Tiro", $"«{nombre}» está repetido en la hoja.")); continue; }
            var orden = f.Entero("orden");
            if (orden is < 0) { plan.Errores.Add(new(Hoja, f.NumeroFila, "Orden", "No puede ser negativo.")); continue; }
            if (existentes.TryGetValue(nombre, out var t))
            {
                var cambiaOrden = orden is int o && o != t.Orden;
                var pf = new PlanFila { Hoja = Hoja, NumeroFila = f.NumeroFila, Codigo = nombre, Datos = new DatosTiro(t.Id, nombre, orden, t.Eliminado),
                    Operacion = cambiaOrden || t.Eliminado ? OperacionFila.Actualizacion : OperacionFila.SinCambios };
                if (t.Eliminado) { pf.Cambios.Add("Estado: En papelera → Activo"); ctx.Crear("TiroRevivido", nombre); }
                if (cambiaOrden) pf.Cambios.Add($"Orden: {t.Orden} → {orden}");
                plan.Filas.Add(pf);
            }
            else
            {
                ctx.Crear("Tiro", nombre);
                plan.Filas.Add(new PlanFila { Hoja = Hoja, NumeroFila = f.NumeroFila, Codigo = nombre, Operacion = OperacionFila.Alta, Datos = new DatosTiro(null, nombre, orden) });
            }
        }
        return plan;
    }

    public async Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var siguiente = (await ctx.Db.Tiros.Where(t => !t.Eliminado).MaxAsync(t => (int?)t.Orden, ct) ?? 0) + 1;
        foreach (var f in plan.Filas)
        {
            var d = (DatosTiro)f.Datos!;
            if (f.Operacion == OperacionFila.Alta)
            {
                var t = new Tiro { Nombre = d.Nombre, Orden = d.Orden ?? siguiente++ };
                ctx.Db.Tiros.Add(t);
                ctx.Alta(t);
            }
            else if (f.Operacion == OperacionFila.Actualizacion)
            {
                var t = await ctx.Db.Tiros.FirstAsync(x => x.Id == d.Id, ct);
                ctx.AntesDeActualizar(t);
                if (d.Revivido) { t.Eliminado = false; t.FechaEliminacion = null; }
                if (d.Orden is int o) t.Orden = o;
            }
        }
    }
}
