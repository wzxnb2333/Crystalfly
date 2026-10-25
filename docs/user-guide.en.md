# Usage and maintenance guide

[Back to overview](../README.en.md) · [简体中文](user-guide.zh-CN.md)

Compatibility, installation rules, storage, and development details. Start with the quick start on the project overview if you are new to Crystalfly.

## Compatibility

| Game build | Loader | DebugMod |
| --- | --- | --- |
| `1.2.2.1` | Modding API v37 or BepInEx 5.4.23.4 | `legacy/1.2.2.1` |
| `1.4.3.2` | Modding API v60 or BepInEx 5.4.23.4 | `legacy/1.4.3.2` |
| `1.5.78.11833` | Modding API v77 or BepInEx 5.4.23.4 | `legacy/1.5.78` |
| `1.5.12459` | BepInEx 5.4.23.4; no matching Modding API package | — |
| Current verified stable | Modding API v78 or BepInEx 5.4.23.4 | `latest` |

The `1.5.12459` identity was verified against its original Steam manifest and file hashes on 2026-09-27; it is no longer offered as an unverified, vanilla-only download. See the [verification evidence](build-verification-1.5.12459.md). This does not establish Modding API compatibility: v77 targets `1.5.78.11833`, and the current v78 release targets `1.5.12620.0`; neither is offered for `1.5.12459`.

The current stable build is decided by the remote catalog's Steam public manifest, never hard-coded. General instances allow BepInEx installation and repair on every game build, including custom builds and unknown Steam manifests, even if an older remote catalog still lists a narrow build allowlist. Modding API and individual mods retain their compatibility checks, and official speedrun instances remain protected. Unverified loader compatibility alone no longer triggers a launch warning or blocks launch. Modding API and individual mod compatibility rules still apply.

- DebugMod: <https://github.com/wzxnb2333/New.HK.Debug/releases/tag/v1.4.10.5-r2>
- Modding API v78: <https://github.com/wzxnb2333/api/releases/tag/1.5.12620.0-78>
- Modding API v37: <https://github.com/wzxnb2333/api/releases/tag/1.2.2.1-37-windows>

## Launch checks, Mod Market, and instance details

After an instance is selected, the launch page checks the executable, running processes, Loader state, Mod dependencies and file hashes, pending transaction recovery, and per-instance LocalLow readiness. Mod-only problems can be force-launched after a detailed confirmation; absolute blockers cannot. A per-instance issue fingerprint can suppress repeated dialogs while the exact issue and file hash remain unchanged, but the red warning frame stays visible.

Download → Mod Market discovers online mods and filters them by keyword, game build, loader, source, and tag. Its detail view shows description, authors, dependencies, integrations, repository, source, and exact compatibility before the user chooses a target instance. A vanilla target can install the catalog's exact required loader after confirmation, then re-evaluates compatibility before installing the mod. Conflicted, drifted, and official speedrun instances remain unavailable; unknown builds are evaluated against the requested package compatibility rules.

The target dialog previews the loader, every recursive dependency, and the requested mod. Confirmation only adds the plan to Download → Download Queue, so the market remains usable. Each dependency chain runs in loader/dependency/mod order; unrelated groups share up to three network slots. Transfers may continue while the game runs, but installation waits for the target game process to exit. Transient network failures retry three times, while deterministic hash, manifest, and compatibility errors fail immediately. Unfinished tasks resume after restart; failed tasks remain available for manual retry.

When the UI is Simplified Chinese, the market also loads Crystalfly's independently maintained HK ModLinks Chinese translation catalog. It searches translated names, descriptions, and labels alongside official English metadata; missing translations fall back to English. The source policy and validation command are documented in [docs/mod-translations.zh-CN.md](mod-translations.zh-CN.md).

