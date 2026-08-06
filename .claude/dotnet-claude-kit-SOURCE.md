# Source: dotnet-claude-kit

- Upstream: https://github.com/codewithmukesh/dotnet-claude-kit
- Commit: `cd83d315986c27621da178dad73bd95d503c1540` (main, 2026-07-25)
- License: MIT — see `dotnet-claude-kit-LICENSE`

## What was imported
- `skills/` — all 47 `SKILL.md` teaching modules (as-is, reference material)
- `rules/` — the 10 always-active coding convention docs (originally `.claude/rules/` upstream)
- `knowledge/` — antipatterns, breaking changes, package recommendations, architecture decision records
- `agents/` — the 10 specialist agent definitions
- `hooks/` — the auto-executing Claude Code hooks (`pre-bash-guard.sh`, `post-edit-format.sh`,
  `post-scaffold-restore.sh`), the manual git pre-commit scripts (`pre-commit-format.sh`,
  `pre-commit-antipattern.sh`), and the utility scripts (`pre-build-validate.sh`,
  `post-test-analyze.sh`)
- `AGENTS.md` — the agent routing table, at project root (`rules/agents.md` points here)

## How hooks are wired here (not the plugin route)
This is a content-only copy, not a `/plugin install`, so `hooks/hooks.json` is not
auto-registered. Its `PreToolUse`/`PostToolUse` config was hand-copied into
`.claude/settings.json` with `${CLAUDE_PLUGIN_ROOT}` swapped for `$CLAUDE_PROJECT_DIR`
(the portable env var Claude Code sets for hook commands outside a plugin).
`.git/hooks/pre-commit` was installed locally (not tracked by git — per-clone, matches
upstream's "install manually" instructions in `hooks/README.md`) to run
`pre-commit-format.sh` && `pre-commit-antipattern.sh`.

## What was deliberately left out
The `CWM.RoslynNavigator` MCP server and `.claude-plugin/` marketplace manifest — the MCP
server is a global `dotnet tool install`, configured outside this repo (already active in
this project's Claude Code session as `mcp__cwm-roslyn-navigator__*`, though possibly behind
upstream's current tool count — check `dotnet tool list -g` against upstream's CHANGELOG
if MCP-powered skills behave unexpectedly). The marketplace manifest only matters for the
`/plugin install` distribution path, which this project isn't using.

To reuse in another project: copy this whole `.claude/` folder, `AGENTS.md`, and
`.git/hooks/pre-commit` (the last one manually, since git doesn't track hook files) over.

## Project CLAUDE.md

The note that used to be here (`/dotnet-init` run 2026-07-29, `templates/web-api/CLAUDE.md`,
EF Core/Identity in Domain, MVC Controllers) described a *different* project's setup — this
`.claude/` folder was copied from an ASP.NET Core web app (HotelListing.App/HotelListing.Vsa)
without adaptation, including `settings.local.json` permission grants scoped to that project's
file paths.

## Stripped down for this project — 2026-08-06

HexGridGenerator is a WinForms desktop tool with zero runtime NuGet packages and no web/DB/
cloud surface (see root `CLAUDE.md`). Because `.claude/rules/*.md` load into every session
automatically, the ASP.NET Core/EF Core-specific content wasn't inert reference material — it
was actively steering guidance toward patterns that don't apply here. Removed rather than kept
as dead weight:

- `rules/`: `architecture.md`, `error-handling.md`, `performance.md`, `security.md` (deleted —
  no generic residue once the HTTP/EF Core specifics are stripped). `testing.md` rewritten
  in place rather than deleted — its AAA/naming/behavior-testing conventions are stack-agnostic
  even though its DB/`WebApplicationFactory` material wasn't. `packages.md` trimmed to drop
  ASP.NET Core-specific version-alignment bullets, keeping the generic version-verification and
  Central Package Management guidance (this solution does use CPM for its analyzer packages).
- `agents/`: `api-designer`, `devops-engineer`, `dotnet-architect`, `ef-core-specialist`,
  `performance-analyst`, `security-auditor`, `test-engineer` deleted. Kept `build-error-resolver`,
  `code-reviewer`, `refactor-cleaner` — stack-agnostic.
- `skills/`: everything web/EF Core/cloud/architecture-specific deleted (36 of 47). Kept
  `modern-csharp`, `project-structure`, `de-sloppify`, `convention-learner`, `health-check`,
  `outdated`, `instinct-system`, `checkpoint`, `wrap-up`, `build-fix`, `code-review`.
- `knowledge/`: kept `common-antipatterns.md`, `dotnet-whats-new.md`, `decisions/template.md`.
  Deleted the kit's own ADRs about its defaults (VSA, EF Core, HybridCache, Result pattern,
  multi-architecture — decisions about the *kit*, not this project), the .NET 9→10 migration
  guide (already on net10.0, guide is now stale), the MediatR migration guide, and the NuGet
  package recommendations list (moot given the zero-package constraint).

To reuse what's left in another project: copy `.claude/` (minus the deleted paths above) —
same caveat as before applies, adapt before trusting.
