using System.ComponentModel;

namespace HexGrid.Core.Settings;

public sealed partial class GridSettings : ICustomTypeDescriptor
{
    // -------------------------------------------------- ICustomTypeDescriptor
    //
    // Grid Type reshapes the PropertyGrid: hex-only rows disappear in Square mode and vice versa,
    // and property text that says "hex" is reworded to "square" for the rows both modes share.
    // Property NAMES and their JSON keys never change - only what the PropertyGrid displays does.
    // GetProperties() is the only member with real logic; the rest delegate to TypeDescriptor's
    // noCustomTypeDesc overloads, which reflect this instance without re-entering this interface.

    private static readonly Dictionary<string, (string? DisplayName, string? Description, string? Category)> SquareText = new(StringComparer.Ordinal)
    {
        [nameof(SizingMode)] = (null, "AutoFitRowsColumns: rows and columns set the square size. FixedHexWidth: the square size sets how many rows and columns there are. Either way the grid fills the map area and is clipped at the frame, so counts may come out slightly higher than requested. Default: AutoFitRowsColumns.", null),
        [nameof(LineColor)] = (null, "Square outline colour. Default: Black.", null),
        [nameof(LineThickness)] = (null, "Square outline thickness in Unit. Default: 0.25.", null),
        [nameof(HexFillColor)] = ("Square fill colour", "Fill for the square interior. Leave the opacity at 0 for a pure overlay. Default: White.", null),
        [nameof(HexFillOpacity)] = ("Square fill opacity %", null, null),
        [nameof(ShowCenterDots)] = (null, "Draw a dot at the exact centre of every square. Default: True.", null),
        [nameof(FontFamily)] = (null, "Used for every piece of text: square labels and edge labels. Default: Segoe UI.", null),
        [nameof(CoordinateSeparator)] = (null, "Placed between the two halves of a square label. Empty gives A1, a dash gives A-1. Default: (empty).", null),
        [nameof(ShowHexLabels)] = ("Show label in every square", "Print the coordinate inside each square. Off by default; edge labels are usually enough for an overlay. Default: False.", null),
        [nameof(HexLabelPosition)] = ("Position in square", "Where the coordinate sits inside its square. Center rests the label just above the centre dot rather than on top of it; with dots switched off it is centred properly. Default: Top.", "5 · Square Labels"),
        [nameof(HexLabelMargin)] = (null, "Gap between the label and the square's top or bottom edge, as a percentage of square height. 0 sits on the edge, 50 sits on the centre dot. Used only when Position in square = Top or Bottom. Default: 20.", "5 · Square Labels"),
        [nameof(HexLabelFontSize)] = (null, null, "5 · Square Labels"),
        [nameof(HexLabelBold)] = (null, null, "5 · Square Labels"),
        [nameof(HexLabelColor)] = (null, null, "5 · Square Labels"),
        [nameof(HexLabelOpacity)] = (null, null, "5 · Square Labels"),
        [nameof(GridInset)] = (null, "Gap in Unit between the frame rule and the grid. Leave at 0 so the squares run right up to the frame. Default: 0.", null),
        [nameof(GridOffsetX)] = (null, "Fine nudge of the whole grid, in Unit. Positive moves right. Default: 0.", null),
        [nameof(ExportLayersSeparately)] = (null, "Write one PNG per layer (grid, dots, square labels, edge labels, frame) alongside the flattened image, ready to stack in Photoshop. Default: False.", null),
    };

    // A regular hexagon's six neighbours all sit at the same centre-to-centre distance, so unlike a
    // square there is no separate "horizontal" and "vertical" gap to portray without distorting the
    // hex into an irregular shape. CellGapX alone is reused as that one gap value; CellGapY is hidden
    // for Hex in IsHiddenFor below.
    private static readonly Dictionary<string, (string? DisplayName, string? Description, string? Category)> HexText = new(StringComparer.Ordinal)
    {
        [nameof(CellGapX)] = ("Gap", "Gap between adjacent hexes, in Unit. The hex's own size is unchanged; this widens the spacing between hex centres on top of it. A regular hexagon's six neighbours all sit at the same distance, so one value widens that spacing equally in every direction - a large enough gap can mean fewer hexes fit than requested. Default: 0.", null),
    };

    AttributeCollection ICustomTypeDescriptor.GetAttributes() =>
        TypeDescriptor.GetAttributes(this, noCustomTypeDesc: true);

    string? ICustomTypeDescriptor.GetClassName() =>
        TypeDescriptor.GetClassName(this, noCustomTypeDesc: true);

    string? ICustomTypeDescriptor.GetComponentName() =>
        TypeDescriptor.GetComponentName(this, noCustomTypeDesc: true);

    TypeConverter ICustomTypeDescriptor.GetConverter() =>
        TypeDescriptor.GetConverter(this, noCustomTypeDesc: true);

    EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent() =>
        TypeDescriptor.GetDefaultEvent(this, noCustomTypeDesc: true);

    PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty() =>
        TypeDescriptor.GetDefaultProperty(this, noCustomTypeDesc: true);

    object? ICustomTypeDescriptor.GetEditor(Type editorBaseType) =>
        TypeDescriptor.GetEditor(this, editorBaseType, noCustomTypeDesc: true);

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents() =>
        TypeDescriptor.GetEvents(this, noCustomTypeDesc: true);

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes) =>
        TypeDescriptor.GetEvents(this, attributes, noCustomTypeDesc: true);

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() =>
        Filter(TypeDescriptor.GetProperties(this, noCustomTypeDesc: true));

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes) =>
        Filter(TypeDescriptor.GetProperties(this, attributes, noCustomTypeDesc: true));

    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;

    private PropertyDescriptorCollection Filter(PropertyDescriptorCollection source)
    {
        var visible = new List<PropertyDescriptor>(source.Count);
        foreach (PropertyDescriptor prop in source)
        {
            if (IsHiddenFor(GridType, prop.Name))
            {
                continue;
            }

            var textMap = GridType == GridType.Square ? SquareText : HexText;
            visible.Add(textMap.TryGetValue(prop.Name, out var text)
                ? new RelabelledPropertyDescriptor(prop, text.DisplayName, text.Description, text.Category)
                : prop);
        }

        return new PropertyDescriptorCollection([.. visible], readOnly: true);
    }

    private static bool IsHiddenFor(GridType gridType, string propertyName) => gridType switch
    {
        GridType.Hex => propertyName is nameof(SquareSize) or nameof(AutoFitSquares) or nameof(FlushAxis) or nameof(CellGapY),
        GridType.Square => propertyName is nameof(HexOrientation) or nameof(HexWidth),
        _ => false,
    };
}
