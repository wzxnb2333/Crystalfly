# AnySilkBoss

https://github.com/xieedechunniunai/AnySilkBoss

[English](#english) | [中文](#中文)

---

## 中文

一个用于丝之歌的 BepInEx 模组，增强了 SilkBoss 的行为并新增了记忆模式。

### 特性

#### 双模式系统
- **正常模式**：基于原版首领行为优化的版本
- **记忆模式**：一种全新设计的高难度首领战斗体验，包含更复杂的攻击模式和阶段转换

#### 核心特性
- 自定义首领攻击控制（轨道、攀爬、BlastBurst、P6网等）
- 阶段控制系统
- 丝线球系统（SilkBall / BigSilkBall）
- 单次网状攻击
- 伤害减免系统
- 伤害叠加机制
- 自定义音频替换
- 平台状态重置
- 多语言支持（I18n）

---

### ⚡ 如何进入记忆模式 ⚡

> **这是本模组的核心功能！**

| 步骤 | 操作 |
|------|--------|
| 1 | 使用一个 **执行 3** 的存档文件 |
| 2 | 进入 `Cradle_03_Destroyed` 房间 |
| 3 | 持续按住 **深海挽歌**（向下 + Needolin）持续 **3 秒钟** |
| 4 | 你将被传送至记忆模式的首领战斗 |

```
Controls: ↓ + Needolin (Hold for 3 seconds)
```

---

### 安装

#### 要求
- 丝之歌
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx)

#### 步骤
1. 确保 BepInEx 已正确安装
2. 下载最新的 `AnySilkBoss.zip`
3. 解压至 `Silksong/BepInEx/plugins/` 目录

### ⚠️ Disclaimer & License
- 本模组是 **完全免费** 的，且不会出售
- 所有源代码均为 **开源**
- The music used is from "Intro". If there is any copyright infringement, please contact us and it will be removed immediately
- **禁止转售、商业用途及未经授权的分发**
- **未经许可不得移植到其他平台**

For full license details, see [LICENSE.md](LICENSE.md)
---

## 中文

一个用于 丝之歌（空洞骑士：丝之歌）的 BepInEx MOD，提供 SilkBoss 行为增强与记忆模式。

### 功能特性

#### 双模式系统
- **普通模式 (Normal)**：基于原版 Boss 行为的增强版本
- **记忆模式 (Memory)**：全新设计的高难度 Boss 战体验，包含更复杂的攻击模式和阶段转换

#### 核心功能
- 自定义 Boss 攻击控制（轨道、攀爬、BlastBurst、P6Web 等）
- 阶段控制系统（PhaseControl）
- 丝球系统（SilkBall / BigSilkBall）
- 单根丝线攻击（SingleWeb）
- 减伤系统（DamageReduction）
- 伤害叠加机制（DamageStack）
- 自定义音频替换
- 平台状态重置
- 多语言支持（I18n）

---

### ⚡ 如何进入记忆模式 ⚡

> **这是本 MOD 的重要玩法！**

| 步骤 | 操作 |
|------|------|
| 1 | 使用**第三幕**的存档 |
| 2 | 进入 `Cradle_03_Destroyed` 房间 |
| 3 | 按住**深渊挽歌**（↓ + 弹琴）持续 **3 秒** |
| 4 | 即可传送至记忆模式 Boss 战 |

```
操作方式：↓ + 弹琴（持续按住 3 秒）
```

---

### 安装

#### 前置要求
- 丝之歌 游戏本体
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx)

#### 安装步骤
1. 确保已正确安装 BepInEx
2. 下载最新版本的 `AnySilkBoss.zip`
3. 解压到 `Silksong/BepInEx/plugins/` 目录

### ⚠️ 免责声明与许可

- 本 MOD **完全免费**，不会以任何形式收费
- 源码**全部开源**
- 所使用音乐取自《Intro》，若有侵权敬请告知，将立即删除
- **严禁倒卖、商业用途、未经授权的转载分发**
- **未经许可禁止移植到其他平台**

完整许可证详见 [LICENSE.md](LICENSE.md)

---

## 开发 / 开发

### 构建要求 / 构建要求
- .NET Standard 2.1
- Visual Studio 2022 或 Rider

### 构建步骤 / 构建步骤
1. 克隆仓库 / 克隆仓库
2. 创建 `SilksongPath.props` 文件 / 创建 `SilksongPath.props` 文件：
   ```xml
   <Project>
     <PropertyGroup>
       <SilksongFolder>Your game installation path / 你的游戏安装路径</SilksongFolder>
     </PropertyGroup>
   </Project>
   ```
3. 运行 `dotnet build`

### 项目结构 / 项目结构
```
Source/
├── Actions/        # Custom PlayMaker Actions / 自定义 PlayMaker Actions
├── Behaviours/     # Boss behavior logic / Boss 行为逻辑
│   ├── Common/     # Common behaviors / 通用行为
│   ├── Memory/     # Memory mode behaviors / 记忆模式行为
│   └── Normal/     # Normal mode behaviors / 普通模式行为
├── Handlers/       # Event handlers / 事件处理器
├── Managers/       # Various managers / 各类管理器
├── Patches/        # Harmony patches / Harmony 补丁
└── Tools/          # Utilities (FSM analyzer, state builder, etc.) / 工具类
```
