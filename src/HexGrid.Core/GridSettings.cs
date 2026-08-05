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
public sealed class GridSettings
{
    // ---------------------------------------------------------------- 1 Canvas

    [Category("1 · Canvas")]
    [DisplayName("Preset")]
    [Description("Paper size (ISO 216) or screen resolution. Choose Custom to drive the canvas from Width/Height below.")]
    [DefaultValue(CanvasPreset.A3)]
    public CanvasPreset Preset { get; set; } = CanvasPreset.A3;

    [Category("1 · Canvas")]
    [DisplayName("Page orientation")]
    [Description("Applies to paper presets only. Screen presets and Custom are used exactly as given.")]
    [DefaultValue(PageOrientation.Landscape)]
    public PageOrientation PageOrientation { get; set; } = PageOrientation.Landscape;

    [Category("1 · Canvas")]
    [DisplayName("Unit")]
    [Description("The unit used for EVERY length in this dialog: canvas size, margins, hex width, line thickness, dot radius, padding and offsets. Font sizes are always in points.")]
    [DefaultValue(LengthUnit.Millimeters)]
    public LengthUnit Unit { get; set; } = LengthUnit.Millimeters;

    [Category("1 · Canvas")]
    [DisplayName("Custom width")]
    [Description("Canvas width, in Unit. Used only when Preset = Custom.")]
    [DefaultValue(420.0)]
    public double CustomWidth { get; set; } = 420.0;

    [Category("1 · Canvas")]
    [DisplayName("Custom height")]
    [Description("Canvas height, in Unit. Used only when Preset = Custom.")]
    [DefaultValue(297.0)]
    public double CustomHeight { get; set; } = 297.0;

    [Category("1 · Canvas")]
    [DisplayName("DPI")]
    [Description("Pixels per inch. Drives physical-unit to pixel conversion for PNG export and font sizing. 300 for print, 96 for screen work.")]
    [DefaultValue(300)]
    public int Dpi { get; set; } = 300;

    // ------------------------------------------------------------------ 2 Grid

    [Category("2 · Grid")]
    [DisplayName("Hex orientation")]
    [Description("FlatTop puts a flat edge at the top of each hex and a vertex left and right. PointyTop is the reverse.")]
    [DefaultValue(HexOrientation.FlatTop)]
    public HexOrientation HexOrientation { get; set; } = HexOrientation.FlatTop;

    [Category("2 · Grid")]
    [DisplayName("Sizing mode")]
    [Description("AutoFitRowsColumns: rows and columns set the hex size. FixedHexWidth: the hex width sets how many rows and columns there are. Either way the grid fills the map area and is clipped at the frame, so counts may come out slightly higher than requested.")]
    [DefaultValue(GridSizingMode.AutoFitRowsColumns)]
    public GridSizingMode SizingMode { get; set; } = GridSizingMode.AutoFitRowsColumns;

    [Category("2 · Grid")]
    [DisplayName("Columns")]
    [Description("Requested column count, treated as a minimum. Used as an input in AutoFitRowsColumns mode only.")]
    [DefaultValue(30)]
    public int Columns { get; set; } = 30;

    [Category("2 · Grid")]
    [DisplayName("Rows")]
    [Description("Requested row count, treated as a minimum. Used as an input in AutoFitRowsColumns mode only.")]
    [DefaultValue(20)]
    public int Rows { get; set; } = 20;

    [Category("2 · Grid")]
    [DisplayName("Hex width")]
    [Description("Width of one hex in Unit: corner-to-corner for flat-top, flat-to-flat for pointy-top. Used only in FixedHexWidth mode.")]
    [DefaultValue(12.0)]
    public double HexWidth { get; set; } = 12.0;

    [Category("2 · Grid")]
    [DisplayName("Grid inset")]
    [Description("Gap in Unit between the frame rule and the grid. Leave at 0 so the hexes run right up to the frame.")]
    [DefaultValue(0.0)]
    public double GridInset { get; set; }

    [Category("2 · Grid")]
    [DisplayName("Grid offset X")]
    [Description("Fine nudge of the whole grid, in Unit. Positive moves right. Hexes stay clipped at the frame.")]
    [DefaultValue(0.0)]
    public double GridOffsetX { get; set; }

    [Category("2 · Grid")]
    [DisplayName("Grid offset Y")]
    [Description("Fine nudge of the whole grid, in Unit. Positive moves down.")]
    [DefaultValue(0.0)]
    public double GridOffsetY { get; set; }

