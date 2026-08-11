namespace HexGrid.Core;

/// <summary>Which shape tiles the map area.</summary>
public enum GridType
{
    /// <summary>Regular hexagons. Always meet the frame by overhanging it and being clipped.</summary>
    Hex,

    /// <summary>Axis-aligned squares. Can tile the map area exactly, so they need not be clipped.</summary>
    Square,
}
