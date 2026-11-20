using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.AvanceObra;

namespace SigWeb.Data.Modelos3D;

// Texto mantiene la presentación usada por los consumidores; la descripción original y el código
// para buscar un IFC se conservan por separado.
public record TipoTorre(string? Texto, string Familia, int Cuerpo, ConfigModelo? Modelo)
{
    public string? TextoOriginal { get; init; }
    public string? Codigo { get; init; }
}

public static class TiposTorre
{
    public const string CodigoTorreModelada = "102NNN";
    private const RegexOptions Opciones = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex Tipo = new(
        @"^\s*(?<familia>[a-z]+(?:[ \t-]+(?!H\s*[0-9])[a-z]+)*)(?:[ \t-]*(?<cuerpo>[0-9]+))?(?=\s|$)", Opciones);
    private static readonly Regex CantidadCuerpo = new(@"\A(?<cuerpo>[0-9]+)(?:\s+cuerpos?)?\z", Opciones);
    private static readonly Regex SoloCantidad = new(@"\A[+\-]?[0-9]+(?:[.,][0-9]+)?(?:\s+cuerpos?)?\z", Opciones);

    public static string? ClaveTipo(IEnumerable<string> claves) => ClaveTipo(claves, new ConfigCotasAvance());

    public static string? ClaveTipo(IEnumerable<string> claves, ConfigCotasAvance marcas)
    {
        if (!string.IsNullOrWhiteSpace(marcas.ClaveTipoTorre)) return marcas.ClaveTipoTorre;
        return claves.Where(c => !MismaClave(c, marcas.ClaveCuerpoTorre) && PrioridadTipo(c) < int.MaxValue)
            .OrderBy(PrioridadTipo).ThenBy(c => c, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    public static string? ClaveCuerpo(IEnumerable<string> claves, ConfigCotasAvance marcas, string? claveTipo)
    {
        if (!string.IsNullOrWhiteSpace(marcas.ClaveCuerpoTorre) && !MismaClave(marcas.ClaveCuerpoTorre, claveTipo))
            return marcas.ClaveCuerpoTorre;
        return claves.Where(c => !MismaClave(c, claveTipo) && EsEncabezadoCuerpo(c))
            .OrderBy(c => Encabezado(c) is "cantidaddecuerpos" or "numerodecuerpos" ? 0 : 1)
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    private static bool MismaClave(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static string Encabezado(string clave) => new(clave.Normalize(NormalizationForm.FormD).ToLowerInvariant()
        .Where(c => char.IsLetterOrDigit(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    private static bool EsEncabezadoCuerpo(string clave) => Encabezado(clave).Contains("cuerpo", StringComparison.Ordinal);

    private static int PrioridadTipo(string clave)
    {
        var n = Encabezado(clave);
        if (n.Contains("cimenta", StringComparison.Ordinal) || n.Contains("suelo", StringComparison.Ordinal)) return int.MaxValue;
        if (n.Contains("tipodeestructura", StringComparison.Ordinal) || n.Contains("tipodetorre", StringComparison.Ordinal)) return 0;
        if (n == "tipo") return 1;
        if (n.Contains("tipo", StringComparison.Ordinal)) return 2;
        return EsEncabezadoCuerpo(clave) ? 3 : int.MaxValue;
    }

    private static int CuerpoPositivo(string digitos) => int.TryParse(digitos, out var n) && n > 0 ? n : 0;

    public static Dictionary<int, TipoTorre> Cargar(AppDbContext db, List<ConfigModelo> cfg, ConfigCotasAvance? marcas = null)
    {
        marcas ??= ConfigCotasAvance.Leer(db.Configuracion.AsNoTracking().Select(c => c.AvanceObraCotas).FirstOrDefault());
        var elementos = db.ElementosTrazado.AsNoTracking().Where(e => !e.Eliminado).Select(e => new { e.Id, e.Codigo, e.Nombre }).ToList();
        var ids = elementos.Select(e => e.Id).ToHashSet();
        var campos = db.CamposExtraTorre.AsNoTracking().Where(c => !c.Eliminado)
            .Select(c => new { c.Clave, c.ElementoTrazadoId, c.Valor }).AsEnumerable()
            .Where(c => ids.Contains(c.ElementoTrazadoId) && (PrioridadTipo(c.Clave) < int.MaxValue ||
                EsEncabezadoCuerpo(c.Clave) || MismaClave(c.Clave, marcas.ClaveTipoTorre) || MismaClave(c.Clave, marcas.ClaveCuerpoTorre)))
            .ToLookup(c => c.Clave, StringComparer.OrdinalIgnoreCase);

        var claveTipo = ClaveTipo(campos.Where(g => g.Any(c => !string.IsNullOrWhiteSpace(c.Valor)
            && !SoloCantidad.IsMatch(c.Valor.Trim()))).Select(g => g.Key), marcas);
        // «Cuerpo» puede contener códigos completos (BP6). Solo se autodetecta como cantidad si
        // la columna contiene valores numéricos.
        var claveCuerpo = ClaveCuerpo(campos.Where(g => g.Any(c => SoloCantidad.IsMatch(c.Valor.Trim())))
            .Select(g => g.Key), marcas, claveTipo);
        Dictionary<int, string> Valores(string? clave) => clave is null ? new()
            : campos[clave].GroupBy(c => c.ElementoTrazadoId).ToDictionary(g => g.Key, g => g.First().Valor.Trim());
        var tipos = Valores(claveTipo);
        var cuerpos = Valores(claveCuerpo);

        var modelada = elementos.FirstOrDefault(e => MismaClave(e.Codigo, CodigoTorreModelada) || MismaClave(e.Nombre, CodigoTorreModelada));
        var modeloBE = cfg.FirstOrDefault(c => c.Vinculado && ConfigModelos3D.Norm(c.Nombre) == "BE");

        var r = new Dictionary<int, TipoTorre>();
        foreach (var e in elementos)
        {
            var original = tipos.GetValueOrDefault(e.Id);
            if (string.IsNullOrWhiteSpace(original)) original = null;
            var m = Tipo.Match(original ?? "");
            var familia = ConfigModelos3D.Norm(m.Groups["familia"].Value);
            var incorporado = CuerpoPositivo(m.Groups["cuerpo"].Value);
            bool separado = cuerpos.TryGetValue(e.Id, out var cc) && !string.IsNullOrWhiteSpace(cc);
            var cuerpo = separado ? CuerpoPositivo(CantidadCuerpo.Match(cc!).Groups["cuerpo"].Value) : incorporado;
            if (familia.Length == 0) cuerpo = 0;
            var codigo = cuerpo > 0 ? $"{familia}{cuerpo}" : null;
            // Una marca de cuerpo explícita prevalece. Sin cuerpo se puede respetar un nombre
            // vinculado explícitamente (p. ej. BE), sin generar un código con sufijo supuesto.
            var nombreExacto = separado && incorporado > 0 && incorporado != cuerpo ? null : original;
            var modelo = ConfigModelos3D.ModeloPara(cfg, codigo, null, 0)
                ?? ConfigModelos3D.ModeloPara(cfg, nombreExacto, null, 0);
            r[e.Id] = new TipoTorre(separado && codigo is not null ? codigo : original, familia, cuerpo, modelo)
                { TextoOriginal = original, Codigo = codigo };
        }

        if (modelada is not null && modeloBE is not null && r.TryGetValue(modelada.Id, out var referencia))
        {
            foreach (var id in r.Keys.ToList())
            {
                var tipo = r[id];
                if (tipo.Modelo is null && (id == modelada.Id || (referencia.TextoOriginal is not null &&
                    MismaClave(tipo.TextoOriginal, referencia.TextoOriginal))))
                    r[id] = tipo with { Modelo = modeloBE };
            }
        }
        return r;
    }
}