The Installed Mods page includes receipt-backed and external Mods and can filter enabled, disabled, local, external, pinned, updateable, or unhealthy entries. External Mods stay read-only until explicit takeover. Managed Mods support health inspection and exact-version repair; local takeovers support re-import or accepting current hashes. Pinning protects entries from batch uninstall and dependency cleanup. Uninstall previews a dependency-impact tree and only suggests unused dependencies instead of deleting them automatically.

Mod Packs store an exact game build, Loader, and managed Mod versions. Local or external entries contain only names and file hashes, never files, download URLs, or local paths. Append mode installs or enables missing entries; exact mode also disables unlisted, unpinned Mods while retaining every transitive dependency of an enabled pinned Mod. Plans run through the existing queue in dependency order, and the restore point is captured when execution is about to modify the instance. The complete Mod Pack group excludes other mutations of that instance, and restore validates pinned entries, receipts, and file health before writing. Mod Pack JSON is limited to 128 KiB and 1,000 entries. Local JSON sharing remains available offline; the hosted service uses 12-character share codes.

Compatibility uses the exact loader package ID rather than treating every Modding API or BepInEx release as interchangeable. Crystalfly-managed loaders can be repaired or removed. A manually installed BepInEx with a verifiable version is detected as externally owned: matching plugins may be installed, but Crystalfly never repairs, removes, overwrites, or takes ownership of the BepInEx installation. Manually installed Modding API remains `Drifted` because no trusted vanilla assembly backup exists.

The Logs page discovers BepInEx, Modding API, and shared `Player.log` files, shows each source path, and reads refreshable tail content. The shared `Player.log` may belong to the most recently launched instance, so instance-local loader logs are the stronger source when diagnosing one instance.

## Speedrun environments

The three built-in RuntimePatches templates create dedicated full copies from a user-selected clean Vanilla instance. Crystalfly pins the AssemblyPatches v1.0.2 Windows release and verifies both the ZIP and inner `Assembly-CSharp.dll` SHA-256.

- `1.2.2.1`, `1.4.3.2`, and `1.5.78` are supported without Modding API, BepInEx, or LoadNormaliser.
- Every option defaults off. `1.2.2.1` has no `FasterIntroSkip`; `1.5.78` has no `ScreenShakeModifier`.
- `FasterIntroSkip` and `MiniSaveStates` surface category-rule warnings. Users must still check the current SRC rules.

Pre-launch validation checks the core game fingerprint, RuntimePatches DLL, isolated configuration, Loader/Mod markers, transactions, and LocalLow state. Technical errors block launch; rule warnings do not. PNGs, skins, and ordinary extra files are ignored. Legacy template instances remain on disk but must be recreated.

Verification reports are pre-launch integrity snapshots. They do not attest that files remain unchanged after the report is written. Published binaries are not Authenticode-signed; signed update manifests and asset checksums are verified separately.

## Steam downloads and offline use

Steam sign-in supports credentials, QR codes, and Steam Guard. Downloads use either the public branch or a manually supplied Windows depot manifest. Public resolves to a real manifest; repair reuses the exact manifest recorded by the instance. Unverified builds retain their true identity and can become verified when a later catalog matches their file fingerprint.

Downloads follow the Windows system proxy. Proxy changes or unexpected disconnects pause Steam work and reconnect saved accounts. A depot shares up to sixteen concurrent chunk requests; verified chunks can be reused across builds and instances through a 20 GiB cache per game directory. Remembered credentials and refresh tokens are encrypted with the current Windows user's DPAPI.

Offline mode disconnects Steam, uses existing valid caches, and leaves network work waiting for online mode. GitHub route selection only affects official GitHub catalogs and GitHub-hosted packages. Steam, custom catalogs, and other hosts keep their own routes; package checks stay in place.

The installer registers the validated `crystalfly://` protocol. External requests are forwarded to the running instance; state changes require a summary and confirmation. The app checks a signed stable update manifest once per day. Installed mode runs the installer; portable updates use same-volume backup and replacement while preserving `Data`.

## Advanced local loaders

