using System.Text.Json.Serialization;

namespace HexGrid.Core;

/// <summary>Paper and screen canvas presets. Paper sizes are ISO 216 millimetre sizes; screen sizes are fixed pixel sizes.</summary>
public enum CanvasPreset
{
    Custom = 0,

    /// <summary>2A0 - 1189 x 1682 mm.</summary>
    [JsonStringEnumMemberName("A2_0")]
    TwoA0,
    A0,
    A1,
    A2,
    A3,
    A4,
    A5,
    A6,

    Uhd8K,      // 7680 x 4320
    Uhd4K,      // 3840 x 2160
    Qhd2K,      // 2560 x 1440
    Fhd1080p,   // 1920 x 1080
}
