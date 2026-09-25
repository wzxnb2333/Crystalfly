# 速通工作台重构 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将速通页重构为不遮挡、可扫描的工作台，并在启动首页增加可维护的 HK / Silksong 社区直达和自定义链接。

**Architecture:** Core 只保存用户自定义链接的可序列化定义和规范化规则；App 层提供内置目录、合并后的 ViewModel 与对话框命令。MainWindow 使用现有 Avalonia/Semi.Avalonia/Lucide 组件，启动页社区栏和速通工作台均复用同一条目模板与 HTTPS 外链打开逻辑。

**Tech Stack:** .NET 10、C#、Avalonia、CommunityToolkit.Mvvm、xUnit、Avalonia.Headless.XUnit、Lucide.Avalonia。

**Spec:** `docs/superpowers/specs/2026-09-12-speedrun-panel-workbench-design.md`

## Global Constraints

- 保持现有速通环境创建、RuntimePatches 安装、验证、启动和成绩缓存行为不变。
- 自定义链接仅允许 HTTPS、无用户信息、长度不超过 2048，图标使用固定 Lucide 键集合。
- 内置链接不可删除；坏的自定义设置必须被忽略而不能阻止页面加载。
- 不新增 UI 动画或外部 UI 框架；沿用现有主题、间距和按钮样式。
- 900×600、1280×720、1920×1080 下主要操作必须可见且不重叠。

### Task 1: Core 社区链接定义与设置往返

**Files:**
- Modify: `src/Crystalfly.Core/Configuration/CrystalflySettings.cs`
- Create: `src/Crystalfly.Core/Configuration/SpeedrunCommunityLinkDefinition.cs`
- Test: `tests/Crystalfly.Core.Tests/Configuration/CrystalflySettingsStoreTests.cs`
- Test: `tests/Crystalfly.Core.Tests/Configuration/SpeedrunCommunityLinkDefinitionTests.cs`

**Interfaces:**
- Produces `SpeedrunCommunityLinkGroup` enum, `SpeedrunCommunityLinkDefinition` record and `SpeedrunCommunityLinkDefinition.TryNormalize(...)`.
- `CrystalflySettings.SpeedrunCommunityLinks` is `IReadOnlyList<SpeedrunCommunityLinkDefinition>` with an empty default.

- [ ] **Step 1: Write failing normalization and round-trip tests** covering valid HTTPS, HTTP rejection, credentials rejection, length rejection, duplicate-normalized values, invalid icon key, empty name/group, and old settings JSON without the new field.
- [ ] **Step 2: Run `dotnet test tests/Crystalfly.Core.Tests/Crystalfly.Core.Tests.csproj -c Release --filter "FullyQualifiedName~SpeedrunCommunityLinkDefinition|FullyQualifiedName~CrystalflySettingsStore"` and confirm new tests fail.
- [ ] **Step 3: Implement the record, enum, fixed icon-key validator, URI normalization, and settings property without changing schema version.
- [ ] **Step 4: Run the same filtered command and confirm all tests pass.
- [ ] **Step 5: Commit `feat: persist speedrun community links`.

### Task 2: App catalog and community ViewModel

**Files:**
- Create: `src/Crystalfly.App/ViewModels/SpeedrunCommunityLinksViewModel.cs`
- Create: `src/Crystalfly.App/ViewModels/SpeedrunCommunityLinkItemViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`
- Modify: the MainViewModel partial that owns settings initialization/save queue
- Test: `tests/Crystalfly.App.Tests/ViewModels/SpeedrunCommunityLinksViewModelTests.cs`

**Interfaces:**
- `SpeedrunCommunityLinksViewModel` exposes `IReadOnlyList<SpeedrunCommunityLinkItemViewModel> HollowKnightLinks`, `SilksongLinks`, `OtherLinks`, `CustomLinks`.
- Commands: `AddCustomLinkCommand`, `EditCustomLinkCommand`, `RemoveCustomLinkCommand`, `OpenLinkCommand`.
- `MainViewModel.SpeedrunCommunityLinks` exposes the child VM and initializes it from `CrystalflySettings.SpeedrunCommunityLinks`.

- [ ] **Step 1: Add failing tests for built-in catalog contents, invalid custom filtering, add/edit/remove persistence, duplicate rejection, and command enablement for built-in versus custom items.
- [ ] **Step 2: Run `dotnet test tests/Crystalfly.App.Tests/Crystalfly.App.Tests.csproj -c Release --filter FullyQualifiedName~SpeedrunCommunityLinksViewModel` and confirm failure.
- [ ] **Step 3: Implement the static catalog with the verified HK/Silksong Speedrun.com, HK Speedrunning, HK-Resources and Discord URLs; merge normalized custom settings and queue saves through existing settings infrastructure.
- [ ] **Step 4: Run the filtered tests and confirm pass.
- [ ] **Step 5: Commit `feat: add speedrun community catalog`.

