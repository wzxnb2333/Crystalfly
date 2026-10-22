# Tool Keybinds
Hollow Knight: Silksong mod that makes tools use separate keybinds
instead of relying on the looking direction.

## Installation
Unarchive the mod in `<Silksong Folder>/BepInEx/plugins`.
After installation, you should have `KikuGie-ToolKeybinds/ToolKeybinds.dll` in that folder.  
*Alternatively, use a mod manager like [Cogfly](https://github.com/Nix-main/Cogfly).*

It's also recommended to install [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/).

## Config
By default, the tools are bound to:
- Upper tool: `G`
- Lower tool: `H`

You can change these in `<Silksong Folder>/BepInEx/config/dev.kikugie.toolkeybinds.cfg`.
Values must be valid [Unity KeyCodes](https://docs.unity3d.com/ScriptReference/KeyCode.html).

## Controllers
For controller support, you need to bind controller buttons to the configured keys.

For example, in Steam you can open `Hollow Knight: Silksong > Controller Settings > Edit Layout`,
and assign the keys to unused controller buttons (e.g. back grip buttons).
