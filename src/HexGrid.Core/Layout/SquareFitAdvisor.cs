using System.Drawing;
using System.Runtime.InteropServices;
using HexGrid.Core.Units;

namespace HexGrid.Core.Layout;

/// <summary>
/// Recommends nearby Columns × Rows (or, in fixed-size mode, a nearby square size) that leave less
/// unused map area for an AutoFitSquares grid. Backs the status-bar hint.
/// </summary>
/// <remarks>
/// Every candidate is solved by <see cref="GridLayoutEngine.SolveFit"/>, the same solve
/// <see cref="GridLayoutEngine.Build"/> uses, so Gap X/Gap Y and the label gutter's dependence on
/// the counts (9 → 10 rows widens the row labels, which narrows the map area) are all accounted for.
/// What a suggestion promises is exactly what applying it produces.
/// </remarks>
public static class SquareFitAdvisor
{
    // How far from the current counts the search goes on each axis. Exact zero gap needs
    // Columns/Rows to exactly equal the map area's aspect ratio, which for arbitrary canvas sizes and
    // margins is a coincidence, not something to count on - but nearby whole-number pairs are a
    // genuine Diophantine approximation problem (best rational approximation of the aspect ratio with
    // a bounded denominator), and brute-forcing a small window around the request solves it exactly
    // rather than guessing at one nudge. A wider window would usually find an even smaller gap, but
    // at a Columns x Rows far enough from the request to defeat the point of asking for roughly that
    // many cells.
    private const int SearchWindow = 20;

    // Below this, the leftover is sub-pixel at any real print DPI - i.e. not actually visible - so
    // it is reported as no gap, and an improvement smaller than this is not worth suggesting.
    private const double NoGapTolerancePx = 0.5;

    private const double Tolerance = 1e-6;

    /// <summary>
    /// The fixed-size suggestion is displayed with this many decimals in Unit and typed back in by
    /// the user. Each candidate side is rounded DOWN to that precision before it is evaluated, so the
    /// displayed value is exactly the one that fits: rounding up could make the typed side just too
    /// big and silently lose a whole column or row.
    /// </summary>
    public const int SuggestedSideDecimals = 2;

    /// <summary>
    /// Searches a window of nearby whole (Columns, Rows) pairs - varying columns and matching the
    /// tightest rows, then vice versa - and returns the one whose solved grid leaves the least unused
    /// map area, measured against the area the grid was sized for so a flushed frame doesn't hide it.
    /// </summary>
    public static SquareFitSuggestion RecommendFit(GridSettings s, GridLayout layout)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(layout);

        bool fixedSize = s.SizingMode == GridSizingMode.FixedHexWidth;
        var search = new Search(s, layout.NominalClipBounds, new UnitScale(s.Unit, s.Dpi), fixedSize);

        // In fixed-size mode Columns/Rows are outputs of the solve, not inputs, so search around what
        // the solve produced rather than around stale settings values.
        (int centreCols, int centreRows) = fixedSize
            ? (layout.Columns, layout.Rows)
            : (Math.Max(1, s.Columns), Math.Max(1, s.Rows));

        // The current state is read off the layout itself, so it is exactly what is on screen.
        RectangleF clip = layout.NominalClipBounds;
        var currentLeftover = new SquareLeftover(
            Math.Max(0, clip.Width - layout.GridBounds.Width),
            Math.Max(0, clip.Height - layout.GridBounds.Height));
        var best = new Candidate(centreCols, centreRows, layout.CellWidthPx, currentLeftover);

        for (int cols = Math.Max(1, centreCols - SearchWindow); cols <= centreCols + SearchWindow; cols++)
        {
            double idealRows = clip.Height * cols / clip.Width;
            best = Tighter(best, search.Evaluate(cols, Math.Max(1, (int)Math.Floor(idealRows))));
            best = Tighter(best, search.Evaluate(cols, Math.Max(1, (int)Math.Ceiling(idealRows))));
        }

        for (int rows = Math.Max(1, centreRows - SearchWindow); rows <= centreRows + SearchWindow; rows++)
        {
            double idealCols = clip.Width * rows / clip.Height;
            best = Tighter(best, search.Evaluate(Math.Max(1, (int)Math.Floor(idealCols)), rows));
            best = Tighter(best, search.Evaluate(Math.Max(1, (int)Math.Ceiling(idealCols)), rows));
        }

        bool hasTighterFit = best.Leftover.TotalPx < currentLeftover.TotalPx - NoGapTolerancePx;
        SquareLeftover leftover = best.Leftover.TotalPx < NoGapTolerancePx ? default : best.Leftover;
        return new SquareFitSuggestion(hasTighterFit, best.Columns, best.Rows, best.SidePx, leftover, currentLeftover);
    }

    private static Candidate Tighter(Candidate a, Candidate? b) =>
        b is { } c && c.Leftover.TotalPx < a.Leftover.TotalPx ? c : a;

    // MA0008 wants an explicit StructLayoutAttribute; see CanvasSpec.cs for the rationale for Auto
    // over Sequential/Explicit - plain value types from a UI hint calculation, not an interop boundary.
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Candidate(int Columns, int Rows, double SidePx, SquareLeftover Leftover);

    /// <summary>Evaluates candidates by applying each to a copy of the settings and solving it for real.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Search(GridSettings Settings, RectangleF Clip, UnitScale Scale, bool FixedSize)
    {
        /// <returns>The candidate as the user would enter it, or null if those settings don't lay out at all.</returns>
        public Candidate? Evaluate(int columns, int rows)
        {
            GridSettings trial = Settings.Clone();
            if (FixedSize)
            {
                // The user acts on a fixed-size suggestion by typing the displayed side back in.
                double factor = Math.Pow(10, SuggestedSideDecimals);
                double side = Math.Min(Clip.Width / columns, Clip.Height / rows);
                double typed = Math.Floor((Scale.FromPx(side) * factor) + Tolerance) / factor;
                if (typed <= 0)
                {
                    return null;
                }

                trial.SquareSize = typed;
            }
            else
            {
                trial.Columns = columns;
                trial.Rows = rows;
            }

            try
            {
                (CellFit fit, RectangleF nominalClip) = GridLayoutEngine.SolveFit(trial);
                double sidePx = fit.CellWidthPx;
                var leftover = new SquareLeftover(
                    Math.Max(0, nominalClip.Width - BlockSpan(fit.Columns, sidePx, Scale.ToPx(trial.CellGapX))),
                    Math.Max(0, nominalClip.Height - BlockSpan(fit.Rows, sidePx, Scale.ToPx(trial.CellGapY))));

                // Counts mode reports the pair to type in; fixed-size mode the counts the side produces.
                return FixedSize
                    ? new Candidate(fit.Columns, fit.Rows, sidePx, leftover)
                    : new Candidate(columns, rows, sidePx, leftover);
            }
            catch (InvalidOperationException)
            {
                // E.g. a count whose wider labels leave no room for the grid. Not a valid suggestion.
                return null;
            }
        }

        private static double BlockSpan(int count, double side, double gap) =>
            (count * side) + ((count - 1) * Math.Max(0.0, gap));
    }
}
