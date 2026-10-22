# DoubleDamageConfigurator

<!-- WIP_LISTING -->
**WIP. This listing is unfinished and has a known bug. Do not treat it as a finished release.**
<!-- /WIP_LISTING -->

A Hollow Knight: Silksong BepInEx plugin. It adds an enemy list under Options > Mods. Each row is one enemy whose dumped prefab `DamageHero` deals 2 or more masks. A Hunter's Journal portrait sits left of the dump name when a record matches. Unmatched names keep a dark empty square.

On caps that enemy to 1 mask. Off keeps vanilla damage. A new install leaves every listed enemy On. Hits from those enemies cap to 1 mask until you flip some Off. The menu shows that legend under Show.

## Install with r2modman

1. Open r2modman and select Hollow Knight: Silksong.
2. Open or create a profile. Local builds in this repo copy into the Development profile.
3. Install [BepInExPack_Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/), [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/), [I18N](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/), and [AssetHelper](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/AssetHelper/) if they are not already in the profile. AssetHelper will not load without I18N.
4. Install this package, or copy the built files into `BepInEx/plugins/json_commits-DoubleDamageConfigurator/`.
5. Launch the game from r2modman. Do not use the Steam play button.

## Use the enemy list

1. Open **Options**, then **Mods**, then **Double Damage Configurator**.
2. During a scan, the list shows percent and file counts (examined / queued) instead of enemy rows. You cannot change Off choices until the scan finishes. A finished scan reloads the open menu.
3. Use **Show** to pick All, Enemies, Bosses, or Other. Enemies are journal-matched non-Other enemies. Other is journal Other plus dump names with no journal record. Bosses are enemies whose prefab FSM shows a boss title.
4. Type in **Search** to match a dump name or a journal name.
5. Leave a row **On** to cap that enemy to 1 mask. Flip it **Off** to keep vanilla damage. Scroll the list to see every match.
6. Press **Regenerate list** after a game patch, or if a previous scan was cut short. Regenerating rebuilds names and damage from the installed game. It does not clear Off choices.

The catalog is written to `BepInEx/config/DoubleDamageConfigurator.enemy-catalog.json`. Off choices are written to `BepInEx/config/DoubleDamageConfigurator.enemy-settings.json`. Neither file is shipped in the zip.

Row names use the Hunter's Journal name when a record matches, and the dump GameObject name otherwise. Journal-matched dump variants that share one journal name share a single On/Off row, and that toggle applies to every dump id in the group. Enemies whose 2-mask hits exist only in PlayMaker are missing until a later pass.

## Build locally

You need the .NET 10 SDK. From the repo root:

```
dotnet build
```

The build copies the DLL into the r2modman profile when `SilksongPath.props` exists. That file is gitignored. Create one with `dotnet new silksongpath` and set `SilksongFolder` to the profile directory, for example `C:/Users/json/AppData/Roaming/r2modmanPlus-local/HollowKnightSilksong/profiles/Development`.

Packaging uses the NuGet `tcli` tool (`init`, `build`, `publish` only). The zip lands in `thunderstore/dist`.

## BepInEx GUID is still the template

`DoubleDamageConfiguratorPlugin.cs` still uses the template GUID `io.github.change_me.doubledamageconfigurator`. Do not change that GUID after the first public release.
