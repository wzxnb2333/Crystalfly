# SaveFileManagerMod

A Hollow Knight: Silksong mod. Adds buttons to name and archive save files.

## Features

### Naming save files

- The mod adds a "Rename" button below each save file to give them a custom name, making it easier to keep track of your saves.
- Names are synchronized to your Steam Cloud.

### Archiving save files
- The mod adds an "Archive" button below each save file, allowing you to:
  - Move a save to the archive, freeing up one of your precious 4 slots.
  - Loading an archived save back into an empty slot.
  - Swapping an active save with one from the archive.
- Archived saves are still synchronized to your Steam Cloud.
- If you have [More Saves] installed, the archive will be the later pages of saves. In that case,
  this mod will simply allow you to move saves between pages.


## Dependencies

- [BepInEx]
- [I18N] (optional)
  - The mod will be displayed in english by default. If I18N is installed, additional languages will be available.
  - Note that currently, only english and german were checked by a human. The remaining languages are MTL/AI.


## Installation

### Managed installation

This mod should be compatible with the mod management tool of your choice. If any of them don't work, please open [an issue on GitHub](https://github.com/mich101mich/Silksong.SaveFileManagerMod/issues).

### Manual installation

Drop the downloaded and extracted files into your game folder's `Bepinex/plugins` folder.

In order for I18N translations to work, make sure that the `languages` folder is directly adjacent to the `SaveFileManagerMod.dll` file and that both
are within a subfolder (so for example `Bepinex/plugins/SaveFileManagerMod`) of plugins. Translations won't work if the files are directly inside
of the plugins folder.


[More Saves]: https://thunderstore.io/c/hollow-knight-silksong/p/Clazex/MoreSaves/
[BepInEx]: https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/
[I18N]: https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/
