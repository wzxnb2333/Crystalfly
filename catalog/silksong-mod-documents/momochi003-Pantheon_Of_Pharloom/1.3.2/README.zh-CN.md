# 空洞骑士的神殿

一个允许你重新挑战首领并体验类似神居的首领连击挑战的模组。  
它还包含一些实用功能，例如可以直接通过模组的UI菜单切换你的指甲升级和某些能力。


**注意：请阅读更新日志以了解新版本的变更内容。**

如果遇到任何问题，最简单的方式是加入 [Hollow Knight Modding](https://discord.com/servers/hollow-knight-modding-879125729936298015) 失败服务器，并查找“空洞骑士的神殿”主题帖。

## 手动安装

1. 安装 [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#installing-bepinex-1)。  
2. 从 **Thunderstore** 下载 **Pharloom 的神殿**。  
3. 解压下载的文件。  
4. 将包含的 `.dll` 文件放入 `BepInEx/plugins` 文件夹中。  
    - **注意**：安装 BepInEx 后，你需要启动游戏一次，以便插件文件夹出现。  
    - 在 Linux 系统上，需要进行一些额外的设置，具体请参考 [here for Bepinix](https://docs.bepinex.dev/articles/user_guide/installation/index.html?tabs=tabid-nix#tabpanel_bHGHmlrG6S-2_tabid-nix)。

## 如何使用

- 加载存档后，按 **B** 键来开启或关闭首领冲刺菜单。  
- 在菜单中可以：
  - 选择并重新挑战单个首领  
  - 启动首领冲刺序列  
  - 调整指甲升级和能力  
  - 绑定指甲、生命值、丝线、技能和工具。  
    - 指甲绑定：减少指甲伤害由 40%。  
    - 生命值绑定：将生命值降低至仅剩 4 张面具。  
    - 丝线绑定：减少总丝线量至 9。（足以完成一次绑定）  
    - 技能绑定：禁止使用技能。  
    - 工具绑定：禁止使用工具。

## 重要说明

- 我修改了存档程序，在模组激活期间不会执行任何操作，以防止玩家意外损坏存档文件（因为首领战斗会设置大量标志/触发条件）。因此，目前在模组运行时将无法进行正常进度推进。
    - **注意：** 我仍强烈建议你备份自己的存档文件，以防万一。
- 为了获得最佳体验，请使用一个 **100% 存档文件**（或已经击败过所有首领的存档）。
  - 当前版本无法重新挑战尚未击败的首领，但无论是否已击败首领，首领激战功能仍然有效。
  - 如果手动下载模组压缩包，其中包含一个 100% 存档文件。

## 自定义首领挑战 [可选]

此模组允许你使用简单的文本文件来**自定义首领遭遇内容**。

### 文件夹结构

将你的文本文件放置在你的 `boss_list` 文件夹下的 BepInEx 插件目录中：

```
BepInEx/
├─ plugins/
│  ├─ PantheonOfPharloom.dll
│  └─ boss_list/
│     ├─ act1.txt
│     ├─ act2.txt
│     ├─ act3.txt
```

- `act1.txt` → 第一次首领挑战的首领。
- `act2.txt` → 第二次首领挑战的首领。
- `act3.txt` → 第三次首领挑战的首领。

* 最终的首领挑战将始终是前三个的合并。
* 所有这些文件都是可选的，如果未在该路径中找到，则模组会直接使用默认列表。这允许你仅更改某一次首领挑战的顺序，而其他部分保持默认。

### 首领挑战文件格式

- 每行一个首领
- 以 `#` 开头的行会被忽略（注释）
- 空行会被忽略
- **重要提示**：如果指定了无效的首领名称，或者在首领挑战列表末尾放置了长椅（bench），模组将立即崩溃。

**示例：**

```
# Custom Act 1 Bosses
Phantom
Bench
Widow
```

* 这将创建一个首领挑战序列：幽灵 → 长椅 → 妻子 → 结束。

### 首领列表 [提示警告]

#### I. 判官

```
Moss Mother
Skull Tyrant
Bell Beast
Savage Beastfly
Fourth Chorus
Fire Lace
Bench
Double Conchfly
Moorwing
Sister Splinter
Widow
Phantom
Last Judge
```

#### II. 有罪者

```
Garmond and Zaza
Cogwork Dancers
Trobbio
Chef Lugoli
Broodmother
Groal
Bench
Moss Mother 2
Single Conchfly
Zango
Voltvyrm
Savage Beastfly 2
Shakra
Bench
Father of Flame
Signis & Gron
Second Sentinel
Unravelled
Flower Lace
First Sinner
```

#### III. 空虚

```
Bell Eater
Crawfather
Gurr
Lost Garmond
Pinstress
Watcher
Tormented Trobbio
Bench
Palestag
Clover Dancer
Seth
Nyleth
Khann
Kramelita
Bench
Grandmother
Lost Lace
```

* 你必须在自定义列表中使用这些名称，且完全一致。`Bench` 是铃屋中的长椅场景。

## 截图与视频

[Quick Demo](https://youtu.be/ABDr8a4e1rc)

## 状态

计划新增功能：
- 更多用户界面优化与改进  
- 首领连击统计  
- 头部显示区域展示缚丝数量  
- 最终神殿中的虚空版本战斗

## 反馈

如果您遇到问题或错误，或者有建议，请告知我。
