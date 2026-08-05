# Handoff — HexGrid Generator

For Claude Code running inside Visual Studio, with the debugger and the real WinForms surface
available. Written by an agent that could not compile the Windows half.

## Current status — fixed, 2026-08-05

Root cause found and fixed. The app now launches, the PropertyGrid binds, and the preview
renders. See "Root cause" below before re-opening any of the speculative suspects further down —
they predate the actual throw site and most were never it.

## Root cause

`hexgrid-crash.log` (and the VS debugger, independently) gave:

```
System.InvalidOperationException: SplitterDistance must be between Panel1MinSize and Width - Panel2MinSize.
   at System.Windows.Forms.SplitContainer.set_SplitterDistance(Int32 value)
   at System.Windows.Forms.SplitContainer.ApplyPanel2MinSize(Int32 value)
   at HexGrid.App.MainForm.BuildUi() in MainForm.cs:line 60
   at HexGrid.App.MainForm..ctor() in MainForm.cs:line 32
```

Not the `Shown`-handler `SplitterDistance = 430` line — that one is guarded and is never reached.
The throw is inside the `SplitContainer` object initializer itself, at `Panel2MinSize = 200`.
Setting `Panel2MinSize` fires `SplitContainer`'s internal `ApplyPanel2MinSize`, which recomputes
`SplitterDistance` immediately — before the control is parented, so `Dock = DockStyle.Fill` has
not had a chance to size it yet. Its default un-parented `Width` is far smaller than
`Panel1MinSize (300) + Panel2MinSize (200) + SplitterWidth (6) = 506`, so the recompute is
mathematically impossible and throws on the spot.

Fix (`MainForm.cs`, in the `SplitContainer` initializer): set `Width = 900` before
`Panel1MinSize`/`Panel2MinSize` so the invariant holds at construction time. `Dock = Fill`
still takes over once the control is parented and the form lays out for real — the explicit
width only needs to survive the object initializer.

The original speculation (below) pointed at the `Shown`-handler line and at PropertyGrid
reflection as the top suspect. Verified after the fix: PropertyGrid renders the `[Flags]` enum
and all eight `Color` properties fine — suspect #1 was not it.

## What this is

A WinForms hex-grid overlay generator for map art. Two projects, **zero NuGet packages** —
deliberate, do not add packages to fix anything.

- `HexGrid.Core` (`net10.0`) — geometry, labelling, SVG. Uses only `System.Drawing.Primitives`
  (`Color`, `PointF`, `RectangleF`), `System.ComponentModel` and `System.Text.Json`.
- `HexGrid.App` (`net10.0-windows`, WinForms) — PropertyGrid + live preview + export. Uses
  `System.Drawing` (GDI+), which ships inside the Windows Desktop framework.

## Requirements

The current specification, as asked for by the user. Where a requirement fixes behaviour rather
than adding an option, it says so.

**Purpose.** Produce a transparent hex grid overlay to drop onto existing map artwork in
Photoshop, Affinity Photo, Krita or GIMP. It is a hex grid generator, not a map editor:
correctness of the grid comes before everything else.

**Delivery.** C# on .NET 10, Windows desktop, built from this solution to an `.exe`.

**Canvas.**

- Presets: 2A0 through A6, plus 8K, 4K, 2K and 1080p.
- Custom width × height for anything not on that list.
- Units: pixels, millimetres, centimetres, inches.
- DPI, so physical sizes resolve to pixels for print.

**Grid.**

- Flat-top and pointy-top.
- Two sizing modes: give rows × columns and the largest hex that fits is computed, or give the
  hex width and the counts that fit are computed.
- Grid offset X and Y for fine adjustment.
- **The grid must meet the frame with little or no gap on all four sides.** This is behaviour,
  not an option: the grid fills the map area and the outermost hexes are clipped at the frame.

**Appearance.**

- Transparent background.
- Line colour, line opacity, line thickness.
- Centre dot in every hex, toggleable, with its own size and colour.

**Coordinate labels.**

- **They sit outside the frame**, in the margin band, as on the user's reference map.
- Any combination of the four sides: none, top, bottom, left, right, all.
- Columns carry letters, rows carry numbers.
- Font family, size, colour, bold, and padding from the frame.
- Coordinate origin selects which corner is A1: top-left, bottom-left, top-right, bottom-right.
- Letters I and O can be skipped, since they read as 1 and 0.
- Labels can additionally be printed inside each hex, toggleable. **When positioned centrally
  they must rest just above the centre dot, not overlap it.**

