# HexGrid Generator

A Windows desktop utility (WinForms, .NET 10) that produces publication-quality hex grid
overlays for map art — transparent PNG or SVG, dropped onto artwork made elsewhere. It is a
grid generator, not a map editor: geometric correctness of the grid comes before every other
concern. Full behavioural spec is in `README.md`; do not restate it here — read it before
touching grid math, labelling, or export.

## Solution shape

```
src/
  HexGrid.Core/   net10.0            geometry, labelling, SVG. No Windows dependency.
  HexGrid.App/    net10.0-windows    WinForms shell: PropertyGrid + live preview + export.
```

`HexGrid.Core` uses only the BCL (`System.Drawing.Primitives`, `System.ComponentModel`,
`System.Text.Json`) and knows nothing about drawing — it produces plain numbers and layered
draw items. `HexGrid.App` is the only project that touches `System.Drawing` (GDI+) or WinForms.
Keep that boundary: rendering-agnostic logic stays in Core, GDI+/WinForms calls stay in App.

## Zero NuGet packages — deliberate

The solution carries no runtime NuGet dependencies at all. The only `PackageReference`s are
dev-time Roslyn analyzers (`Directory.Packages.props`), which don't ship in the built exe.
**Do not add a package to solve a problem** — this is a stated constraint, not an oversight.
If a problem genuinely seems to need one, say so and ask first.

## No web/DB/cloud surface

This is a single-process local desktop tool. It has no HTTP endpoints, no database, no auth,
no network calls, and no CI/deploy pipeline. Most generic ASP.NET Core/EF Core advice
(minimal APIs, `ProblemDetails`, `HybridCache`, CORS, `WebApplicationFactory`) does not apply
here and has been removed from `.claude/`.

## `.claude/` setup

This project's `.claude/` started as a copy of `dotnet-claude-kit`, an ASP.NET Core/EF
Core-oriented rule/skill set (see `.claude/dotnet-claude-kit-SOURCE.md` for provenance and
license). It was stripped down to the stack-agnostic subset on 2026-08-06: rules, agents, and
skills assuming a web API, a database, or cloud deployment were removed rather than left as
dead weight, since `.claude/rules/*.md` load into every session automatically. What remains:

- `rules/` — `coding-style.md`, `git-workflow.md`, `hooks.md`, `priorities.md`,
  `packages.md`, `agents.md`, `testing.md` (testing.md rewritten to assume a dependency-free
  Core library, not `WebApplicationFactory`/Testcontainers)
- `agents/` — `build-error-resolver`, `code-reviewer`, `refactor-cleaner` (stack-agnostic)
- `skills/` — `modern-csharp`, `project-structure`, `de-sloppify`, `convention-learner`,
  `health-check`, `outdated`, `instinct-system`, `checkpoint`, `wrap-up`, `build-fix`,
  `code-review`
- `knowledge/` — `common-antipatterns.md`, `dotnet-whats-new.md`, `decisions/template.md`

Don't run `/dotnet-init` on this project — its project-type detection has no branch for a
WinForms/desktop app, and its questionnaire (architecture style, database, auth, caching,
messaging) doesn't map onto a two-project GDI+ tool.

## Testing

`HexGrid.Core.Tests` and `HexGrid.App.Tests` are xUnit test projects, run with `dotnet test`.
`HexGrid.Core.Tests` covers the highest-value surface: hex tiling, clipping, label placement,
SVG output, preset round-tripping. `HexGrid.App.Tests` covers the WinForms shell. See
`rules/testing.md` for naming and structure conventions.

## WinForms gotchas already hit in this codebase

See `HANDOFF.md` for a documented root-cause writeup of a `SplitContainer` construction-order
crash (`SplitterDistance` computed before the control is parented/sized). Read it before
touching `MainForm.BuildUi()` so history doesn't repeat.
