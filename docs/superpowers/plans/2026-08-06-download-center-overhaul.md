# 下载中心大改造 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把下载队列从 `MainViewModel.DownloadQueue.cs`（partial）拆成完整独立的 `DownloadCenterViewModel`，并补齐批量操作、ETA/总览、完成通知、错误分类、时间/耗时与列表组织。

**Architecture:** `DownloadCenterViewModel : ViewModelBase` 通过 `DownloadCenterDependencies` 构造注入依赖，持有 `DownloadQueueService` 与投影逻辑；MainViewModel 构造时创建并暴露 `DownloadCenter` 属性；UI 绑定迁移到 `DownloadCenter.xxx`。`DownloadQueueService` 新增 `ClearCompletedAsync`。

**Tech Stack:** C# / .NET 10, Avalonia, CommunityToolkit.Mvvm, xUnit

## Global Constraints

- 依赖传递用构造注入（`DownloadCenterDependencies` record），不引入 DI 容器。
- 完成通知仅限本会话入队的任务（VM 记录本会话 `EnqueueAsync` 的 GroupId 集合）；重启后恢复的旧任务完成时静默。
- ETA 基于 `BytesPerSecond` 与剩余字节计算；速度为 0 时不显示。
- 列表组织：活动优先、失败次之、已完成折叠置底（在投影排序时应用）。
- 项目 warnings-as-errors：零警告。
- 测试用 xUnit；App 层测试沿用 `MainViewModelStateTests.cs` 的 fixture（`TestDirectory`、`NetworkPolicy` 等）。
- 风格：4 空格缩进、file-scoped namespace、nullable reference types。

---

### Task 1: DownloadQueueService 新增 ClearCompletedAsync

**Files:**
- Modify: `src/Crystalfly.App/Downloads/DownloadQueueService.cs`
- Test: 新增 `tests/Crystalfly.App.Tests/Downloads/DownloadQueueServiceTests.cs`（若不存在则新建）

**Interfaces:**
- Produces: `public async Task ClearCompletedAsync(CancellationToken cancellationToken = default)` —— 从 `Groups` 与持久化 `download-queue.json` 中移除全部 `DownloadQueueGroupState.Completed` 的组，并触发变更通知。

**背景:** 批量操作"清除已完成"需要服务层支持从持久化移除（仅 UI 隐藏会在重启后复现）。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public async Task ClearCompletedAsync_removes_completed_groups_and_persists()
{
    // 用 TestDirectory 创建临时 app-data，构造 DownloadQueueService（executor 用简单的假 executor）
    // 入队 3 个组，手动把其中 2 个标记为 Completed、1 个 Pending
    // 调用 ClearCompletedAsync，断言 Groups 只剩 1 个 Pending 组
    // 重新 InitializeAsync，断言持久化后仍只剩 1 个组
}
```

（测试需要 `IDownloadQueueExecutor` 假实现——若已有测试 fixture 复用，否则新建 `FakeQueueExecutor : IDownloadQueueExecutor`，`TransferAsync` 直接完成、`IsTransient` 返回 false。）

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ClearCompletedAsync"`

Expected: FAIL（方法不存在，编译错误）。

- [ ] **Step 3: 实现**

在 `DownloadQueueService.cs` 中参照现有 `RetryAsync`/`CancelAsync` 的模式新增：

```csharp
public async Task ClearCompletedAsync(CancellationToken cancellationToken = default)
{
    // 加锁遍历 Groups，收集 State == Completed 的 Id
    // 从内存集合移除，调用现有持久化路径（参照 RetryAsync 末尾的保存逻辑）
    // 触发变更通知（参照现有 OnGroupsChanged / 通知机制）
}
```

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ClearCompletedAsync"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/Downloads/DownloadQueueService.cs tests/Crystalfly.App.Tests/Downloads/DownloadQueueServiceTests.cs
git commit -m "feat: clear completed download queue groups from persistence"
```

---

### Task 2: DownloadCenterViewModel 拆分（核心迁移）

**Files:**
- Create: `src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs`
- Create: `src/Crystalfly.App/ViewModels/DownloadCenterDependencies.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`（创建 + 暴露 `DownloadCenter`）
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`（下载区域绑定迁移）
- Delete: `src/Crystalfly.App/ViewModels/MainViewModel.DownloadQueue.cs`（内容迁走后删除）

