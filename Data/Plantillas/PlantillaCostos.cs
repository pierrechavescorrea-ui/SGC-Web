using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

public abstract class PlantillaCostos : IPlantillaModulo
{
    public const string Items = "costos-items", Presupuesto = "costos-presupuesto",
        Previstas = "costos-previstas", Produccion = "costos-produccion";

    public static bool EsDeCostos(string clave) => clave is Items or Presupuesto or Previstas or Produccion;

    public abstract string Clave { get; }
    public abstract string Nombre { get; }
    public abstract int Orden { get; }
    protected abstract HojaPlantilla Hoja { get; }
    public IReadOnlyList<HojaPlantilla> Hojas => new[] { Hoja };

    public abstract Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct);
    public abstract Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct);
    public abstract Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct);

    protected PlanModulo NuevoPlan() => new() { Clave = Clave, Nombre = Nombre };
    protected IReadOnlyList<FilaLeida> Filas(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas) =>
        hojas.TryGetValue(Hoja.Nombre, out var filas) ? filas : Array.Empty<FilaLeida>();

    protected void Error(PlanModulo plan, FilaLeida fila, string columna, string mensaje) =>
        plan.Errores.Add(new(Hoja.Nombre, fila.NumeroFila, Hoja.Columnas.First(c => c.Clave == columna).Titulo, mensaje));

    internal static string Norm(string? texto) => (texto ?? "").Trim().ToUpperInvariant();

    protected static ColumnaPlantilla Col(string clave, string titulo, TipoColumna tipo = TipoColumna.Texto, bool obligatoria = false) =>
        new(clave, titulo, tipo, obligatoria);
    protected static ColumnaPlantilla Ref(string clave, string titulo, string entidad, bool obligatoria = false) =>
        new(clave, titulo, TipoColumna.Lista, obligatoria, ListaEntidad: entidad);
    protected static ColumnaPlantilla EnumCol<T>(string clave, string titulo, bool obligatoria = false) where T : struct, Enum =>
        new(clave, titulo, TipoColumna.Lista, obligatoria, ListaFija: Enum.GetNames<T>());

    protected decimal? Decimal(PlanModulo plan, FilaLeida fila, string clave, int escala = 4, bool admitirNegativo = false)
    {
        if (!fila.Tiene(clave)) return null;
        decimal maximo = escala == 2 ? 10_000_000_000_000_000m : 100_000_000_000_000m;
        if (decimal.TryParse(fila.Texto(clave), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) &&
            d > -maximo && d < maximo && (admitirNegativo || d >= 0) && decimal.Round(d, escala) == d)
            return d;
        Error(plan, fila, clave, $"Importe o cantidad fuera de rango; use hasta {escala} decimales.");
        return null;
    }

    protected static int? IdPorCodigo<T>(IEnumerable<T> filas, Func<T, string> codigo, Func<T, int> id, string valor) =>
        filas.Where(x => Norm(codigo(x)) == Norm(valor)).Select(x => (int?)id(x)).Take(2).ToArray() switch
        {
            [int unico] => unico,
            _ => null
        };

    protected sealed record Cambio<T>(T Nuevo, int? Id, string? Antes, string? Padre = null) where T : class;

    protected void Planear<T>(PlanModulo plan, FilaLeida fila, string clave, T nuevo, T? anterior, int? id,
        AppDbContext db, string? padre = null, bool cambioPadre = false) where T : class
    {
        var antes = anterior is null ? null : ServicioPlantillas.Instantanea(db, anterior);
        var cambios = anterior is null ? new List<string>() : db.Entry(nuevo).Properties
            .Where(p => !p.Metadata.IsPrimaryKey() && !Equals(p.CurrentValue, db.Entry(anterior).Property(p.Metadata.Name).CurrentValue))
            .Select(p => $"{p.Metadata.Name}: {db.Entry(anterior).Property(p.Metadata.Name).CurrentValue} → {p.CurrentValue}").ToList();
        if (cambioPadre) cambios.Add($"Capítulo: {padre}");
        var pf = new PlanFila
        {
            Hoja = Hoja.Nombre, NumeroFila = fila.NumeroFila, Codigo = clave,
            Operacion = anterior is null ? OperacionFila.Alta : cambios.Count == 0 ? OperacionFila.SinCambios : OperacionFila.Actualizacion,
            Datos = new Cambio<T>(nuevo, id, antes, padre)
        };
        pf.Cambios.AddRange(cambios);
        plan.Filas.Add(pf);
    }

    protected static T Copiar<T>(AppDbContext db, T? entidad) where T : class, new() =>
        entidad is null ? new T() : (T)db.Entry(entidad).CurrentValues.ToObject();

    protected static T Integrar<T>(ContextoAplicacion ctx, Cambio<T> cambio, T? vigente) where T : class
    {
        if (cambio.Id is null && vigente is null)
        {
            ctx.Db.Add(cambio.Nuevo);
            ctx.Alta(cambio.Nuevo);
            return cambio.Nuevo;
        }
        if (vigente is null || cambio.Id is null ||
            !Equals(ctx.Db.Entry(vigente).Property("Id").CurrentValue, cambio.Id.Value) ||
            ServicioPlantillas.Instantanea(ctx.Db, vigente) != cambio.Antes)
            throw new InvalidOperationException("Los datos cambiaron desde la vista previa. Cargue el archivo de nuevo para validar.");

        ctx.AntesDeActualizar(vigente);
        ctx.Db.Entry(vigente).CurrentValues.SetValues(cambio.Nuevo);
        return vigente;
    }

    protected static T? Unico<T>(IEnumerable<T> filas)
    {
        var encontrados = filas.Take(2).ToArray();
        if (encontrados.Length > 1) throw new InvalidOperationException("La identidad está duplicada en el proyecto. Corrija las filas existentes.");
        return encontrados.FirstOrDefault();
    }

    protected static IEnumerable<PlanFila> Modificadas(PlanModulo plan) =>
        plan.Filas.Where(f => f.Operacion != OperacionFila.SinCambios);
}

