using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Modelos3D;

// Modelos IFC cargados desde la app (ModeloIfcCargado): versión vigente por código, nombres de archivo,
// lectura de la validación y los supuestos del IFC y borrado físico.
public static class ModelosCargados
{
    public const string CarpetaIfc = "uploads/modelos-ifc";
    public const string CarpetaJson = "uploads/modelos3d";

    // Todas las versiones que no están en la Papelera. Sin migración aplicada = lista vacía.
    public static List<ModeloIfcCargado> Todos(AppDbContext db)
    {
        try { return db.ModelosIfcCargados.AsNoTracking().OrderBy(m => m.Codigo).ThenByDescending(m => m.Version).ToList(); }
        catch { return new(); }
    }

    // La versión más alta de cada código.
    public static List<ModeloIfcCargado> Vigentes(IEnumerable<ModeloIfcCargado> todos) =>
        todos.GroupBy(m => ConfigModelos3D.Norm(m.Codigo)).Select(g => g.OrderByDescending(m => m.Version).First()).ToList();

    public static bool EsVigente(IEnumerable<ModeloIfcCargado> todos, ModeloIfcCargado m) =>
        !todos.Any(x => x.Id != m.Id && ConfigModelos3D.Norm(x.Codigo) == ConfigModelos3D.Norm(m.Codigo) && x.Version > m.Version);

    // Siguiente número de versión (cuenta también las de la Papelera para no repetir nombres de archivo).
    public static int SiguienteVersion(AppDbContext db, string codigo)
    {
        var k = ConfigModelos3D.Norm(codigo);
        var versiones = db.ModelosIfcCargados.IgnoreQueryFilters().AsNoTracking().Select(m => new { m.Codigo, m.Version }).ToList();
        return versiones.Where(v => ConfigModelos3D.Norm(v.Codigo) == k).Select(v => v.Version).DefaultIfEmpty(0).Max() + 1;
    }

    // c-bp7-v2: el .ifc y el .json comparten raíz (así se emparejan, como torre-bp7.ifc ↔ torre-bp7.json).
    public static string Raiz(string codigo, int version) => $"c-{ConfigModelos3D.Norm(codigo).ToLowerInvariant()}-v{version}";

    // Raíz del archivo (clave de AsignacionPiezaModelo.Modelo).
    public static string RaizDe(ModeloIfcCargado m) => Path.GetFileNameWithoutExtension(m.ArchivoIfc).ToLowerInvariant();

    public record LecturaIfc(bool EsIfc, bool TieneTorre, int Validaciones, int Supuestos, string? Esquema);

    private static readonly Regex RxProp = new(@"IFCPROPERTYSINGLEVALUE\('(Validacion|Supuesto)\d*'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RxEsquema = new(@"FILE_SCHEMA\s*\(\s*\(\s*'([^']+)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Lee el IFC línea por línea: encabezado STEP, esquema y propiedades Validacion*/Supuesto* del Pset SigWeb_Torre.
    public static LecturaIfc LeerIfc(string ruta)
    {
        bool esIfc = false, torre = false; int val = 0, sup = 0; string? esquema = null;
        using var r = new StreamReader(ruta);
        string? linea; var n = 0;
        while ((linea = r.ReadLine()) is not null)
        {
            if (n++ == 0) esIfc = linea.TrimStart('﻿', ' ').StartsWith("ISO-10303-21", StringComparison.OrdinalIgnoreCase);
            if (esquema is null && RxEsquema.Match(linea) is { Success: true } e) esquema = e.Groups[1].Value;
            if (!torre && linea.Contains("'SigWeb_Torre'", StringComparison.Ordinal)) torre = true;
            foreach (Match m in RxProp.Matches(linea))
                if (m.Groups[1].Value.StartsWith("V", StringComparison.OrdinalIgnoreCase)) val++; else sup++;
        }
        return new LecturaIfc(esIfc, torre, val, sup, esquema);
    }

    // Número de miembros del JSON del modelo; null = no es un JSON de modelo (falta "miembros").
    public static int? LeerJson(string ruta)
    {
        try
        {
            using var fs = File.OpenRead(ruta);
            using var doc = System.Text.Json.JsonDocument.Parse(fs);
            return doc.RootElement.TryGetProperty("miembros", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.Array ? m.GetArrayLength() : null;
        }
        catch { return null; }
    }

    public static IEnumerable<string?> Archivos(ModeloIfcCargado m) => new[] { m.ArchivoIfc, m.ArchivoJson, m.ArchivoMiniatura, m.ArchivoInforme };
}
