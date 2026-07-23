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
dotnet test '.\Crystalfly.slnx' -c Release --no-build
dotnet run --project '.\src\Crystalfly.App\Crystalfly.App.csproj'
```

Use `scripts/build-release.ps1` to produce the self-contained ZIP, installer, and checksums. After closing Crystalfly, `scripts/build-and-install.ps1` performs the complete build and updates `D:\Program Files\Crystalfly`.

## Coding Style & Naming Conventions

Use four-space indentation, file-scoped namespaces, nullable reference types, and implicit usings. Warnings are errors. Name types and public members with `PascalCase`, locals and parameters with `camelCase`, private fields with descriptive camelCase names, and asynchronous methods with an `Async` suffix. Keep cancellation tokens flowing through I/O operations. Prefer existing transaction, JSON, and path-validation helpers over new parallel abstractions.

## Testing Guidelines

Tests use xUnit; UI tests use `Avalonia.Headless.XUnit`. Name tests by behavior, for example `ApplyDirectory_rejects_reparse_point_staging_root`. Add regression coverage for changed behavior, including failure, cancellation, rollback, and path-safety cases where relevant. There is no fixed coverage percentage; the full solution test run is the merge gate.

### Change-to-Test Routing

Run only the affected test project for faster feedback:

| Changed paths | Test command |
| --- | --- |
| `src/Crystalfly.App/**` | `dotnet test tests/Crystalfly.App.Tests -c Release --no-build` |
| `src/Crystalfly.Core/**` | `dotnet test tests/Crystalfly.Core.Tests -c Release --no-build` |
| `src/Crystalfly.Steam/**` | `dotnet test tests/Crystalfly.Steam.Tests -c Release --no-build` |
| Cross-module or `Directory.*.props` changes | Full solution test run |

## Commit & Pull Request Guidelines

Use concise Conventional Commit subjects seen in history: `feat:`, `fix:`, or `build:`. Keep commits scoped and avoid mixing generated output or unrelated cleanup. Pull requests should explain the background, changes, observable result, and verification commands. Link relevant issues, call out compatibility or data-migration risks, and include before/after screenshots for Avalonia UI changes.

## Security & Configuration

Never commit tokens, Steam credentials, `.env` files, local `Data/`, game files, or user saves. Preserve atomic writes, hash verification, ZIP traversal checks, and reparse-point guards when changing install or download flows.
