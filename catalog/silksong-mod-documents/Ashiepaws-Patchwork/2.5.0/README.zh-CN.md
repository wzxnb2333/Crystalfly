![Patchwork](https://raw.githubusercontent.com/Ashiepaws/silksong-patchwork/refs/heads/master/img/Patchwork.png)
[![Discord](https://img.shields.io/discord/1297141570805694554?style=for-the-badge&label=Discord&link=https%3A%2F%2Fdiscord.gg%2FDeCtHA84AM)](https://discord.gg/DeCtHA84AM)
 [![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/Ashiepaws/Patchwork?style=for-the-badge)](https://thunderstore.io/c/hollow-knight-silksong/p/Ashiepaws/Patchwork/) [![Twitch Status](https://img.shields.io/twitch/status/ashiepaws?style=for-the-badge&link=https%3A%2F%2Ftwitch.tv%2Fashiepaws)](https://twitch.tv/ashiepaws)
 [![Static Badge](https://img.shields.io/badge/ko--fi-donate-blue?style=for-the-badge&link=https%3A%2F%2Fko-fi.com%2Fashiepaws)](https://ko-fi.com/ashiepaws)

一款专为《空洞骑士：丝之歌》设计的自定义资源模组，特别注重创作的便捷性。

# 在更新 PATCHWORK 之前的重要说明！  
**请始终将你正在制作的资源包备份到 PATCHWORK 文件夹之外！**  
Thunderstore（以及 Gale）在更新到新版本时会删除文件夹内的所有资源包。请务必在安全的位置（且不在任何游戏文件夹内）备份你正在制作的资源包。

## 特性  
* 使用独立的精灵图像文件进行精灵替换，包含正确的名称、旋转与尺寸设置——无需臃肿的精灵图集  
* 支持 Texture2D 精灵（此前需要 Silksong Customizer + CustomizerT2D）  
* 与基于精灵图集的皮肤完全兼容  
* 当文件发生变化时自动重新加载资源，因此你可以立即在游戏内查看皮肤效果，无需关闭游戏  
* 内置精灵导出功能  
* 音频替换支持音效和音乐  
* 场景替换操作简便——只需拖放视频文件即可  
* 自定义对话文本替换  

### 计划中的特性  
* 条件精灵（e.g. 根据纹章、生命值等状态为大黄蜂显示不同纹理）  
* 游戏内资源包管理器

## 配置

### 导出
* `DumpSprites`: 启用精灵导出，将任何已加载场景中的精灵保存到 "Patchwork/Dumps" 文件夹中。这些文件可用于制作新的纹理包。**不要在正常游戏过程中启用此功能**，因为它会显著减慢游戏的加载速度。如果启用了此项，当按下配置于“键位绑定/FullDumpKey”下的按钮时（默认：F6），模组将允许你导出游戏中所有场景的纹理。
* `DumpText`: 启用文本导出，将游戏所有语言中的文本内容导出到 "Patchwork/TextDumps" 文件夹中。**不要在正常游戏过程中启用此功能**，因为它会显著延长游戏启动时间。

### 界面
* `LogAudioDuration`: 音效名称在日志中显示的时间长度（单位：秒）。（默认值：5）
* `HideModdedAudioInLog`: 默认开启。若启用，已由模组文件覆盖的音效将不会出现在日志中，这有助于更方便地查找你想要修改的具体音效名称。
* `TextLogDuration`: 文本键名称在日志中显示的时间长度（单位：秒）。（默认值：10）

### 菜单快捷键
* `ShowAudioLog`: 显示音频日志窗口的快捷键，该窗口记录 Patchwork 可访问的所有音效名称。如果你在 "Sounds" 文件夹中添加自定义文件，必须使用与日志中显示完全相同的名称，才能使 Patchwork 替换这些文件。（默认值：1）
* `ShowAudioList`: 显示音频列表的快捷键，列出所有静态加载的声音效果。可用于查找未出现在音频日志中的音效名称。（默认值：2）
* `ShowAnimationController`: 显示动画控制器的快捷键，可冻结单个对象、暂停和播放特定动画，并立即选择单独的精灵图像进行导出与编辑。（默认值：3）
* `ShowTextLog`: 显示文本日志的快捷键，记录游戏请求显示的任何文本的表名/键名。（默认值：4）

### 动画控制器快捷键
* `AnimationControllerPauseKey`: 切换当前选中对象的“暂停”状态，冻结动画并允许手动选择帧。
* `AnimationControllerNextFrameKey`: 若当前选中对象处于暂停状态，则向前推进一帧动画。
* `AnimationControllerPrevFrameKey`: 若当前选中对象处于暂停状态，则显示上一帧动画。
* `AnimationControllerFreezeKey`: 切换当前选中对象的“冻结”状态，锁定其在游戏世界中的位置，以便更清晰地观察和测试包含移动效果的动画。

## 纹理创建指南  
1. 加载一个存档文件并打开 Patchwork 动画控制器（默认绑定到 "3" 键）  
2. 打开暂停菜单，点击你想要修改精灵图的物体名称。该物体名称在动画控制器中会变为绿色  
3. 使用下拉菜单选择你想要编辑的动画，或者按动画控制器的暂停键（默认为 "Home"）暂停正在执行的动画，以便编辑目标帧  
4. 使用动画控制器的帧前/后键（默认为 "Page Up" / "Insert"）选择你想要编辑的具体帧  
5. 点击“编辑当前精灵”按钮（如果你之前关闭了暂停菜单，可能需要再次打开）  
6. 精灵图将自动在你默认的图像编辑程序中打开。你可以自由编辑并保存文件  
7. 你的编辑后的精灵图现在会自动出现在游戏中，无需重启游戏即可应用更改  

**注意：** 点击“编辑当前精灵”按钮后，你可以找到自定义精灵图位于 `BepInEx/plugins/Ashiepaws-Patchwork/Sprites`。

## 音效制作指南  
1. 游戏启动后，打开音效日志和音效列表窗口。（默认这些功能按键为 "1" 和 "2"）  
2. 触发你想要修改的音效。你应该在屏幕右上角的音效日志中看到该音效的名称。  
   * 如果你在音效日志中没有看到该音效，它可能是预先加载的音效。此时请检查“已加载音效”列表。  
3. 将一个与音效日志（或已加载音效列表）中看到的名称相同的音效文件放入“Patchwork/Sounds”文件夹中。例如，要替换标题音乐，文件名应为“Title.wav”或“Title.mp3”。  
4. 再次触发该音效。此时它应该播放你自定义的音效！  

**注意：** Patchwork 支持所有 [Unity-supported audio formats.](https://docs.unity3d.com/Manual/AudioFiles-compatibility.html)

## 场景替换指南  
1. 找到你想要替换的场景的内部名称。你可以找到所有可用场景的列表 [here.](https://github.com/Ashiepaws/silksong-patchwork/wiki/Cutscene-Names)  
2. 将你的替换视频文件放入 `plugins/Ashiepaws-Patchwork/Videos` 文件夹，文件名需与你想要替换的场景名称相同。（例如：`Intro_Cinematic.mp4`）  
3. 现在你的视频文件将替代原场景播放！  

**注意：** Patchwork 支持所有 [Unity-supported video formats.](https://docs.unity3d.com/Manual/VideoSources-FileCompatibility.html)

## 文本替换指南  
Patchwork 直接接入丝之歌的本地化系统，因此游戏内显示的任何文本均可被替换。游戏采用“表键系统”来管理文本，Patchwork 通过文本文件允许你覆盖该系统中的特定文本片段。文件结构如下：

`Patchwork/Text/[Sheet]/[LANGUAGE].yml`

其中，“Sheet”为文本表的名称，“LANGUAGE”是语言的 2 位大写标识符。（例如：`Patchwork/Text/MainMenu/EN.yml` 替换名为 `MainMenu` 的文本表中英文语言的文本）

这些文件本身是简单的 YAML 文件，应始终遵循以下结构：

`[KEY]: "All text goes here."`

其中，“KEY”是文本表内的键名。（请确保不要包含 `[]` 的方括号！）

### 如何查找表名与键名  
为了查找特定文本行的表名与键名，你有两种选择：

1. 启用 `DumpText` 配置选项并启动一次游戏。之后你可以再次禁用该选项。这会将整个游戏中的所有文本导出到 `Patchwork/TextDumps` 文件夹中，文件结构正确，因此你可以直接从中复制内容。  
2. 在游戏中打开文本日志（默认快捷键：4），并触发你想要替换的文本显示。文本日志会显示文本内容，以及表名和键名，格式为 `Sheet.KEY`

## 在 Thunderstore 上发布模组包

Patchwork 的设计方式使得创作者可以将他们的模组包作为插件发布到 Thunderstore！为了确保玩家下载时能够被正确自动安装，请务必遵循以下结构来组织你的插件：

```
YourName-PackName.zip
 \- icon.png
 \- manifest.json
 \- README.md
 \- plugins
     \- Patchwork
         |- Sprites
         |   \- <your files here...>
         |- Spritesheets
         |   \- <your files here...>
         |- Sounds
         |   \- <your files here...>
         \- Videos
             \- <your files here...>
```

## 特别致谢

* 致 [Douglas Gregory](https://bsky.app/profile/dmgregory.ca)，没有他的帮助，我无法实现与GPU相关的性能优化，从而使Patchwork v2达到如今的运行速度。
* 致 [RatherChaoticDev](https://next.nexusmods.com/profile/RatherChaoticDev)，创造了Patchwork所基于的原始 [Silksong Customizer](https://www.nexusmods.com/hollowknightsilksong/mods/142) 模组。
* 致 [Su4enka](https://next.nexusmods.com/profile/Su4enka)，其CustomizerT2D插件（作为其 [Cute Hornet skin](https://www.nexusmods.com/hollowknightsilksong/mods/203) 的一部分）帮助实现了Patchwork对T2D的支持。