**Interfaces:**
- Produces:
  - `public sealed record DownloadCenterDependencies(DownloadQueueService DownloadQueue, LocalizationViewModel Loc, Action<string>? ToastRequested, Func<bool> IsGameProcessRunning, CancellationToken LifetimeCancellation, Func<GameCatalog> Catalog, INetworkPolicy NetworkPolicy, Paths paths)` —— 按实际需要调整字段（`CreateDownloadQueue` 中用的依赖都要传入）。
  - `public sealed class DownloadCenterViewModel : ViewModelBase`：`ObservableCollection<DownloadQueueGroupItemViewModel> DownloadQueueGroups`、`ActiveDownloadCount`、`HasActiveDownloads`、`HasUnfinishedDownloads`、`ActiveDownloadSummary`、`DownloadQueue`（internal）、投影机制（迁移 `QueueDownloadQueueProjection`/`ApplyPendingDownloadQueueProjection`/`ScheduleRefreshAfterQueueMutation`）、命令 `CancelQueuedDownloadCommand`、`RetryQueuedDownloadCommand`、入队方法（`EnqueueMarketModAsync` 等从 MainViewModel 迁入或保留为公共方法）。
  - `MainViewModel.DownloadCenter`（`DownloadCenterViewModel` 属性）。

**背景:** 把 `MainViewModel.DownloadQueue.cs` 的队列集合、投影、单项命令迁入新 VM。入队命令（`EnqueueSelectedMarketModAsync` 等）依赖市场/实例状态，**保留在 MainViewModel**，改为调用 `DownloadCenter` 的入队 API（如 `DownloadCenter.EnqueueAsync(DownloadQueueGroup)`）。

- [ ] **Step 1: 创建 DownloadCenterDependencies + DownloadCenterViewModel 骨架**

新建两个文件，把 `MainViewModel.DownloadQueue.cs` 中的：字段（`downloadQueue`、投影相关）、`DownloadQueueGroups`、状态属性（ActiveDownloadCount 等）、`QueueDownloadQueueProjection`/`ApplyPendingDownloadQueueProjection`/`ScheduleRefreshAfterQueueMutation`/`RefreshAfterQueueMutationAsync`、`CancelQueuedDownloadAsync`/`RetryQueuedDownloadAsync` 迁入。`Loc` 引用改为 `dependencies.Loc`，`ToastRequested` 改为 `dependencies.ToastRequested`，`lifetimeCancellation` 改为 `dependencies.LifetimeCancellation`。

- [ ] **Step 2: 运行确认编译失败（MainViewModel 引用断裂）**

Run: `dotnet build src/Crystalfly.App/Crystalfly.App.csproj -c Release`

Expected: FAIL（MainViewModel 引用旧成员）。

- [ ] **Step 3: MainViewModel 接线**

在 `MainViewModel` 构造函数中：保留 `CreateDownloadQueue()` 创建 `DownloadQueueService`，随后 `DownloadCenter = new DownloadCenterViewModel(dependencies)`；暴露 `public DownloadCenterViewModel DownloadCenter { get; }`。原 `MainViewModel.DownloadQueue.cs` 中未迁走的入队命令改为调用 `DownloadCenter` 对应方法；`OnDownloadQueueChanged` 等事件订阅改为订阅到 `DownloadCenter` 的内部事件（或由 DownloadCenter 直接订阅服务）。删除 `MainViewModel.DownloadQueue.cs`。

- [ ] **Step 4: MainWindow.axaml 绑定迁移**

下载区域（约 2144-2260 行）所有 `#Root.DataContext.xxx` 的下载绑定改为 `#Root.DataContext.DownloadCenter.xxx`（如 `RetryQueuedDownloadCommand`、`CancelQueuedDownloadCommand`、`DownloadQueueGroups`）。

- [ ] **Step 5: 运行确认编译通过 + 现有测试**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS（现有下载相关测试——`MainViewModelStateTests` 中涉及下载的用例——更新绑定/成员引用后通过）。

- [ ] **Step 6: 提交**

```bash
git add src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs src/Crystalfly.App/ViewModels/DownloadCenterDependencies.cs src/Crystalfly.App/ViewModels/MainViewModel.cs src/Crystalfly.App/ViewModels/MainViewModel.DownloadQueue.cs src/Crystalfly.App/Views/MainWindow.axaml
git commit -m "refactor: extract download center into a standalone view model"
```

---

### Task 3: 批量操作命令

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs`
- Test: `tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs`（新建）

**Interfaces:**
- Produces: `RetryAllCommand`、`PauseAllCommand`、`ResumeAllCommand`、`CancelAllCommand`、`ClearCompletedCommand`（`[RelayCommand]`）；`CanRetryAll`/`CanPauseAll`/`CanResumeAll`/`CanCancelAll`/`CanClearCompleted` 状态属性（随投影刷新）。

**背景:** 批量操作语义已确认：RetryAll 遍历 Failed 组、PauseAll/ResumeAll 调服务全局暂停恢复、CancelAll 遍历未完成组、ClearCompleted 调服务 `ClearCompletedAsync`。

- [ ] **Step 1: 写失败测试**

在 `DownloadCenterViewModelTests.cs`（新建，用假 `DownloadQueueService` 或真实服务+假 executor）验证：5 个命令存在且行为正确（RetryAll 对 2 个 Failed 组各调用一次 Retry；ClearCompleted 调用服务并清空 Completed 组等）。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~DownloadCenterViewModelTests"`

