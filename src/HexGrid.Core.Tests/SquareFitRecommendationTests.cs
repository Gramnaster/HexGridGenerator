using System.Globalization;
using HexGrid.Core.Layout;
using HexGrid.Core.Units;

namespace HexGrid.Core.Tests;

/// <summary>
/// <see cref="SquareFitAdvisor.RecommendFit"/> backs the status-bar hint. Its numbers are only
/// useful if acting on them reproduces them, so each test feeds the suggestion back through
/// <see cref="GridLayoutEngine"/> and checks the grid actually comes out as promised.
/// </summary>
public class SquareFitRecommendationTests
{
    // 0.5 px: the same sub-pixel threshold below which the hint reports "no gap".
    private const double PxTolerance = 0.5;

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void RecommendFit_WithCellGap_CurrentLeftoverMatchesLayout(double gapMm)
    {
        // Arrange
        GridSettings s = CountsSettings(7, 7, gapMm);
        GridLayout layout = GridLayoutEngine.Build(s);

        // Act
        SquareFitSuggestion fit = SquareFitAdvisor.RecommendFit(s, layout);

        // Assert
        Assert.Equal(LeftoverX(layout), fit.CurrentLeftover.XPx, PxTolerance);
        Assert.Equal(LeftoverY(layout), fit.CurrentLeftover.YPx, PxTolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void RecommendFit_AutoFitCounts_SuggestedCountsReproduceClaimedLeftover(double gapMm)
    {
        // Arrange
        GridSettings s = CountsSettings(7, 7, gapMm);
        SquareFitSuggestion fit = SquareFitAdvisor.RecommendFit(s, GridLayoutEngine.Build(s));

        // Act
        GridLayout applied = GridLayoutEngine.Build(CountsSettings(fit.Columns, fit.Rows, gapMm));

        // Assert
        Assert.True(fit.HasTighterFit);
        Assert.Equal(fit.Leftover.XPx, LeftoverX(applied), PxTolerance);
        Assert.Equal(fit.Leftover.YPx, LeftoverY(applied), PxTolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void RecommendFit_FixedSize_DisplayedSideReproducesClaimedCounts(double gapMm)
    {
        // Arrange
        GridSettings s = FixedSettings(30.0, gapMm);
        SquareFitSuggestion fit = SquareFitAdvisor.RecommendFit(s, GridLayoutEngine.Build(s));
        var scale = new UnitScale(s.Unit, s.Dpi);

        // The status bar shows the side with "0.##"; that typed-back value is what must fit.
        double typed = double.Parse(scale.FromPx(fit.SidePx).ToString("0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        // Act
        GridLayout applied = GridLayoutEngine.Build(FixedSettings(typed, gapMm));

        // Assert
        Assert.Equal(fit.Columns, applied.Columns);
        Assert.Equal(fit.Rows, applied.Rows);
        Assert.Equal(fit.Leftover.XPx + fit.Leftover.YPx, LeftoverX(applied) + LeftoverY(applied), PxTolerance);
    }

    [Fact]
    public void RecommendFit_NoCellGap_BeatsGutterBlindSuggestion()
    {
        // Arrange: regression for the old model, which held the map area fixed across candidates.
        // For 7 x 7 on landscape A3 it recommended 23 x 16 claiming 1.4 px of leftover, but 16 rows
        // widen the row labels to two digits, which narrows the map area, and 23 x 16 really leaves
        // about 24 px. The suggestion must beat what that pair actually produces.
        GridSettings s = CountsSettings(7, 7, 0.0);
        GridLayout gutterBlind = GridLayoutEngine.Build(CountsSettings(23, 16, 0.0));

        // Act
        SquareFitSuggestion fit = SquareFitAdvisor.RecommendFit(s, GridLayoutEngine.Build(s));

        // Assert
        Assert.True(fit.Leftover.TotalPx < LeftoverX(gutterBlind) + LeftoverY(gutterBlind));
    }

    [Fact]
    public void RecommendFit_FixedSizeWithCellGap_FindsTheGapAwareExactSide()
    {
        // Arrange: a 417 × 295 px map area with a 10 px gap. n squares of side s plus (n - 1) gaps
        // fill an axis exactly when n·(s + 10) = available + 10, and 427 and 305 share only the
        // factor 61, so s = 51 (7 × 5) is the one side that closes both axes. A candidate side
        // that ignores the gap, min(417 / 7, 295 / 5), would be 59 and drop a column and a row.
        GridSettings s = TestSettings.Minimal();
        s.GridType = GridType.Square;
        s.SizingMode = GridSizingMode.FixedHexWidth;
        s.CustomWidth = 417;
        s.CustomHeight = 295;
        s.SquareSize = 45;
        s.CellGapX = 10;
        s.CellGapY = 10;

        // Act
        SquareFitSuggestion fit = SquareFitAdvisor.RecommendFit(s, GridLayoutEngine.Build(s));

        // Assert
        Assert.True(fit.HasTighterFit);
        Assert.Equal((7, 5), (fit.Columns, fit.Rows));
        Assert.Equal(51.0, fit.SidePx, 6);
        Assert.Equal(default, fit.Leftover);
    }

    private static GridSettings CountsSettings(int columns, int rows, double gapMm) => new()
    {
        GridType = GridType.Square,
        AutoFitSquares = true,
        Columns = columns,
        Rows = rows,
        CellGapX = gapMm,
        CellGapY = gapMm,
    };

    private static GridSettings FixedSettings(double sideMm, double gapMm) => new()
    {
        GridType = GridType.Square,
        AutoFitSquares = true,
        SizingMode = GridSizingMode.FixedHexWidth,
        SquareSize = sideMm,
        CellGapX = gapMm,
        CellGapY = gapMm,
    };

    private static double LeftoverX(GridLayout l) => l.NominalClipBounds.Width - l.GridBounds.Width;

    private static double LeftoverY(GridLayout l) => l.NominalClipBounds.Height - l.GridBounds.Height;
}
