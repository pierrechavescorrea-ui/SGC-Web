using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Catalogos;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

// Adaptador de Catálogos: definiciones (DefinicionCatalogo) e ítems (ItemCatalogo).
// Identidad: definición = Nombre (sin mayúsculas ni espacios extremos); ítem = (Tipo, Nombre) sin mayúsculas.
// Los ítems nuevos se anuncian en ContextoValidacion para que las hojas de módulos posteriores acepten sus valores.
public class PlantillaCatalogos : IPlantillaModulo
{
    private const string HojaCatalogos = "Catalogos";
    private const string HojaItems = "ItemsCatalogo";
    private const string TipoLocalizacion = "Localización";

    // Singleton de DI: después de aplicar hay que soltar su caché de estilos (colores/forma de los valores).
    private readonly EstilosCatalogo _estilos;

    public PlantillaCatalogos(EstilosCatalogo estilos) => _estilos = estilos;

    public string Clave => "catalogos";
    public string Nombre => "Catálogos";
    public int Orden => 10;                                    // primero: los demás módulos usan sus valores

    public IReadOnlyList<HojaPlantilla> Hojas { get; } = new[]
    {
        new HojaPlantilla(HojaCatalogos,
            "Catálogos maestros (definiciones). El nombre identifica el catálogo y es el tipo de sus ítems.",
            new[]
            {
                new ColumnaPlantilla("nombre", "Catálogo", Obligatoria: true, Ayuda: "Nombre del catálogo; identifica la fila."),
                new ColumnaPlantilla("entidad", "Entidad asociada", Ayuda: "Texto informativo: a qué entidad o propiedad se aplica."),
                new ColumnaPlantilla("activo", "Activo", TipoColumna.SiNo, Ayuda: "Vacío = activo. Un catálogo predeterminado no se puede desactivar."),
                new ColumnaPlantilla("orden", "Orden", TipoColumna.Entero, Ayuda: "Orden en la pantalla de Catálogos. Vacío = al final."),
            }),
        new HojaPlantilla(HojaItems,
            "Valores de cada catálogo. El catálogo debe existir o venir en la hoja Catalogos del mismo libro.",
            new[]
            {
                new ColumnaPlantilla("catalogo", "Catálogo", Obligatoria: true, Ayuda: "Nombre del catálogo (hoja Catalogos o ya existente)."),
                new ColumnaPlantilla("valor", "Valor", Obligatoria: true, Ayuda: "Nombre del ítem; con el catálogo identifica la fila."),
                new ColumnaPlantilla("descripcion", "Descripción", Ayuda: "Texto libre del ítem."),
                new ColumnaPlantilla("activo", "Activo", TipoColumna.SiNo, Ayuda: "Vacío = activo."),
            }),
    };

    // Revivir: una definición en la Papelera con el mismo nombre vuelve (como torres y tiros); así deshacer una
    // carga y volver a cargarla funciona.
    private sealed record DatosDefinicion(int? Id, string Nombre, string? Entidad, bool? Activo, int? Orden, bool Revivir = false);
    private sealed record DatosItem(int? Id, string Tipo, string Nombre, string? Descripcion, bool? Activo);

    private static string ClaveItem(string tipo, string valor) =>
        tipo.Trim().ToLowerInvariant() + "\u001f" + valor.Trim().ToLowerInvariant();