A local loader requires a Crystalfly JSON manifest and a ZIP in the same directory; a bare loader ZIP is not accepted. The manifest contains the package identity, loader state, exact supported builds, managed paths, size, and SHA-256. Local sources always remain marked unverified.

```json
{
  "schemaVersion": 1,
  "id": "community-loader",
  "name": "Community Loader",
  "version": "1.0.0",
  "loaderState": "moddingApi",
  "packageFile": "CommunityLoader.zip",
  "sizeBytes": 123456,
  "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "supportedBuildIds": ["1.5.78.11833"],
  "managedFiles": ["hollow_knight_Data/Managed/Assembly-CSharp.dll"]
}
```

`loaderState` must be `moddingApi` or `bepInEx`. Paths, ZIP size, and SHA-256 are checked before modifying the instance. Settings can also replace the official ModLinks source with an HTTPS catalog bound to an exact build and Modding API loader; custom content remains unverified.

## Data locations and recovery

Installed settings live in `%LOCALAPPDATA%\Crystalfly`; portable settings use `Data` beside the executable when `portable.flag` exists. Instance identity is stored in `.crystalfly-instance.json`. Metadata, transactions, cache, LocalLow data, and snapshots live under the active game directory's `.crystalfly`; Silksong uses separate per-game state.

Crystalfly backs up shared LocalLow before taking ownership. Interrupted transactions recover automatically only when recorded stages and hashes establish safety; otherwise the instance enters `NeedsAttention` and launch is blocked. See the [Silksong guide](silksong-thunderstore.md) for its save subpaths and migration rules.

## Develop

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet run --project '.\src\Crystalfly.App\Crystalfly.App.csproj'
```

For routine development, run one check scoped to the change, preferably filtered by test name. Documentation and script changes only need their relevant lightweight checks. Reuse passing results while code and inputs remain unchanged; do not automatically repeat full regression, installation acceptance, or another audit round. CI retains the full test suite as the merge gate.

## Release build

```powershell
pwsh -NoProfile -File '.\scripts\build-release.ps1' -Version '1.1.8'

# Build and install locally without an update-signing key; no update manifest is emitted.
pwsh -NoProfile -File '.\scripts\build-and-install.ps1' -Version '1.1.8' -UnsignedLocal

# Opt into full regression only when needed; both build scripts support -RunTests.
pwsh -NoProfile -File '.\scripts\build-release.ps1' -Version '1.1.8' -RunTests
```

Both build scripts skip the test suite by default and build only the projects needed for packaging. Pass `-RunTests` explicitly to build the entire solution and run all tests first. Package consistency, signatures, and checksum checks remain enabled.

The scripts automatically locate Inno Setup 6 from `PATH` or its standard install directories. Pass `-IsccPath '<path to ISCC.exe>'` for a custom location. Release builds read `CRYSTALFLY_UPDATE_SIGNING_KEY` from the ignored `.env.update-signing` file and use `tools/Crystalfly.ReleaseTool` to sign the update manifest; never commit the private key file. For local verification only, pass `-UnsignedLocal`; this omits `update-manifest.v1.json` and must not be uploaded as a public Release. `build-and-install.ps1` reads the version from `Directory.Build.props`, runs the Release build, validates the artifacts, then silently updates `D:\Program Files\Crystalfly` with administrator approval and verifies the installed version. It stops when Crystalfly is running and never terminates the process. The installer defaults to `D:\Program Files\Crystalfly` and requests administrator privileges; the portable ZIP can be extracted elsewhere. Outputs under `artifacts` include the self-contained publish, updater helper, portable ZIP, installer, signed `update-manifest.v1.json`, and `SHA256SUMS.txt`. Assets are not Authenticode-signed yet; the client still verifies the Ed25519 manifest signature plus each asset's SHA-256, size, and version.

Application settings use `%LOCALAPPDATA%\Crystalfly`, or `Data` beside the executable when `portable.flag` exists. Per-instance state stays under the active game directory's `.crystalfly` folder.
