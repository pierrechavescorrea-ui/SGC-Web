using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SigWeb.Data.Seguridad;

// Aislamiento de adjuntos por proyecto (auditoría 2026-11-22, S01).
// Todos los proyectos guardan sus archivos en wwwroot/uploads, pero cada archivo queda referenciado por su URL
// ("/uploads/…") en la base de UN proyecto. Un archivo se entrega solo si la base del proyecto activo (o la de la
// Empresa, cuyos módulos se comparten en solo lectura) lo referencia; así una URL conocida no sirve desde otro proyecto.
// Índice por proyecto: se arma una vez leyendo las columnas de texto del modelo EF (sin texto del usuario en el SQL:
// tablas y columnas salen del modelo, el valor buscado va como parámetro) y, ante una URL que no está, se busca solo
// esa URL (archivo recién subido) antes de negar. Seguridad:AislarAdjuntos = "Bloquear" (por defecto) | "Registrar" | "Apagado".
public sealed class IndiceArchivosProyecto
{
    private sealed class Indice
    {
        public required HashSet<string> Urls { get; init; }
        public required HashSet<string> Raices { get; init; }   // carpeta + nombre sin extensión (modelos 3D: IFC, JSON, informe)
        public DateTime Armado { get; init; }
    }

    private static readonly Regex RxUrl = new(@"/?uploads/[^""'\s<>()\\]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly StringComparer Comparador = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    // Carpetas de modelos 3D: un modelo cargado son varios archivos con la misma raíz (c-bp6-v1.ifc, .json, .png, informes/…).
    private static readonly string[] CarpetasModelo = { "/uploads/modelos-ifc/", "/uploads/modelos3d/" };
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PausaNegativo = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<int, Indice> _indices = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _candados = new();
    private readonly ConcurrentDictionary<(int, string), DateTime> _negativos = new();
    private readonly ConcurrentDictionary<(int, string), DateTime> _temporales = new();
    private readonly ServicioProyectos _proyectos;
    private readonly ILogger<IndiceArchivosProyecto> _log;

    public IndiceArchivosProyecto(ServicioProyectos proyectos, ILogger<IndiceArchivosProyecto> log, IWebHostEnvironment env)
    {
        _proyectos = proyectos;
        RaizWeb = env.WebRootPath;
        _log = log;
    }

    // Archivos que no se guardan en la base (vistas previas temporales, compilados para descargar): la pantalla los
    // registra para el proyecto activo y se entregan durante un día.
    public void PermitirTemporal(string url)
    {
        if (AmbienteSig.Actual?.Proyecto is not { } p) return;
        _temporales[(p.Id, Normalizar(url))] = DateTime.UtcNow.AddDays(1);
    }

    // Llamar tras guardar un archivo nuevo si se quiere evitar la búsqueda puntual del primer acceso (opcional).
    public void Agregar(int proyectoId, string url)
    {
        if (_indices.TryGetValue(proyectoId, out var i)) lock (i) { i.Urls.Add(Normalizar(url)); i.Raices.Add(Raiz(Normalizar(url))); }
    }

    public async Task<bool> PermitidoAsync(SesionSig sesion, string ruta, CancellationToken ct)
    {
        var url = Normalizar(ruta);
        var p = sesion.Proyecto;
        if (_temporales.TryGetValue((p.Id, url), out var vence))
        {
            if (vence > DateTime.UtcNow) return true;
            _temporales.TryRemove((p.Id, url), out _);
        }
        if (await EnProyectoAsync(p, url, ct)) return true;
        // Recién subido y aún sin guardar el registro (vista previa del formulario): se entrega solo si su nombre lleva un
        // identificador aleatorio (Guid, no adivinable) y el archivo tiene menos de una hora.
        if (RecienSubido(url)) return true;
        var empresa = _proyectos.Empresa();
        return empresa is not null && empresa.Id != p.Id && _proyectos.Preparado(empresa.Id) && await EnProyectoAsync(empresa, url, ct);
    }

    private async Task<bool> EnProyectoAsync(ProyectoActivo p, string url, CancellationToken ct)
    {
        var indice = await ObtenerAsync(p, ct);
        if (Contiene(indice, url)) return true;
        // No está: puede ser un archivo recién guardado. Búsqueda puntual (con pausa para no repetirla en cada petición).
        if (_negativos.TryGetValue((p.Id, url), out var hasta) && hasta > DateTime.UtcNow) return false;
        var encontrado = await BuscarAsync(p, url, ct);
        if (encontrado) { lock (indice) { indice.Urls.Add(url); indice.Raices.Add(Raiz(url)); } return true; }
        _negativos[(p.Id, url)] = DateTime.UtcNow + PausaNegativo;
        if (_negativos.Count > 5000) foreach (var k in _negativos.Where(x => x.Value < DateTime.UtcNow).Select(x => x.Key).ToList()) _negativos.TryRemove(k, out _);
        return false;
    }

    private static bool Contiene(Indice i, string url)
    {
        lock (i)
        {
            if (i.Urls.Contains(url)) return true;
            return EsDeModelo(url) && i.Raices.Contains(Raiz(url));
        }
    }

    private async Task<Indice> ObtenerAsync(ProyectoActivo p, CancellationToken ct)
    {
        if (_indices.TryGetValue(p.Id, out var i) && DateTime.UtcNow - i.Armado < Vigencia) return i;
        var candado = _candados.GetOrAdd(p.Id, _ => new SemaphoreSlim(1, 1));
        await candado.WaitAsync(ct);
        try
        {
            if (_indices.TryGetValue(p.Id, out i) && DateTime.UtcNow - i.Armado < Vigencia) return i;
            var urls = new HashSet<string>(Comparador);
            await using (var db = _proyectos.CrearContexto(p))
                foreach (var (sql, _) in Consultas(db, null))
                    await LeerAsync(db, sql, null, valor =>
                    {
                        foreach (Match m in RxUrl.Matches(valor)) urls.Add(Normalizar(m.Value));
                    }, ct);
            var nuevo = new Indice { Urls = urls, Raices = new HashSet<string>(urls.Where(EsDeModelo).Select(Raiz), Comparador), Armado = DateTime.UtcNow };
            _indices[p.Id] = nuevo;
            _log.LogInformation("Índice de adjuntos de {Proyecto}: {N} archivos.", p.Codigo, urls.Count);
            return nuevo;
        }
        finally { candado.Release(); }
    }

    private async Task<bool> BuscarAsync(ProyectoActivo p, string url, CancellationToken ct)
    {
        var hallado = false;
        await using var db = _proyectos.CrearContexto(p);
        var sinBarra = url.TrimStart('/');
        foreach (var (sql, _) in Consultas(db, "@u"))
        {
            await LeerAsync(db, sql, sinBarra, valor =>
            {
                if (RxUrl.Matches(valor).Any(m => Comparador.Equals(Normalizar(m.Value), url)) ||
                    (EsDeModelo(url) && RxUrl.Matches(valor).Any(m => Comparador.Equals(Raiz(Normalizar(m.Value)), Raiz(url)))))
                    hallado = true;
            }, ct);
            if (hallado) return true;
        }
        return false;
    }

    // Una consulta por tabla con todas sus columnas de texto (UNION ALL). Identificadores tomados del modelo EF.
    private static IEnumerable<(string Sql, string Tabla)> Consultas(AppDbContext db, string? parametro)
    {
        foreach (var t in db.Model.GetEntityTypes())
        {
            var tabla = t.GetTableName();
            if (tabla is null || t.GetViewName() is not null) continue;
            var so = StoreObjectIdentifier.Table(tabla, t.GetSchema());
            var cols = t.GetProperties().Where(x => x.ClrType == typeof(string))
                .Select(x => x.GetColumnName(so)).Where(c => c is not null).Distinct().ToList();
            if (cols.Count == 0) continue;
            var nombre = (t.GetSchema() is { } s ? Q(s) + "." : "") + Q(tabla);
            var filtro = parametro is null ? "'%uploads/%'" : "'%' || " + parametro + " || '%'";
            // Al buscar una raíz de modelo (sin extensión) se usa el nombre sin extensión.
            var partes = cols.Select(c => $"SELECT {Q(c!)}::text AS v FROM {nombre} WHERE {Q(c!)} LIKE {filtro}");
            yield return (string.Join(" UNION ALL ", partes), tabla);
        }
    }

    private async Task LeerAsync(AppDbContext db, string sql, string? valorParametro, Action<string> alLeer, CancellationToken ct)
    {
        var cn = db.Database.GetDbConnection();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        await using DbCommand cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        if (valorParametro is not null)
        {
            var prm = cmd.CreateParameter();
            prm.ParameterName = "u";
            // Raíz de modelo: se busca sin extensión para encontrar el IFC/JSON hermano.
            prm.Value = EsDeModelo("/" + valorParametro) ? Raiz("/" + valorParametro).TrimStart('/') : valorParametro;
            cmd.Parameters.Add(prm);
        }
        try
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                if (!r.IsDBNull(0)) alLeer(r.GetString(0));
        }
        catch (DbException ex)
        {
            // Una tabla del modelo que aún no existe en esta base (migración pendiente) no debe tumbar la entrega de archivos.
            _log.LogDebug(ex, "Índice de adjuntos: consulta omitida.");
        }
    }

