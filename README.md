# HexGrid Generator

A Windows app that makes hex or square grid overlays for maps. Set up the grid, then export a
transparent PNG or an SVG and drop it on top of your artwork in Photoshop, Affinity, Krita, GIMP,
Inkscape or Illustrator.

It only makes grids. There is no map editor, title block, legend or scale bar.

## Download

Grab `HexGridGenerator.exe` from the latest GitHub release. It is a single file that runs on any
64-bit Windows machine with nothing else to install.

## Using it

The left panel holds every setting. Click a setting to see what it does in the help box below
the panel. The right side is a live preview of exactly what will be exported.

- **Zoom** with the mouse wheel. The point under the cursor stays put. 100% fits the whole
  canvas in the window. You can zoom out to 10%, or in until one canvas pixel is 32 screen
  pixels. Right-click the preview for fixed zoom levels.
- **Pan** by dragging with the middle mouse button, or with the scrollbars.
- **Smooth zoom** (top bar) keeps lines and text sharp when you zoom in past the canvas's real
  pixels. With it off, you see the PNG's actual pixels magnified. It only changes the preview,
  never the export.
- **Export PNG, Export SVG, Export both** save the current grid. Exports run in the background,
  so the window stays usable. The export and save buttons are disabled until the file is written,
  and the app won't close mid-export.
- **Save preset, Load preset** store all settings in a JSON file. **Reset** restores the defaults.

The status bar shows the grid you actually got (columns, rows, cell size) and tips for removing
leftover gaps.

## Grid types

**Grid type** switches between Hex and Square. Settings that apply to both shapes stay where they
are and just get renamed (Hex width becomes Square size, for example). Settings for only one shape
are hidden in the other mode. Switching never loses a setting, and presets save the same way in
both modes.

## Page layout

From the canvas edge inward:

```
canvas edge
  safe margin     keeps everything off the trim edge
  label band      column letters and row numbers, outside the frame
  frame           a single line
  map area        the grid, clipped at the frame
```

**Hex grids** always fill the map area edge to edge. The outer hexes are cut off at the frame.
Set **Grid inset** above 0 if you want a gap between the grid and the frame instead.

**Square grids** can do the same (**Auto-fit squares** off), or fit whole squares only and centre
them, leaving the spare room as a margin (**Auto-fit squares** on, the default).

## Sizing

**Sizing mode** has two options:

- **AutoFitRowsColumns.** Rows and columns decide the cell size. Use this for paper: "A3, 40 by
  26, fill it".
- **FixedHexWidth** (FixedSquareSize for squares). The cell size decides how many rows and columns
  fit. Use this for screens: "4K, 64 px hexes, as many as fit".

Rows and columns are a minimum. The grid usually adds cells on one axis to reach the frame. The
exception is a fitted square grid, which gives you exactly the count you asked for.

Hex width is measured across the hex horizontally: corner to corner for flat-top, flat side to
flat side for pointy-top.

### Gaps in a fitted square grid

Whole squares rarely fill a rectangle exactly, so a fitted square grid usually has spare room on
one axis. The margins are even left and right, and even top and bottom, but often not the same on
both axes.

Two tools help:

- **Status bar tips.** The app checks nearby row and column counts (or, in fixed-size mode,
  nearby square sizes) and suggests one that leaves no gap, or the smallest gap it can find.
  Entering a suggestion gives exactly the result it promised.
- **Flush axis.** Shrinks the frame to sit tight against the grid on the chosen axis, so the spare
  room ends up outside the frame instead of inside it. The frame stays put on the coordinate
  origin side and moves in on the opposite side. Only works with Auto-fit squares on.

## Cell gaps

**Gap X** and **Gap Y** add space between cells. Cells never stretch: squares stay square and hexes
stay regular.

- In a fitted square grid sized by rows and columns, you keep exactly the rows and columns you
  asked for and the squares shrink to make room, like a CSS grid gap.
- Everywhere else the cell size stays fixed, so a big gap means fewer cells fit. The status bar
  shows how many actually fit.

