using System.ComponentModel;
using System.Drawing;

namespace HexGrid.Core;

/// <summary>
/// Every knob the generator exposes. Bound directly to a WinForms PropertyGrid, so ordering,
/// grouping and help text all come from the attributes below.
/// </summary>
/// <remarks>
/// <para>
/// Unit convention: every <b>length</b> (canvas size, margins, hex width, line thickness, dot radius,
/// padding, offsets) is expressed in <see cref="Unit"/>. Every <b>font size</b> is in points, because
/// points are resolution-independent and convert cleanly through <see cref="Dpi"/>.
/// </para>
/// <para>
/// Layout order from the canvas edge inward: safe margin, coordinate-label band, frame rule, then the
/// map area. The grid always fills the map area edge to edge and is clipped at the frame.
/// </para>
/// </remarks>
public sealed class GridSettings : ICustomTypeDescriptor
{
    // ------------------------------------------------------------ 0 Grid type

    [Category("0 · Grid Type")]
    [DisplayName("Grid type")]
    [Description("Hex tiles the map area with regular hexagons, which meet the frame by overhanging it and being clipped. Square tiles it with axis-aligned squares, which can fit the area exactly. Switching this reshapes the options below. Default: Hex.")]
    [DefaultValue(GridType.Hex)]
    public GridType GridType { get; set; } = GridType.Hex;

    // ---------------------------------------------------------------- 1 Canvas

    [Category("1 · Canvas")]
    [DisplayName("Preset")]
    [Description("Paper size (ISO 216) or screen resolution. Choose Custom to drive the canvas from Width/Height below. Default: A3.")]
    [DefaultValue(CanvasPreset.A3)]
    public CanvasPreset Preset { get; set; } = CanvasPreset.A3;

    [Category("1 · Canvas")]
    [DisplayName("Page orientation")]
    [Description("Applies to paper presets only. Screen presets and Custom are used exactly as given. Default: Landscape.")]
    [DefaultValue(PageOrientation.Landscape)]
    public PageOrientation PageOrientation { get; set; } = PageOrientation.Landscape;

    [Category("1 · Canvas")]
    [DisplayName("Unit")]
    [Description("The unit used for EVERY length in this dialog: canvas size, margins, hex width, line thickness, dot radius, padding and offsets. Font sizes are always in points. Default: Millimeters.")]
    [DefaultValue(LengthUnit.Millimeters)]
    public LengthUnit Unit { get; set; } = LengthUnit.Millimeters;

    [Category("1 · Canvas")]
    [DisplayName("Custom width")]
    [Description("Canvas width, in Unit. Used only when Preset = Custom. Default: 420.")]
    [DefaultValue(420.0)]
    public double CustomWidth { get; set; } = 420.0;

    [Category("1 · Canvas")]
    [DisplayName("Custom height")]
    [Description("Canvas height, in Unit. Used only when Preset = Custom. Default: 297.")]
    [DefaultValue(297.0)]
    public double CustomHeight { get; set; } = 297.0;

    [Category("1 · Canvas")]
    [DisplayName("DPI")]
    [Description("Pixels per inch. Drives physical-unit to pixel conversion for PNG export and font sizing. 300 for print, 96 for screen work. Default: 300.")]
    [DefaultValue(300)]
    public int Dpi { get; set; } = 300;

    // ------------------------------------------------------------------ 2 Grid

    [Category("2 · Grid")]
    [DisplayName("Hex orientation")]
    [Description("FlatTop puts a flat edge at the top of each hex and a vertex left and right. PointyTop is the reverse. Default: FlatTop.")]
    [DefaultValue(HexOrientation.FlatTop)]
    public HexOrientation HexOrientation { get; set; } = HexOrientation.FlatTop;

    [Category("2 · Grid")]
    [DisplayName("Auto-fit squares")]
    [Description("On: whole squares only. The grid is centred in the map area and any leftover space becomes an even margin - nothing is clipped. Off: the grid fills the map area edge to edge like the hex grid does, and the outermost squares are clipped at the frame. Default: True.")]
    [DefaultValue(true)]
    public bool AutoFitSquares { get; set; } = true;

