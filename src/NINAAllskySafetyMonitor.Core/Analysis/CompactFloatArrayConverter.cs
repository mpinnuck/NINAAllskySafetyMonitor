using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NINAAllskySafetyMonitor.Core.Analysis;

/// <summary>
/// Writes a float array as one string of space-separated numbers ("-" for a missing value), so the
/// clear-sky map adds a few lines to the settings file instead of thousands.
/// </summary>
public sealed class CompactFloatArrayConverter : JsonConverter<float[]>
{
    public override float[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<float>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) list.Add(reader.GetSingle());
            return list.ToArray();
        }

        string text = reader.GetString() ?? string.Empty;
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t == "-" ? float.NaN : float.Parse(t, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public override void Write(Utf8JsonWriter writer, float[] value, JsonSerializerOptions options)
    {
        var text = new StringBuilder(value.Length * 6);
        for (int i = 0; i < value.Length; i++)
        {
            if (i > 0) text.Append(' ');
            text.Append(float.IsFinite(value[i]) ? value[i].ToString("0.####", CultureInfo.InvariantCulture) : "-");
        }

        writer.WriteStringValue(text.ToString());
    }
}
