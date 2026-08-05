# 模组管理大改造 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把已安装模组管理从 MainViewModel 拆成 `ModManagementViewModel` + `DependencyGraphViewModel` 两个独立 VM，补齐文件冲突检测、依赖交互增强、检查全部可更新、右侧详情面板。

**Architecture:** 沿用下载中心的领域 VM 模式（构造注入）。`ModManagementViewModel` 迁移已安装模组集合/过滤/命令，`DependencyGraphViewModel` 封装现有 `DependencyGraphModel`；Core 新增 `ModConflictDetector` 服务检测模组间文件冲突。

**Tech Stack:** C# / .NET 10, Avalonia, CommunityToolkit.Mvvm, xUnit

## Global Constraints

- 依赖传递用构造注入（`ModManagementDependencies` record），不引入 DI 容器。
- 文件冲突检测基于 `ModHealthReport.ModifiedFiles`（`src/Crystalfly.Core/Models/ModHealthReport.cs:21`）。
- 依赖图复用现有 `DependencyGraphModel`（`src/Crystalfly.App/ViewModels/DependencyGraph/`）与 `DependencyGraphView`，不重写。
- 缺失依赖修复复用现有 `RepairDependencies` 流程。
- 模组市场（Market）不拆，保留在 MainViewModel。
- 项目 warnings-as-errors：零警告。
- 测试用 xUnit；Core 测试在 `tests/Crystalfly.Core.Tests/`，App 测试在 `tests/Crystalfly.App.Tests/`。

---

### Task 1: ModConflictDetector（Core 服务）

**Files:**
- Create: `src/Crystalfly.Core/Mods/ModConflictDetector.cs`
- Test: `tests/Crystalfly.Core.Tests/Mods/ModConflictDetectorTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record ModConflictInput(string ModId, string ModName, IReadOnlyList<string> ModifiedFiles);
  public sealed record ModConflictPair(string ModA, string ModB, IReadOnlyList<string> OverlappingFiles);
  public static class ModConflictDetector
  {
      public static IReadOnlyList<ModConflictPair> Detect(IReadOnlyList<ModConflictInput> mods);
  }
  ```
  `Detect` 收集所有模组的 modified files，按文件路径分组，输出共享同一文件的模组对（每对含重叠文件列表，文件路径比较用 `StringComparison.OrdinalIgnoreCase`——Windows 路径不区分大小写）。

**背景:** 文件冲突 = 两个模组修改同一游戏文件。`ModHealthReport.ModifiedFiles` 已有此数据。

- [ ] **Step 1: 写失败测试**

```csharp
[Fact]
public void Detect_returns_pairs_for_mods_sharing_files()
{
    var result = ModConflictDetector.Detect(
    [
        new("a", "Mod A", ["hollow_knight_Data/Managed/Mods/A.dll"]),
        new("b", "Mod B", ["hollow_knight_Data/Managed/Mods/A.dll"]),
        new("c", "Mod C", ["hollow_knight_Data/Managed/Mods/C.dll"])
    ]);
    Assert.Single(result);
    Assert.Equal(("a", "b"), (result[0].ModA, result[0].ModB));
    Assert.Equal(["hollow_knight_Data/Managed/Mods/A.dll"], result[0].OverlappingFiles);
}

[Fact]
public void Detect_returns_empty_when_no_overlap() { /* 三个模组各改不同文件 → Empty */ }

[Fact]
public void Detect_matches_file_paths_case_insensitively() { /* "Managed/A.dll" vs "managed/a.dll" → 冲突 */ }

[Fact]
public void Detect_reports_multi_file_overlap_once_per_pair() { /* 两个模组共享 2 个文件 → 1 对含 2 文件 */ }
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.Core.Tests -c Release --filter "FullyQualifiedName~ModConflictDetector"`

Expected: FAIL（类型不存在，编译错误）。

- [ ] **Step 3: 实现**

```csharp
public static IReadOnlyList<ModConflictPair> Detect(IReadOnlyList<ModConflictInput> mods)
{
    var pairs = new List<ModConflictPair>();
    for (var i = 0; i < mods.Count - 1; i++)
    {
        for (var j = i + 1; j < mods.Count; j++)
        {
            var overlap = mods[i].ModifiedFiles
                .Intersect(mods[j].ModifiedFiles, StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (overlap.Length > 0)
            {
                pairs.Add(new(mods[i].ModId, mods[j].ModId, overlap));
            }
        }
    }
    return pairs;
}
```