Expected: FAIL（命令不存在）。

- [ ] **Step 3: 实现**

在 `DownloadCenterViewModel` 新增 5 个 `[RelayCommand]` 方法：

```csharp
[RelayCommand]
private async Task RetryAllAsync() // foreach Failed 组 → downloadQueue.RetryAsync
[RelayCommand]
private async Task PauseAllAsync() // downloadQueue.PauseSteamDownloadsAsync
[RelayCommand]
private async Task ResumeAllAsync() // downloadQueue.ResumeSteamDownloadsAsync
[RelayCommand]
private async Task CancelAllAsync() // foreach 未完成组 → downloadQueue.CancelAsync
[RelayCommand]
private async Task ClearCompletedAsync() // downloadQueue.ClearCompletedAsync
```

状态属性（`CanRetryAll` 等）在投影通知时刷新（`NotifyDownloadQueueProperties` 中更新）。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~DownloadCenterViewModelTests"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs
git commit -m "feat: add batch operations to download center"
```

---

### Task 4: ETA 与总览汇总

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/DownloadQueueGroupItemViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/DownloadQueueItemViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs`
- Test: `tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs`

**Interfaces:**
- Produces: `DownloadQueueGroupItemViewModel.EtaText`（`string`）、`DownloadQueueItemViewModel.EtaText`、`DownloadCenterViewModel.TotalSpeedText`、`OverallEtaText`、`ActiveCountText`。

**背景:** ETA 基于 `BytesPerSecond` 与剩余字节；速度为 0 时返回空字符串。总览为活动任务速度求和与汇总 ETA。

- [ ] **Step 1: 写失败测试**

验证：给定组 `TotalBytes=1000, CompletedBytes=250, BytesPerSecond=50` → `EtaText` 为 "00:15"（15 秒）；速度为 0 → 空字符串；`TotalSpeedText` 对两个活动任务求和格式化。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~EtaText"`

Expected: FAIL。

- [ ] **Step 3: 实现**

在 `QueueDisplayText`（`src/Crystalfly.App/ViewModels/QueueDisplayText.cs`，若存在）或 VM 中新增 ETA 格式化辅助：

```csharp
public static string Eta(double bytesPerSecond, long completed, long total)
    => bytesPerSecond > 0 && total > completed
        ? TimeSpan.FromSeconds((total - completed) / bytesPerSecond).ToString(@"hh\:mm\:ss")
        : string.Empty;
```

组/项 VM 增加 `EtaText` 属性；`DownloadCenterViewModel` 增加 `TotalSpeedText`（活动组 `BytesPerSecond` 求和）、`OverallEtaText`（用总剩余/总速度）、`ActiveCountText`。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~EtaText"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/DownloadQueueGroupItemViewModel.cs src/Crystalfly.App/ViewModels/DownloadQueueItemViewModel.cs src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs
git commit -m "feat: show per-task ETA and download overview totals"
```

---

### Task 5: 下载完成通知（仅本会话任务）

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs`
- Test: `tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs`

**Interfaces:**
- Produces: 内部 `HashSet<string> sessionEnqueuedGroupIds`；`OnQueueChanged` 投影时检测 `Running→Completed` 跃迁并弹 toast（本地化文案 `QueueCompletedFormat` 需在 `LocalizationViewModel.cs` 新增，中英各一条）。

**背景:** 完成通知仅限本会话入队任务；重启恢复的旧任务静默。投影时比较上一快照与当前快照的状态。

- [ ] **Step 1: 写失败测试**

验证：本会话入队的组 Running→Completed 时 `ToastRequested` 被调用；非本会话（预置）组完成时不调用。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~CompletionToast"`

Expected: FAIL。

- [ ] **Step 3: 实现**

`EnqueueAsync` 包装方法在成功入队后把 `group.Id` 加入 `sessionEnqueuedGroupIds`。投影时维护 `Dictionary<string, DownloadQueueGroupState> previousStates`；对状态从 `Running`（或 `Pending`）跃迁到 `Completed` 且 Id 在会话集合中的组，调用 `dependencies.ToastRequested(string.Format(Loc["QueueCompletedFormat"], group.Name))`。

