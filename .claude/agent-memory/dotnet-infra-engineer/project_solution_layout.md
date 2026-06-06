---
name: project-solution-layout
description: The established .NET solution skeleton for ficsit-mcp — projects, root config files, TFM, and where versions/settings live
metadata:
  type: project
---

Solution skeleton established by PR #28 (closes issue #1), branch `feat/1-scaffold-solution`.

Layout (authoritative; read the files for current detail — this is a snapshot):
- `FicsitMcp.sln` — classic .sln format at repo root (NOT .slnx; the .NET 10 SDK defaults to .slnx, override with `dotnet new sln --format sln`).
- `src/FicsitMcp` — console host (`OutputType=Exe`). `Program.cs` is ONLY host/transport wiring. Tools live in `src/FicsitMcp/Tools/`.
- `src/FicsitMcp.Domain` — domain + surface clients. Has NO reference to ModelContextProtocol; keep it that way so domain/service logic stays testable in isolation.
- `tests/FicsitMcp.Tests` — xUnit, references both projects.

Root config:
- `global.json` — pins SDK `10.0.108`, `rollForward: latestMinor`. CI (#2) must consume the same pin.
- `Directory.Build.props` — shared settings for ALL projects: `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `ManagePackageVersionsCentrally=true`. Put shared build settings here, not in csproj.
- `Directory.Packages.props` — central package management. ALL versions here as `<PackageVersion>`; per-project `<PackageReference>` entries are version-less.

**Why:** Centralizing rigor at the root (the .NET analogue of a Cargo workspace) makes version skew and setting drift between projects structurally impossible.
**How to apply:** When adding a project, it inherits net10.0 + all settings automatically. When adding a package, add the version to Directory.Packages.props and a version-less PackageReference to the csproj. Never put a Version attribute on a PackageReference.

Key versions at scaffold time: ModelContextProtocol 1.4.0 (stable, not prerelease), Microsoft.Extensions.Hosting 10.0.7 (pinned to match the Hosting.Abstractions MCP pulls transitively — avoids a downgrade warning that TreatWarningsAsErrors turns into a build failure). SDK .NET 10 installed; TFM is net10.0 (retargeted from net9.0 during the up-to-date verification pass: net9 is STS ending Nov 2026, net10 is LTS to Nov 2028).

See [[convention-stdout-jsonrpc]], [[convention-editorconfig-rationale]], [[convention-mcp-tool-shape]].
