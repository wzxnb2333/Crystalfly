# Speedrun Activity 赛况加载与刷新机制 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Speedrun Activity 赛况动态从"每次切页触发加载"改为"内存常驻 + 15 分钟后台定时刷新"，实现切页秒开、提醒去重、toast 仅限 Speedrun 页、离线兜底。

**Architecture:** 数据常驻现有 `SpeedrunActivities` / `VisibleSpeedrunActivities` 集合；新增 `speedrunActivityLastLoadedAt` 记录缓存新鲜度；切页时 `EnsureSpeedrunActivityLoaded()` 只在无缓存时显示 loading、缓存过期时后台静默刷新；`InitializeAsync` 启动 15 分钟后台循环刷新；`SpeedrunActivityDetector` 合并文档已知 RunId 消除重复检测；toast 触发前检查当前页。

**Tech Stack:** C# / .NET 10, Avalonia, CommunityToolkit.Mvvm, xUnit

## Global Constraints

- 数据常驻现有 `SpeedrunActivities` / `VisibleSpeedrunActivities` ObservableCollection，不改变 ViewModel 展示结构。
- 刷新间隔固定为 15 分钟（`SpeedrunActivityRefreshInterval`），不可配置。
- toast 提醒仅在 `CurrentPage == "Speedrun"` 时触发。
- 定时刷新仅应用运行时发生（用现有 `lifetimeCancellation` 管理生命周期）。
- 项目 warnings-as-errors：任何警告都会导致构建失败，代码必须零警告。
- 风格：4 空格缩进、file-scoped namespace、nullable reference types、public 成员 PascalCase、私有字段 camelCase。
- 测试用 xUnit；App 层测试在 `MainViewModelStateTests.cs`（已有 `SpeedrunResponseHandler` / `PartialSpeedrunResponseHandler` fixture）。

---

### Task 1: 修复 SpeedrunActivityDetector 重复检测

**Files:**
- Modify: `src/Crystalfly.Core/Speedrun/SpeedrunComModels.cs:147`（`SpeedrunActivityDetector.Apply` 的 knownRuns 判定）
- Test: `tests/Crystalfly.Core.Tests/Speedrun/SpeedrunActivityDetectorTests.cs`

**Interfaces:**
- Consumes: 现有 `SpeedrunActivityDocument.Activities`（`IReadOnlyList<SpeedrunActivityEntry>`，每条含 `RunId` 与 `Board.Key`）、`SpeedrunActivityEntry`、`SpeedrunBoardSnapshot`。
- Produces: 无对外接口变化；`SpeedrunActivityDetector.Apply` 行为修复——已进入活动历史的 run 不再被重新判定为新活动。

**背景:** 根因是 `knownRuns` 只取 `previous.Entries` 的 RunId。当某个 board 一次拉取失败、baseline 未更新（`LastSuccessfulCheck` 停留在旧值），下次成功时 snapshot 里早已被记录进 `Document.Activities` 的 run 会因 `VerifiedAt > LastSuccessfulCheck` 且不在 baseline entries 中而被再次判定为新活动 → 重复 toast。

- [ ] **Step 1: 写失败测试**

在 `SpeedrunActivityDetectorTests.cs` 的 `SpeedrunActivityDetectorTests` 类中追加：

```csharp
[Fact]
public void Does_not_re_detect_a_run_already_recorded_in_activity_history_after_a_failed_scan()
{
    var checkedAt = DateTimeOffset.Parse("2026-08-02T00:00:00Z");
    var record = Run("record", 1, 90, checkedAt.AddHours(1));
    // The stale baseline predates the world record; the record is already in activity history
    // because an intermediate scan for this board failed before its baseline could be refreshed.
    var old = Document(checkedAt, Run("old", 1, 100, checkedAt)) with
    {
        Activities =
        [
            new SpeedrunActivityEntry(
                "record",
                SpeedrunActivityKind.WorldRecord,
                Board,
                record,
                checkedAt.AddHours(1))
        ]
    };

    var result = SpeedrunActivityDetector.Apply(
        old,
        [new(Board, [Run("old", 1, 100, checkedAt), record])],
        checkedAt.AddHours(2));

    Assert.Empty(result.NewActivities);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Crystalfly.Core.Tests -c Release --filter "FullyQualifiedName~Does_not_re_detect_a_run_already_recorded"`

