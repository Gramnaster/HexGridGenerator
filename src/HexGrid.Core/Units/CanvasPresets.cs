namespace HexGrid.Core.Units;

/// <summary>A canvas size resolved to a concrete physical or pixel size.</summary>
/// <param name="WidthMm">Width in millimetres, or null for pixel-defined presets.</param>
/// <param name="HeightMm">Height in millimetres, or null for pixel-defined presets.</param>
/// <param name="WidthPx">Width in pixels, or null for paper presets.</param>
/// <param name="HeightPx">Height in pixels, or null for paper presets.</param>
public readonly record struct CanvasSpec(double? WidthMm, double? HeightMm, int? WidthPx, int? HeightPx)
{
    public bool IsPaper => WidthMm.HasValue;

    public static CanvasSpec Paper(double wMm, double hMm) => new(wMm, hMm, null, null);

    public static CanvasSpec Screen(int wPx, int hPx) => new(null, null, wPx, hPx);
}

public static class CanvasPresets
{
    /// <summary>ISO 216 portrait dimensions in millimetres, and fixed pixel dimensions for screen presets.</summary>
    public static CanvasSpec Resolve(CanvasPreset preset) => preset switch
    {
        CanvasPreset.A2_0 => CanvasSpec.Paper(1189, 1682),
        CanvasPreset.A0 => CanvasSpec.Paper(841, 1189),
        CanvasPreset.A1 => CanvasSpec.Paper(594, 841),
        CanvasPreset.A2 => CanvasSpec.Paper(420, 594),
        CanvasPreset.A3 => CanvasSpec.Paper(297, 420),
        CanvasPreset.A4 => CanvasSpec.Paper(210, 297),
        CanvasPreset.A5 => CanvasSpec.Paper(148, 210),
        CanvasPreset.A6 => CanvasSpec.Paper(105, 148),

        CanvasPreset.Uhd8K => CanvasSpec.Screen(7680, 4320),
        CanvasPreset.Uhd4K => CanvasSpec.Screen(3840, 2160),
        CanvasPreset.Qhd2K => CanvasSpec.Screen(2560, 1440),
        CanvasPreset.Fhd1080p => CanvasSpec.Screen(1920, 1080),

        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Custom has no fixed size."),
    };

    /// <summary>Short token used in generated filenames.</summary>
    public static string ShortName(CanvasPreset preset) => preset switch
    {
        CanvasPreset.Custom => "Custom",
        CanvasPreset.A2_0 => "2A0",
        CanvasPreset.Uhd8K => "8K",
        CanvasPreset.Uhd4K => "4K",
        CanvasPreset.Qhd2K => "2K",
        CanvasPreset.Fhd1080p => "1080p",
        _ => preset.ToString(),
    };
}
