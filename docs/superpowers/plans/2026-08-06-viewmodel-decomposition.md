# VM 拆分 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 拆出 `SettingsViewModel`、`InstancesViewModel`、`ProtocolService` 三个领域单元，MainWindow 后置瘦身（`MotionCoordinator` + 交互分组），产出领域 VM 拆分规范文档与 `ViewModelBase` 基础设施。

**Architecture:** 组合模式（MainViewModel 持有领域 VM，`CurrentPage` + `IsVisible` 绑定不变），构造注入（`XxxDependencies` record），共享状态由 MainViewModel 协调。`MotionCoordinator` 承接动画逻辑，交互处理器按区域分组 partial。

**Tech Stack:** C# / .NET 10, Avalonia, CommunityToolkit.Mvvm, xUnit

## Global Constraints

- 组合模式：领域 VM 作为 MainViewModel 属性，页面切换机制（`CurrentPage` + `IsVisible`）不变。
- 共享状态（`SelectedInstance`、catalog、networkPolicy、`lifetimeCancellation`）由 MainViewModel 持有，经 Dependencies record 传递。
- 不引入 DI 容器、不引入共享状态单例、不做导航服务重构。
- 动画逻辑仅搬移不重写（`RequestAnimationFrame` 驱动保持）。
- 项目 warnings-as-errors：零警告。
- 测试用 xUnit；App 层测试沿用 `MainViewModelStateTests.cs` fixture。

---

### Task 1: 拆分规范文档 + ViewModelBase 基础设施

**Files:**
- Create: `docs/domain-viewmodel-guidelines.md`
- Modify: `src/Crystalfly.App/ViewModels/ViewModelBase.cs`

**Interfaces:**
- Produces: `ViewModelBase` 新增 `public event Action<string>? ToastRequested;`、`protected void NotifyToast(string message)`；规范文档定义领域 VM 模式。

**背景:** 规范是下载/模组 agent 的参照，先行产出。

- [ ] **Step 1: 写规范文档**

`docs/domain-viewmodel-guidelines.md` 内容：
- 命名：领域 VM 用 `XxxViewModel`，依赖包用 `XxxDependencies`（record）。
- 构造注入：`XxxDependencies` 只含该 VM 需要的依赖（Loc、toast 回调、工厂 Func、取消令牌等）。
- 导航接入：领域 VM 作为 MainViewModel 属性暴露，`CurrentPage` + `IsVisible` 绑定不变。
- 跨 VM 状态：共享状态由 MainViewModel 持有，经 Dependencies 传入；跨 VM 联动由 MainViewModel 协调。
- 测试约定：领域 VM 用依赖包构造，独立单测；迁移类改动须跑全量 App 测试。
- 拆分 checklist：确认依赖包字段 → 迁移成员 → MainViewModel 接线 → UI 绑定迁移 → 测试更新 → 全量测试。

- [ ] **Step 2: 扩展 ViewModelBase**

在 `ViewModelBase` 增加公共 `ToastRequested` 事件与 `NotifyToast` 辅助（下载/模组/设置 VM 共用）。

- [ ] **Step 3: 提交**

```bash
git add docs/domain-viewmodel-guidelines.md src/Crystalfly.App/ViewModels/ViewModelBase.cs
git commit -m "docs: define domain view model decomposition guidelines"
```

---

### Task 2: SettingsViewModel 拆分

