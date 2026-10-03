# Session Handoff

> Generated: 2026-08-16 | Branch: master

## Completed
- No code changes this session — scoping/estimation discussion for a WinForms → Avalonia
  conversion of `HexGrid.App`. Working tree is clean, nothing to commit.
- Resolved the one real blocking decision: whether to hand-roll a settings panel or use
  a third-party Avalonia PropertyGrid package (Avalonia has no built-in `PropertyGrid`
  equivalent to WinForms'). User approved a **scoped** exception to the zero-runtime-package
  policy for this migration only. Full rationale: `MEMORY.md` →
  `project_avalonia_migration_package_exception.md`.

## Pending
- [ ] Scaffold a new Avalonia project (`Avalonia`, `Avalonia.Desktop`,
  `Avalonia.Themes.Fluent`) and reference `HexGrid.Core` — Core has zero Windows
  dependency and needs no changes.
- [ ] Research and pick a maintained Avalonia PropertyGrid-equivalent NuGet package
  (check GitHub activity, license, current Avalonia-version compat — don't reuse a
  version from memory, verify latest stable per `packages.md`). This replaces
  `System.Windows.Forms.PropertyGrid` + `RelabelledPropertyDescriptor` +
  `PropertyGridBoolOverlay.cs` (283 lines, a WinForms-specific checkbox-rendering hack
  that should just disappear, not be ported).
- [ ] Rewrite `src/HexGrid.App/Rendering/SceneRasterizer.cs` (261 lines) against
  `Avalonia.Media`/`DrawingContext` instead of `System.Drawing.Graphics` — geometry
  building changes shape (`StreamGeometry` instead of direct `DrawPolygon` calls).
- [ ] Rewrite `src/HexGrid.App/PreviewPanel.cs` (221 lines, currently a WinForms
  `OnPaint` override) as a custom Avalonia `Control` overriding `Render(DrawingContext)`.
- [ ] Rewrite `src/HexGrid.App/Rendering/ExportService.cs` (78 lines) — PNG export moves
  from `Bitmap.Save` to Avalonia's `RenderTargetBitmap`.
- [ ] Rewrite `src/HexGrid.App/MainForm.cs` (636 lines) as Avalonia XAML + code-behind.
  `SplitContainer` → `Grid` + `GridSplitter`. See the `TrySetPreferredSplit` comments in
  the current `MainForm.cs` for the construction-order bug this already hit once in
  WinForms — worth deliberately avoiding the equivalent trap in the Avalonia layout.
- [ ] Rewrite `src/HexGrid.App/Program.cs` (85 lines, entry point) for Avalonia's
  `AppBuilder` startup.
- [ ] Rewrite `HexGrid.App.Tests` (714 lines) against `Avalonia.Headless.XUnit` —
  current tests instantiate WinForms controls directly and won't survive as-is.
- [ ] Set up headless visual verification early (not as an afterthought): a small test
  harness using `AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }` +
  `window.CaptureRenderedFrame()` to save PNGs I can actually look at via the Read tool
  during the rewrite, rather than only finding out layout is broken when the user runs
  it. Details in the memory file below.

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Zero-runtime-package policy | Scoped exception for Avalonia core + PropertyGrid package only | No Avalonia equivalent to WinForms `PropertyGrid`; hand-rolling was judged not worth the extra code just to preserve a policy that predates this UI framework decision |
| "Performance" packages | Not approved | No measured bottleneck in current app; Avalonia's default Skia renderer is already the fast path for this workload; violates `priorities.md` performance-last discipline without a driver |
| Policy document rewrite | Declined | Exception is migration-scoped, not a rewrite of `CLAUDE.md`/`packages.md`; `HexGrid.Core` and the rest of the repo stay zero-package |

## Learned
- Avalonia has no built-in `PropertyGrid` analog — this is the single biggest cost/risk
  driver in the conversion, not the GDI+→`DrawingContext` rendering rewrite.
- `Avalonia.Headless` can do real pixel rendering (`UseHeadlessDrawing = false` +
  `CaptureRenderedFrame()` → `WriteableBitmap` → PNG), which means visual layout/render
  correctness is something I can verify myself mid-rewrite via the Read tool, not
  something that only gets checked when the user runs the app. Correct this into any
  future estimate of "can I verify this myself."

## Context
- Branch: master | Last commit: 7b42941 "Normalise line endings to LF"
- Uncommitted changes: no
- Solution: `HexGridGenerator.sln` (repo root)
- `HexGrid.Core` (~untouched by this migration) is the geometry/labelling/SVG-export
  engine; scope of the actual rewrite is `HexGrid.App` (1,564 lines) + `HexGrid.App.Tests`
  (714 lines).
