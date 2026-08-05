# VM 拆分设计

日期：2026-08-06
状态：已获用户批准（方案 A：规范先行 → 领域 VM → 后置瘦身）

## 背景与目标

`MainViewModel` 主文件 5158 行、`MainWindow.axaml.cs` 3032 行，是承载多领域的 God object（见 `docs/adr/0001-mainviewmodel-domain-decomposition.md`）。下载中心与模组管理已由独立方向拆分（各自 spec + plan）。本方向负责**其余领域**的拆分、MainWindow 后置瘦身，并产出**领域 VM 拆分规范**作为下载/模组 agent 的参照。

**目标**：拆出设置、实例管理、协议三个领域；MainWindow 动画与交互处理器瘦身；产出拆分规范文档与 `ViewModelBase` 基础设施。

## 方案：规范先行 → 领域 VM → 后置瘦身

### 1. 拆分基础设施（先行）

- **规范文档** `docs/domain-viewmodel-guidelines.md`：领域 VM 命名（`XxxViewModel` + `XxxDependencies`）、Dependencies record 模式、构造注入、导航接入（组合模式）、跨 VM 状态（MainViewModel 协调）、测试约定、拆分 checklist。
- **`ViewModelBase` 演进**：公共 `ToastRequested` 事件、`Loc` 注入辅助、状态属性基类。

### 2. 三个领域 VM（组合模式）

MainViewModel 构造时创建领域 VM（各配 `XxxDependencies` record），暴露为属性；页面切换仍用 `CurrentPage` + `IsVisible` 绑定，绑定迁移到领域 VM 属性。共享状态（`SelectedInstance`、catalog、networkPolicy、`lifetimeCancellation`）由 MainViewModel 持有，领域 VM 通过 Dependencies 获得；跨 VM 联动由 MainViewModel 协调（实例切换时通知相关 VM）。

| VM | 迁移内容 |
|----|----------|
| `SettingsViewModel` | 语言/主题/背景/仓库路由/自定义目录设置（MainViewModel 迁移 + 现有 `MainViewModel.Appearance.cs`） |
| `InstancesViewModel` | 实例列表/选择/创建/删除/克隆/重命名/速通实例（迁移 `MainViewModel.GameDirectories.cs` 与实例命令） |
| `ProtocolService` | `crystalfly://` 协议命令解析与分发（从 `MainViewModel.Protocol.cs` 迁出，作为服务） |

### 3. MainWindow 后置瘦身

- **`MotionCoordinator`**：入场/微交互/骑士行走动画逻辑（接收控件引用，自持 `RequestAnimationFrame` 帧驱动）；MainWindow 只调用接口（`AnimateEntrance(control)` / `ConfigureMicroInteractions(root)` 等）。
- **交互处理器分组**：下载/模组/速通/设置/实例区域的事件处理器抽到独立 partial（`MainWindow.DownloadHandlers.cs` 等）。

### 4. UI 变更

`MainWindow.axaml` 绑定迁移：设置页 → `Settings.*`，实例/版本页 → `Instances.*`，速通页实例相关 → `Instances.*`；页面结构与切换机制不变。`MainWindow.axaml.cs` 事件处理器按区域分组，动画逻辑转发给 `MotionCoordinator`。

## 测试策略

- **`SettingsViewModel`**：设置读写、主题/语言应用回调。
- **`InstancesViewModel`**：实例选择/创建/删除/克隆逻辑。
- **`ProtocolService`**：协议命令解析、参数校验、分发。
- **`MotionCoordinator`**：动画目标注册、帧驱动状态。
- **现有测试**：`MainWindowCodeBehindTests`/`MainWindowStructureTests` 绑定与结构断言更新。

## 不做的事（YAGNI）

- 不做导航服务重构（`CurrentPageViewModel` 注册表）——本次用组合模式，导航服务重构定义为规范中的"后续演进方向"。
- 不引入 DI 容器或共享状态服务单例。
- 不拆速通/预设/外观为独立 VM（已有 partial，留待后续迭代；`MainViewModel.Appearance.cs` 内容并入 `SettingsViewModel`）。
- 不重写动画逻辑，仅搬移（`RequestAnimationFrame` 驱动保持现状）。
