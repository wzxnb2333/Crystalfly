# Tool Keybinds
空洞骑士：丝之歌模组，使工具使用独立的键位绑定
不再依赖视线方向。

## 安装
将模组解压到 `<Silksong Folder>/BepInEx/plugins`。
安装完成后，你应该在该文件夹中看到 `KikuGie-ToolKeybinds/ToolKeybinds.dll`。  
*或者，使用像 [Cogfly](https://github.com/Nix-main/Cogfly) 这样的模组管理器。*

也建议安装 [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/)。

## 配置
默认情况下，工具的绑定为：
- 上方工具：`G`
- 下方工具：`H`

您可以在 `<Silksong Folder>/BepInEx/config/dev.kikugie.toolkeybinds.cfg` 中进行更改。
值必须是有效的 [Unity KeyCodes](https://docs.unity3d.com/ScriptReference/KeyCode.html)。

## 控制器
为了支持控制器，你需要将控制器按钮绑定到配置的按键上。

例如，在 Steam 中你可以打开 `Hollow Knight: Silksong > Controller Settings > Edit Layout`，
并将按键分配给未使用的控制器按钮（e.g. 后握把按钮）。
