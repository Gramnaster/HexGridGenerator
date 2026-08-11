using System.ComponentModel;
using System.Globalization;

namespace HexGrid.Core;

/// <summary>
/// Renders <see cref="GridSizingMode.FixedHexWidth"/> as "Fixed square size" in the PropertyGrid
/// when the owning <see cref="GridSettings.GridType"/> is Square, without changing the enum member
/// name that presets serialise to JSON.
/// </summary>
internal sealed class GridSizingModeConverter() : EnumConverter(typeof(GridSizingMode))
{
    private const string SquareDisplayText = "FixedSquareSize";

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is GridSizingMode.FixedHexWidth &&
            context?.Instance is GridSettings { GridType: GridType.Square })
        {
            return SquareDisplayText;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text && string.Equals(text, SquareDisplayText, StringComparison.Ordinal)
            ? GridSizingMode.FixedHexWidth
            : base.ConvertFrom(context, culture, value);
}
