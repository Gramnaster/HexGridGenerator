using HexGrid.Core.Labels;
using HexGrid.Core.Settings;

namespace HexGrid.Core.Tests;

public class CoordinateLabellerTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    public void ToLetters_SpreadsheetStyleIndex_ReturnsExpectedLetters(int index, string expected)
    {
        // Act
        string actual = CoordinateLabeller.ToLetters(index, skipIo: false);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(7, "H")]
    [InlineData(8, "J")]
    [InlineData(12, "N")]
    [InlineData(13, "P")]
    public void ToLetters_SkipIoTrue_SkipsIAndO(int index, string expected)
    {
        // Act
        string actual = CoordinateLabeller.ToLetters(index, skipIo: true);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToLetters_NegativeIndex_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => CoordinateLabeller.ToLetters(-1, skipIo: false));
    }

    [Theory]
    [InlineData(0, 10, false, "1")]
    [InlineData(0, 10, true, "01")]
    [InlineData(9, 10, true, "10")]
    [InlineData(0, 100, true, "001")]
    public void ToNumber_ZeroPad_PadsToWidthOfCount(int index, int count, bool zeroPad, string expected)
    {
        // Act
        string actual = CoordinateLabeller.ToNumber(index, count, zeroPad);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildAxes_TopLeftOrigin_ColumnsLettersAscendingRowsNumbersAscending()
    {
        // Act
        (string[] columns, string[] rows) = CoordinateLabeller.BuildAxes(
            3, 2, LabelScheme.LettersNumbers, CoordinateOrigin.TopLeft, skipIo: false, zeroPad: false);

        // Assert
        Assert.Equal(["A", "B", "C"], columns);
        Assert.Equal(["1", "2"], rows);
    }

    [Fact]
    public void BuildAxes_TopRightOrigin_ColumnsCountRightToLeft()
    {
        // Act
        (string[] columns, string[] rows) = CoordinateLabeller.BuildAxes(
            3, 2, LabelScheme.LettersNumbers, CoordinateOrigin.TopRight, skipIo: false, zeroPad: false);

        // Assert: leftmost displayed column is labelled C (A sits at the physical top-right corner).
        Assert.Equal(["C", "B", "A"], columns);
        Assert.Equal(["1", "2"], rows);
    }

    [Fact]
    public void BuildAxes_BottomLeftOrigin_RowsCountBottomToTop()
    {
        // Act
        (string[] columns, string[] rows) = CoordinateLabeller.BuildAxes(
            3, 2, LabelScheme.LettersNumbers, CoordinateOrigin.BottomLeft, skipIo: false, zeroPad: false);

        // Assert: topmost displayed row is labelled 2 (row 1 sits at the physical bottom-left corner).
        Assert.Equal(["A", "B", "C"], columns);
        Assert.Equal(["2", "1"], rows);
    }

    [Fact]
    public void BuildAxes_NumbersLettersScheme_SwapsWhichAxisCarriesLetters()
    {
        // Act
        (string[] columns, string[] rows) = CoordinateLabeller.BuildAxes(
            2, 2, LabelScheme.NumbersLetters, CoordinateOrigin.TopLeft, skipIo: false, zeroPad: false);

        // Assert
        Assert.Equal(["1", "2"], columns);
        Assert.Equal(["A", "B"], rows);
    }

    [Fact]
    public void BuildAxes_NumbersNumbersScheme_BothAxesNumeric()
    {
        // Act
        (string[] columns, string[] rows) = CoordinateLabeller.BuildAxes(
            2, 2, LabelScheme.NumbersNumbers, CoordinateOrigin.TopLeft, skipIo: false, zeroPad: false);

        // Assert
        Assert.Equal(["1", "2"], columns);
        Assert.Equal(["1", "2"], rows);
    }

    [Theory]
    [InlineData("A", "1", "", "A1")]
    [InlineData("A", "1", "-", "A-1")]
    public void Combine_JoinsColumnAndRowWithSeparator(string column, string row, string separator, string expected)
    {
        // Act
        string actual = CoordinateLabeller.Combine(column, row, separator);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(LabelScheme.LettersNumbers, false, false)]
    [InlineData(LabelScheme.LettersNumbers, false, true)]
    [InlineData(LabelScheme.NumbersLetters, false, false)]
    [InlineData(LabelScheme.NumbersLetters, true, false)]
    [InlineData(LabelScheme.NumbersNumbers, false, false)]
    [InlineData(LabelScheme.NumbersNumbers, true, true)]
    public void MaxRowLabelLength_AnyRowCount_MatchesLongestBuiltRowLabel(LabelScheme scheme, bool skipIo, bool zeroPad)
    {
        // Arrange: the arithmetic shortcut must agree exactly with actually building every label,
        // across digit and letter-count boundaries (9 → 10, Z → AA, ZZ → AAA with and without I/O).
        for (int rows = 1; rows <= 800; rows++)
        {
            (_, string[] built) = CoordinateLabeller.BuildAxes(1, rows, scheme, CoordinateOrigin.BottomRight, skipIo, zeroPad);
            int expected = built.Max(label => label.Length);

            // Act
            int actual = CoordinateLabeller.MaxRowLabelLength(rows, scheme, skipIo);

            // Assert
            Assert.True(expected == actual, $"rows={rows}: built {expected}, computed {actual}");
        }
    }
}