    [Category("2 · Grid")]
    [DisplayName("Sizing mode")]
    [Description("AutoFitRowsColumns: rows and columns set the hex size. FixedHexWidth: the hex width sets how many rows and columns there are. Either way the grid fills the map area and is clipped at the frame, so counts may come out slightly higher than requested. Default: AutoFitRowsColumns.")]
    [DefaultValue(GridSizingMode.AutoFitRowsColumns)]
    public GridSizingMode SizingMode { get; set; } = GridSizingMode.AutoFitRowsColumns;

    [Category("2 · Grid")]
    [DisplayName("Columns")]
    [Description("Requested column count, treated as a minimum. Used as an input in AutoFitRowsColumns mode only. Default: 30.")]
    [DefaultValue(30)]
    public int Columns { get; set; } = 30;

    [Category("2 · Grid")]
    [DisplayName("Rows")]
    [Description("Requested row count, treated as a minimum. Used as an input in AutoFitRowsColumns mode only. Default: 20.")]
    [DefaultValue(20)]
    public int Rows { get; set; } = 20;

    [Category("2 · Grid")]
    [DisplayName("Hex width")]
    [Description("Width of one hex in Unit: corner-to-corner for flat-top, flat-to-flat for pointy-top. Used only in FixedHexWidth mode. Default: 12.")]
    [DefaultValue(12.0)]
    public double HexWidth { get; set; } = 12.0;

    [Category("2 · Grid")]
    [DisplayName("Square size")]
    [Description("Side length of one square, in Unit. Used only in FixedHexWidth mode. Default: 12.")]
    [DefaultValue(12.0)]
    public double SquareSize { get; set; } = 12.0;

    [Category("2 · Grid")]
    [DisplayName("Grid inset")]
    [Description("Gap in Unit between the frame rule and the grid. Leave at 0 so the hexes run right up to the frame. Default: 0.")]
    [DefaultValue(0.0)]
    public double GridInset { get; set; }

    [Category("2 · Grid")]
    [DisplayName("Grid offset X")]
    [Description("Fine nudge of the whole grid, in Unit. Positive moves right. Hexes stay clipped at the frame. Default: 0.")]
    [DefaultValue(0.0)]
    public double GridOffsetX { get; set; }

    [Category("2 · Grid")]
    [DisplayName("Grid offset Y")]
    [Description("Fine nudge of the whole grid, in Unit. Positive moves down. Default: 0.")]
    [DefaultValue(0.0)]
    public double GridOffsetY { get; set; }

    // ------------------------------------------------------------ 3 Appearance

    [Category("3 · Appearance")]
    [DisplayName("Line colour")]
    [Description("Hex outline colour. Default: Black.")]
    public Color LineColor { get; set; } = Color.Black;

    [Category("3 · Appearance")]
    [DisplayName("Line opacity %")]
    [Description("0 = invisible, 100 = solid. Overlays usually read best at 40-70. Default: 100.")]
    [DefaultValue(100)]
    public int LineOpacity { get; set; } = 100;

    [Category("3 · Appearance")]
    [DisplayName("Line thickness")]
    [Description("Hex outline thickness in Unit. Default: 0.25.")]
    [DefaultValue(0.25)]
    public double LineThickness { get; set; } = 0.25;

    [Category("3 · Appearance")]
    [DisplayName("Hex fill colour")]
    [Description("Fill for the hex interior. Leave the opacity at 0 for a pure overlay. Default: White.")]
    public Color HexFillColor { get; set; } = Color.White;

    [Category("3 · Appearance")]
    [DisplayName("Hex fill opacity %")]
    [Description("0 = no fill (the usual choice for an overlay). Default: 0.")]
    [DefaultValue(0)]
    public int HexFillOpacity { get; set; }

    [Category("3 · Appearance")]
    [DisplayName("Show centre dots")]
    [Description("Draw a dot at the exact centre of every hex. Default: True.")]
    [DefaultValue(true)]
    public bool ShowCenterDots { get; set; } = true;

    [Category("3 · Appearance")]
    [DisplayName("Dot radius")]
    [Description("Centre dot radius in Unit. Default: 0.4.")]
    [DefaultValue(0.4)]
    public double DotRadius { get; set; } = 0.4;

    [Category("3 · Appearance")]
    [DisplayName("Dot colour")]
    [Description("Default: Black.")]
    public Color DotColor { get; set; } = Color.Black;

    [Category("3 · Appearance")]
    [DisplayName("Dot opacity %")]
    [Description("Default: 100.")]
    [DefaultValue(100)]
    public int DotOpacity { get; set; } = 100;