    private static readonly Regex RxAleatorio = new("[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
    public string? RaizWeb { get; set; }

    private bool RecienSubido(string url)
    {
        if (RaizWeb is null || !RxAleatorio.IsMatch(Path.GetFileName(url))) return false;
        var fisica = ArchivosSeguros.RutaFisica(RaizWeb, url);
        return fisica is not null && File.Exists(fisica) && DateTime.UtcNow - File.GetLastWriteTimeUtc(fisica) < TimeSpan.FromHours(1);
    }

    private static string Q(string identificador) => "\"" + identificador.Replace("\"", "\"\"") + "\"";

    public static string Normalizar(string ruta)
    {
        var r = ruta.Split('?', '#')[0];
        try { r = Uri.UnescapeDataString(r); } catch (UriFormatException) { }
        r = r.Replace('\\', '/');
        if (!r.StartsWith('/')) r = "/" + r;
        return r;
    }

    private static bool EsDeModelo(string url) => CarpetasModelo.Any(c => url.StartsWith(c, StringComparison.OrdinalIgnoreCase));

    // "/uploads/modelos-ifc/informes/c-bp6-v1.md" → "/uploads/modelos-ifc/c-bp6-v1"; la carpeta del modelo, sin subcarpetas.
    private static string Raiz(string url)
    {
        var carpeta = CarpetasModelo.FirstOrDefault(c => url.StartsWith(c, StringComparison.OrdinalIgnoreCase));
        if (carpeta is null) return url;
        var nombre = url[(url.LastIndexOf('/') + 1)..];
        var punto = nombre.IndexOf('.');
        if (punto > 0) nombre = nombre[..punto];
        return carpeta + nombre;
    }
}