    public async Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct)
    {
        if (string.Equals(hoja, HojaCatalogos, StringComparison.OrdinalIgnoreCase))
            return (await db.DefinicionesCatalogo.AsNoTracking().IgnoreQueryFilters()
                    .Where(d => !d.Eliminado).OrderBy(d => d.Orden).ThenBy(d => d.Nombre).ToListAsync(ct))
                .Select(d => new Dictionary<string, object?>
                {
                    ["nombre"] = d.Nombre,
                    ["entidad"] = d.EntidadAsociada,
                    ["activo"] = d.Activo,
                    ["orden"] = d.Orden,
                }).ToList();

        if (string.Equals(hoja, HojaItems, StringComparison.OrdinalIgnoreCase))
            return (await db.Catalogos.AsNoTracking().OrderBy(i => i.Tipo).ThenBy(i => i.Nombre).ToListAsync(ct))
                .Select(i => new Dictionary<string, object?>
                {
                    ["catalogo"] = i.Tipo,
                    ["valor"] = i.Nombre,
                    ["descripcion"] = i.Descripcion,
                    ["activo"] = i.Activo,
                }).ToList();

        return Array.Empty<Dictionary<string, object?>>();
    }

    public async Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct)
    {
        var plan = new PlanModulo { Clave = Clave, Nombre = Nombre };

        var definiciones = await ctx.Db.DefinicionesCatalogo.AsNoTracking().IgnoreQueryFilters().ToListAsync(ct);

        // Nombre canónico de cada catálogo (definición vigente o, si no, el Tipo de un ítem ya existente).
        var canonicos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in definiciones.Where(d => !d.Eliminado))
            canonicos.TryAdd(d.Nombre.Trim(), d.Nombre.Trim());

        // ---------- Hoja Catalogos ----------
        if (hojas.TryGetValue(HojaCatalogos, out var filasDefiniciones))
        {
            var existentes = definiciones
                .OrderBy(d => d.Eliminado)
                .GroupBy(d => d.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var siguiente = (definiciones.Where(d => !d.Eliminado).Max(d => (int?)d.Orden) ?? 0) + 1;

            foreach (var f in filasDefiniciones)
            {
                var nombre = f.Texto("nombre")!;
                if (!vistos.Add(nombre))
                {
                    plan.Errores.Add(new(HojaCatalogos, f.NumeroFila, "Catálogo", $"«{nombre}» está repetido en la hoja."));
                    continue;
                }

                var entidad = f.Texto("entidad");
                var activo = f.SiNo("activo");
                var orden = f.Entero("orden");
                if (orden is < 0)
                {
                    plan.Errores.Add(new(HojaCatalogos, f.NumeroFila, "Orden", "No puede ser negativo."));
                    continue;
                }

                if (existentes.TryGetValue(nombre, out var d))
                {
                    if (activo == false && d.EsPredeterminado && d.Activo)
                    {
                        plan.Errores.Add(new(HojaCatalogos, f.NumeroFila, "Activo", "Un catálogo predeterminado no se puede desactivar."));
                        continue;
                    }

                    var cambios = new List<string>();
                    if (d.Eliminado) { cambios.Add("Estado: En papelera → Activo"); canonicos[nombre] = d.Nombre.Trim(); }
                    if (entidad is not null && !string.Equals(entidad, d.EntidadAsociada, StringComparison.Ordinal))
                        cambios.Add($"Entidad asociada: {d.EntidadAsociada} → {entidad}");
                    if (activo is bool a && a != d.Activo)
                        cambios.Add($"Activo: {(d.Activo ? "Sí" : "No")} → {(a ? "Sí" : "No")}");
                    if (orden is int o && o != d.Orden)
                        cambios.Add($"Orden: {d.Orden} → {o}");

                    var pf = new PlanFila
                    {
                        Hoja = HojaCatalogos, NumeroFila = f.NumeroFila, Codigo = nombre,
                        Operacion = cambios.Count > 0 ? OperacionFila.Actualizacion : OperacionFila.SinCambios,
                        Datos = new DatosDefinicion(d.Id, d.Nombre, entidad, activo, orden, d.Eliminado),
                    };
                    pf.Cambios.AddRange(cambios);
                    plan.Filas.Add(pf);
                }
                else
                {
                    var ordenFinal = orden ?? siguiente++;
                    plan.Filas.Add(new PlanFila
                    {
                        Hoja = HojaCatalogos, NumeroFila = f.NumeroFila, Codigo = nombre, Operacion = OperacionFila.Alta,
                        Datos = new DatosDefinicion(null, nombre, entidad, activo ?? true, ordenFinal),
                    });
                    canonicos[nombre] = nombre;
                }
            }
        }

        // ---------- Hoja ItemsCatalogo ----------
        if (hojas.TryGetValue(HojaItems, out var filasItems))
        {
            var items = await ctx.Db.Catalogos.AsNoTracking().ToListAsync(ct);
            // Tipos heredados sin definición: se aceptan, salvo que la definición exista en la Papelera.
            var nombresDefinicion = definiciones.Select(d => d.Nombre.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var tipo in items.Select(i => i.Tipo.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                if (!nombresDefinicion.Contains(tipo)) canonicos.TryAdd(tipo, tipo);

            var existentes = new Dictionary<string, ItemCatalogo>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in items) existentes.TryAdd(ClaveItem(i.Tipo, i.Nombre), i);
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in filasItems)
            {
                var catalogo = f.Texto("catalogo")!;
                var valor = f.Texto("valor")!;
                if (!canonicos.TryGetValue(catalogo, out var tipo))
                {
                    plan.Errores.Add(new(HojaItems, f.NumeroFila, "Catálogo", $"«{catalogo}» no existe en la base ni en la hoja {HojaCatalogos}."));
                    continue;
                }

                var clave = ClaveItem(tipo, valor);
                if (!vistos.Add(clave))
                {
                    plan.Errores.Add(new(HojaItems, f.NumeroFila, "Valor", $"«{valor}» está repetido para el catálogo «{tipo}» en la hoja."));
                    continue;
                }

                var descripcion = f.Texto("descripcion");
                var activo = f.SiNo("activo");

                if (existentes.TryGetValue(clave, out var i))
                {
                    var cambios = new List<string>();
                    if (descripcion is not null && !string.Equals(descripcion, i.Descripcion, StringComparison.Ordinal))
                        cambios.Add($"Descripción: {i.Descripcion} → {descripcion}");
                    if (activo is bool a && a != i.Activo)
                        cambios.Add($"Activo: {(i.Activo ? "Sí" : "No")} → {(a ? "Sí" : "No")}");

                    // Los ítems de Localización de una torre son espejo del Trazado: aquí solo se permiten si no cambian.
                    var esLocalizacionVinculada = string.Equals(i.Tipo.Trim(), TipoLocalizacion, StringComparison.OrdinalIgnoreCase)
                                                  && i.ElementoTrazadoId is not null;
                    if (esLocalizacionVinculada && cambios.Count > 0)
                    {
                        plan.Errores.Add(new(HojaItems, f.NumeroFila, "", "Los ítems de Localización vinculados a una torre los administra el Trazado y no se pueden modificar."));
                        continue;
                    }

                    var pf = new PlanFila
                    {
                        Hoja = HojaItems, NumeroFila = f.NumeroFila, Codigo = valor,
                        Operacion = cambios.Count > 0 ? OperacionFila.Actualizacion : OperacionFila.SinCambios,
                        Datos = new DatosItem(i.Id, i.Tipo, i.Nombre, descripcion, activo),
                    };
                    pf.Cambios.AddRange(cambios);
                    plan.Filas.Add(pf);

                    if (activo ?? i.Activo) ctx.AgregarACatalogo(i.Tipo, i.Nombre);
                }
                else
                {
                    var activoFinal = activo ?? true;
                    plan.Filas.Add(new PlanFila
                    {
                        Hoja = HojaItems, NumeroFila = f.NumeroFila, Codigo = valor, Operacion = OperacionFila.Alta,
                        Datos = new DatosItem(null, tipo, valor, descripcion, activoFinal),
                    });
                    if (activoFinal) ctx.AgregarACatalogo(tipo, valor);
                }
            }
        }

        return plan;
    }

    public async Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        var siguiente = (await ctx.Db.DefinicionesCatalogo.IgnoreQueryFilters().MaxAsync(d => (int?)d.Orden, ct) ?? 0) + 1;

        foreach (var f in plan.Filas)
        {
            switch (f.Datos)
            {
                case DatosDefinicion d when f.Operacion == OperacionFila.Alta:
                {
                    var def = new DefinicionCatalogo
                    {
                        Nombre = d.Nombre,
                        EntidadAsociada = d.Entidad ?? "",
                        EsPredeterminado = false,
                        Activo = d.Activo ?? true,
                        Orden = d.Orden ?? siguiente++,
                    };
                    ctx.Db.DefinicionesCatalogo.Add(def);
                    ctx.Alta(def);
                    break;
                }
                case DatosDefinicion d when f.Operacion == OperacionFila.Actualizacion:
                {
                    var def = await ctx.Db.DefinicionesCatalogo.IgnoreQueryFilters().FirstAsync(x => x.Id == d.Id, ct);
                    ctx.AntesDeActualizar(def);
                    if (d.Revivir) { def.Eliminado = false; def.FechaEliminacion = null; }
                    if (d.Entidad is not null) def.EntidadAsociada = d.Entidad;
                    if (d.Activo is bool a) def.Activo = a;
                    if (d.Orden is int o) def.Orden = o;
                    break;
                }
                case DatosItem it when f.Operacion == OperacionFila.Alta:
                {
                    var item = new ItemCatalogo
                    {
                        Tipo = it.Tipo,
                        Nombre = it.Nombre,
                        Descripcion = it.Descripcion ?? "",
                        Activo = it.Activo ?? true,
                        Forma = "pildora",
                        Negrita = true,
                    };
                    ctx.Db.Catalogos.Add(item);
                    ctx.Alta(item);
                    break;
                }
                case DatosItem it when f.Operacion == OperacionFila.Actualizacion:
                {
                    var item = await ctx.Db.Catalogos.FirstAsync(x => x.Id == it.Id, ct);
                    ctx.AntesDeActualizar(item);
                    if (it.Descripcion is not null) item.Descripcion = it.Descripcion;
                    if (it.Activo is bool a) item.Activo = a;
                    break;
                }
            }
        }
    }

    // El caché de EstilosCatalogo es singleton: al cambiar ítems hay que soltarlo.
    public Task DespuesDeGuardarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct)
    {
        _estilos.Recargar();
        return Task.CompletedTask;
    }
}