    // ------------------------------------------------- 4 Text and coordinates

    [Category("4 · Text & Coordinates")]
    [DisplayName("Font family")]
    [Description("Used for every piece of text: hex labels and edge labels. Default: Segoe UI.")]
    [DefaultValue("Segoe UI")]
    public string FontFamily { get; set; } = "Segoe UI";

    [Category("4 · Text & Coordinates")]
    [DisplayName("Label scheme")]
    [Description("Which axis carries letters. LettersNumbers gives A1, B1, C1 across the top. Default: LettersNumbers.")]
    [DefaultValue(LabelScheme.LettersNumbers)]
    public LabelScheme LabelScheme { get; set; } = LabelScheme.LettersNumbers;

    [Category("4 · Text & Coordinates")]
    [DisplayName("Coordinate origin")]
    [Description("Which physical corner of the grid is A1. Default: TopLeft.")]
    [DefaultValue(CoordinateOrigin.TopLeft)]
    public CoordinateOrigin CoordinateOrigin { get; set; } = CoordinateOrigin.TopLeft;

    [Category("4 · Text & Coordinates")]
    [DisplayName("Skip letters I and O")]
    [Description("Standard military-mapping practice: I and O are dropped because they read as 1 and 0. Default: True.")]
    [DefaultValue(true)]
    public bool SkipLettersIO { get; set; } = true;

    [Category("4 · Text & Coordinates")]
    [DisplayName("Zero-pad numbers")]
    [Description("Pad the numeric axis to the width of its largest value, so 1 becomes 01 when the grid reaches 10+. Default: False.")]
    [DefaultValue(false)]
    public bool ZeroPadNumbers { get; set; }

    [Category("4 · Text & Coordinates")]
    [DisplayName("Coordinate separator")]
    [Description("Placed between the two halves of a hex label. Empty gives A1, a dash gives A-1. Default: (empty).")]
    [DefaultValue("")]
    public string CoordinateSeparator { get; set; } = string.Empty;

    // ------------------------------------------------------------ 5 Hex labels

    [Category("5 · Hex Labels")]
    [DisplayName("Show label in every hex")]
    [Description("Print the coordinate inside each hex. Off by default; edge labels are usually enough for an overlay. Default: False.")]
    [DefaultValue(false)]
    public bool ShowHexLabels { get; set; }

    [Category("5 · Hex Labels")]
    [DisplayName("Position in hex")]
    [Description("Where the coordinate sits inside its hex. Center rests the label just above the centre dot rather than on top of it; with dots switched off it is centred properly. Default: Top.")]
    [DefaultValue(HexLabelPosition.Top)]
    public HexLabelPosition HexLabelPosition { get; set; } = HexLabelPosition.Top;

    [Category("5 · Hex Labels")]
    [DisplayName("Top/bottom margin %")]
    [Description("Gap between the label and the hex's top or bottom edge, as a percentage of hex height. 0 sits on the edge, 50 sits on the centre dot. Used only when Position in hex = Top or Bottom. Default: 20.")]
    [DefaultValue(20)]
    public int HexLabelMargin { get; set; } = 20;

    [Category("5 · Hex Labels")]
    [DisplayName("Font size (pt)")]
    [Description("Default: 6.")]
    [DefaultValue(6.0)]
    public double HexLabelFontSize { get; set; } = 6.0;

    [Category("5 · Hex Labels")]
    [DisplayName("Bold")]
    [Description("Default: False.")]
    [DefaultValue(false)]
    public bool HexLabelBold { get; set; }

    [Category("5 · Hex Labels")]
    [DisplayName("Colour")]
    [Description("Default: Black.")]
    public Color HexLabelColor { get; set; } = Color.Black;

    [Category("5 · Hex Labels")]
    [DisplayName("Opacity %")]
    [Description("Default: 100.")]
    [DefaultValue(100)]
    public int HexLabelOpacity { get; set; } = 100;

    // ----------------------------------------------------------- 6 Edge labels

    [Category("6 · Edge Labels")]
    [DisplayName("Sides")]
    [Description("Which margins carry coordinate labels. They sit OUTSIDE the frame rule. Top|Left is the wargame convention; All is the atlas convention. Default: All.")]
    [DefaultValue(LabelSides.All)]
    public LabelSides MarginalLabelSides { get; set; } = LabelSides.All;

