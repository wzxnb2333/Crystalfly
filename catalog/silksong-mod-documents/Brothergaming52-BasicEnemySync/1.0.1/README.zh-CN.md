<div align="center">

# ✦ SSMP 敌人同步插件 ✦

### 针对空洞骑士：丝之歌多人模式（SSMP）的全面敌人、首领及复活同步功能

[![Version](https://img.shields.io/badge/version-1.0.1-blueviolet?style=for-the-badge)](https://github.com/Brothergaming52/EnemySyncing/releases)  
[![Game](https://img.shields.io/badge/game-Hollow%20Knight:%20Silksong-red?style=for-the-badge)](https://www.teamcherry.com.au/)  
[![SSMP](https://img.shields.io/badge/SSMP-Silksong%20Multiplayer-orange?style=for-the-badge&logo=github)](https://github.com/Extremelyd1/SSMP)  
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Me-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/brothergaming52)

**实时敌人同步 · 首领阶段同步 · 倒下茧系统 · 队友复活**

---

</div>

## 🧩 什么是 SSMP 敌人同步插件？

**SSMP 敌人同步插件** 是为 **[Hollow Knight: Silksong Multiplayer (SSMP)](https://github.com/Extremelyd1/SSMP)** 开发的扩展模组。它实现了整个 Pharloom 地图中所有标准敌人、小首领以及复杂多阶段首领的完整实时多人同步。

除了敌人同步功能外，该插件还引入了 **合作模式倒下与茧中复活系统**，让队友在战斗中可以互相救援！

> [!WARNING]
> ### ⚠️ 开发中说明与免责声明
> **本模组仍处于积极开发阶段，尚不完美。** 在多人对战过程中，您可能会遇到不同步、卡顿或 **破坏游戏体验的严重 Bug**。
> 
> 请提前备份您的存档文件，将问题反馈至 [GitHub Issues](https://github.com/Brothergaming52/EnemySyncing/issues) 页面，并分享您的使用体验！

## ✨ v1.0.1 新增内容

- 🔌 **原生 BepInEx 插件集成：** 增加了 `EnemySyncingPlugin` 入口点（`bg52.enemysync.ssmp`），使模组可直接由 BepInEx 加载，并通过 Thunderstore / r2modman 简洁管理。
- 🎯 **首领仇恨与目标 HUD 提示：** 当首领在战斗中锁定玩家时，该玩家的名称标签会动态变为红色，为队友提供即时战术反馈。
- 💀 **尸体快照与死亡同步：** 敌人死亡时，现在会同步所有客户端的击退速度、位置、旋转角度和攻击角度。
- 🛡️ **场景拥有者死亡验证：** 实现了权威性死亡验证（`VerifyEnemyDieRequest`），以防止客户端脱同步和出现重复的虚幻敌人。
- 🏹 **增强的弹道同步：** 支持弹道动画片段状态、自定义缩放（`ScaleX`）以及更精确的轨迹追踪。
- 🧠 **PlayMaker FSM 目标补丁：** 重写了 FSM 中的距离与位置计算（`GetDistance`, `GetXDistance`, `GetYDistance`, `GetPosition`），确保敌人始终朝向其当前的网络目标移动。
- 🔄 **防重复与脱同步恢复：** 背景监控程序会持续检测并恢复丢失的场景敌人，同时清理过期或已死亡的实体。

## ⚡ 核心功能

### ⚔️ 完整的网络同步

| 功能 | 描述 |
|---|---|
| **位置与动画同步** | 对敌人位置、旋转、缩放以及激活的动画状态进行平滑插值 |
| **伤害与生命值** | 同步生命值条、伤害追踪、场景拥有者死亡验证以及远程死亡触发 |
| **弹幕同步** | 同步敌方弹幕、法术攻击、自定义缩放以及生成的子对象 |
| **FSM与首领阶段同步** | 同步PlayMaker有限状态机（FSMs）及多阶段首领战斗中的子状态变更 |

### 👑 动态权限与占位系统

- **邻近性与视野占位**：距离最近且正在观察敌人的玩家将自动获得控制该敌人AI与移动的权限。
- **首领目标高亮**：当前被活跃首领锁定的玩家将显示红色用户名标签进行高亮。
- **主机回退机制**：未被占位或距离较远的敌人将自动回退至主机端模拟，以消除不同步问题。
- **缺失敌人的自动生成**：为新加入的玩家自动实例化缺失的场景预制体。

### 🕸️ 倒下与茧复活系统

- **多人倒下状态**：受到致命伤害时，玩家不会立即死亡，而是进入保护性的**茧状态**。
- **队友救援复活**：附近的队友可使用武器击打倒下的玩家的茧，将其击破并将其复活。
- **危险自动落地**：落入尖刺或环境危险的茧将自动落到最近的安全地面。
- **按住自杀操作**：被困或孤立的玩家可按住其**快速地图键**（默认：`TAB` / 控制器对应键）以主动投降并重新生成。
- **团队清空逻辑**：若当前场景中所有玩家同时进入茧状态，模组将触发同步的原生团队失败机制。

## 🎮 它是如何工作的

```
1. Take Fatal Damage  →  Player enters Cocoon State (Invulnerable to hazards)
2. Teammate Hits      →  Striking the cocoon reduces HP & breaks player free!
3. Team Wipe Check    →  If everyone is downed → Sync'd team defeat & respawn
```

---

## 📥 安装指南

### 通过 Thunderstore / r2modman（推荐）
1. 通过 **Thunderstore 模组管理器** 或 **r2modman** 直接安装 **SSMP 敌人同步插件**。
2. 通过模组管理器启动游戏。

### 手动安装
1. **前置条件：**
   - **空洞骑士：丝之歌**
   - **[BepInExPack for Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)**
   - **[SSMP (Silksong Multiplayer)](https://github.com/Extremelyd1/SSMP)**
2. **安装插件：**
   - 将 `EnemySyncing.dll` 提取至你的 BepInEx 插件目录：
     `path\to\Hollow Knight Silksong\BepInEx\plugins\EnemySyncing\`（或放入 `BepInEx\plugins\SSMP\` 内部）。
3. **运行游戏：**
   - 启动丝之歌。BepInEx 和 SSMP 将自动加载 `EnemySyncing.dll`。
   - **注意：** 主机与加入的客户端都必须安装 `EnemySyncing`。

## ⚙️ 键位与操作

| 操作 | 控制方式 |
|---|---|
| **复活队友** | 对远程玩家的茧使用近战攻击（`Nail / Needle`） |
| **茧中自尽 / 放弃** | 在茧中按住**快速地图键**（默认：`TAB` 或控制器按钮） |

## 🛠️ 从源码构建

### 要求
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) 或更高版本
- Visual Studio 2022 / JetBrains Rider / VS Code

### 步骤

```bash
# 1. Clone the repository
git clone https://github.com/Brothergaming52/EnemySyncing.git
cd EnemySyncing

# 2. Build the solution
dotnet build EnemySyncing.csproj -c Release
```

编译后的二进制文件将被放置在 `bin/Release/netstandard2.1/EnemySyncing.dll` 中。

## 🙌 Credits & Acknowledgements

- **Brothergaming52** – Creator & Lead Developer
- **FieryCGamingz** – Addon Tester & Quality Assurance

---

## ☕ 支持该项目

<div align="center">

如果这个模组提升了你的丝之歌合作游戏体验，欢迎支持开发与测试团队！

[![Brothergaming52 Ko-fi](https://img.shields.io/badge/Brothergaming52-Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/brothergaming52)
[![FieryCGamingz Ko-fi](https://img.shields.io/badge/FieryCGamingz%20(Tester)-Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/fierycgamingz)

**[ko-fi.com/brothergaming52](https://ko-fi.com/brothergaming52)** · **[ko-fi.com/fierycgamingz](https://ko-fi.com/fierycgamingz)**

</div>

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).

---

<div align="center">

**由 Brothergaming52 用心打造**

[SSMP Project](https://github.com/Extremelyd1/SSMP) · [Ko-fi](https://ko-fi.com/brothergaming52) · [GitHub Repo](https://github.com/Brothergaming52/EnemySyncing)

</div>
