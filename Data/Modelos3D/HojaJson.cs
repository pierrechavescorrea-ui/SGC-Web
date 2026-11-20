using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SigWeb.Data.Modelos3D;

/// <summary>
/// Número de hoja de plano en los JSON de modelos 3D: acepta 38, 38.0, "38", "H38", "H_38" o null (barra sin hoja de
/// detalle: franja "sin hoja"). Lo que no se pueda leer queda en 0. Así un JSON generado o cargado por otros no rompe
/// la ficha ni la lista de materiales.
/// </summary>
public sealed class HojaJson : JsonConverter<int>
{
    public override bool HandleNull => true;

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var i) ? i : reader.TryGetDouble(out var d) && Math.Abs(d) < int.MaxValue ? (int)Math.Round(d) : 0;
            case JsonTokenType.String:
                var s = reader.GetString() ?? "";
                var dig = new string(s.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(dig, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
            default:
                // null → 0; objeto o arreglo (no debería venir): se salta entero. Skip() no sirve al leer por stream.
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray) reader.TrySkip();
                return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