**Files:**
- Create: `src/Crystalfly.App/ViewModels/SettingsViewModel.cs`
- Create: `src/Crystalfly.App/ViewModels/SettingsDependencies.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`（设置相关成员迁移）
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`（设置页绑定迁移）

**Interfaces:**
- Produces: `SettingsDependencies`（Loc、toast 回调、`ApplyTheme`/`ApplyLanguage` 回调、`Func<GameCatalog> Catalog`、`Action<CrystalflySettings>` 保存回调、`lifetimeCancellation`、paths）；`SettingsViewModel`：`SelectedLanguage`/`SelectedTheme`/`AccentColor`/`SelectedGitHubRoute`/`BackgroundScope` 等设置属性与命令（从 MainViewModel 与 `MainViewModel.Appearance.cs` 迁移）。

**背景:** 迁移设置相关成员。注意 `OnSelectedLanguageChanged`/`OnSelectedThemeChanged` 等 partial 回调中的 `settings = settings with {...}` 与 `QueueSettingsSave`——保存回调经 `SettingsDependencies` 传入（MainViewModel 提供）。

- [ ] **Step 1: 创建 SettingsDependencies + SettingsViewModel**

迁移：`SelectedLanguage`/`SelectedTheme`/`SetAccentColor`/`SelectedGitHubRoute`/`BackgroundScope` 相关、`MainViewModel.Appearance.cs` 的背景设置、设置选项集合（`RebuildSettingOptions`）。`settings` 写入经 `saveCallback`，`ApplyTheme`/`ApplyLanguage` 经回调。

- [ ] **Step 2: 运行确认编译失败**

Run: `dotnet build src/Crystalfly.App/Crystalfly.App.csproj -c Release`

Expected: FAIL。

- [ ] **Step 3: MainViewModel 接线 + UI 绑定迁移**

MainViewModel 创建 `Settings = new SettingsViewModel(dependencies)`；原设置成员删除或委托。MainWindow 设置页绑定改为 `Settings.*`。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS（现有设置相关测试更新引用后通过）。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/SettingsViewModel.cs src/Crystalfly.App/ViewModels/SettingsDependencies.cs src/Crystalfly.App/ViewModels/MainViewModel.cs src/Crystalfly.App/ViewModels/MainViewModel.Appearance.cs src/Crystalfly.App/Views/MainWindow.axaml
git commit -m "refactor: extract settings into a standalone view model"
```

---

### Task 3: InstancesViewModel 拆分

**Files:**
- Create: `src/Crystalfly.App/ViewModels/InstancesViewModel.cs`
- Create: `src/Crystalfly.App/ViewModels/InstancesDependencies.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`（实例成员迁移）
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`（实例/版本页绑定迁移）

**Interfaces:**
- Produces: `InstancesDependencies`（Loc、toast、实例服务工厂 Func、`SelectedInstance` 读写回调、`lifetimeCancellation`）；`InstancesViewModel`：`Instances`/`VisibleInstances`/`SpeedrunInstances`、`SelectedInstance`（经回调与 MainViewModel 同步）、创建/删除/克隆/重命名命令（从 `MainViewModel.GameDirectories.cs` 与主文件迁移）。

**背景:** 迁移实例相关成员（集合 202-210 行、`SelectInstanceForLaunchCommand`、创建/删除/克隆/重命名命令、游戏目录发现）。`SelectedInstance` 是全局共享状态——经 `InstancesDependencies` 的读写回调与 MainViewModel 同步（MainViewModel 协调模式）。

- [ ] **Step 1: 创建 InstancesDependencies + InstancesViewModel**

迁移实例集合、选择、创建/删除/克隆/重命名命令、游戏目录注册/发现（`MainViewModel.GameDirectories.cs`）。`SelectedInstance` 经回调读写。

- [ ] **Step 2: 运行确认编译失败**

Run: `dotnet build src/Crystalfly.App/Crystalfly.App.csproj -c Release`

Expected: FAIL。

- [ ] **Step 3: MainViewModel 接线 + UI 绑定迁移**

MainViewModel 创建 `Instances = new InstancesViewModel(dependencies)`；`SelectedInstance` 属性保留在 MainViewModel（全局共享），经回调同步。MainWindow 实例/版本页绑定改为 `Instances.*`（`SelectedInstance` 相关绑定保留 `#Root.DataContext`）。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS（实例相关测试更新后通过——包括此前的实例记忆测试）。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/InstancesViewModel.cs src/Crystalfly.App/ViewModels/InstancesDependencies.cs src/Crystalfly.App/ViewModels/MainViewModel.cs src/Crystalfly.App/ViewModels/MainViewModel.GameDirectories.cs src/Crystalfly.App/Views/MainWindow.axaml
git commit -m "refactor: extract instance management into a standalone view model"
```

---

### Task 4: ProtocolService 拆分

**Files:**
- Create: `src/Crystalfly.App/Services/ProtocolService.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`（协议成员迁移）
- Test: `tests/Crystalfly.App.Tests/ViewModels/ProtocolServiceTests.cs`（新建）

**Interfaces:**
- Produces: `public sealed class ProtocolService`：`Parse(string input)` 返回协议命令结果、`ExecuteAsync(ProtocolCommand, ...)`；命令集合与参数校验从 `MainViewModel.Protocol.cs` 迁移。

**背景:** `crystalfly://` 协议处理从 VM 迁出为服务，便于独立测试。

- [ ] **Step 1: 写失败测试**

`ProtocolServiceTests`：合法命令解析、非法协议拒绝、参数校验（参照 `MainViewModel.Protocol.cs` 现有行为）。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ProtocolServiceTests"`

