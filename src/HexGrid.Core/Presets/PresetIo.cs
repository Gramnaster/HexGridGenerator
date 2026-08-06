using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexGrid.Core.Presets;

/// <summary>Saves and loads a <see cref="GridSettings"/> as human-editable JSON.</summary>
public static class PresetIo
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        o.Converters.Add(new ColorJsonConverter());
        return o;
    }

    public static string Serialize(GridSettings settings) =>
        JsonSerializer.Serialize(settings, Options);

    public static GridSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<GridSettings>(json, Options)
        ?? throw new InvalidDataException("The preset file was empty or not valid JSON.");

    public static void Save(GridSettings settings, string path) =>
        File.WriteAllText(path, Serialize(settings));

    public static GridSettings Load(string path) =>
        Deserialize(File.ReadAllText(path));
}