### Task 3: Add/edit dialog and localization

**Files:**
- Create: `src/Crystalfly.App/Views/SpeedrunCommunityLinkDialog.axaml`
- Create: `src/Crystalfly.App/Views/SpeedrunCommunityLinkDialog.axaml.cs`
- Modify: `src/Crystalfly.App/Views/MainWindow.SpeedrunHandlers.cs`
- Modify: `src/Crystalfly.App/ViewModels/LocalizationViewModel.cs`
- Test: `tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs`

- [ ] **Step 1: Add structure tests for dialog labels, group selector, icon selector, URL validation message, save/cancel buttons, and community command bindings.
- [ ] **Step 2: Run the targeted structure test and confirm failure.
- [ ] **Step 3: Implement the dialog with existing Ursa overlay host and native labels/controls; return a normalized definition only on valid save.
- [ ] **Step 4: Add matching Simplified Chinese and English localization keys for titles, groups, validation, add/edit/remove, and community sections.
- [ ] **Step 5: Run the targeted UI tests and confirm pass.
- [ ] **Step 6: Commit `feat: add custom community link dialog`.

### Task 4: Startup community rail and responsive layout

**Files:**
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`
- Modify: `src/Crystalfly.App/Styles/*.axaml` files containing page/rail styles
- Test: `tests/Crystalfly.App.Tests/Ui/LayoutRenderingTests.cs`
- Test: `tests/Crystalfly.App.Tests/Ui/DocumentationScreenshotTests.cs`

- [ ] **Step 1: Add failing layout tests for the startup three-column grid, community headings/items, AutomationProperties.Name, and minimum-size visibility of launch/preflight actions.
- [ ] **Step 2: Run the affected tests and confirm failure.
- [ ] **Step 3: Change launch page columns to `224,*,272`, add a scrollable community rail with grouped item templates, add/edit/remove actions, and keep launch/preflight in the first two columns.
- [ ] **Step 4: Add the narrow-window layout trigger used by the existing styles so the community rail moves below the main content at 900×600 without overlaying it.
- [ ] **Step 5: Run affected UI tests at 900×600, 1280×720 and 1920×1080; update screenshots only when the new render is stable.
- [ ] **Step 6: Commit `feat: add startup speedrun community rail`.

### Task 5: Speedrun workbench and activity layout correction

**Files:**
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`
- Modify: speedrun style resources discovered in Task 4
- Test: `tests/Crystalfly.App.Tests/Ui/LayoutRenderingTests.cs`
- Test: `tests/Crystalfly.App.Tests/Ui/DocumentationScreenshotTests.cs`

- [ ] **Step 1: Add failing tests asserting the Tab switch is in normal top flow, the environment summary precedes configuration, and empty activity state does not occupy the star-sized list region.
- [ ] **Step 2: Run the affected tests and confirm failure.
- [ ] **Step 3: Move the Tab switch above the three-column content, keep the primary verification action in the summary, group RuntimePatches rows, and remove the fixed bottom overlap margin.
- [ ] **Step 4: Replace the activity star-row empty state with an auto-sized content panel while keeping the list scrollable when items exist; keep refresh/status/error adjacent to the action.
- [ ] **Step 5: Run layout and screenshot tests at all supported sizes, then inspect the three generated renders visually.
- [ ] **Step 6: Commit `fix: clarify speedrun workbench layout`.

### Task 6: Full verification and delivery evidence

**Files:**
- Create: `artifacts/speedrun-panel-workbench/MODIFIED_FILE`
- Create: `artifacts/speedrun-panel-workbench/DIFF.patch`
- Create: `artifacts/speedrun-panel-workbench/VERIFICATION.txt`
- Create: `artifacts/speedrun-panel-workbench/ROLLBACK.sh`

- [ ] **Step 1: Record the pristine hash of each modified source file and capture baseline targeted test output.
- [ ] **Step 2: Run `dotnet build .\\Crystalfly.slnx -c Release --no-restore` and `dotnet test .\\Crystalfly.slnx -c Release --no-build` in the modified worktree.
- [ ] **Step 3: Generate `DIFF.patch`, copy the primary modified file into `MODIFIED_FILE`, and write a portable rollback script that restores a target copy from pristine sibling bytes.
- [ ] **Step 4: Execute rollback against a separate copy, rerun the same focused UI input, and record restored hash, literal output, stderr and exit status.
- [ ] **Step 5: Reopen all four artifacts, verify the worktree is clean except for intentional committed changes, and commit `test: verify speedrun workbench redesign`.