Expected: FAIL（类型不存在）。

- [ ] **Step 3: 实现 + 接线**

创建 `ProtocolService`，迁移 `MainViewModel.Protocol.cs` 的解析/校验逻辑；MainViewModel 创建服务并转发（`OnProtocolActivated` 等入口）。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/Services/ProtocolService.cs src/Crystalfly.App/ViewModels/MainViewModel.cs src/Crystalfly.App/ViewModels/MainViewModel.Protocol.cs tests/Crystalfly.App.Tests/ViewModels/ProtocolServiceTests.cs
git commit -m "refactor: extract protocol handling into a service"
```

---

### Task 5: MotionCoordinator 动画抽取

**Files:**
- Create: `src/Crystalfly.App/Services/MotionCoordinator.cs`
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml.cs`（动画逻辑迁移）

**Interfaces:**
- Produces: `public sealed class MotionCoordinator`：`RegisterEntranceTarget(control)`、`AnimateEntrance(control)`、`ConfigureMicroInteractions(root)`、`StartKnightWalk(...)`、`Shutdown()`（内部自持 `RequestAnimationFrame` 驱动与 `DispatcherTimer`）。

**背景:** 从 `MainWindow.axaml.cs`（3032 行）迁移动画逻辑（入场动画 680-830 行、微交互 355-480 行、骑士行走 170-260 行、动画字段 42-54 行）。注意 `IsFullMotionEnabled`/`AreClientAreaAnimationsEnabled` 依赖——作为 `Func<bool>` 注入。

- [ ] **Step 1: 创建 MotionCoordinator 骨架**

迁移动画字段、`EnsureEntranceAnimationTimer`/`OnEntranceAnimationFrame`、`ConfigureMicroInteractionTransitions`、`StartKnightWalkAnimation` 等（`IsMotionEnabled` 经 `Func<bool>` 注入）。MainWindow 保留事件转发（`OnOpened` 调用 `coordinator.Start()`）。

- [ ] **Step 2: 运行确认编译通过**

Run: `dotnet build src/Crystalfly.App/Crystalfly.App.csproj -c Release`

Expected: PASS（若 FAIL 说明有遗漏引用，补齐）。

- [ ] **Step 3: 运行现有测试确认无回归**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS（`ThemeRenderingTests`、`MainWindowStructureTests` 等含动画相关断言——若断言了具体方法名，更新引用）。

- [ ] **Step 4: 提交**

```bash
git add src/Crystalfly.App/Services/MotionCoordinator.cs src/Crystalfly.App/Views/MainWindow.axaml.cs
git commit -m "refactor: extract motion logic into a coordinator service"
```

---

### Task 6: 交互处理器分组 + 集成清理

**Files:**
- Create: `src/Crystalfly.App/Views/MainWindow.DownloadHandlers.cs`（示例：按区域分组的 partial）
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml.cs`
- Modify: `tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs` / `MainWindowCodeBehindTests.cs`

**Interfaces:**
- Consumes: Task 2-5 的领域 VM 与 `MotionCoordinator`。

**背景:** 事件处理器按区域分组为 partial；验证 MainViewModel/MainWindow 瘦身效果。

- [ ] **Step 1: 分组事件处理器**

按区域（下载/模组/速通/设置/实例）把 `MainWindow.axaml.cs` 的 `Click`/`KeyDown`/`PointerPressed` 处理器移到对应 partial 文件（首建 `MainWindow.DownloadHandlers.cs` 示范，其余同模式）。partial 方法可互相调用（同一类）。

- [ ] **Step 2: 更新结构测试**

`MainWindowStructureTests`/`MainWindowCodeBehindTests` 中涉及具体处理器方法名的断言更新为分组后位置。

- [ ] **Step 3: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS。

- [ ] **Step 4: 提交**

```bash
git add src/Crystalfly.App/Views/MainWindow.DownloadHandlers.cs src/Crystalfly.App/Views/MainWindow.axaml.cs tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs tests/Crystalfly.App.Tests/Ui/MainWindowCodeBehindTests.cs
git commit -m "refactor: group window event handlers by area"
```

---

## 完成后验证

- 全量测试：`dotnet test .\Crystalfly.slnx -c Release`（全绿，零警告）。
- 手动验收：切页/改设置/实例操作/页面动画均正常；协议命令（`crystalfly://`）可用。
- 瘦身验证：`MainViewModel.cs` 主文件行数与 `MainWindow.axaml.cs` 行数显著下降（记录前后行数）。