    [Category("6 · Edge Labels")]
    [DisplayName("Font size (pt)")]
    [Description("Default: 7.")]
    [DefaultValue(7.0)]
    public double MarginalFontSize { get; set; } = 7.0;

    [Category("6 · Edge Labels")]
    [DisplayName("Bold")]
    [Description("Default: False.")]
    [DefaultValue(false)]
    public bool MarginalBold { get; set; }

    [Category("6 · Edge Labels")]
    [DisplayName("Colour")]
    [Description("Default: Black.")]
    public Color MarginalColor { get; set; } = Color.Black;

    [Category("6 · Edge Labels")]
    [DisplayName("Padding from frame")]
    [Description("Gap in Unit between the frame rule and the label text. Default: 1.2.")]
    [DefaultValue(1.2)]
    public double LabelPadding { get; set; } = 1.2;

    // ----------------------------------------------------------------- 7 Frame

    [Category("7 · Frame")]
    [DisplayName("Style")]
    [Description("The rule between the map area and the coordinate-label band. The grid is clipped at it. Default: Line.")]
    [DefaultValue(MapBorderStyle.Line)]
    public MapBorderStyle BorderStyle { get; set; } = MapBorderStyle.Line;

    [Category("7 · Frame")]
    [DisplayName("Colour")]
    [Description("Default: Black.")]
    public Color BorderColor { get; set; } = Color.Black;

    [Category("7 · Frame")]
    [DisplayName("Thickness")]
    [Description("Frame rule thickness in Unit. Default: 0.5.")]
    [DefaultValue(0.5)]
    public double BorderThickness { get; set; } = 0.5;

    [Category("7 · Frame")]
    [DisplayName("Safe margin")]
    [Description("Gap in Unit between the canvas edge and the outside of the label band, so nothing touches the trim edge. Default: 2.")]
    [DefaultValue(2.0)]
    public double SafeMargin { get; set; } = 2.0;

    // ---------------------------------------------------------------- 8 Export

    [Category("8 · Export")]
    [DisplayName("PNG background")]
    [Description("Transparent is the overlay default. White or Black help when proofing the grid on its own. Default: Transparent.")]
    [DefaultValue(PngBackground.Transparent)]
    public PngBackground PngBackground { get; set; } = PngBackground.Transparent;

    [Category("8 · Export")]
    [DisplayName("PNG custom background")]
    [Description("Used only when PNG background = Custom. Default: White.")]
    public Color PngCustomBackground { get; set; } = Color.White;

    [Category("8 · Export")]
    [DisplayName("Antialiasing")]
    [Description("Off gives hard aliased pixels, which some pixel-art workflows prefer. Affects PNG only; SVG is resolution independent. Default: True.")]
    [DefaultValue(true)]
    public bool Antialiasing { get; set; } = true;

    [Category("8 · Export")]
    [DisplayName("Export layers separately")]
    [Description("Write one PNG per layer (grid, dots, hex labels, edge labels, frame) alongside the flattened image, ready to stack in Photoshop. Default: False.")]
    [DefaultValue(false)]
    public bool ExportLayersSeparately { get; set; }

    [Category("8 · Export")]
    [DisplayName("Filename pattern")]
    [Description("Tokens: {preset} {w} {h} {cols} {rows} {hexw} {hexwu} {dpi} {orient}. Extension is added automatically. Default: HexGrid_{preset}_{cols}x{rows}_{hexw}px.")]
    [DefaultValue("HexGrid_{preset}_{cols}x{rows}_{hexw}px")]
    public string FileNamePattern { get; set; } = "HexGrid_{preset}_{cols}x{rows}_{hexw}px";

    public GridSettings Clone() => (GridSettings)MemberwiseClone();

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

            visible.Add(GridType == GridType.Square && SquareText.TryGetValue(prop.Name, out var text)
                ? new RelabelledPropertyDescriptor(prop, text.DisplayName, text.Description, text.Category)
                : prop);
        }

        return new PropertyDescriptorCollection([.. visible], readOnly: true);
    }

    private static bool IsHiddenFor(GridType gridType, string propertyName) => gridType switch
    {
        GridType.Hex => propertyName is nameof(SquareSize) or nameof(AutoFitSquares),
        GridType.Square => propertyName is nameof(HexOrientation) or nameof(HexWidth),
        _ => false,
    };
}