在 `LocalizationViewModel.cs` 新增 `QueueCompletedFormat`（中："{0} 下载完成" / 英："{0} finished downloading"）。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~CompletionToast"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs src/Crystalfly.App/ViewModels/LocalizationViewModel.cs tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs
git commit -m "feat: toast when session downloads complete"
```

---

### Task 6: 错误分类与复制 + 时间/耗时

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/DownloadQueueGroupItemViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs`
- Test: `tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs`

**Interfaces:**
- Produces: `enum DownloadErrorCategory { Offline, Network, Verification, Permission, Other }`；`DownloadQueueGroupItemViewModel.ErrorCategory`、`ErrorCategoryText`、`DurationText`、`CopyErrorCommand`。

**背景:** 从 `Error` 文本与组状态推断分类；`DurationText` 用 `StartedAt`/`CompletedAt`；复制用 `Clipboard`。

- [ ] **Step 1: 写失败测试**

验证：Error 含 "offline"/"Offline mode" → Offline；含 "SHA-256"/"hash" → Verification；含 "HTTP" → Network；含 "access"/"permission" → Permission；其他 → Other。`DurationText` 对 StartedAt+10min → "10:00"。`CopyErrorCommand` 把 `Error` 写入剪贴板（Avalonia headless 可验证剪贴板文本）。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ErrorCategory"`

Expected: FAIL。

- [ ] **Step 3: 实现**

在组 VM 增加 `ErrorCategory`（从 `group.Error` 与 `group.State` 推断）、`ErrorCategoryText`（`Loc` 文案，本地化新增 `DownloadErrorOffline/Network/Verification/Permission/Other`）、`DurationText`、`CopyErrorCommand`（`Clipboard.SetTextAsync`）。`DownloadCenterViewModel` 暴露错误分类辅助方法供复用。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ErrorCategory"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/DownloadQueueGroupItemViewModel.cs src/Crystalfly.App/ViewModels/DownloadCenterViewModel.cs src/Crystalfly.App/ViewModels/LocalizationViewModel.cs tests/Crystalfly.App.Tests/ViewModels/DownloadCenterViewModelTests.cs
git commit -m "feat: classify download errors and show durations"
```

---

### Task 7: UI 变更（工具栏、总览条、卡片增强、列表组织）

**Files:**
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`（下载区域）
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml.cs`（如需代码后置处理复制）
- Modify: `tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs`

**Interfaces:**
- Consumes: Task 2-6 的 `DownloadCenter.*` 属性与命令、`EtaText`、`DurationText`、`ErrorCategoryText`、`CopyErrorCommand`、批量命令。

**背景:** 下载区域 UI 增强与绑定迁移。

- [ ] **Step 1: 更新 UI 结构测试（先失败）**

在 `MainWindowStructureTests.cs` 增加断言：下载队列区域存在批量操作按钮（全部重试/全部暂停·恢复/清除已完成，绑定 `DownloadCenter.RetryAllCommand` 等）与总览条（绑定 `DownloadCenter.TotalSpeedText`）。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~MainWindowStructureTests"`

Expected: FAIL（新绑定不存在）。

- [ ] **Step 3: 实现 UI**

下载区域（约 2144 行起）：
1. 队列顶部加批量操作工具栏（`RetryAllCommand`/`PauseAllCommand`/`ResumeAllCommand`/`ClearCompletedCommand` 按钮，按 `Can*` 启用）。
2. 总览条（`ActiveCountText` + `TotalSpeedText` + `OverallEtaText`）。
3. 任务卡片：进度行加 `EtaText`；标题下加 `DurationText`（完成/失败时）；错误行显示 `ErrorCategoryText` 标签 + 复制按钮（`CopyErrorCommand`）。
4. 列表组织：`ApplyPendingDownloadQueueProjection` 的排序改为"活动优先、失败次之、已完成置底"（Task 2 迁移的排序逻辑调整——原为 `CreatedAt` 降序，改为状态优先级 + `CreatedAt` 降序）。
5. 全部绑定用 `#Root.DataContext.DownloadCenter.xxx`。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS（全量 App 测试，含 UI 结构测试更新）。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/Views/MainWindow.axaml src/Crystalfly.App/Views/MainWindow.axaml.cs tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs
git commit -m "feat: enhance download center UI with batch toolbar and overview"
```

---

## 完成后验证

- 全量测试：`dotnet test .\Crystalfly.slnx -c Release`（App + Core + Steam + Updater 全绿，零警告）。
- 手动验收：入队 2+ 任务 → 批量暂停/恢复/取消/重试 → 观察 ETA/总览/完成通知（仅本会话）→ 清除已完成 → 验证错误分类与复制。
