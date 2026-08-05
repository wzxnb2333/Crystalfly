# 模组管理大改造设计

日期：2026-08-06
状态：已获用户批准（方案 A：双 VM + Core 冲突服务）

## 背景与目标

已安装模组管理逻辑当前耦合在 `MainViewModel`（5158 行主文件）中。模组管理已有：批量启用/禁用、批量更新（选中）、卸载、接管、固定、修复、健康检查（`ModHealthReport`）、依赖图（含 Cycle 环检测）、依赖修复、readme/发行说明展示。缺少：独立详情面板、文件冲突检测、依赖树交互增强、"检查全部可更新"入口。

**目标**：将已安装模组管理拆为两个独立 ViewModel（`ModManagementViewModel` + `DependencyGraphViewModel`），补齐文件冲突检测、依赖交互增强、检查全部可更新、右侧详情面板。沿用下载中心确立的领域 VM 模式（见 `docs/adr/0001-mainviewmodel-domain-decomposition.md`）。

## 方案：双 VM + Core 冲突服务

### 1. 架构与组件

- **`ModManagementViewModel : ViewModelBase`**（独立 VM，构造注入 `ModManagementDependencies`）：
  - 已安装模组：`InstalledMods`/`VisibleInstalledMods` 集合与过滤（从 MainViewModel 迁移）。
  - 健康与操作：健康报告、启用/禁用/更新/卸载/接管/固定/修复命令（迁移）。
  - 更新检查：`HasUpdatesAvailable` 聚合 + "检查全部可更新"命令（扫描所有已装模组 `HasUpdate`）。
  - 详情面板：`SelectedInstalledMod` 联动（依赖、作者/仓库、健康、版本、readme、安装历史）。
- **`DependencyGraphViewModel`**（独立 VM）：封装现有 `DependencyGraphModel`，提供节点选择/定位/过滤、依赖树展开/收起交互状态、缺失依赖引导。
- **`ModConflictDetector`**（Core 新服务）：输入模组集合的 modified files → 输出冲突对。
- **`ModManagementDependencies`**（record，构造注入）：模组服务工厂、catalog、Loc、toast 回调、lifetime 取消令牌等。
- **`MainViewModel`**：构造时创建两个 VM，暴露 `ModManagement` 与 `DependencyGraph` 属性；原模组命令迁移，MainWindow 绑定更新。

### 2. 数据流

- **文件冲突检测流**：实例加载时收集各模组 `ModHealthReport.ModifiedFiles` → `ModConflictDetector.Detect(mods)` → 冲突对列表（`ModConflictPair`：模组 A/B + 重叠文件）→ 列表标红（`HasConflicts`）+ 详情面板展示冲突双方与文件。
- **依赖交互**：`DependencyGraphViewModel` 维护展开/收起状态；图中缺失节点（现有 `Missing` 状态）→ 详情面板显示"缺失依赖" + 修复按钮（复用现有 `RepairDependencies` 流程）。
- **更新检查**：`HasUpdatesAvailable`（聚合所有已装模组 `HasUpdate`）+ 列表过滤"可更新"（复用现有 `ModStatusFilter.Updates`）+ 可更新徽标。

### 3. 详情面板内容

`SelectedInstalledMod` 联动，区块：

| 区块 | 来源 |
|------|------|
| 基本信息 | 名称、版本、当前 vs 最新版本（`HasUpdate` 时） |
| 依赖关系 | `CatalogManifest.Dependencies` + 缺失/冲突状态 |
| 作者与仓库 | `CatalogManifest.Authors` / `RepositoryUrl`（可点击） |
| 健康详情 | `HealthReport` 状态 + 修改文件列表 |
| readme/发行说明 | 现有 `SelectedModReadmeMarkdown` / `SelectedModReleaseNotesMarkdown` |
| 安装历史 | `Receipt`（安装时间、来源、固定状态） |

### 4. UI 变更

`MainWindow.axaml` 的 Manage → Mods 区域：
1. master-detail 布局：左侧已安装模组列表 + 右侧详情面板。
2. 详情面板区块（基本信息/依赖/作者仓库/健康/readme/安装历史）。
3. 冲突标红（`HasConflicts`）+ 冲突文件展示。
4. "检查全部可更新"按钮 + 可更新徽标 + "可更新"过滤 chip。
5. 依赖图交互（展开/收起、缺失节点修复引导）。
6. 绑定迁移：Mods 区域改为 `ModManagement.*` / `DependencyGraph.*`。

## 测试策略

- **`ModConflictDetector`（Core）**：重叠文件 → 冲突对；无重叠 → 空；三模组共享文件。
- **`ModManagementViewModel`**：更新检查聚合、详情面板联动、命令迁移。
- **`DependencyGraphViewModel`**：展开/收起状态、缺失依赖引导。
- **UI 结构测试**：master-detail 布局、更新按钮、绑定断言更新。

## 不做的事（YAGNI）

- 不做更新前版本对比/changelog 确认（本次仅"检查全部可更新"入口；对比与确认留待后续）。
- 不做一键全部更新（现有 `UpdateSelectedModsAsync` 已覆盖选中批量更新）。
- 不做更新后自动验证（现有健康检查可手动触发）。
- 不拆模组市场（保留在 MainViewModel，独立交互域）。
