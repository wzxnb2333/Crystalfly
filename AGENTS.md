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

Use `scripts/build-release.ps1` to produce the self-contained ZIP, installer, and checksums. After closing Crystalfly, `scripts/build-and-install.ps1` builds and updates `D:\Program Files\Crystalfly`. Both scripts skip the test suite by default; pass `-RunTests` explicitly when a full regression run is wanted.

## Coding Style & Naming Conventions

Use four-space indentation, file-scoped namespaces, nullable reference types, and implicit usings. Warnings are errors. Name types and public members with `PascalCase`, locals and parameters with `camelCase`, private fields with descriptive camelCase names, and asynchronous methods with an `Async` suffix. Keep cancellation tokens flowing through I/O operations. Prefer existing transaction, JSON, and path-validation helpers over new parallel abstractions.

## Testing Guidelines

Tests use xUnit; UI tests use `Avalonia.Headless.XUnit`. Name tests by behavior, for example `ApplyDirectory_rejects_reparse_point_staging_root`. Add regression coverage for changed behavior, including failure, cancellation, rollback, and path-safety cases where relevant. There is no fixed coverage percentage; CI runs the full solution suite for the merge gate.

For routine development, run the smallest relevant check once. Prefer a focused `--filter` over an entire test project. Reuse passing results while the relevant code and inputs remain unchanged. Documentation and script-only changes need only their relevant lightweight checks. Do not automatically run full regression, rebuild installers, perform desktop acceptance, or start another audit round after a focused check passes.

### Change-to-Test Routing

Choose tests within the affected project; build changed test code before using `--no-build`:

| Changed paths | Test command |
| --- | --- |
| `src/Crystalfly.App/**` | `dotnet test tests/Crystalfly.App.Tests -c Release --filter 'FullyQualifiedName~<affected test>'` |
| `src/Crystalfly.Core/**` | `dotnet test tests/Crystalfly.Core.Tests -c Release --filter 'FullyQualifiedName~<affected test>'` |
| `src/Crystalfly.Steam/**` | `dotnet test tests/Crystalfly.Steam.Tests -c Release --filter 'FullyQualifiedName~<affected test>'` |
| `scripts/build*.ps1` | The corresponding `scripts/test-build*.ps1` check |
| Cross-module or `Directory.*.props` changes | One build and relevant cross-module tests; full suite stays in CI or an explicit `-RunTests` run |

## Commit & Pull Request Guidelines

Use concise Conventional Commit subjects seen in history: `feat:`, `fix:`, or `build:`. Keep commits scoped and avoid mixing generated output or unrelated cleanup. Pull requests should explain the background, changes, observable result, and verification commands. Link relevant issues, call out compatibility or data-migration risks, and include before/after screenshots for Avalonia UI changes.

## Security & Configuration

Never commit tokens, Steam credentials, `.env` files, local `Data/`, game files, or user saves. Preserve atomic writes, hash verification, ZIP traversal checks, and reparse-point guards when changing install or download flows.
