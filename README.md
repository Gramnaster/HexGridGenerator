# HexGrid Generator

A Windows desktop utility that produces publication-quality hex grid overlays for map art:
transparent PNG for Photoshop, Affinity Photo, Krita and GIMP, or SVG for anything vector.

It is deliberately not a map editor and not a cartographic decoration tool. There is no title
block, no legend and no scale bar: every option exists to get a geometrically correct hex grid
onto someone else's artwork.

## Build

Requires the .NET 10 SDK. No NuGet packages, so restore cannot fail.

**Visual Studio.** Open `HexGridGenerator.sln`, set `HexGrid.App` as the startup project,
build. The exe lands in `src\HexGrid.App\bin\Release\net10.0-windows\HexGridGenerator.exe`.

**Command line.** Run `build.cmd`, which publishes a single-file exe to `publish\`:

| Command | Output | Needs |
| --- | --- | --- |
| `build.cmd` | ~280 KB exe | .NET 10 Desktop Runtime installed |
| `build.cmd standalone` | ~47 MB exe | nothing |

## Layout

```
src/
  HexGrid.Core/        net10.0          geometry, labelling, SVG. No Windows dependency.
    Units/             canvas presets, unit and DPI conversion
    Layout/            hex maths, fit solver, clipping bounds
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

## Page structure

Working inward from the canvas edge:

```
canvas edge
  safe margin          keeps everything off the trim edge
  label band           coordinate letters and numbers, OUTSIDE the frame
  frame rule           a single line, nothing fancier
  map area             the grid, clipped at the frame
```

The grid always fills the map area. Every hex centre lands inside the map area and the
outermost hexes overhang it and are clipped, so the grid meets the frame on all four sides with
no gap. Set **Grid inset** above 0 for a deliberate gap instead.

Row and column counts are a minimum, not an exact request. The axis that constrains the hex
size comes out exactly as asked; the other axis gains however many hexes it takes to reach the
frame. The status bar reports the counts actually produced.

## Units

Every **length** in the options (canvas size, margins, hex width, line thickness, dot radius,
label padding, offsets) is expressed in whatever `Unit` is set to. Every **font size** is in
points, because points are resolution-independent and convert cleanly through `DPI`.

`DPI` ties physical units to pixels. Use 300 for print, 96 for screen work. It has no effect
when `Unit` is Pixels and the canvas is a screen preset, except on font sizes.

## Sizing modes

**AutoFitRowsColumns.** Rows and columns set the hex size. Used for paper: "A3, 40 x 26,
fill it".

**FixedHexWidth.** The hex width sets how many rows and columns there are. Used for screen
overlays: "4K, 64 px hexes, as many as fit". At 4K with no margins that yields 81 x 39 flat-top
hexes.

Hex width means corner-to-corner for flat-top and flat-to-flat for pointy-top: the horizontal
extent either way.

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
image: `..._HexGrid.png`, `..._CenterDots.png`, `..._EdgeLabels.png`, `..._Border.png`, ready
to stack as Photoshop layers.

Filenames are generated from a token pattern: `{preset} {w} {h} {cols} {rows} {hexw} {hexwu}
{dpi} {orient}`.

## Presets

**Save preset** and **Load preset** write the whole settings object as readable JSON, colours
as hex strings. Keep one per campaign map.

## Testing

`HexGrid.Core.Tests` and `HexGrid.App.Tests` are xUnit test projects covering hex tiling,
clipping, label placement, SVG output, preset round-tripping and the WinForms shell. Run them
with `dotnet test`.

## Correctness notes

- **Every hex edge is stroked exactly once.** Adjacent hexes share an edge; stroking whole
  polygons would draw internal edges twice, which at reduced line opacity makes them visibly
  darker than the outer edges and thickens them under antialiasing. The scene builder emits a
  deduplicated edge set instead, so line weight and opacity are uniform across the whole grid.
- Hexes are always regular. The grid fills the page by clipping, never by stretching.
- Edge-label gutters are reserved from an estimate of text width rather than a real
  measurement, so the geometry layer stays free of font dependencies. Increase **Padding from
  frame** if a long label ever crowds the band.
- Very large canvases are memory-hungry to rasterise. A0 at 300 dpi is 139 megapixels, roughly
  0.6 GB while rendering; the app warns above 100 megapixels. SVG has no such limit.
