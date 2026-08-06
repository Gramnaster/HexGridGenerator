using System.Runtime.InteropServices;

namespace HexGrid.Core.Units;

/// <summary>A canvas size resolved to a concrete physical or pixel size.</summary>
/// <param name="WidthMm">Width in millimetres, or null for pixel-defined presets.</param>
/// <param name="HeightMm">Height in millimetres, or null for pixel-defined presets.</param>
/// <param name="WidthPx">Width in pixels, or null for paper presets.</param>
/// <param name="HeightPx">Height in pixels, or null for paper presets.</param>
// MA0008 wants an explicit StructLayoutAttribute. Auto (the runtime's own default) is the deliberate
// choice, not Sequential/Explicit: this is a plain in-memory value type resolved a handful of times
// per grid rebuild, not a hot path or an interop boundary, so there is nothing to earn by pinning
// field order. See ../../../CLAUDE.md and the parent CSharp/CLAUDE.md performance doctrine: struct
// layout is a measured hot-path optimization, not a default to reach for.
[StructLayout(LayoutKind.Auto)]
public readonly record struct CanvasSpec(double? WidthMm, double? HeightMm, int? WidthPx, int? HeightPx)
{
    public bool IsPaper => WidthMm.HasValue;

    public static CanvasSpec Paper(double wMm, double hMm) => new(wMm, hMm, WidthPx: null, HeightPx: null);

    public static CanvasSpec Screen(int wPx, int hPx) => new(WidthMm: null, HeightMm: null, wPx, hPx);
}
