namespace HexGrid.Benchmarks;

/// <summary>Representative workloads, from what most users run to the large end of what the tool supports.</summary>
public enum Scenario
{
    /// <summary>Out-of-the-box settings: A3, 30 × 20 flat-top hexes, edge labels, centre dots.</summary>
    DefaultHex,

    /// <summary>120 × 80 hexes with a label in every cell: about 10,500 cells and 54,000 draw items.</summary>
    LargeHexLabelled,

    /// <summary>40 × 28 auto-fit squares with Gap X/Y: exercises the square engine and the fit advisor.</summary>
    SquareAutoFitGapped,
}
