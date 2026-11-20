using System.Collections.Concurrent;
using System.Text.Json;

namespace SigWeb.Data.Modelos3D;

// Datos del catálogo de Modelos 3D leídos del "meta" del JSON del modelo (alto, peso, miembros, validación, supuestos).
// Se leen una vez por archivo y fecha de modificación.
public record ResumenModelo(double? AltoMm, double? PesoKg, int Miembros, int Validaciones, int Supuestos);

public static class ResumenModelos
{
    private static readonly ConcurrentDictionary<string, (DateTime Mod, ResumenModelo? R)> Cache = new();

    public static ResumenModelo? Leer(string ruta)
    {
        try
        {
            if (!File.Exists(ruta)) return null;
            var mod = File.GetLastWriteTimeUtc(ruta);
            if (Cache.TryGetValue(ruta, out var c) && c.Mod == mod) return c.R;
            ResumenModelo? r = null;
            using (var fs = File.OpenRead(ruta))
            using (var doc = JsonDocument.Parse(fs))
            {
                var raiz = doc.RootElement;
                double? Num(JsonElement m, string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.GetDouble() > 0 ? v.GetDouble() : null;
                int Largo(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? v.GetArrayLength() : 0;
                var meta = raiz.TryGetProperty("meta", out var mt) ? mt : default;
                r = meta.ValueKind == JsonValueKind.Object
                    ? new ResumenModelo(Num(meta, "alto_total_mm"), Num(meta, "peso_total_kg"), Largo(raiz, "miembros"), Largo(meta, "validacion"), Largo(meta, "supuestos"))
                    : new ResumenModelo(null, null, Largo(raiz, "miembros"), 0, 0);
            }
            Cache[ruta] = (mod, r);
            return r;
        }
        catch { return null; }
    }
}
