# Repository Guidelines

## Project Structure & Module Organization

Crystalfly is a .NET 10 Windows desktop application. Production code lives under `src/`:

- `Crystalfly.App`: Avalonia/Semi/Ursa UI, view models, localization, and application services.
- `Crystalfly.Core`: instance, catalog, package, transaction, Loader, Mod, and LocalLow domain logic.
- `Crystalfly.Steam`: SteamKit2 authentication and depot downloading.

Tests mirror these modules under `tests/*Tests`. Catalog JSON and schemas belong in `catalog/`; architecture notes and UI screenshots belong in `docs/`. Release automation is in `scripts/`, while `installer/Crystalfly.iss` defines the Windows installer. Generated `artifacts/`, `bin/`, and `obj/` content is ignored and must not be committed.

## Build, Test, and Development Commands

Run commands from the repository root:

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet run --project '.\src\Crystalfly.App\Crystalfly.App.csproj'
```

Use `scripts/build-release.ps1` to produce the self-contained ZIP, installer, and checksums. After closing Crystalfly, `scripts/build-and-install.ps1` builds and updates `D:\Program Files\Crystalfly`. Both scripts skip tests by default; `-RunTests` explicitly runs the standard regression suite, excluding optional visual scans.

After completing changes and the relevant focused checks, always run `pwsh -NoProfile -File .\scripts\build-and-install.ps1 -UnsignedLocal` to build and overwrite the local installation before the final response. Verify the installed files match the build output. This is the user's standing local-delivery preference.

## Coding Style & Naming Conventions

Use four-space indentation, file-scoped namespaces, nullable reference types, and implicit usings. Warnings are errors. Name types and public members with `PascalCase`, locals and parameters with `camelCase`, private fields with descriptive camelCase names, and asynchronous methods with an `Async` suffix. Keep cancellation tokens flowing through I/O operations. Prefer existing transaction, JSON, and path-validation helpers over new parallel abstractions.

## Testing Guidelines

Tests use xUnit; UI tests use `Avalonia.Headless.XUnit`. Name tests by behavior, for example `ApplyDirectory_rejects_reparse_point_staging_root`. Add regression coverage for changed behavior, including failure, cancellation, rollback, and path-safety cases where relevant. There is no fixed coverage percentage. CI uses `scripts/select-test-projects.ps1` to select affected projects from changes since the last successful CI run, including changes from failed or superseded runs; Core, Steam, and Updater production changes also select App tests. Shared configuration and unknown paths select all projects. Documentation and screenshots do not trigger CI; embedded release notes and catalog documents still do. Manual CI with `full_tests` enabled runs all projects including visual scans.

For routine development, run the smallest relevant check once. Prefer a focused `--filter` over an entire test project. Reuse passing results while the relevant code and inputs remain unchanged. Documentation and script-only changes need only their relevant lightweight checks. Do not automatically run full regression, perform desktop acceptance, or start another audit round after a focused check passes. The local build and overwrite installation required above remains part of delivery.

Do not add tests for README wording, fixed release version strings, trivial property forwarding, or every theme/language/size combination. Use representative cases that cover distinct layout branches. Cosmetic changes need a build and, where useful, one affected screenshot; add a behavioral regression only for a real interaction or accessibility defect. Existing file safety, rollback, cancellation, credential, and save protection tests remain required.

App tests exclude `Category=VisualScan` by default through the project's `VSTestTestCaseFilter`. Run optional scans explicitly with `dotnet test tests/Crystalfly.App.Tests -c Release --filter 'Category=VisualScan'`; an explicit `--filter` replaces the default filter. Full local regression is an explicit request: `dotnet test Crystalfly.slnx -c Release --filter 'FullyQualifiedName~Crystalfly'`.

### Change-to-Test Routing

Choose tests within the affected project; build changed test code before using `--no-build`:

| Changed paths | Test command |
| --- | --- |
| `src/Crystalfly.App/**` | `dotnet test tests/Crystalfly.App.Tests -c Release --filter 'FullyQualifiedName~<affected test>'` |
| `src/Crystalfly.Core/**` | `dotnet test tests/Crystalfly.Core.Tests -c Release --filter 'FullyQualifiedName~<affected test>'` |
| `src/Crystalfly.Steam/**` | `dotnet test tests/Crystalfly.Steam.Tests -c Release --filter 'FullyQualifiedName~<affected test>'` |
| `scripts/build*.ps1` | The corresponding `scripts/test-build*.ps1` check |
| `.github/workflows/ci.yml` or test selection changes | `pwsh -NoProfile -File scripts/test-select-test-projects.ps1`; no product tests unless production behavior changes |
| Cross-module or `Directory.*.props` changes | One build and relevant cross-module tests; full suite only for affected shared configuration in CI or an explicit local request |

## Commit & Pull Request Guidelines

Use concise Conventional Commit subjects seen in history: `feat:`, `fix:`, or `build:`. Keep commits scoped and avoid mixing generated output or unrelated cleanup. Pull requests should explain the background, changes, observable result, and verification commands. Link relevant issues, call out compatibility or data-migration risks, and include before/after screenshots for Avalonia UI changes.

## Security & Configuration

Never commit tokens, Steam credentials, `.env` files, local `Data/`, game files, or user saves. Preserve atomic writes, hash verification, ZIP traversal checks, and reparse-point guards when changing install or download flows.
