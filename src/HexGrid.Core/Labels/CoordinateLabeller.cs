using System.Globalization;
using System.Text;

namespace HexGrid.Core.Labels;

/// <summary>Turns zero-based axis indices into map coordinates.</summary>
public static class CoordinateLabeller
{
    private const string FullAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string NoIoAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>
    /// Spreadsheet-style letters: A, B, ... Z, AA, AB, ... Optionally drops I and O, which is
    /// standard practice in military mapping because they read as 1 and 0.
    /// </summary>
    public static string ToLetters(int index, bool skipIo)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        string alphabet = skipIo ? NoIoAlphabet : FullAlphabet;
        int radix = alphabet.Length;

        var sb = new StringBuilder(3);
        int n = index;

        // SS003: integer division is the algorithm, not a rounding accident: this is base-`radix`
        // digit extraction (the spreadsheet A..Z, AA..AZ column-naming scheme), and floating-point
        // division would corrupt it.
#pragma warning disable SS003
        do
        {
            sb.Insert(0, alphabet[n % radix]);
            n = (n / radix) - 1;
        }
        while (n >= 0);
#pragma warning restore SS003

        return sb.ToString();
    }

    /// <summary>Numeric axis label, optionally zero-padded to the width of the largest value on that axis.</summary>
    public static string ToNumber(int index, int count, bool zeroPad)
    {
        int value = index + 1;
        if (!zeroPad)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        int width = count.ToString(CultureInfo.InvariantCulture).Length;
        return value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
    }

    /// <summary>
    /// Builds the display-ordered label arrays for both axes, honouring the origin corner.
    /// Index 0 of each array is the leftmost column / topmost row on the canvas.
    /// </summary>
    public static (string[] ColumnLabels, string[] RowLabels) BuildAxes(
        int columns,
        int rows,
        LabelScheme scheme,
        CoordinateOrigin origin,
        bool skipIo,
        bool zeroPad)
    {
        bool lettersOnColumns = scheme == LabelScheme.LettersNumbers;
        bool numbersOnBoth = scheme == LabelScheme.NumbersNumbers;

        bool countColumnsRightToLeft = origin is CoordinateOrigin.TopRight or CoordinateOrigin.BottomRight;
        bool countRowsBottomToTop = origin is CoordinateOrigin.BottomLeft or CoordinateOrigin.BottomRight;

        var columnLabels = new string[columns];
        for (int c = 0; c < columns; c++)
        {
            int ordinal = countColumnsRightToLeft ? columns - 1 - c : c;
            columnLabels[c] = numbersOnBoth || !lettersOnColumns
                ? ToNumber(ordinal, columns, zeroPad)
                : ToLetters(ordinal, skipIo);
        }

        var rowLabels = new string[rows];
        for (int r = 0; r < rows; r++)
        {
            int ordinal = countRowsBottomToTop ? rows - 1 - r : r;
            rowLabels[r] = numbersOnBoth || lettersOnColumns
                ? ToNumber(ordinal, rows, zeroPad)
                : ToLetters(ordinal, skipIo);
        }

        return (columnLabels, rowLabels);
    }

    public static string Combine(string columnLabel, string rowLabel, string separator) =>
        string.Concat(columnLabel, separator, rowLabel);

    /// <summary>Longest label on each axis, used to reserve the edge-label gutters.</summary>
    public static (int ColumnChars, int RowChars) MaxLabelLengths(string[] columnLabels, string[] rowLabels)
    {
        int c = 0;
        foreach (string s in columnLabels)
        {
            c = Math.Max(c, s.Length);
        }

        int r = 0;
        foreach (string s in rowLabels)
        {
            r = Math.Max(r, s.Length);
        }

        return (c, r);
    }
}
