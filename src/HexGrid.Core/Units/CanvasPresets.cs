namespace HexGrid.Core.Units;

public static class CanvasPresets
{
    /// <summary>ISO 216 portrait dimensions in millimetres, and fixed pixel dimensions for screen presets.</summary>
    // SS018: the "_" arm here is doing real, deliberate work: it's reached only by Custom, which by
    // definition has no fixed size, so throwing is correct. Listing Custom explicitly would just
    // duplicate the throw under a different label.
#pragma warning disable SS018
    public static CanvasSpec Resolve(CanvasPreset preset) => preset switch
    {
        CanvasPreset.TwoA0 => CanvasSpec.Paper(1189, 1682),
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
#pragma warning restore SS018

    /// <summary>Short token used in generated filenames.</summary>
    // SS018: the "_" arm here is also doing real work: A0..A6 deliberately fall through to
    // preset.ToString(), since the enum member names ("A0", "A1", ...) already are the desired short
    // names. Listing them explicitly would just duplicate CanvasPreset's own member names by hand.
#pragma warning disable SS018
    public static string ShortName(CanvasPreset preset) => preset switch
    {
        CanvasPreset.Custom => "Custom",
        CanvasPreset.TwoA0 => "2A0",
        CanvasPreset.Uhd8K => "8K",
        CanvasPreset.Uhd4K => "4K",
        CanvasPreset.Qhd2K => "2K",
        CanvasPreset.Fhd1080p => "1080p",
        _ => preset.ToString(),
    };
#pragma warning restore SS018
}
