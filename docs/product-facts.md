# Product facts

Verified on 2026-07-16.

## Runtime

- Windows 10/11 x64.
- .NET SDK 10.0.302.
- Avalonia 12.1.0. Avalonia 12 supports .NET 10 and uses the classic desktop
  application lifetime for this project.
- Lucide.Avalonia 0.2.13. The older LucideAvalonia 1.6.2 package targets
  Avalonia 11.1 and fails at runtime on Avalonia 12.
- SteamKit2 3.4.0. Crystalfly uses SteamKit directly for authentication,
  manifest access, and CDN chunk downloads.

## Hollow Knight Steam data

- App ID: `367520`.
- Windows depot ID: `367521`.
- `1.2.2.1` manifest: `648876203478229944`.
- `1.4.3.2` manifest: `7100970131266256005`.
- `1.5.78.11833` manifest: `4539921744901848404`.
- `1.5.12459` manifest: `4709992615819941531` (original Steam file identity verified on 2026-09-27; see [evidence](build-verification-1.5.12459.md)).
- Public manifest at verification time: `257781644874438846`.

The public manifest is runtime catalog data, not a hard-coded definition of
"latest" in UI or compatibility logic.

## Loader mapping

- `1.2.2.1`: Modding API v37.
- `1.4.3.2`: Modding API v60.
- `1.5.78.11833`: Modding API v77.
- `1.5.12459`: BepInEx 5.4.23.4; no matching Modding API or verified DebugMod package as of 2026-09-27. Game identity verification does not imply API compatibility.
- Verified public build `1.5.12620.0`: Modding API v78 or BepInEx 5.4.23.4.

BepInEx 5.4.23.4 may also be installed and repaired on every other game build
in general instances, including unknown or custom builds. Older catalog
allowlists do not restrict BepInEx installation.

Modding API and individual mods keep their build restrictions. Unknown builds
with loaders still require launch compatibility confirmation; installation
availability is not a guarantee of plugin compatibility. Official speedrun
instances remain protected from loader installation.

## Speedrun tools

- Crystalfly pins the AssemblyPatches v1.0.2 Windows release for `1.2.2.1`,
  `1.4.3.2`, and `1.5.78`; each ZIP contains one root `Assembly-CSharp.dll`.
- The client verifies both release ZIP and inner DLL SHA-256. The published
  binaries are authoritative because the repository source does not fully
  reproduce the v1.0.2 assets.
- RuntimePatches configuration fields are `ScreenShakeModifier`,
  `MiniSaveStates`, `FasterIntroSkip`, and `TextMasher`. Version capability
  differences are enforced locally, and every option defaults off.

## Primary sources

- Avalonia docs: <https://github.com/avaloniaui/avalonia-docs>
- SteamKit: <https://github.com/SteamRE/SteamKit>
- Hollow Knight resources: <https://github.com/hk-speedrunning/HK-Resources>
- Hollow Knight rules: <https://github.com/hk-speedrunning/HK-Rules>
- AssemblyPatches v1.0.2: <https://github.com/olvior/AssemblyPatches/releases/tag/v1.0.2>
- MiniSaveStates: <https://github.com/Shichien/MiniSavestates>