Hex grids have one **Gap** setting, because every hex is the same distance from all six neighbours.

## Units

All lengths use the **Unit** you pick: pixels, millimetres, centimetres or inches. Font sizes are
always in points. **DPI** converts between real-world units and pixels: 300 for print, 96 for
screen.

## Coordinates

Columns are lettered A to Z, then AA, AB and so on. **Skip letters I and O** is on by default,
because they look like 1 and 0.

**Coordinate origin** picks which corner is A1. Labels can go inside every cell, around the
outside of the frame on any of the four sides, or both. Top and left is the usual wargame
style. All four sides is the usual atlas style.

## Export

**SVG** is saved at the real physical size, so it prints at the right size. Each part of the
grid (lines, fill, labels, frame) is its own named layer in Illustrator, Affinity and Inkscape.

**PNG** is rendered at full resolution with the DPI saved in the file. The background can be
transparent, white, black or a custom colour. Turn off **Antialiasing** for crisp pixel-art lines.

**Export layers separately** also saves one transparent PNG per layer (grid, fill, dots, labels,
frame), ready to stack in Photoshop.

The app warns before exporting a PNG over 100 megapixels. A0 at 300 DPI is 139 megapixels and
needs about 0.6 GB of memory while rendering. SVG has no such limit.

### File names

File names come from the **Filename pattern** setting. Available tokens:

| Token | Becomes |
| --- | --- |
| `{grid}` | `Hex` or `Square` |
| `{preset}` | canvas preset name |
| `{w}` `{h}` | canvas width and height |
| `{cols}` `{rows}` | column and row count |
| `{cellw}` `{cellwu}` | cell width, without and with the unit |
| `{dpi}` | DPI |
| `{orient}` | hex orientation (empty for square grids) |

`{hexw}` and `{hexwu}` still work, so old presets keep their file names.

## How the grid stays accurate

- Every cell edge is drawn exactly once. Shared edges are never drawn twice, so semi-transparent
  lines have the same weight everywhere.
- Cells are never stretched to fill the page. Hex grids are cut off at the frame instead.
- The preview is drawn the same way as the PNG export, so what you see is what you get.
- Edge label space is estimated from the text length, not measured from the font. If a long label
  crowds the frame, raise **Padding from frame**.

## Building from source

You need the .NET 10 SDK. The app uses no NuGet packages.

Open `HexGridGenerator.sln` in Visual Studio and run `HexGrid.App`, or use one of the scripts:

| Script | Output | Needs |
| --- | --- | --- |
| `build.cmd` | `publish\HexGridGenerator.exe`, about 280 KB | .NET 10 Desktop Runtime |
| `build-standalone.cmd` | `publish-standalone\HexGridGenerator.exe`, about 47 MB | nothing |

The standalone build is the one attached to GitHub releases.

### Project layout

```
src/
  HexGrid.Core/        grid maths, labels and SVG. No Windows code.
    Settings/          all settings and how the panel shows them
    Units/             canvas presets, unit and DPI conversion
    Layout/            hex and square layout, fitting, clipping
    Labels/            coordinate letters and numbers
    Scene/             turns the layout into layers of shapes to draw
    Rendering/         SVG writer
    Presets/           JSON save and load
    Naming/            file name patterns
  HexGrid.App/         the Windows app
    Rendering/         preview and PNG drawing, background rendering, PNG writer, export
  HexGrid.Core.Tests/  tests for HexGrid.Core
  HexGrid.App.Tests/   tests for HexGrid.App
  HexGrid.Benchmarks/  performance benchmarks (not shipped)
```

Core works out the grid as plain numbers and shapes. The renderers (SVG, and GDI+ for the preview
and PNG) only draw them. A new export format means writing one more renderer.

### Tests

```
dotnet test
```

### Benchmarks

Benchmarks cover every preview stage, SVG export, PNG export and the square-fit tips. Run them in
Release from the repo root:

```
dotnet run -c Release --project src/HexGrid.Benchmarks -- --filter "*"
```

Use a narrower filter such as `*PipelineBenchmarks*` to run one group. Any performance change
should come with before and after results.