    // ------------------------------------------------------------ 3 Appearance

    [Category("3 · Appearance")]
    [DisplayName("Line colour")]
    [Description("Hex outline colour.")]
    public Color LineColor { get; set; } = Color.Black;

    [Category("3 · Appearance")]
    [DisplayName("Line opacity %")]
    [Description("0 = invisible, 100 = solid. Overlays usually read best at 40-70.")]
    [DefaultValue(100)]
    public int LineOpacity { get; set; } = 100;

    [Category("3 · Appearance")]
    [DisplayName("Line thickness")]
    [Description("Hex outline thickness in Unit.")]
    [DefaultValue(0.25)]
    public double LineThickness { get; set; } = 0.25;

    [Category("3 · Appearance")]
    [DisplayName("Hex fill colour")]
    [Description("Fill for the hex interior. Leave the opacity at 0 for a pure overlay.")]
    public Color HexFillColor { get; set; } = Color.White;

    [Category("3 · Appearance")]
    [DisplayName("Hex fill opacity %")]
    [Description("0 = no fill (the usual choice for an overlay).")]
    [DefaultValue(0)]
    public int HexFillOpacity { get; set; }

    [Category("3 · Appearance")]
    [DisplayName("Show centre dots")]
    [Description("Draw a dot at the exact centre of every hex.")]
    [DefaultValue(true)]
    public bool ShowCenterDots { get; set; } = true;

    [Category("3 · Appearance")]
    [DisplayName("Dot radius")]
    [Description("Centre dot radius in Unit.")]
    [DefaultValue(0.4)]
    public double DotRadius { get; set; } = 0.4;

    [Category("3 · Appearance")]
    [DisplayName("Dot colour")]
    public Color DotColor { get; set; } = Color.Black;

    [Category("3 · Appearance")]
    [DisplayName("Dot opacity %")]
    [DefaultValue(100)]
    public int DotOpacity { get; set; } = 100;

    // ------------------------------------------------- 4 Text and coordinates

    [Category("4 · Text & Coordinates")]
    [DisplayName("Font family")]
    [Description("Used for every piece of text: hex labels and edge labels.")]
    [DefaultValue("Segoe UI")]
    public string FontFamily { get; set; } = "Segoe UI";

    [Category("4 · Text & Coordinates")]
    [DisplayName("Label scheme")]
    [Description("Which axis carries letters. LettersNumbers gives A1, B1, C1 across the top.")]
    [DefaultValue(LabelScheme.LettersNumbers)]
    public LabelScheme LabelScheme { get; set; } = LabelScheme.LettersNumbers;

    [Category("4 · Text & Coordinates")]
    [DisplayName("Coordinate origin")]
    [Description("Which physical corner of the grid is A1.")]
    [DefaultValue(CoordinateOrigin.TopLeft)]
    public CoordinateOrigin CoordinateOrigin { get; set; } = CoordinateOrigin.TopLeft;

    [Category("4 · Text & Coordinates")]
    [DisplayName("Skip letters I and O")]
    [Description("Standard military-mapping practice: I and O are dropped because they read as 1 and 0.")]
    [DefaultValue(true)]
    public bool SkipLettersIO { get; set; } = true;

    [Category("4 · Text & Coordinates")]
    [DisplayName("Zero-pad numbers")]
    [Description("Pad the numeric axis to the width of its largest value, so 1 becomes 01 when the grid reaches 10+.")]
    [DefaultValue(false)]
    public bool ZeroPadNumbers { get; set; }

    [Category("4 · Text & Coordinates")]
    [DisplayName("Coordinate separator")]
    [Description("Placed between the two halves of a hex label. Empty gives A1, a dash gives A-1.")]
    [DefaultValue("")]
    public string CoordinateSeparator { get; set; } = string.Empty;

    // ------------------------------------------------------------ 5 Hex labels

    [Category("5 · Hex Labels")]
    [DisplayName("Show label in every hex")]
    [Description("Print the coordinate inside each hex. Off by default; edge labels are usually enough for an overlay.")]
    [DefaultValue(false)]
    public bool ShowHexLabels { get; set; }

    [Category("5 · Hex Labels")]
    [DisplayName("Position in hex")]
    [Description("Where the coordinate sits inside its hex. Center rests the label just above the centre dot rather than on top of it; with dots switched off it is centred properly.")]
    [DefaultValue(HexLabelPosition.Top)]
    public HexLabelPosition HexLabelPosition { get; set; } = HexLabelPosition.Top;

