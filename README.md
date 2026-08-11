# HexGrid Generator

A Windows desktop utility that produces publication-quality hex or square grid overlays for map
art: transparent PNG for Photoshop, Affinity Photo, Krita and GIMP, or SVG for anything vector.

It is deliberately not a map editor and not a cartographic decoration tool. There is no title
block, no legend and no scale bar: every option exists to get a geometrically correct grid onto
someone else's artwork.

## Build

Requires the .NET 10 SDK. No NuGet packages, so restore cannot fail.

**Visual Studio.** Open `HexGridGenerator.sln`, set `HexGrid.App` as the startup project,
build. The exe lands in `src\HexGrid.App\bin\Release\net10.0-windows\HexGridGenerator.exe`.

**Command line.** Two scripts, each publishing a single-file exe:

| Command | Output | Needs |
| --- | --- | --- |
| `build.cmd` | `publish\HexGridGenerator.exe`, ~280 KB | .NET 10 Desktop Runtime installed |
| `build-standalone.cmd` | `publish-standalone\HexGridGenerator.exe`, ~47 MB | nothing |

`build.cmd` is for local iteration. `build-standalone.cmd` is the release build: self-contained,
runs on any Windows machine, and is what gets attached to a GitHub release.

## Layout

```
src/
  HexGrid.Core/        net10.0          geometry, labelling, SVG. No Windows dependency.
    Units/             canvas presets, unit and DPI conversion
    Layout/            hex and square maths, fit solver, clipping bounds
    Labels/            column letters, row numbers, origin corners
    Scene/             renderer-agnostic draw items grouped into layers
    Rendering/         SVG writer
    Presets/           JSON save and load
    Naming/            filename token expansion
  HexGrid.App/         net10.0-windows  WinForms shell
    Rendering/         GDI+ rasteriser (live preview and PNG export), export service
  HexGrid.Core.Tests/  net10.0          xUnit tests for HexGrid.Core
  HexGrid.App.Tests/   net10.0-windows  xUnit tests for HexGrid.App
```

The layout engine produces plain numbers and knows nothing about drawing. `SceneBuilder` turns
those numbers into layered draw items. Renderers consume the layers. Adding a PDF or EPS export
later means writing one more renderer and touching nothing else.

## Grid types

**Grid Type**, at the top of the options panel, switches the whole tool between **Hex** and
**Square**. Every other option that makes sense for both shapes carries over — fill colour,
line style, in-cell labels, centre dots, edge labels, frame, export — and simply relabels itself
for the shape in use (a hex-specific option like Hex Width becomes Square Size). Options that
only apply to one shape (hex orientation; the square-only AutoFitSquares fit behaviour) appear
only in that mode.

Saved presets and their JSON are unaffected by which mode the panel is currently showing:
switching Grid Type never renames or discards a setting, it only changes how that setting is
presented and which shape it drives.

## Page structure

Working inward from the canvas edge:

```
canvas edge
  safe margin          keeps everything off the trim edge
  label band           coordinate letters and numbers, OUTSIDE the frame
  frame rule           a single line, nothing fancier
  map area             the grid, clipped at the frame
```

**Hex grids** always fill the map area. Every hex centre lands inside the map area and the
outermost hexes overhang it and are clipped, so the grid meets the frame on all four sides with
no gap. Set **Grid inset** above 0 for a deliberate gap instead.

**Square grids** can do the same edge-to-edge, clip-the-partials behaviour (**AutoFitSquares**
off), or fit exactly whole squares only, centred in the map area with the leftover slack pushed
out into a margin (**AutoFitSquares** on, the default). Squares, unlike hexes, tile a rectangle
exactly, so nothing has to be clipped.

Row and column counts are a minimum, not an exact request, except for a fitted square grid
(AutoFitSquares on), where they are exact: the axis that constrains the cell size comes out
exactly as asked; the other axis gains however many cells it takes to reach the frame (or, for a
fitted square grid, is simply the count requested). The status bar reports the counts actually
produced, plus a hint for which axis is currently driving the size.

A fitted square grid's margin is only even on *both sides of the same axis* (left = right, top =
bottom). It is not generally even *between* the two axes: unless Columns:Rows happens to match
the map area's aspect ratio, one axis ends up flush against the frame while the other carries all
the leftover slack, which can look like a lopsided gap rather than a clean border. Exact zero gap
needs the counts and the canvas's aspect ratio to line up exactly, which for an arbitrary request
is a coincidence, not something to expect from the numbers you typed in.

When the gap is visible, the status bar searches nearby whole (Columns, Rows) pairs, a window on
either side of what's currently set, for the one that leaves the least leftover, and reports it:
a "no gap" pair when the search finds one, otherwise the tightest one it found and the residual
size. In Fixed square size mode, where Columns/Rows are computed from the square size rather than
set directly, it reports the nearby square size that produces a "no gap" or tightest grid instead.
The search only looks near the current request. A pair far away might coincidentally fit tighter
still, but recommending it would change the grid density far more than "close the gap" implies.