**Frame and margins.**

- The frame is a **single plain rule**. Keep the margin design simple.
- A safe margin keeps the label band clear of the canvas edge.

**Export.**

- SVG and PNG.
- PNG background: transparent, white or black.
- Antialiasing on or off.
- Layer separation for stacking in Photoshop: one PNG per layer, and named layer groups in the
  SVG.
- Generated filenames from a token pattern.

**Live preview.** Worth more than extra features; it must update as settings change.

**Explicitly out of scope.** No title block, no legend, no scale bar, no decorative frame styles.
The user does these by hand in Photoshop. Do not add them, and do not add adjacent map furniture.

*Not required, but implemented:* preset save/load as JSON.

## Verification status — read this before you touch anything

`HexGrid.Core` was **fully verified on Linux with the .NET 10 SDK**: it compiles clean and a
**71-assertion harness passes**, covering canvas sizing, flat-top and pointy-top geometry, hex
tiling with no gaps, dot centring, edge-to-edge fill with clipping, edge deduplication, the label
band sitting outside the frame, centred labels clearing the centre dot, letter rollover, I/O
skipping, origin corners, preset round-tripping, filename tokens and the SVG clip-path rules.
Generated SVGs were rasterised and visually compared against the user's reference map.

`HexGrid.App` was **never semantically compiled** by that agent — the Windows Desktop targeting
pack was unavailable, so only a Roslyn syntax check ran (clean). Every WinForms and GDI+ call in
it is unverified against the real API.

**Therefore: the fault is in `HexGrid.App` with very high probability. Do not go looking in
`HexGrid.Core`, and do not "tidy" it.**

## Step 1 — get the actual exception

Everything below is speculation until you have the type, message and stack. Get it first:

- The app now installs its own handlers in `Program.Main`
  (`Application.ThreadException`, `AppDomain.CurrentDomain.UnhandledException`) and
  `Program.Report` writes the full text to **`hexgrid-crash.log` next to the exe** as well as
  showing it in a message box. Check that file.
- In VS, Debug → Windows → Exception Settings → tick **Common Language Runtime Exceptions** so
  you break at the throw site rather than at the top of the stack.

## Startup sequence, so you know where to put the first breakpoint

1. `Program.Main` — `SetHighDpiMode`, `EnableVisualStyles`,
   `SetCompatibleTextRenderingDefault(false)`, `SetUnhandledExceptionMode(CatchException)`,
   handler wiring, then `Application.Run(new MainForm())`.
2. `MainForm` field initialisers — includes `new SceneRasterizer()`, whose constructor creates a
   1×1 `Bitmap` and a `Graphics` from it.
3. `MainForm.BuildUi()` — `TableLayoutPanel`, `SplitContainer`, `PropertyGrid`, `PreviewPanel`,
   button bar.
4. `_properties.SelectedObject = _settings;` — **PropertyGrid reflects over ~45 properties on
   `GridSettings`**, including eight `System.Drawing.Color` properties and a `[Flags]` enum.
   This is by far the densest reflection surface in the app.
5. `Shown` handlers, in order: set `SplitterDistance` (already wrapped in a broad catch), then
   `Rebuild()`.
6. `_preview.Resize` → `ScheduleRebuild()` → 180 ms `Timer` → `Rebuild()`.

`Rebuild()` is now fully wrapped in try/catch and reports into the preview panel instead of
throwing, so if the crash still comes from the render path it is escaping from somewhere else.

## Ranked suspects

1. **PropertyGrid binding (step 4).** `LabelSides` is a `[Flags]` enum with composite members
   (`TopLeft = Top | Left`, `All = ...`); PropertyGrid's flags editor is known to be awkward with
   composites. Category strings also contain `·` (U+00B7) and `&`.
   *Quick bisect:* comment out `_properties.SelectedObject = _settings;`. If the window comes up,
   the fault is here, and the next bisect is to remove `[Flags]` composites, then the `Color`
   properties, then the odd characters in `[Category]`.
2. **`SceneRasterizer.DrawText`.** `FontFamily.GetCellAscent(style)` and `GetEmHeight(style)`
   throw `ArgumentException` when the family does not support the requested style.
   `new Font(string familyName, ...)` silently substitutes a family when the name is unknown, and
   the substitute may not carry the style. Guard with `family.IsStyleAvailable(style)` and fall
   back to `FontStyle.Regular` / a nominal 0.8 ascent ratio if not.
