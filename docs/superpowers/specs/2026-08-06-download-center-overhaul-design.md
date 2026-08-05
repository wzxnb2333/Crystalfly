# 下载中心大改造设计

日期：2026-08-06
状态：已获用户批准（方案 A：完整独立 VM + 构造注入）

## 背景与目标

下载队列逻辑当前耦合在 `MainViewModel.DownloadQueue.cs`（partial，501 行）与 `MainViewModel` 主文件（5158 行）中，是 God object 的一部分。UI 已具备单项重试、取消、速度/进度显示、错误显示、展开详情、入队 toast、自动重试，但缺少：批量操作、下载完成通知、ETA、历史管理与错误分类。同时，`MainViewModel` 需要按领域拆分为独立 ViewModel（见 `docs/adr/0001-mainviewmodel-domain-decomposition.md`），下载中心是第一个拆分样例，也是领域 VM 规范的模板。

**目标**：将下载中心拆为完整独立的 `DownloadCenterViewModel`，并补齐批量操作、进度通知、历史错误能力。

## 方案：完整独立 VM + 构造注入

### 1. 架构与组件

- **`DownloadCenterViewModel : ViewModelBase`**（新独立 VM）：
  - 持有 `DownloadQueueService`（由 MainViewModel 创建后通过依赖包注入）。
  - `DownloadQueueGroups`（`ObservableCollection<DownloadQueueGroupItemViewModel>`）与投影逻辑从 `MainViewModel.DownloadQueue.cs` 迁移。
  - 批量命令：`RetryAllCommand`、`PauseAllCommand`、`ResumeAllCommand`、`CancelAllCommand`、`ClearCompletedCommand`。
  - ETA 与总览：每任务 ETA（基于 `BytesPerSecond` 与剩余字节）、总进度、总速度、汇总 ETA、活动计数。
  - 完成通知：监听组状态跃迁 `Running→Completed`，仅本会话入队的任务弹 toast。
  - 错误分类：从 `Error` 与状态推断分类（离线/网络/校验/权限/其他）+ 复制命令。
- **`DownloadCenterDependencies`**（record，构造注入）：`DownloadQueueService`、catalog、networkPolicy、`Loc`、toast 回调、lifetime 取消令牌等。
- **`MainViewModel`**：构造时创建 `DownloadCenterViewModel`，暴露 `DownloadCenter` 属性；原下载相关命令/属性迁移，MainWindow 绑定相应更新。
- **`DownloadQueueGroupItemViewModel` 增强**：`EtaText`、`DurationText`（基于 `StartedAt`/`CompletedAt`）、`ErrorCategory`、复制错误命令。

### 2. 数据流

- **投影流**：`DownloadQueueService.Groups` 变化 → 投影调度（迁移现有 `QueueDownloadQueueProjection` 机制，500ms 轮询/变更驱动）→ 更新 `DownloadQueueGroups` → UI 刷新。
- **命令流**：UI 按钮 → VM 批量命令 → 服务调用（语义见下）。
- **通知流**：投影时检测组状态跃迁 `Running→Completed` → 仅当组是本会话入队的（VM 记录本会话 `EnqueueAsync` 的 GroupId 集合）→ `ToastRequested`。重启后恢复的旧任务完成时静默。

### 3. 批量操作语义

| 命令 | 语义 |
|------|------|
| `RetryAll` | 遍历所有 `Failed` 组 → 逐个 `RetryAsync` |
| `PauseAll` / `ResumeAll` | 调用服务层 `PauseSteamDownloadsAsync` / `ResumeSteamDownloadsAsync` |
| `CancelAll` | 遍历所有未完成组 → 逐个 `CancelAsync` |
| `ClearCompleted` | **给 `DownloadQueueService` 新增 `ClearCompletedAsync`**：从持久化 `download-queue.json` 移除已完成组 |

### 4. 进度与通知

- **ETA 剩余时间**：每任务基于 `BytesPerSecond` + 剩余字节计算；速度为 0 时不显示。
- **总览汇总**：活动计数、总速度（活动任务速度求和）、汇总 ETA。
- **下载完成通知**：仅本会话入队任务的 `Running→Completed` 跃迁触发 toast（本地化文案含任务名）。

### 5. 历史与错误

- **时间与耗时**：任务卡片显示完成/失败时间与耗时（`StartedAt`/`CompletedAt` 数据已存在，UI 未展示）。
- **错误分类与复制**：`ErrorCategory` 枚举（Offline/Network/Verification/Permission/Other）+ 本地化文案 + 一键复制错误信息到剪贴板。
- **列表组织**：活动优先、失败次之、已完成折叠置底（排序在投影时应用）。

### 6. UI 变更

`MainWindow.axaml` 下载区域：
1. 批量操作工具栏（全部重试/全部暂停·恢复/清除已完成，按状态启用/禁用）。
2. 总览条（活动计数 + 总速度 + 汇总 ETA）。
3. 任务卡片增强（ETA、时间/耗时、错误分类标签 + 复制按钮）。
4. 列表组织（活动优先、完成折叠置底）。
5. 绑定迁移：下载区域从 `#Root.DataContext.xxx` 改为 `DownloadCenter.xxx`。

## 测试策略

- **`DownloadCenterViewModel`**：投影更新、批量命令遍历语义、ETA 计算、完成通知（仅本会话任务；恢复的旧任务静默）、错误分类、ClearCompleted 调用。
- **`DownloadQueueService`**：新增 `ClearCompletedAsync` 的持久化/移除行为。
- **UI 结构测试**：`MainWindowStructureTests` 更新批量按钮与绑定断言。

## 不做的事（YAGNI）

- 不引入 DI 容器（保持手动 new，仅用依赖包 record 显式传递）。
- 不做断点续传、多线程下载、下载限速（超出当前范围）。
- 不做任务拖拽排序（列表组织仅按状态排序）。
- 完成通知不做聚合（每个完成单独通知，仅限本会话任务）。
