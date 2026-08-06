namespace HexGrid.Core;

/// <summary>Which axis carries letters and which carries numbers.</summary>
public enum LabelScheme
{
    /// <summary>Columns A B C..., rows 1 2 3... (A1, B1, C1).</summary>
    LettersNumbers,

    /// <summary>Columns 1 2 3..., rows A B C... (1A, 1B, 1C).</summary>
    NumbersLetters,

    /// <summary>Both axes numeric (01.01 style).</summary>
    NumbersNumbers,
}