> O(n²·f) 实现；已安装模组数通常 < 50，性能足够。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.Core.Tests -c Release --filter "FullyQualifiedName~ModConflictDetector"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.Core/Mods/ModConflictDetector.cs tests/Crystalfly.Core.Tests/Mods/ModConflictDetectorTests.cs
git commit -m "feat: detect file conflicts between installed mods"
```

---

### Task 2: ModManagementViewModel 拆分

**Files:**
- Create: `src/Crystalfly.App/ViewModels/ModManagementViewModel.cs`
- Create: `src/Crystalfly.App/ViewModels/ModManagementDependencies.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`（Mods 区域绑定迁移）

**Interfaces:**
- Produces:
  - `public sealed record ModManagementDependencies(...)` —— 按需字段：模组服务工厂（`CreateModInstallService`/`CreateModManager` 的 Func）、catalog、Loc、toast 回调、lifetime 取消令牌、`InstalledModDependencyGraph` 相关静态工具。
  - `public sealed class ModManagementViewModel : ViewModelBase`：`InstalledMods`/`VisibleInstalledMods`、`ModStatusOptions`/`SelectedModStatus`、过滤逻辑（迁移 `ApplyModFilters`）、模组命令（迁移 `ToggleSelectedModAsync`/`UpdateSelectedModAsync`/`UninstallSelectedModsAsync`/`EnableSelectedModsAsync`/`DisableSelectedModsAsync`/`TakeOverSelectedModAsync`/`ToggleSelectedModPinnedAsync`/`RepairSelectedModAsync` 等）、`SelectedInstalledMod`、详情面板属性。
  - `MainViewModel.ModManagement` 属性。

**背景:** 迁移 `MainViewModel.cs` 中已安装模组相关成员（集合 243/245 行、过滤 528 行、命令 2179-2452 行区域）。注意 `HasModDependencyProblems`（293 行）依赖 `InstalledMods` 与 `LaunchPreflight`——保留在 MainViewModel 或由 `ModManagement` 暴露 `InstalledMods` 供其读取。

- [ ] **Step 1: 创建 ModManagementDependencies + ModManagementViewModel 骨架**

迁移集合、过滤、命令（从 MainViewModel.cs 相应区域）。`Loc`/`ToastRequested`/`lifetimeCancellation` 改为依赖注入。依赖工厂（`CreateModInstallService` 等）从 MainViewModel 传入（`ModManagementDependencies` 中的 `Func<...>`）。

- [ ] **Step 2: 运行确认编译失败**

Run: `dotnet build src/Crystalfly.App/Crystalfly.App.csproj -c Release`

Expected: FAIL（MainViewModel 引用旧成员）。

- [ ] **Step 3: MainViewModel 接线**

构造函数创建 `ModManagement = new ModManagementViewModel(dependencies)`；`InstalledMods` 相关引用改为 `ModManagement.InstalledMods`（含 `HasModDependencyProblems`、预设、依赖图构建等使用点）；删除已迁移的成员。MainWindow Mods 区域绑定改为 `ModManagement.*`。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS（现有模组相关测试更新引用后通过）。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/ModManagementViewModel.cs src/Crystalfly.App/ViewModels/ModManagementDependencies.cs src/Crystalfly.App/ViewModels/MainViewModel.cs src/Crystalfly.App/Views/MainWindow.axaml
git commit -m "refactor: extract installed mod management into a standalone view model"
```

---

### Task 3: DependencyGraphViewModel 拆分

**Files:**
- Create: `src/Crystalfly.App/ViewModels/DependencyGraph/DependencyGraphViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`（`InstalledModGraph` 迁移/委托）

**Interfaces:**
- Produces: `public sealed class DependencyGraphViewModel : ViewModelBase`：包装 `DependencyGraphModel Graph`（`DependencyGraphModel` 已有 `Select`/`MoveNode`/`RestoreAutomaticLayout`/节点状态）、`ExpandedNodeIds`（`HashSet<string>` 展开状态）、`ExpandNodeCommand`/`CollapseNodeCommand`、`IsExpanded(nodeId)`、缺失节点定位（`MissingNodeIds`）。

