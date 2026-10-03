namespace HexGrid.Core.Layout;

/// <summary>One-axis spacing arithmetic shared by the hex and square engines.</summary>
internal static class GridAxis
{
    // The auto-fit size is solved so the requested count lands exactly on the boundary, which
    // rounds the wrong way often enough to matter. Nudge before flooring.
    private const double Tolerance = 1e-6;

    /// <summary>
    /// How many centres, <paramref name="pitch"/> apart, it takes to span <paramref name="available"/>.
    /// The first and last centres sit inside the area; their cells overhang it and are clipped.
    /// </summary>
    public static int CoverCount(double available, double pitch) =>
        Math.Max(1, (int)Math.Floor((available / pitch) + Tolerance) + 1);

    /// <summary>Start position that centres a run of length <paramref name="span"/> inside [start, start + size].</summary>
    public static double CentredStart(double start, double size, double span) =>
        start + ((size - span) / 2.0);

    /// <summary>Evenly pitched centre positions along one axis, beginning at <paramref name="first"/>.</summary>
    public static double[] Centres(double first, double pitch, int count)
    {
        var centres = new double[count];
        for (int i = 0; i < count; i++)
        {
            centres[i] = first + (i * pitch);
        }

        return centres;
    }
}
