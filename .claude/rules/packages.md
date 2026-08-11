---
alwaysApply: true
description: >
  Enforces latest stable NuGet package versions and proper dependency management.
  Prevents outdated package references from training data.
---

# Package Management Rules

## This Project Carries Zero Runtime Packages By Design

HexGrid.Core and HexGrid.App reference nothing beyond the BCL and the Windows Desktop
framework (`System.Drawing`, `System.ComponentModel`, `System.Text.Json`). See project root
`CLAUDE.md`. The only `PackageReference`s in the solution are dev-time Roslyn analyzers,
applied via `Directory.Build.props`. **Do not add a runtime NuGet package to solve a problem
without confirming with the user first**. That decision has already been made deliberately.

## Always Use Latest Stable Versions

Applies when the user does ask for a package (analyzer updates, or a deliberate exception to
the rule above).

- **Never hardcode package versions from memory.** Your training data contains outdated
  versions. Always verify the latest stable version before adding a package.
- **Run `dotnet add package <name>` without a `--version` flag** to automatically pull the
  latest stable release from NuGet.org. This is the safest default.
- **When writing `<PackageReference>` in .csproj files**, use `dotnet add package` first to
  resolve the correct version, then copy it into the project file.

## Central Package Management

This solution already uses `Directory.Packages.props` with
`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`. Individual
`.csproj` files must NOT specify `Version=` on `<PackageReference>`.

## Version Verification

- If unsure about the latest version, suggest the user verify on NuGet.org or run
  `dotnet package search <name>`.
- **Never downgrade a package** that is already in the project unless explicitly asked or
  there is a known compatibility issue.
- Prefer release versions over preview/RC unless the project explicitly targets preview
  features.