**Flush axis** closes the gap outright rather than just shrinking or relocating it. By default the
leftover on a non-binding axis is centred, split evenly between, say, the top and bottom margins,
as dead space *inside* the frame. Setting Flush axis to Vertical, Horizontal or Both instead shrinks
the frame itself on the side away from **Coordinate origin** until it touches the grid exactly
(plus Grid inset, if set). The border rule and that side's edge-label band move with it, since both
are drawn from the frame's bounds. The space that used to be a gap inside the frame becomes, instead,
extra room between the (now smaller) frame and the canvas edge: visible, but outside the map area
rather than an awkward pocket inside it. Which side shrinks follows Coordinate origin: the frame
stays put on the origin side (so the A1 corner's margin is unchanged) and pulls in on the far side.
This only applies when AutoFitSquares is on, and only reshapes the frame on the axis or axes
flushed. Combine it with the Columns/Rows or square-size recommendation above to close gaps on both
axes at once, or leave the other axis centred if a symmetric margin there is preferred.

## Units

Every **length** in the options (canvas size, margins, hex width, line thickness, dot radius,
label padding, offsets) is expressed in whatever `Unit` is set to. Every **font size** is in
points, because points are resolution-independent and convert cleanly through `DPI`.

`DPI` ties physical units to pixels. Use 300 for print, 96 for screen work. It has no effect
when `Unit` is Pixels and the canvas is a screen preset, except on font sizes.

## Sizing modes

**AutoFitRowsColumns.** Rows and columns set the cell size. Used for paper: "A3, 40 x 26,
fill it".

**FixedHexWidth** (Fixed square size in Square mode). The cell size sets how many rows and
columns there are. Used for screen overlays: "4K, 64 px hexes, as many as fit". At 4K with no
margins that yields 81 x 39 flat-top hexes.

For hexes, width means corner-to-corner for flat-top and flat-to-flat for pointy-top: the
horizontal extent either way. For squares it is simply the side length, and combines with
**AutoFitSquares** the same way AutoFitRowsColumns does: on gives whole squares only (floor
division, no clipping), off fills edge to edge and clips the outermost partial squares.

## Coordinates

Column letters roll over spreadsheet-style: A, B, ... Z, AA, AB. **Skip letters I and O** is on
by default, standard military-mapping practice because they read as 1 and 0.

**Coordinate origin** picks which physical corner is A1, so the same grid serves conventions
that count from the top-left or the bottom-left.

Labels appear in two independent places: inside every hex (**Hex Labels**) and in the band
outside the frame (**Edge Labels**, any combination of the four sides). Top plus Left is the
wargame convention; all four is the atlas convention.

## Export

**SVG is the source of truth.** It carries the real physical size in millimetres with a pixel
`viewBox`, so it prints at exactly the right size and rasterises to exactly the target pixel
dimensions. Each layer becomes a named `<g>` group tagged as an Inkscape layer, which
Illustrator, Affinity and Inkscape read as real layers. The grid layers carry a `clip-path` so
the hexes are trimmed at the frame in vector form too.

**PNG** is rasterised through GDI+ at full resolution with the DPI written into the file.
Background can be transparent, white, black or custom. Antialiasing can be switched off for
pixel-art workflows.

**Export layers separately** writes one transparent PNG per layer alongside the flattened
image: `..._HexGrid.png`, `..._CenterDots.png`, `..._EdgeLabels.png`, `..._Border.png` for a hex
grid, or `..._SquareGrid.png`, `..._SquareFill.png`, `..._SquareLabels.png` and so on for a
square grid, ready to stack as Photoshop layers.

Filenames are generated from a token pattern: `{grid} {preset} {w} {h} {cols} {rows} {cellw}
{cellwu} {dpi} {orient}`. `{grid}` expands to `Hex` or `Square`; `{orient}` is empty in Square
mode. `{hexw}`/`{hexwu}` still work as aliases for `{cellw}`/`{cellwu}`, so presets saved before
Square support keep producing the same filenames.

## Presets

**Save preset** and **Load preset** write the whole settings object as readable JSON, colours
as hex strings. Keep one per campaign map.

## Testing

`HexGrid.Core.Tests` and `HexGrid.App.Tests` are xUnit test projects covering hex and square
tiling, clipping, label placement, SVG output, preset round-tripping and the WinForms shell. Run
them with `dotnet test`.

## Correctness notes

- **Every cell edge is stroked exactly once**, hex or square. Adjacent cells share an edge;
  stroking whole polygons would draw internal edges twice, which at reduced line opacity makes
  them visibly darker than the outer edges and thickens them under antialiasing. The scene
  builder emits a deduplicated edge set instead, so line weight and opacity are uniform across
  the whole grid.
- Hexes and squares are always regular. A hex grid fills the page by clipping, never by
  stretching. A square grid does the same unless AutoFitSquares is on, in which case it fits
  exactly and centres with a margin instead of clipping.
- Edge-label gutters are reserved from an estimate of text width rather than a real
  measurement, so the geometry layer stays free of font dependencies. Increase **Padding from
  frame** if a long label ever crowds the band.
- Very large canvases are memory-hungry to rasterise. A0 at 300 dpi is 139 megapixels, roughly
  0.6 GB while rendering; the app warns above 100 megapixels. SVG has no such limit.
