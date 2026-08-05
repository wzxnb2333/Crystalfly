# Speedrun Activity 赛况加载与刷新机制设计

日期：2026-08-05
状态：已获用户批准（方案 B：内存常驻 + 定时刷新）

## 背景与目标

Speedrun 页的 **Activity（活动动态）** tab 展示 speedrun.com 的赛况动态——世界纪录、并列纪录、第二名、第三名的更新，支持 All/HollowKnight/Silksong 过滤，数据来源为 speedrun.com API（15 分钟磁盘缓存），检测逻辑由 `SpeedrunActivityDetector` 完成。

当前加载机制存在体验问题，用户反馈以下痛点：

1. **切页重复加载**：每次切到 Speedrun 页或 Activity tab 都触发完整加载流程（`OnCurrentPageChanged` / `OnCurrentSpeedrunTabChanged` → `BeginSpeedrunActivityLoad(false)`），即使 15 分钟缓存有效也显示 loading 转圈、可能等待网络。
2. **无自动更新**：只在切页或手动点击刷新时拉取，无后台定时刷新，赛况数据需要手动保持新。
3. **重复弹提醒**：`SpeedrunActivityDetector` 依赖每个 board 的 `LastSuccessfulCheck` 判断新活动；当部分 board 拉取失败时不更新该 board 的 baseline，下次成功时会重新检测历史 run，导致重复 toast。
4. **加载慢 / 离线体验差**：podium 拉取并发限 3（`SemaphoreSlim(3, 3)`），榜单多时较慢；离线时若磁盘缓存失效则无兜底数据可展示。

**目标**：重构 Activity 数据的加载与刷新机制，实现切页秒开、后台自动保持数据新、提醒去重、离线可用的体验。

## 方案：内存常驻 + 定时刷新

将 Activity 数据的加载从"每次切页触发"改为"内存常驻 + 应用级驱动"。

### 1. 内存缓存与加载时机

- 数据常驻现有 `SpeedrunActivities` / `VisibleSpeedrunActivities` 集合（保持 ViewModel 结构不变）。
- 新增字段 `speedrunActivityLastLoadedAt`（`DateTimeOffset?`）记录最近一次成功加载时间。
- 删除 `OnCurrentPageChanged` / `OnCurrentSpeedrunTabChanged` 中每次切页都 `BeginSpeedrunActivityLoad(false)` 的逻辑，改为 `EnsureSpeedrunActivityLoaded()`：
  - **无缓存**（从未加载）→ 首次加载（显示 loading）。
  - **有缓存且未过期**（距上次加载 < 15 分钟）→ 直接显示，不做任何网络操作。
  - **有缓存但已过期** → 直接显示缓存，同时触发**后台静默刷新**（不阻塞 UI，完成后自动更新显示）。
- `LoadSpeedrunActivityAsync` 增加参数区分"首次 / 手动刷新（显示 loading）"与"后台静默刷新（不显示 loading、不打扰）"。

### 2. 定时刷新循环

- 在 `MainViewModel.InitializeAsync` 启动一个后台循环任务：每 15 分钟执行一次 `BeginSpeedrunActivityLoad(forceRefresh: true, showLoading: false)`。
- 生命周期由现有 `lifetimeCancellation` 管理，应用关闭自动停止。
- 全局后台刷新（不限于 Speedrun 页），保证用户切到 Activity tab 时数据已是最新，实现"切页零等待"。
- 刷新使用 `forceRefresh: true`，确保拉取 speedrun.com 新数据（与磁盘 15 分钟缓存间隔对齐，避免命中旧缓存）。

### 3. 提醒（toast）作用域与去重

- **作用域**：`LoadSpeedrunActivityAsync` 中触发 toast 前检查 `CurrentPage == "Speedrun"`；仅当用户当前在 Speedrun 页时才弹 toast，其他页面静默更新（用户选择）。
- **去重修复**（`SpeedrunActivityDetector.Apply`）：判定已知 run 的集合除当前 board 的 `previous baseline` 外，**合并 `document.Activities` 中该 board 已有的全部 RunId**。即使 baseline 因部分 board 拉取失败而过期，已进入活动历史（或已提醒过）的 run 也绝不会被重新判定为"新活动"，从根源消除重复 toast。

### 4. 离线体验

- 离线或 speedrun.com 不可达时：**直接展示内存 / 磁盘缓存**（上次成功数据），状态栏显示"离线 / 上次更新时间"（复用现有 `SpeedrunActivityUpdatedAt` / `SpeedrunActivityError`）。
- 后台刷新失败时**不清空已有数据**，保留缓存展示，不因网络失败清空页面。

## 改动文件

| 文件 | 改动 |
|------|------|
| `src/Crystalfly.App/ViewModels/MainViewModel.SpeedrunLeaderboard.cs` | 加载时机重构、内存缓存、定时刷新循环、toast 作用域 |
| `src/Crystalfly.Core/Speedrun/SpeedrunComModels.cs` | `SpeedrunActivityDetector.Apply` 去重修复（合并文档已知 RunId） |

## 测试策略

- **Core**（`SpeedrunComClientTests`）：部分 board 拉取失败 → 恢复后同一 run 不重复进入 `NewActivities`。
- **App**（`MainViewModelStateTests`）：
  - 切页不重复加载（有有效缓存时零网络请求）。
  - 缓存过期时后台静默刷新（不显示 loading）。
  - 定时刷新循环启动并在 15 分钟后触发。
  - toast 仅在当前页为 Speedrun 时触发。

## 不做的事（YAGNI）

- 不做真正意义的后台进程/服务化拉取（仅在应用运行时刷新）。
- 不改变 Activity 行的展示结构、过滤方式或 speedrun.com API 的数据抓取范围。
- 不调整 15 分钟刷新间隔为可配置（先固定，后续如需再做设置项）。