Expected: FAIL——`Assert.Empty` 报 NewActivities 含 1 项（`record` 被重复判定为 WorldRecord）。

- [ ] **Step 3: 修复 knownRuns 判定**

修改 `src/Crystalfly.Core/Speedrun/SpeedrunComModels.cs` 的 `SpeedrunActivityDetector.Apply`：

将当前：

```csharp
var knownRuns = previous.Entries.Select(run => run.RunId).ToHashSet(StringComparer.Ordinal);
```

改为：

```csharp
var knownRuns = previous.Entries
    .Select(run => run.RunId)
    .Concat(document.Activities
        .Where(activity =>
            string.Equals(activity.Board.Key, snapshot.Board.Key, StringComparison.Ordinal))
        .Select(activity => activity.RunId))
    .ToHashSet(StringComparer.Ordinal);
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Crystalfly.Core.Tests -c Release --filter "FullyQualifiedName~SpeedrunActivityDetectorTests"`

Expected: PASS（含新增测试 + 既有 `First_scan_establishes_baseline_without_activity`、`Retains_failed_boards_and_only_keeps_latest_one_hundred_activities` 等）。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.Core/Speedrun/SpeedrunComModels.cs tests/Crystalfly.Core.Tests/Speedrun/SpeedrunActivityDetectorTests.cs
git commit -m "fix: avoid re-detecting speedrun activity already in history"
```

---

### Task 2: 切页零拉取 + 内存缓存

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs:54-70`（`OnCurrentPageChanged` / `OnCurrentSpeedrunTabChanged`）、`:96-97`（`BeginSpeedrunActivityLoad`）、`:99-206`（`LoadSpeedrunActivityAsync`）
- Test: `tests/Crystalfly.App.Tests/ViewModels/MainViewModelStateTests.cs`

**Interfaces:**
- Consumes: 现有 `SpeedrunActivities` 集合、`BeginSpeedrunActivityLoad(bool)`、`LoadSpeedrunActivityAsync(bool)`、`SpeedrunActivityStatus`。
- Produces:
  - 字段 `private DateTimeOffset? speedrunActivityLastLoadedAt;`
  - 常量 `private static readonly TimeSpan SpeedrunActivityCacheLifetime = TimeSpan.FromMinutes(15);`
  - 方法 `private void EnsureSpeedrunActivityLoaded()`
  - `BeginSpeedrunActivityLoad(bool forceRefresh, bool showLoading = true)`
  - `LoadSpeedrunActivityAsync(bool forceRefresh, bool showLoading)`（Task 3 依赖此签名）

**背景:** 每次切到 Speedrun 页或 Activity tab 都触发 `BeginSpeedrunActivityLoad(false)` 并显示 loading。改为：有新鲜内存缓存直接显示；无缓存首次加载；缓存过期则显示已有数据并后台静默刷新。

- [ ] **Step 1: 写失败测试（切页不重新加载）**

在 `MainViewModelStateTests.cs` 的 `MainViewModelStateTests` 类中追加，并添加 `CountingSpeedrunResponseHandler`（见 Step 3）：

