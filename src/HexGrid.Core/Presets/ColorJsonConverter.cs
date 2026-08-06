using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexGrid.Core.Presets;

/// <summary>Reads and writes <see cref="Color"/> as #RRGGBB or #AARRGGBB.</summary>
public sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Color.Black;
        }

        string hex = raw.TrimStart('#');
        return hex.Length switch
        {
            6 => Color.FromArgb(255, Parse(hex, 0), Parse(hex, 2), Parse(hex, 4)),
            8 => Color.FromArgb(Parse(hex, 0), Parse(hex, 2), Parse(hex, 4), Parse(hex, 6)),
            _ => Color.FromName(raw),
        };

        static int Parse(string h, int i) =>
            int.Parse(h.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        string hex = value.A == 255
            ? $"#{value.R:x2}{value.G:x2}{value.B:x2}"
            : $"#{value.A:x2}{value.R:x2}{value.G:x2}{value.B:x2}";
        writer.WriteStringValue(hex);
    }
}
