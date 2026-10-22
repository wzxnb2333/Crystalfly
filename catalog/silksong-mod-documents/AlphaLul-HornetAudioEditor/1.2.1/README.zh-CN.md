一个丝之歌模组，允许用户使用自己的 .wav 音频文件自定义游戏音效。该模组内置了原版空洞骑士游戏中的大黄蜂语音片段（包括 shaw）。

### [Video guide on YouTube](https://youtu.be/YPCi3jd9szw)
### [GitHub page](https://github.com/AlphaLul/Silksong.HornetAudioEditor)

# 指南

**前言：** 本模组仅允许你自定义使用 `RandomAudioClipTable` 内部音频。大多数包含多个随机选择音效的音频使用 `RandomAudioClipTable`。要确定哪些音频符合要求，请阅读下方的 [BepInEx Config](#bepinex-config) 部分。

## 手动安装  
1. 安装 Silksong 的 BepInEx 5  
2. 在你的 `BepInEx/plugins` 文件夹中，创建一个名为 `HornetAudioEditor` 的文件夹  
3. 下载并解压模组 zip 文件  
4. 将 `plugins` 文件夹中的内容移动到你刚刚创建的 `HornetAudioEditor` 文件夹中  
5. 将 `patchers` 文件夹中的文件移动到你的 `BepInEx/patchers` 文件夹中  
6. 启动游戏  
   - 启动游戏后，你将可以在 `audioCollections.json` 文件中访问 `HornetAudioEditor` 文件夹中的内容

## 基础使用  
配置完成后，你的 `HornetAudioEditor` 文件夹应类似于如下结构：  
<pre>  
HornetAudioEditor/  
├─ Clips/  
│  ├─ clip1.wav  
│  ├─ clip2.wav  
│  └─ subfolder/  
│     ├─ clip3.wav  
│     └─ clip4.wav  
├─ Collection Presets/  
│  ├─ preset1.json  
│  └─ preset2.json  
├─ audioCollections.json  
└─ HornetAudioEditor.dll  
</pre>  

### 添加音频片段  
要向模组中添加音频片段，请将 .wav 文件放入 `Clips` 文件夹或其任意子文件夹中。  

### 配置 `audioCollections.json`  
为了告诉游戏使用哪些文件夹来加载每个音频表，需编辑 `audioCollections.json` 文件。  
- **键**：相对于 `Clips` 的文件夹名称  
    - 使用空字符串 `""` 表示基础的 `Clips` 文件夹  
- **值**：应使用该文件夹音频的音频表名称数组  

#### 示例：  
- `table1` 从基础的 `Clips` 文件夹加载音频  
- `table3` 和 `table4` 从 `Clips/subfolder` 文件夹加载音频  
- `table2` 从两个文件夹均加载音频  
```json
{
  "": [
    "table1",
    "table2"
  ],
  "subfolder": [
    "table2",
    "table3",
    "table4"
  ]
}
```

## 保留原版音效  
如果你想让音频表在保留原始（原版）音效的同时，也包含模组添加的音效，请在表名前加上 `+`。

#### 示例：  
- `table1` 仅用模组音效替换原有音效  
- `table2` 添加模组音效，同时保留原版音效  
```json
{
  "subfolder": [
    "table1",
    "+table2"
  ]
}
```

## 集合预设  
集合预设是可选的 .json 文件，存储在 `Collection Presets` 文件夹中。它们定义了可以一次性被引用的音频表集合，用于 `audioCollections.json`。

使用这些预设可以避免你的 `audioCollections.json` 文件变得过于臃肿，或者方便地与他人共享大量音频表集合。

#### 示例：  
`preset1.json`  
```json
[
  "table1",
  "table2",
  "etc"
]
```
`audioCollections.json`  
- 与 `table1`、`table2`、`etc`、`singletable` 解释相同  
```json
{
  "subfolder": [
    "preset1.json",
    "singletable"
  ]
}
```

## BepInEx 配置  
运行游戏一次后，可在 `BepInEx/config/alphalul.HornetAudioEditor.cfg` 找到一个 BepInEx 配置文件。该文件允许你自定义一些基本设置。

| 设置           | 默认值 | 描述                                                                                                |
|----------------|--------|------------------------------------------------------------------------------------------------------------|
| 启用            | true   | 模组是否激活。设为 false 可禁用音频修改。                                  |
| RefreshOnSaveQuit        | true   | 返回标题画面时是否重新应用 `audioCollections.json`。                             |
| LogAudio        | false  | 当播放声音时是否记录 `RandomAudioClipTable` 的名称。有助于查找表名。         |
| LogSpamCooldown        | 0.2     | 在再次记录相同 `RandomAudioClipTable` 前等待多少秒。有助于减少控制台日志输出。   |

### ⚠️ 查找音频表名称  
要正确使用 `LogAudio`，你需要启用 BepInEx 控制台以实时查看日志内容。操作步骤如下：  
1. 打开 `BepInEx/config/BepInEx.cfg`  
2. 找到 `[Logging.Console]` 部分  
3. 设置 `Enabled = true`  

这将启动一个与游戏并行的控制台窗口，用于显示日志。现在，如果你将 `LogAudio` 设为 `true`，每当播放一个 `RandomAudioClipTable` 时，控制台中就会显示其名称，从而可以确定每个名称对应哪个声音。

---
### ⚠️ 重要提示  
单声道 .wav 文件似乎比立体声文件音量更小。如果你的音频片段听起来比预期要小，请考虑在音频编辑器（如 Audacity）中将其转换为立体声，通过选择导出选项中的立体声重新导出。