```csharp
[Fact]
public async Task Switching_to_activity_tab_with_fresh_cache_does_not_reload()
{
    string root = applicationData.CreateDirectory("speedrun-fresh-cache");
    using var policy = new NetworkPolicy();
    var handler = new CountingSpeedrunResponseHandler();
    using var httpClient = new HttpClient(handler);
    var speedrunClient = new SpeedrunComClient(
        httpClient,
        Path.Combine(root, "speedrun-cache"),
        policy);
    await using var viewModel = new MainViewModel(
        root,
        speedrunComClientOverride: speedrunClient)
    {
        CurrentPage = "Speedrun",
        CurrentSpeedrunTab = "Environment"
    };

    // First load populates the in-memory cache.
    await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);
    int callsAfterFirstLoad = handler.RequestCount;
    Assert.True(callsAfterFirstLoad > 0);

    // Switching to the Activity tab inside the cache window must not hit the network.
    viewModel.SelectSpeedrunTabCommand.Execute("Activity");

    Assert.Equal(callsAfterFirstLoad, handler.RequestCount);
    Assert.False(viewModel.IsSpeedrunActivityLoading);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~Switching_to_activity_tab_with_fresh_cache"`

Expected: FAIL——当前每次切 tab 都触发 `BeginSpeedrunActivityLoad`，切到 Activity 后 `handler.RequestCount` 增加。

- [ ] **Step 3: 添加计数 handler**

在 `MainViewModelStateTests.cs` 的 `SpeedrunResponseHandler` 类（约 3276 行）之后追加：

```csharp
private sealed class CountingSpeedrunResponseHandler : SpeedrunResponseHandler
{
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        return base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 4: 实现内存缓存与切页零拉取**

修改 `src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs`：

在 `speedrunLeaderboardLoadGeneration` 字段（第 15 行）附近新增：

```csharp
private DateTimeOffset? speedrunActivityLastLoadedAt;
private static readonly TimeSpan SpeedrunActivityCacheLifetime = TimeSpan.FromMinutes(15);
```

将 `OnCurrentPageChanged`（54-60 行）改为：

```csharp
partial void OnCurrentPageChanged(string value)
{
    if (value == "Speedrun" && IsSpeedrunActivityTab)
    {
        EnsureSpeedrunActivityLoaded();
    }
}
```

将 `OnCurrentSpeedrunTabChanged`（62-68 行）改为：

```csharp
partial void OnCurrentSpeedrunTabChanged(string value)
{
    if (value == "Activity")
    {
        EnsureSpeedrunActivityLoaded();
    }
}
```

将 `BeginSpeedrunActivityLoad`（96-97 行）改为：

```csharp
private void BeginSpeedrunActivityLoad(bool forceRefresh, bool showLoading = true) =>
    speedrunLeaderboardLoadTask = LoadSpeedrunActivityAsync(forceRefresh, showLoading);
