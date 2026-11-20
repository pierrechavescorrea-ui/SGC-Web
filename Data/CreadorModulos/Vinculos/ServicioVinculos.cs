using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Dossier;
using SigWeb.Data.Seguridad;
using SigWeb.Data.Tablas;

namespace SigWeb.Data.CreadorModulos;

public sealed record FuenteVinculo(string Clave, string Nombre, string Icono, string Modulo);
public sealed record ColumnaVinculo(string Clave, string Nombre);
public sealed record FilaVinculo(int Id, string Etiqueta, IReadOnlyDictionary<string, string> Valores)
{
    // Las cifras con formato pueden incluir separadores de miles o estar redondeadas.
    internal IReadOnlyDictionary<string, double>? NumerosFormateados { get; init; }
}
public sealed record VinculoElegido(string Fuente, int Id, string Etiqueta);
public sealed record TablaVinculo(FuenteVinculo Fuente, IReadOnlyList<ColumnaVinculo> Columnas, IReadOnlyList<FilaVinculo> Filas);

// Solo se conservan textos: ningún contexto ni motor mutable sale del scope de carga.
public sealed class ServicioVinculos : IDisposable
{
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<ServicioVinculos> log;
    private readonly RegistroModulosCreados modulos;
    private int versionFuentes;

    public ServicioVinculos(IServiceScopeFactory scopes, ILogger<ServicioVinculos> log, RegistroModulosCreados modulos)
    {
        this.scopes = scopes;
        this.log = log;
        this.modulos = modulos;
        modulos.Cambio += CambiaronModulos;
    }

    private void CambiaronModulos()
    {
        Interlocked.Increment(ref versionFuentes);
        cache.Clear(); // Las definiciones publicadas afectan a todos los proyectos.
    }

    public void Dispose() => modulos.Cambio -= CambiaronModulos;

    private sealed record Entrada(DateTimeOffset Fecha, TablaVinculo Tabla, IReadOnlyDictionary<int, string> Urls,
        IReadOnlyDictionary<int, IReadOnlyList<ArchivoDossier>> Archivos);
    private readonly ConcurrentDictionary<(int Proyecto, string Fuente), Entrada> cache = new();
    private readonly ConcurrentDictionary<(int Proyecto, string Fuente), SemaphoreSlim> turnos = new();
    // Se calcula desde la instantánea vigente: un módulo publicado/archivado aparece/desaparece al consultar.
    public IReadOnlyList<FuenteVinculo> Fuentes => FuentesDossier.Todas
        .Select(f => new FuenteVinculo(f.Clave, f.Nombre, f.Icono, FuentesDossier.ModuloDe(f.Clave))).ToArray();
    public FuenteVinculo? Fuente(string clave) => Fuentes.FirstOrDefault(f => f.Clave == clave);
    private static bool Puede(string clave) => AmbienteSig.Actual is { } s &&
        (s.Permisos.AdminGlobal || FuentesDossier.Permitida(clave, Accion.Ver, s.Permisos.Puede));
    public IReadOnlyList<FuenteVinculo> FuentesVisibles() => Fuentes.Where(f => Puede(f.Clave)).ToArray();
    private TablaVinculo Vacia(string clave) => new(Fuente(clave) ?? new(clave, clave, "enlace", ""), [], []);

    public async Task<TablaVinculo> TablaAsync(string fuente, CancellationToken ct = default) =>
        (await CargarAsync(fuente, ct))?.Tabla ?? Vacia(fuente);

    public async Task<IReadOnlyList<ArchivoDossier>> ArchivosAsync(string fuente, int id, CancellationToken ct = default) =>
        (await CargarAsync(fuente, ct))?.Archivos.GetValueOrDefault(id) ?? [];

