using System.Drawing;
using HexGrid.Core.Layout;
using HexGrid.Core.Naming;

namespace HexGrid.Core.Tests;

public class FileNameBuilderTests
{
    [Fact]
    public void Build_ExpandsEveryToken()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.Preset = CanvasPreset.A3;
        s.Unit = LengthUnit.Pixels;
        s.Dpi = 300;
        s.HexOrientation = HexOrientation.FlatTop;
        s.FileNamePattern = "{preset}_{w}x{h}_{cols}x{rows}_{hexw}_{hexwu}_{dpi}_{orient}";
        GridLayout layout = FakeLayout(canvasW: 800.4, canvasH: 600.6, columns: 12, rows: 8, hexWidthPx: 25.6);

        // Act
        string name = FileNameBuilder.Build(s, layout);

        // Assert
        Assert.Equal("A3_800x601_12x8_26_25.6_300_flat", name);
    }

    [Fact]
    public void Build_ReplacesSpacesWithUnderscore()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.FileNamePattern = "Hex Grid {cols}";
        GridLayout layout = FakeLayout(columns: 12);

        // Act
        string name = FileNameBuilder.Build(s, layout);

        // Assert
        Assert.Equal("Hex_Grid_12", name);
    }

    [Fact]
    public void Build_ReplacesWindowsInvalidFileNameCharsWithUnderscore()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.FileNamePattern = "Grid:{cols}";
        GridLayout layout = FakeLayout(columns: 12);

        // Act
        string name = FileNameBuilder.Build(s, layout);

        // Assert
        Assert.Equal("Grid_12", name);
    }

    [Fact]
    public void Build_PatternThatSanitisesToNothing_FallsBackToHexGrid()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.FileNamePattern = "   ";
        GridLayout layout = FakeLayout();

        // Act
        string name = FileNameBuilder.Build(s, layout);

        // Assert
        Assert.Equal("HexGrid", name);
    }

    [Fact]
    public void Build_TokensAreCaseInsensitive()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.FileNamePattern = "{COLS}x{Rows}";
        GridLayout layout = FakeLayout(columns: 12, rows: 8);

        // Act
        string name = FileNameBuilder.Build(s, layout);

        // Assert
        Assert.Equal("12x8", name);
    }

    [Fact]
    public void Build_NullSettings_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => FileNameBuilder.Build(null!, FakeLayout()));
    }

    [Fact]
    public void Build_NullLayout_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => FileNameBuilder.Build(TestSettings.Minimal(), null!));
    }

    /// <summary>Hand-built layout so these tests exercise only <see cref="FileNameBuilder"/>, not <see cref="GridLayoutEngine"/>.</summary>
    private static GridLayout FakeLayout(
        double canvasW = 100, double canvasH = 100, int columns = 1, int rows = 1, double hexWidthPx = 10) => new()
    {
        CanvasWidthPx = canvasW,
        CanvasHeightPx = canvasH,
        CanvasWidthMm = canvasW,
        CanvasHeightMm = canvasH,
        Columns = columns,
        Rows = rows,
        CellRadiusPx = hexWidthPx / 2,
        CellWidthPx = hexWidthPx,
        CellHeightPx = hexWidthPx,
        FrameBounds = RectangleF.Empty,
        ClipBounds = RectangleF.Empty,
        GridBounds = RectangleF.Empty,
        Cells = [],
        ColumnCenterXs = [],
        RowCenterYs = [],
        ColumnLabels = [],
        RowLabels = [],
    };
}
