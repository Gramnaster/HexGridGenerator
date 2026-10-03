using System.Drawing;
using HexGrid.Core.Scene;

namespace HexGrid.App.Rendering;

/// <summary>One preview frame to render: a region of the scene at one scale.</summary>
/// <param name="Region">The pixels to draw, in the coordinates of the whole canvas rendered at <paramref name="Scale"/>.</param>
internal sealed record PreviewRequest(DrawScene Scene, Color Background, bool Antialias, double Scale, Rectangle Region)
{
    /// <summary>
    /// Floor for stroke widths in device pixels. Below the canvas's own resolution hairlines are kept
    /// visible, as the preview always has. At scale 1 nothing is floored, so the frame holds exactly
    /// the pixels the PNG export writes.
    /// </summary>
    public double MinStrokePx => Scale < 1.0 ? 1.0 : 0;

    /// <summary>True when both frames show the same scene at the same scale, differing at most in region.</summary>
    public bool SameSceneAndScale(PreviewRequest other) =>
        ReferenceEquals(Scene, other.Scene) && Math.Abs(Scale - other.Scale) < 1e-9;
}