    [Category("5 · Hex Labels")]
    [DisplayName("Font size (pt)")]
    [DefaultValue(6.0)]
    public double HexLabelFontSize { get; set; } = 6.0;

    [Category("5 · Hex Labels")]
    [DisplayName("Bold")]
    [DefaultValue(false)]
    public bool HexLabelBold { get; set; }

    [Category("5 · Hex Labels")]
    [DisplayName("Colour")]
    public Color HexLabelColor { get; set; } = Color.Black;

    [Category("5 · Hex Labels")]
    [DisplayName("Opacity %")]
    [DefaultValue(100)]
    public int HexLabelOpacity { get; set; } = 100;

    // ----------------------------------------------------------- 6 Edge labels

    [Category("6 · Edge Labels")]
    [DisplayName("Sides")]
    [Description("Which margins carry coordinate labels. They sit OUTSIDE the frame rule. Top|Left is the wargame convention; All is the atlas convention.")]
    [DefaultValue(LabelSides.All)]
    public LabelSides MarginalLabelSides { get; set; } = LabelSides.All;

    [Category("6 · Edge Labels")]
    [DisplayName("Font size (pt)")]
    [DefaultValue(7.0)]
    public double MarginalFontSize { get; set; } = 7.0;

    [Category("6 · Edge Labels")]
    [DisplayName("Bold")]
    [DefaultValue(false)]
    public bool MarginalBold { get; set; }

    [Category("6 · Edge Labels")]
    [DisplayName("Colour")]
    public Color MarginalColor { get; set; } = Color.Black;

    [Category("6 · Edge Labels")]
    [DisplayName("Padding from frame")]
    [Description("Gap in Unit between the frame rule and the label text.")]
    [DefaultValue(1.2)]
    public double LabelPadding { get; set; } = 1.2;

    // ----------------------------------------------------------------- 7 Frame

    [Category("7 · Frame")]
    [DisplayName("Style")]
    [Description("The rule between the map area and the coordinate-label band. The grid is clipped at it.")]
    [DefaultValue(MapBorderStyle.Line)]
    public MapBorderStyle BorderStyle { get; set; } = MapBorderStyle.Line;

    [Category("7 · Frame")]
    [DisplayName("Colour")]
    public Color BorderColor { get; set; } = Color.Black;

    [Category("7 · Frame")]
    [DisplayName("Thickness")]
    [Description("Frame rule thickness in Unit.")]
    [DefaultValue(0.5)]
    public double BorderThickness { get; set; } = 0.5;

    [Category("7 · Frame")]
    [DisplayName("Safe margin")]
    [Description("Gap in Unit between the canvas edge and the outside of the label band, so nothing touches the trim edge.")]
    [DefaultValue(2.0)]
    public double SafeMargin { get; set; } = 2.0;

    // ---------------------------------------------------------------- 8 Export

    [Category("8 · Export")]
    [DisplayName("PNG background")]
    [Description("Transparent is the overlay default. White or Black help when proofing the grid on its own.")]
    [DefaultValue(PngBackground.Transparent)]
    public PngBackground PngBackground { get; set; } = PngBackground.Transparent;

    [Category("8 · Export")]
    [DisplayName("PNG custom background")]
    [Description("Used only when PNG background = Custom.")]
    public Color PngCustomBackground { get; set; } = Color.White;

    [Category("8 · Export")]
    [DisplayName("Antialiasing")]
    [Description("Off gives hard aliased pixels, which some pixel-art workflows prefer. Affects PNG only; SVG is resolution independent.")]
    [DefaultValue(true)]
    public bool Antialiasing { get; set; } = true;

    [Category("8 · Export")]
    [DisplayName("Export layers separately")]
    [Description("Write one PNG per layer (grid, dots, hex labels, edge labels, frame) alongside the flattened image, ready to stack in Photoshop.")]
    [DefaultValue(false)]
    public bool ExportLayersSeparately { get; set; }

    [Category("8 · Export")]
    [DisplayName("Filename pattern")]
    [Description("Tokens: {preset} {w} {h} {cols} {rows} {hexw} {hexwu} {dpi} {orient}. Extension is added automatically.")]
    [DefaultValue("HexGrid_{preset}_{cols}x{rows}_{hexw}px")]
    public string FileNamePattern { get; set; } = "HexGrid_{preset}_{cols}x{rows}_{hexw}px";

    public GridSettings Clone() => (GridSettings)MemberwiseClone();
}