```

将手动刷新命令 `RefreshSpeedrunActivityAsync`（72-73 行）改为传入 `showLoading: true`（用户主动刷新应显示 loading）：

```csharp
[RelayCommand]
private Task RefreshSpeedrunActivityAsync() => LoadSpeedrunActivityAsync(forceRefresh: true, showLoading: true);
```

将 `LoadSpeedrunActivityAsync` 签名与开头（99-112 行）改为：

```csharp
private async Task LoadSpeedrunActivityAsync(bool forceRefresh, bool showLoading)
{
    long generation = Interlocked.Increment(ref speedrunLeaderboardLoadGeneration);
    var replacement = new CancellationTokenSource();
    var previous = Interlocked.Exchange(ref speedrunLeaderboardLoadCancellation, replacement);
    previous?.Cancel();
    previous?.Dispose();
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(
        lifetimeCancellation.Token,
        replacement.Token);
    CancellationToken cancellationToken = linked.Token;
    IsSpeedrunActivityLoading = showLoading;
    SpeedrunActivityError = null;
    SpeedrunActivityStatus = showLoading ? Loc["SpeedrunActivityLoading"] : string.Empty;
```

在成功路径末尾（`SpeedrunActivityUpdatedAt` 赋值之后，约 184 行）新增：

```csharp
speedrunActivityLastLoadedAt = speedrunComClient.UtcNow;
```

新增 `EnsureSpeedrunActivityLoaded` 方法（放在 `BeginSpeedrunActivityLoad` 附近）。判断只看 `speedrunActivityLastLoadedAt` 新鲜度，**不依赖 `SpeedrunActivities.Count`**——空活动是合法的缓存状态（例如离线或全部榜单失败），否则会误判为"无缓存"而重复加载：

```csharp
private void EnsureSpeedrunActivityLoaded()
{
    if (speedrunActivityLastLoadedAt is { } lastLoaded
        && speedrunComClient.UtcNow - lastLoaded < SpeedrunActivityCacheLifetime)
    {
        return;
    }

    if (speedrunActivityLastLoadedAt is null)
    {
        BeginSpeedrunActivityLoad(forceRefresh: false, showLoading: true);
        return;
    }

    BeginSpeedrunActivityLoad(forceRefresh: true, showLoading: false);
}
```

> `speedrunActivityLastLoadedAt` 在 `LoadSpeedrunActivityAsync` 成功路径末尾（`IsCurrentSpeedrunActivityLoad(generation)` 检查通过后）设置——即使某次加载部分/全部榜单失败也记录时间，避免离线时每 15 分钟内反复重试。

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~MainViewModelStateTests"`

Expected: PASS——新增测试通过；既有 speedrun 测试（`Speedrun_activity_tab_establishes_baseline...`、`Speedrun_activity_filter...`）不回归。

- [ ] **Step 6: 提交**

```bash
git add src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs tests/Crystalfly.App.Tests/ViewModels/MainViewModelStateTests.cs
git commit -m "feat: keep speedrun activity cached in memory and avoid reloading on tab switch"
```

---

### Task 3: 15 分钟后台定时刷新循环

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs:807`（`InitializeCoreAsync` 内启动循环）
- Test: `tests/Crystalfly.App.Tests/ViewModels/MainViewModelStateTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `BeginSpeedrunActivityLoad(bool forceRefresh, bool showLoading)`、现有 `lifetimeCancellation`（`CancellationTokenSource`）。
- Produces:
  - 常量 `private static readonly TimeSpan SpeedrunActivityRefreshInterval = TimeSpan.FromMinutes(15);`
  - `internal void StartSpeedrunActivityRefreshLoop()`
  - `private async Task SpeedrunActivityRefreshLoopAsync()`

**背景:** 应用运行期间每 15 分钟后台静默刷新一次赛况，保证用户切到 Activity 页时数据已新。

- [ ] **Step 1: 写失败测试（后台循环静默刷新）**

在 `MainViewModelStateTests.cs` 追加：

```csharp
[Fact]
public async Task Background_refresh_loop_reloads_activity_without_loading_indicator()
{
    string root = applicationData.CreateDirectory("speedrun-refresh-loop");
    using var policy = new NetworkPolicy();
    var handler = new CountingSpeedrunResponseHandler();
    using var httpClient = new HttpClient(handler);
    var speedrunClient = new SpeedrunComClient(
        httpClient,
        Path.Combine(root, "speedrun-cache"),
        policy);
    await using var viewModel = new MainViewModel(
        root,
        speedrunComClientOverride: speedrunClient)
    {
        CurrentPage = "Speedrun",
        CurrentSpeedrunTab = "Environment"
    };

    await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);
    int callsAfterFirstLoad = handler.RequestCount;

    // Shorten the production 15-minute interval so the test can observe a loop iteration.
    var intervalField = typeof(MainViewModel).GetField(
        "SpeedrunActivityRefreshInterval",
        BindingFlags.Static | BindingFlags.NonPublic);
    Assert.NotNull(intervalField);
    intervalField.SetValue(null, TimeSpan.FromMilliseconds(150));

    viewModel.StartSpeedrunActivityRefreshLoop();
    await Task.Delay(500);

    Assert.True(handler.RequestCount > callsAfterFirstLoad);
    Assert.False(viewModel.IsSpeedrunActivityLoading);
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~Background_refresh_loop"`

Expected: FAIL——`StartSpeedrunActivityRefreshLoop` 未定义（编译错误）或未触发额外请求。

- [ ] **Step 3: 实现定时刷新循环**

在 `MainViewModel.SpeedrunLeaderboard.cs` 新增：

```csharp
private static readonly TimeSpan SpeedrunActivityRefreshInterval = TimeSpan.FromMinutes(15);

internal void StartSpeedrunActivityRefreshLoop() => _ = SpeedrunActivityRefreshLoopAsync();

private async Task SpeedrunActivityRefreshLoopAsync()
{
    try
    {
        while (!lifetimeCancellation.IsCancellationRequested)
        {
            await Task.Delay(SpeedrunActivityRefreshInterval, lifetimeCancellation.Token);
            if (lifetimeCancellation.IsCancellationRequested)
            {
                break;
            }

            BeginSpeedrunActivityLoad(forceRefresh: true, showLoading: false);
            await speedrunLeaderboardLoadTask;
        }
    }
    catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
    {
    }
}
```

在 `src/Crystalfly.App/ViewModels/MainViewModel.cs` 的 `InitializeCoreAsync` 中，`await Task.WhenAll(refreshTask, InitializeDownloadQueueAsync());`（约 811 行）之后新增：

```csharp
StartSpeedrunActivityRefreshLoop();
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~MainViewModelStateTests"`

Expected: PASS——后台循环触发额外网络请求且不显示 loading。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs src/Crystalfly.App/ViewModels/MainViewModel.cs tests/Crystalfly.App.Tests/ViewModels/MainViewModelStateTests.cs
git commit -m "feat: refresh speedrun activity in the background every 15 minutes"
```

---

### Task 4: toast 提醒仅在 Speedrun 页生效

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs:185-188`（toast 触发处）
- Test: `tests/Crystalfly.App.Tests/ViewModels/MainViewModelStateTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `LoadSpeedrunActivityAsync(bool, bool)`、现有 `CurrentPage`、现有 `ToastRequested` 事件、`SpeedrunActivityDetector` 的 `NewActivities`。
- Produces: 无对外接口变化；toast 仅在 `CurrentPage == "Speedrun"` 时触发。

**背景:** 后台刷新发现新纪录时，只有用户当前在 Speedrun 页才应弹 toast；其他页面静默更新。

- [ ] **Step 1: 写失败测试（非 Speedrun 页不 toast）**

在 `MainViewModelStateTests.cs` 追加：

```csharp
[Fact]
public async Task Activity_refresh_outside_the_speedrun_page_suppresses_toast_notifications()
{
    string root = applicationData.CreateDirectory("speedrun-toast-scope");
    // Seed a stale baseline so the refresh detects the returned run as a new world record.
    var baselineAt = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
    var board = new SpeedrunBoardDescriptor(
        SpeedrunGame.HollowKnight,
        "category-any",
        "Any%",
        null,
        null,
        []);
    await AtomicJsonStore.WriteAsync(
        Path.Combine(root, "speedrun-activity.json"),
        new SpeedrunActivityDocument
        {
            Boards = new Dictionary<string, SpeedrunBoardBaseline>(StringComparer.Ordinal)
            {
                [board.Key] = new(
                    board,
                    baselineAt,
                    [new("old-run", 1, "Old Runner", "PT40M", 2400, baselineAt, null)])
            }
        },
        CancellationToken.None);

    using var policy = new NetworkPolicy();
    using var httpClient = new HttpClient(new NewRecordSpeedrunResponseHandler());
    var speedrunClient = new SpeedrunComClient(
        httpClient,
        Path.Combine(root, "speedrun-cache"),
        policy);
    await using var viewModel = new MainViewModel(
        root,
        speedrunComClientOverride: speedrunClient)
    {
        CurrentPage = "Launch",
        CurrentSpeedrunTab = "Activity"
    };
    var toasts = new List<string>();
    viewModel.ToastRequested += toast => toasts.Add(toast);

    await viewModel.RefreshSpeedrunActivityCommand.ExecuteAsync(null);

    Assert.Empty(toasts);
}
```

- [ ] **Step 1b: 添加能产生新纪录的 handler**

在 `MainViewModelStateTests.cs` 的 `SpeedrunResponseHandler` 类之后追加（categories 带 `type: "per-game"`、levels 带 `name`、leaderboards 返回一个新纪录 run——现有 `SpeedrunResponseHandler` 的 levels 响应缺 `name` 会导致 `ParseLevels` 抛 `JsonException`，加载失败无新活动，因此必须用此 handler）：

```csharp
private sealed class NewRecordSpeedrunResponseHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string json = request.RequestUri!.AbsolutePath switch
        {
            var path when path.EndsWith("/categories", StringComparison.Ordinal) => """
            {
              "data": [
                { "id": "category-any", "name": "Any%", "type": "per-game" }
              ]
            }
            """,
            var path when path.Contains("/leaderboards/", StringComparison.Ordinal) => """
            {
              "data": {
                "runs": [
                  {
                    "place": 1,
                    "run": {
                      "id": "new-record",
                      "weblink": "https://www.speedrun.com/hollowknight/runs/new-record",
                      "status": { "status": "verified", "verify-date": "2026-08-01T01:00:00Z" },
                      "times": { "primary": "PT31M" },
                      "players": [{ "rel": "user", "id": "p1" }]
                    }
                  }
                ],
                "players": [{ "id": "p1", "names": { "international": "Runner" } }]
              }
            }
            """,
            _ => """
            {
              "data": [
                { "id": "level-1", "name": "Level 1" }
              ]
            }
            """
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}
```

> 说明：`GetPodiumAsync` 对 full-game 类别请求 `/leaderboards/76rqmld8/category/category-any?top=3&embed=players`，命中 `NewRecordSpeedrunResponseHandler` 的 leaderboards 分支，返回 `new-record`（verified 2026-08-01T01:00Z，PT31M）。预置 baseline 的 `LastSuccessfulCheck` 为 2026-07-31T00:00Z、旧纪录 PT40M，因此 `new-record` 会被判定为新的 WorldRecord，`NewActivities` 非空。

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~Activity_refresh_outside_the_speedrun_page"`

Expected: FAIL——当前 toast 无条件触发，`toasts` 非空。

- [ ] **Step 3: 实现 toast 作用域**

修改 `MainViewModel.SpeedrunLeaderboard.cs` 的 toast 触发处（185-188 行）：

将当前：

```csharp
foreach (SpeedrunActivityEntry activity in detection.NewActivities)
{
    ToastRequested?.Invoke(ActivityToastText(activity));
}
```

改为：

```csharp
if (CurrentPage == "Speedrun")
{
    foreach (SpeedrunActivityEntry activity in detection.NewActivities)
    {
        ToastRequested?.Invoke(ActivityToastText(activity));
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~MainViewModelStateTests"`

Expected: PASS——新增测试通过；既有测试不回归。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs tests/Crystalfly.App.Tests/ViewModels/MainViewModelStateTests.cs
git commit -m "feat: only toast speedrun activity updates while on the speedrun page"
```

---

## 完成后验证

- 全量测试：`dotnet test .\Crystalfly.slnx -c Release`（Core + App + Steam + Updater 全部通过，零警告）。
- 手动验收：启动 Crystalfly → 切到 Speedrun → Activity tab 秒开（不转圈）→ 保持应用运行 15 分钟，后台自动刷新且不在其他页面弹 toast；断网后仍展示上次缓存并显示"离线 / 上次更新时间"。
