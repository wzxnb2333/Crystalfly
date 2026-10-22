# 丝之歌 焦点体验优化

一款专为 **空洞骑士：丝之歌** 设计的模组，用于改善窗口焦点行为。  
当游戏窗口失去焦点时，会自动暂停并静音游戏；当焦点恢复时，在指定时间窗口内会自动恢复游戏运行。

## 特性
- 当失去焦点时暂停游戏  
- 当失去焦点时静音音频  
- 当重新获得焦点时自动恢复游戏  
- 可配置快速切换标签页的时间窗口  
- 支持通过 **[Configuration Manager](https://www.nexusmods.com/hollowknightsilksong/mods/26)** 实时调整设置

## 配置

首次启动后，会生成配置文件：`BepInEx/config/com.zlq.silksongfocusqol.cfg`

你可以通过手动编辑 `.cfg` 文件来修改设置 **（需要重新启动游戏）**，或者使用 **[Configuration Manager](https://www.nexusmods.com/hollowknightsilksong/mods/26)** 在游戏运行时实时调整设置。

#### 配置文件示例
```ini
## Settings file was created by plugin Silksong Focus QoL v1.0.1
## Plugin GUID: com.zlq.silksongfocusqol

[Settings]

## Mute audio when window loses focus
EnableAutoMute = true

## Pause game when window loses focus
EnableAutoPause = true

## Unpause when focus returns
EnableAutoUnpause = true

## Time window to auto-unpause
## 0 = always unpause when focus returns; >0 = only if refocus occurs within this many seconds
AutoUnpauseWindow = 3
```


## 安装

**要求：** 必须安装 [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) 才能使用本模组。

1. 在 Steam 中，右键点击 **空洞骑士：丝之歌** → **管理 → 浏览本地文件**。  
2. 下载 BepInEx `.zip` 并将其内容解压到游戏文件夹中。  
3. 启动游戏一次，然后退出。  
4. 打开游戏目录中的 `BepInEx` 文件夹，你应该会看到 `plugins` 和 `config` 文件夹 —— 这表示 BepInEx 已正确加载。  
5. 将本模组的 `.dll` 文件放入 `BepInEx/plugins` 中。

为了更方便地在游戏中进行配置，你也可以使用 BepInEx 5 配合来自 Nexus Mods 的 [Configuration Manager](https://www.nexusmods.com/hollowknightsilksong/mods/26)。

## 更新日志

查看完整更新日志 [here](https://github.com/zlq23/SilksongFocusQoL/blob/main/CHANGELOG.md)。


技术标识：`QoL`