**背景:** 封装现有 `DependencyGraphModel`（`src/Crystalfly.App/ViewModels/DependencyGraph/DependencyGraphModel.cs`）。`InstalledModGraph`（MainViewModel 248 行）迁入此 VM。

- [ ] **Step 1: 写失败测试（展开/收起状态）**

新建 `tests/Crystalfly.App.Tests/ViewModels/DependencyGraphViewModelTests.cs`：构造图（3 节点链），断言初始全部折叠、`ExpandNodeCommand` 展开后 `IsExpanded` 为 true、`CollapseNodeCommand` 收起。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~DependencyGraphViewModelTests"`

Expected: FAIL（类型不存在）。

- [ ] **Step 3: 实现**

```csharp
public sealed class DependencyGraphViewModel : ViewModelBase
{
    public DependencyGraphModel Graph { get; }
    public HashSet<string> ExpandedNodeIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsExpanded(string id) => ExpandedNodeIds.Contains(id);
    [RelayCommand] private void ExpandNode(string? id) { ... ExpandedNodeIds.Add; OnPropertyChanged(nameof(IsExpanded)); }
    [RelayCommand] private void CollapseNode(string? id) { ... }
    public IReadOnlyList<string> MissingNodeIds => Graph.Nodes.Where(n => n.IsMissing).Select(n => n.Id).ToArray();
}
```

MainViewModel 的 `InstalledModGraph` 改为由 `DependencyGraphViewModel` 承载（`ModManagement` 或 MainViewModel 暴露 `DependencyGraph` 属性，`RebuildInstalledModGraph` 迁移）。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~DependencyGraphViewModelTests"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/DependencyGraph/DependencyGraphViewModel.cs src/Crystalfly.App/ViewModels/MainViewModel.cs tests/Crystalfly.App.Tests/ViewModels/DependencyGraphViewModelTests.cs
git commit -m "refactor: wrap installed mod graph in a view model"
```

---

### Task 4: 检查全部可更新 + 冲突接入

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/ModManagementViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/InstalledModItemViewModel.cs`
- Test: `tests/Crystalfly.App.Tests/ViewModels/ModManagementViewModelTests.cs`（新建）

**Interfaces:**
- Produces: `ModManagementViewModel.HasUpdatesAvailable`（`bool`，聚合所有 `HasUpdate`）、`CheckForUpdatesCommand`（`[RelayCommand]`，刷新更新状态）、`ModManagementViewModel.Conflicts`（`IReadOnlyList<ModConflictPair>` 或 `ObservableCollection`）、`InstalledModItemViewModel.HasConflicts`、`ConflictWithText`（冲突对方模组名）。

**背景:** 更新检查 = 扫描所有已装模组 `HasUpdate` 聚合。冲突接入 = 实例加载后调用 `ModConflictDetector.Detect`，结果映射到模组 VM。

- [ ] **Step 1: 写失败测试**

验证：2 个模组 `HasUpdate` → `HasUpdatesAvailable` true、`CheckForUpdatesCommand` 后刷新；`Conflicts` 含冲突对 → 对应 `InstalledModItemViewModel.HasConflicts` true。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ModManagementViewModelTests"`

Expected: FAIL。

- [ ] **Step 3: 实现**

