# 领域 ViewModel 拆分规范

> 本文档是下载中心 / 模组管理 / 设置与实例等方向拆分领域 ViewModel 时的共同参照。配套决策见 `docs/adr/0001-mainviewmodel-domain-decomposition.md`，设计说明见 `docs/superpowers/specs/2026-08-06-viewmodel-decomposition-design.md`。

## 目标

`MainViewModel`（主文件 5158 行 + 8 个 partial）承载了下载、模组、设置、实例、速通、协议、更新等多领域职责。按业务领域拆出独立 ViewModel 与服务，使每个领域单元可独立理解与测试，并让 `MainWindow.axaml.cs` 后置代码瘦身。

## 命名

- 领域 ViewModel 用 `XxxViewModel` 命名（如 `SettingsViewModel`、`InstancesViewModel`）。
- 依赖包用 `XxxDependencies` 命名，声明为 `internal sealed record`，放在与 VM 相同的 `Crystalfly.App.ViewModels` 命名空间。
- 无状态领域服务用 `XxxService` 命名（如 `ProtocolService`、`MotionCoordinator`），放在 `Crystalfly.App.Services`。

## 构造注入：`XxxDependencies` record

领域 VM 不在内部 new 依赖，而是通过构造函数注入一个 `XxxDependencies` record：

- record 只包含该 VM 需要的依赖：`Func<LocalizationViewModel>`（Loc 访问器）、toast 回调、工厂 `Func`、取消令牌（`lifetimeCancellation`）等。
- 共享状态（`SelectedInstance`、catalog、networkPolicy、`CrystalflySettings` 等）留在 MainViewModel，经 record 传入**读写回调**（`Func<T>` 读、`Action<T>` 写）。
- 跨 VM 联动（如语言切换后需要重建市场目录）由 MainViewModel 在构造 record 时用闭包接线，领域 VM 不反向引用 MainViewModel。
- 不要引入 DI 容器，不要做共享状态单例。

## 导航接入：组合模式

- 领域 VM 作为 MainViewModel 的公开属性暴露（如 `MainViewModel.Settings`、`MainViewModel.Instances`）。
- 页面切换机制（`CurrentPage` + `IsVisible` 绑定）不变。
- XAML 绑定迁移到领域 VM 属性（`{Binding Settings.SelectedTheme}`、`{Binding Instances.VisibleInstances}`）；全局共享状态（`SelectedInstance`）相关绑定保留在 `#Root.DataContext`。
- MainViewModel 保留测试与旧入口需要的**委托外观**（facade）：属性转发到领域 VM，并订阅领域 VM 的 `PropertyChanged` 向自身转发，保证旧绑定/测试仍收到通知。

## 跨 VM 状态

- 共享状态由 MainViewModel 持有，经 Dependencies 传入；领域 VM 不持有共享状态的副本。
- 跨 VM 联动由 MainViewModel 协调：MainViewModel 订阅领域 VM 的事件（如 toast）或提供回调（如语言变更后的目录重建）。
- 动画逻辑仅搬移不重写（`RequestAnimationFrame` 驱动保持）。

## 测试约定

- 领域 VM 用依赖包构造，可独立单测。
- 迁移类改动（搬移成员）以现有测试为基线：迁移后全量 `dotnet test tests/Crystalfly.App.Tests -c Release` 必须通过。
- 迁移时优先保留 MainViewModel 的委托外观，避免大规模改写既有测试；仅当测试断言了具体实现位置（如反射私有方法、代码文件内容）时才更新测试。
- 新增服务（如 `ProtocolService`）用独立测试文件覆盖解析、校验、门控逻辑。

## 拆分 checklist

1. 确认依赖包字段：列出目标成员用到的所有外部状态，逐项确定读/写回调。
2. 迁移成员：把成员整体搬入领域 VM，`[ObservableProperty]` 的 partial 变更回调一并迁移。
3. MainViewModel 接线：构造函数创建领域 VM 并装配 `XxxDependencies`；原成员删除或委托。
4. UI 绑定迁移：MainWindow.axaml 中相关绑定加 `Xxx.` 前缀；`SelectedInstance` 相关绑定保留 DataContext。
5. 测试更新：仅更新断言具体实现位置或文件内容的测试。
6. 全量测试：`dotnet test tests/Crystalfly.App.Tests -c Release` 全绿、零警告。
7. 提交：Conventional Commits（`refactor: extract ... into a standalone view model`）。