3. **`Bitmap.SetResolution((float)(scene.Dpi * scale), ...)`** in `SceneRasterizer.Render`.
   Throws on a non-positive argument. `scale` is clamped to `[0.001, 1.0]`, so at 96 dpi this can
   reach 0.096 — verify GDI+ accepts it, and floor the argument at 1 if not.
4. **`Graphics.SetClip(RectangleF)` / `ResetClip()` combined with `ScaleTransform`.** Confirm
   `SetClip` interprets the rectangle in world coordinates as assumed; if it does not, the clip
   will be wrong rather than throwing, but check it while you are here.
5. **`TableLayoutPanel` cell assignment** in `BuildUi` — three rows, one column, explicit
   `ColumnStyle` and `RowStyle` entries added.

## Fixes already applied blind, do not undo them

- `root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100))` — without it the single column
  defaults to AutoSize and collapses the window.
- `Rebuild()` wraps `UpdateStatus()` and `RenderPreview()` as well as the layout call, so a bad
  setting combination leaves the window usable.
- `SplitterDistance` assignment catches broadly; the exception type it throws when out of range
  has varied across versions.
- `SceneRasterizer` caches one `StringFormat` instead of reading `StringFormat.GenericTypographic`
  per text item — that property allocates a fresh disposable GDI+ object on every access and was
  leaking handles across a grid with a label in every hex.

## Rules for fixing

- Fix by correcting the call, never by adding a NuGet package.
- Do not change `HexGrid.Core` unless the debugger puts the throw site inside it. Its geometry is
  verified by the harness; an edit there is a regression, not a fix.
- Keep `SvgRenderer.BaselineY` as the single source of text baseline placement. The GDI+ path
  calls it deliberately so PNG and SVG position text identically. Do not reimplement it.
- Keep `LayerRules.IsClipped` as the single definition of which layers get trimmed at the frame.
  Both renderers consult it; they must not diverge.
- Keep the edge deduplication in `SceneBuilder.AddHexes`. Reverting to whole-polygon strokes
  double-strokes every internal edge, which at reduced line opacity makes internal edges visibly
  darker and thicker than the outer ones. The harness asserts this.
- Keep the renderer-agnostic split: `HexLayoutEngine` produces numbers, `SceneBuilder` produces
  layered draw items, renderers consume them. Nothing in `HexGrid.Core` may reference GDI+.
- Scope discipline: this is a hex grid generator, not a map tool. The user explicitly removed the
  title block, the legend, the scale bar and all decorative frame styles. Do not reintroduce map
  furniture, and do not add features while fixing the crash.

## Acceptance checks once it runs

1. Launches showing the A3 landscape default on a checkerboard, proving transparency, with
   coordinate labels on all four sides **outside** a single-line frame, and hexes clipped flush
   against that frame with no gap.
2. Toggling **Show centre dots** updates the preview within ~200 ms.
3. `Uhd4K` + `Pixels` + `FixedHexWidth` + hex width 64 + safe margin 0 + edge labels `None` +
   frame `None` reports **81 × 39 hexes** in the status bar, with the grid running off all four
   canvas edges. That number is verified against the harness.
4. **Line opacity** 40, preview zoomed: every hex edge is the same weight and darkness. Any edge
   that looks darker means the deduplication broke.
5. **Hex labels** on with position `Center`: the label rests just above the centre dot, not on it.
6. Export PNG with **Export layers separately** on: one file per layer plus the flattened image,
   all transparent, grid layers trimmed at the frame, label and frame layers not trimmed.
7. Export SVG, open in a browser: matches the preview, clipping included.
8. Save a preset, change settings, load it back: everything restores.

## Known soft spots, by design

- Row and column counts are minimums. The axis that constrains the hex size matches the request
  exactly; the other axis gains hexes until it reaches the frame. That is what edge-to-edge fill
  costs, and it is intentional.
- Edge-label gutters are reserved from an estimated glyph width (0.62 em), not a real
  measurement, so `HexGrid.Core` needs no font stack. A long label can crowd the band; the fix is
  user-side (**Padding from frame**), not code-side.
- The leftmost column and topmost row are usually half-hexes. That is the point of clipping.