`CheckForUpdatesAsync`：`OnPropertyChanged(nameof(HasUpdatesAvailable))`（数据源 `InstalledMods` 的 `HasUpdate` 已在 VM 中）。冲突：`RefreshConflicts()` 用 `ModConflictDetector.Detect(InstalledMods.Select(m => new ModConflictInput(m.Id, m.Name, m.HealthReport.ModifiedFiles)))`，把冲突模组 Id 集合写入 `InstalledModItemViewModel`（新增 `SetConflict(string? otherModName)` 或 `HasConflicts` 属性）。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ModManagementViewModelTests"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/ModManagementViewModel.cs src/Crystalfly.App/ViewModels/InstalledModItemViewModel.cs tests/Crystalfly.App.Tests/ViewModels/ModManagementViewModelTests.cs
git commit -m "feat: check all mod updates and surface file conflicts"
```

---

### Task 5: 详情面板（VM 属性）

**Files:**
- Modify: `src/Crystalfly.App/ViewModels/ModManagementViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/InstalledModItemViewModel.cs`
- Test: `tests/Crystalfly.App.Tests/ViewModels/ModManagementViewModelTests.cs`

**Interfaces:**
- Produces: `InstalledModItemViewModel` 新增：`DependenciesText`（`CatalogManifest.Dependencies` 拼接）、`AuthorsText`、`RepositoryUrl`、`HasRepositoryUrl`、`InstallDateText`（`Receipt` 安装时间）、`LatestVersionText`（`HasUpdate` 时）、`HasLatestVersion`、`ModifiedFilesText`（健康修改文件）。`ModManagementViewModel.SelectedInstalledMod` 联动已存在。

**背景:** 详情面板数据来自 `CatalogManifest`、`Receipt`、`HealthReport`（`InstalledModItemViewModel` 已有这些对象）。

- [ ] **Step 1: 写失败测试**

构造 `InstalledModItemViewModel`（用测试 fixture 的 `ModManifest`），断言 `DependenciesText`/`AuthorsText`/`RepositoryUrl`/`InstallDateText` 等格式正确；`HasUpdate` 模组的 `LatestVersionText` 正确。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ModManagementViewModelTests"`

Expected: FAIL。

- [ ] **Step 3: 实现**

在 `InstalledModItemViewModel` 增加展示属性（只读计算，从已有 `CatalogManifest`/`Receipt`/`HealthReport` 派生）。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~ModManagementViewModelTests"`

Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/ViewModels/ModManagementViewModel.cs src/Crystalfly.App/ViewModels/InstalledModItemViewModel.cs tests/Crystalfly.App.Tests/ViewModels/ModManagementViewModelTests.cs
git commit -m "feat: expose mod detail panel data"
```

---

### Task 6: UI 变更（master-detail + 冲突标红 + 更新入口 + 绑定迁移）

**Files:**
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`（Manage → Mods 区域）
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml.cs`（如需）
- Modify: `tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs`

**Interfaces:**
- Consumes: Task 2-5 的 `ModManagement.*`、`DependencyGraph.*`、`HasConflicts`、`HasUpdatesAvailable`、`CheckForUpdatesCommand`、详情属性。

**背景:** Mods 区域改为 master-detail，绑定迁移。

- [ ] **Step 1: 更新 UI 结构测试（先失败）**

`MainWindowStructureTests.cs` 增加断言：Mods 区域存在右侧详情面板（绑定 `ModManagement.SelectedInstalledMod`）、"检查全部可更新"按钮（`ModManagement.CheckForUpdatesCommand`）、冲突标红类（`cfp-mod-conflict` 或等效）。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~MainWindowStructureTests"`

Expected: FAIL。

- [ ] **Step 3: 实现 UI**

Mods 区域（MainWindow.axaml 约 1340-1450 行）：
1. 布局改 master-detail：左侧 `InstalledModsList`（保留），右侧新增详情面板 `Border`（绑定 `ModManagement.SelectedInstalledMod` 的各详情属性：依赖/作者/仓库/健康/版本/安装历史/readme）。
2. 列表行冲突标红（`Classes.conflict="{Binding HasConflicts}"` + 冲突文案）。
3. 工具栏加"检查全部可更新"按钮（`ModManagement.CheckForUpdatesCommand`，`IsVisible` 绑 `HasUpdatesAvailable` 或常显）+ 可更新徽标。
4. 依赖图区域绑定改为 `DependencyGraph.*`（`Graph`、展开命令）。
5. 全部绑定 `ModManagement.*`/`DependencyGraph.*`。

- [ ] **Step 4: 运行确认通过**

Run: `dotnet test tests/Crystalfly.App.Tests -c Release`

Expected: PASS（全量 App 测试）。

- [ ] **Step 5: 提交**

```bash
git add src/Crystalfly.App/Views/MainWindow.axaml src/Crystalfly.App/Views/MainWindow.axaml.cs tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs
git commit -m "feat: add mod detail panel and conflict highlighting"
```

---

## 完成后验证

- 全量测试：`dotnet test .\Crystalfly.slnx -c Release`（全绿，零警告）。
- 手动验收：安装两个改同一文件的模组 → 冲突标红 + 详情面板冲突展示；有更新的模组 → "检查全部可更新" + 徽标；选中模组 → 详情面板各区块；依赖图展开/收起 + 缺失依赖修复引导。