    private async Task<Entrada?> CargarAsync(string fuente, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Puede(fuente) || AmbienteSig.ProyectoId is not int proyecto || FuentesDossier.De(fuente) is not { } f) return null;
        var clave = (proyecto, fuente);
        if (cache.TryGetValue(clave, out var e) && DateTimeOffset.UtcNow - e.Fecha < TimeSpan.FromSeconds(60)) return e;
        var turno = turnos.GetOrAdd(clave, _ => new(1, 1));
        await turno.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(clave, out e) && DateTimeOffset.UtcNow - e.Fecha < TimeSpan.FromSeconds(60)) return e;
            var version = Volatile.Read(ref versionFuentes);
            using var scope = scopes.CreateScope();
            await using var compartida = scope.ServiceProvider.GetRequiredService<FabricaDbCompartida>().ParaModulo(FuentesDossier.ModuloDe(fuente));
            var db = compartida ?? scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var datos = await f.CargarAsync(db, ct);
            var campos = datos.Motor.Visibles.DistinctBy(c => c.Clave).ToArray();
            var filas = datos.Motor.Filas.Select(r =>
            {
                var nombre = datos.Texto(r.Id, datos.ColumnaNombre);
                Dictionary<string, double>? numeros = null;
                var valores = campos.ToDictionary(c => c.Clave, c =>
                {
                    if (c.Decimales is null) return datos.Motor.Texto(r.Id, c, out _);
                    try
                    {
                        var valor = datos.Motor.Valor(r.Id, c);
                        if (valor is double n) (numeros ??= new())[c.Clave] = n;
                        return MotorTabla.Mostrar(c, valor);
                    }
                    catch (Exception) { return "#ERROR"; }
                });
                return new FilaVinculo(r.Id, string.IsNullOrWhiteSpace(nombre) ? r.Etiqueta : nombre, valores)
                    { NumerosFormateados = numeros };
            }).ToArray();
            var urls = new Dictionary<int, string>();
            // Datos que no están expuestos como columnas: una consulta por fuente, nunca por fila.
            if (fuente == "resultados-ensayo")
                foreach (var r in await db.ResultadosEnsayo.AsNoTracking().Select(r => new { r.Id, r.TipoEnsayoId }).ToListAsync(ct))
                    urls[r.Id] = $"ensayos/{r.TipoEnsayoId}?resultado={r.Id}";
            if (fuente == "informes")
            {
                var primeros = await db.ResultadosEnsayo.AsNoTracking().Where(r => !r.Eliminado && r.InformeId != null)
                    .GroupBy(r => r.InformeId!.Value).Select(g => new { Id = g.Key, Resultado = g.Min(r => r.Id) }).ToDictionaryAsync(r => r.Id, r => r.Resultado, ct);
                foreach (var r in await db.Informes.AsNoTracking().Select(r => new { r.Id, r.TipoEnsayoId }).ToListAsync(ct))
                    urls[r.Id] = primeros.TryGetValue(r.Id, out var resultado)
                        ? $"ensayos/{r.TipoEnsayoId}?resultado={resultado}" : $"ensayos/{r.TipoEnsayoId}";
            }
            foreach (var r in filas)
            {
                string Q(string c) => Uri.EscapeDataString(datos.Texto(r.Id, c));
                if (fuente == "planos") urls[r.Id] = $"planos?buscar={Q("Codigo")}";
                if (fuente == "comunicados") urls[r.Id] = $"comunicados?buscar={Q("Consecutivo")}";
            }
            var tabla = new TablaVinculo(new(f.Clave, f.Nombre, f.Icono, FuentesDossier.ModuloDe(f.Clave)),
                campos.Select(c => new ColumnaVinculo(c.Clave, c.Nombre)).ToArray(), filas);
            var archivos = datos.Elementos.Where(el => el.Archivos.Count > 0)
                .ToDictionary(el => el.Id, el => (IReadOnlyList<ArchivoDossier>)Array.AsReadOnly(el.Archivos.ToArray()));
            var entrada = new Entrada(DateTimeOffset.UtcNow, tabla, urls, archivos);
            if (version == Volatile.Read(ref versionFuentes)) cache[clave] = entrada;
            return entrada;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { log.LogError(ex, "No se pudo cargar la fuente de vínculos {Fuente} del proyecto {Proyecto}", fuente, proyecto); return null; }
        finally { turno.Release(); }
    }

    public static IReadOnlyList<FilaVinculo> FiltrarFilas(TablaVinculo tabla, CampoModulo campo,
        IReadOnlyList<VinculoElegido>? elegidos)
    {
        if (string.IsNullOrWhiteSpace(campo.FiltroCampo) || elegidos is null) return tabla.Filas;
        if (elegidos.Count == 0) return [];

        // Sin columna se cotejan Id de la misma fuente. Con columna se cotejan tokens separados por
        // punto y coma contra la etiqueta y su código, normalizando tildes, caja y prefijos de torre.
        if (string.IsNullOrWhiteSpace(campo.ColumnaEnlace))
        {
            var ids = elegidos.Where(e => e.Fuente == campo.Fuente).Select(e => e.Id).ToHashSet();
            return tabla.Filas.Where(f => ids.Contains(f.Id)).ToArray();
        }

        var claves = elegidos.SelectMany(e => ClavesEnlace(e.Etiqueta)).ToHashSet(StringComparer.Ordinal);
        return tabla.Filas.Where(f => f.Valores.TryGetValue(campo.ColumnaEnlace, out var valor)
            && valor.Split(';').SelectMany(ClavesEnlace).Any(claves.Contains)).ToArray();
    }

    private static IEnumerable<string> ClavesEnlace(string texto)
    {
        var normal = DefinicionModulo.Normalizar(texto);
        if (normal.Length == 0) yield break;
        yield return normal;
        var codigo = normal.Split(" · ", 2, StringSplitOptions.TrimEntries)[0];
        if (codigo.Length == 0) yield break;
        yield return codigo;
        var prefijo = codigo.StartsWith("sn-", StringComparison.Ordinal) ? 3
            : codigo.StartsWith("t-", StringComparison.Ordinal) ? 2
            : codigo.StartsWith('t') ? 1 : 0;
        if (prefijo > 0 && codigo.Length > prefijo) yield return codigo[prefijo..].Trim();
    }

    public async Task<IReadOnlyDictionary<int, string>> EtiquetasAsync(string fuente, IEnumerable<int> ids, CancellationToken ct = default)
    {
        var elegidos = ids.ToHashSet();
        return (await TablaAsync(fuente, ct)).Filas.Where(r => elegidos.Contains(r.Id)).ToDictionary(r => r.Id, r => r.Etiqueta);
    }

    // Solo memoria: puede llamarse al pintar fichas sin consultar la base de datos.
    public string? UrlDe(string fuente, int id)
    {
        if (id <= 0 || Fuente(fuente) is null || !Puede(fuente)) return null;
        if (fuente.StartsWith("mod/", StringComparison.Ordinal)) return $"{fuente}?registro={id}";
        if (cache.TryGetValue((AmbienteSig.ProyectoId ?? 0, fuente), out var e) && e.Urls.TryGetValue(id, out var url)) return url;
        return fuente switch
        {
            "torres" => $"trazado?torre={id}&hdv=true",
            "equipos" => $"equipos/{id}/hoja-de-vida",
            "documentos" => $"documentos?doc={id}&abrir=todos",
            "materiales" => $"materiales?material={id}",
            "planes-accion" => $"planes-accion/{id}",
            "riesgos" => $"riesgos/{id}",
            "auditoria-interna" => $"auditoria-interna/{id}",
            "tiros" => $"tiros?tiro={id}",
            "indicadores" => $"indicadores?indicador={id}",
            _ => FuentesDossier.RutaDe(fuente)
        };
    }

    public void Invalidar(string? fuente = null)
    {
        var proyecto = AmbienteSig.ProyectoId ?? 0;
        foreach (var clave in cache.Keys.Where(k => k.Proyecto == proyecto && (fuente is null || k.Fuente == fuente))) cache.TryRemove(clave, out _);
    }
}
