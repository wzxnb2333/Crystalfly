# Radiance

## 概述  
`Radiance` 是一款《空洞骑士：丝之歌》的模组，会在 **BellTown** 的左上角区域新增一个挑战房间。  
从这个新入口，你可以挑战 **Radiance**（光辉）。  

所有模组内的视觉、音频及游戏玩法资源均源自《空洞骑士》。

### 功能特性  
- 增加一个用于挑战 Radiance 的新 BellTown 过渡点/房间入口。  
- 从嵌入的 AssetBundle 中加载 `GG_Radiance` 场景。  
- 处理挑战完成或退出后返回原始场景的流程。  
- 修复 PlayMaker FSM 及场景切换行为在运行时的兼容性问题。

### 代码概要  
- `RadiancePlugin.cs`  
  - 入口点。  
  - 在场景加载时注册 Harmony 补丁并创建持久化管理器。  
- `Source/Managers/AssetManager.cs`  
  - 预加载并管理嵌入的 `radiance.bundle`。  
  - 在 `LoadSceneMode.Single` 中加载 Radiance 场景。  
- `Source/Managers/RadianceSceneManager.cs`  
  - 核心流程控制器。  
  - 注入 BellTown 入口，追踪进入/退出状态，设置自定义场景行为，并在返回时恢复游戏状态。  
- `Source/Patches/*.cs`  
  - 截获场景切换以重定向 `GG_Radiance` 的加载。  
  - 在必要时强制使用自定义场景内存行为。  
  - 修复 FSM 事件处理器，并禁用自定义场景中与 Godfinder 图标不兼容的操作。  
- `Source/Behaviours/Common/RadianceReturnOnDialogueBehavior.cs`  
  - 捕获 Radiance FSM 对话状态，并安全地触发返回原始场景。

### 依赖项  
- `BepInEx-BepInExPack_Silksong` (`5.4.2304`)  

### Credits
- Asset sources: **Hollow Knight (HK1)**.

### License
This project is licensed under the **MIT License**. See `LICENSE` for details.

---

## 中文

### 模组简介
`Radiance` 是一个 空洞骑士：丝之歌 模组，在 **BellTown（钟心镇）左上角**新增了一个挑战入口房间。  
通过这个入口可以挑战 **Radiance（辐光）**。

模组内使用到的视觉、音频与玩法资源均来自 **《空洞骑士》一代**。

### 功能说明
- 在 BellTown 新增用于挑战 Radiance 的传送点/房间入口。  
- 通过内嵌 AssetBundle 加载 `GG_Radiance` 场景。  
- 挑战结束或退出后，自动返回原场景并恢复状态。  
- 在运行时补丁中处理 PlayMaker FSM 与场景切换兼容问题。

### 代码简述
- `RadiancePlugin.cs`  
  - 插件入口。  
  - 注册 Harmony 补丁，并在场景切换时创建常驻管理器。  
- `Source/Managers/AssetManager.cs`  
  - 负责预加载与管理内嵌 `radiance.bundle`。  
  - 以 `LoadSceneMode.Single` 加载 Radiance 场景。  
- `Source/Managers/RadianceSceneManager.cs`  
  - 核心流程管理器。  
  - 注入 BellTown 入口，维护进出自定义场景状态，处理自定义场景初始化与返回清理。  
- `Source/Patches/*.cs`  
  - 拦截场景切换并重定向 `GG_Radiance` 的加载流程。  
  - 必要时强制启用记忆场景行为。  
  - 修复 FSM 事件处理，并在自定义场景中禁用不兼容的 Godfinder 图标逻辑。  
- `Source/Behaviours/Common/RadianceReturnOnDialogueBehavior.cs`  
  - 挂接 Radiance FSM 对话状态，安全触发返回原场景。

### 依赖
- `BepInEx-BepInExPack_Silksong`（`5.4.2304`）

### 致谢
- 资源来源：**《空洞骑士》一代（HK1）**。

### 开源协议
本项目采用 **MIT License** 开源，详见 `LICENSE`。
