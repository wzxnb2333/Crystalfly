# Crystalfly 0.6.1 补齐 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将遗漏的 UI、配置、存档编辑、动画和目录防护功能安全整合到最新 main，并作为 0.6.1 发布。

**Architecture:** 保持 `origin/main` 作为唯一产品基线；从 `E:\AI_collection\crystalfly` 的未提交工作复制最小产品差异，而非回退整个旧工作树。新增的核心读写服务维持实例/快照范围、原子写入和进程互斥；UI 只绑定现有 MainViewModel 与实例选择状态。

**Tech Stack:** .NET 10、Avalonia 12、Semi.Avalonia、Irihi.Ursa、CommunityToolkit.Mvvm、xUnit、Avalonia.Headless.XUnit、PowerShell 7。

## Global Constraints

- 基线固定为 `origin/main` 的 `758058a`，来源工作树不得被重置、切换或清理。
- 排除 `.qoder`、临时分析输出和没有运行时代码引用的 `textures/` 内容。
- 存档编辑限定当前实例或其命名快照内的 `user1.dat` 至 `user4.dat`。
- 加载动画只做 `RenderTransform` 位移，保留减少动态效果设置，固定在内容区中心。
- Release 使用新标签 `v0.6.1`，保留 v0.6.0 的资产与哈希历史。

---

### Task 1: 建立可复核的来源清单与回归基线

**Files:**
- Create: `docs/superpowers/specs/2026-07-23-061-ui-config-animation-recovery-design.md`
- Create: `docs/superpowers/plans/2026-07-23-061-ui-config-animation-recovery.md`
- Modify: `.gitignore` only if an imported generated path would otherwise be tracked.

**Interfaces:**
- Consumes: dirty source at `E:\AI_collection\crystalfly` based on `7cc9be4`.
- Produces: a source allowlist covering app/core/tests/assets and an exclusion list for generated content.

- [ ] **Step 1: Verify the clean integration baseline**

Run: `dotnet test .\Crystalfly.slnx -c Release --no-build`

Expected: all Core, App, Steam and Updater tests pass before source integration.

- [ ] **Step 2: Record the source boundaries**

Allow only runtime code, tests and `Assets\knight-icon.png` / `Assets\knight-walk.png`; compare every modified tracked file against `origin/main` before copying.

- [ ] **Step 3: Commit the design and plan documents**

Run: `git add docs/superpowers && git commit -m "docs: plan 0.6.1 recovery integration"`

Expected: documentation commit precedes production migration.

### Task 2: Restore isolated configuration and save editing core

**Files:**
- Create: `src/Crystalfly.Core/Configuration/IniDocument.cs`
- Create: `src/Crystalfly.Core/Configuration/AppConfigService.cs`
- Create: `src/Crystalfly.Core/Saves/SaveFileCodec.cs`
- Create: `src/Crystalfly.Core/Saves/SaveGameEditor.cs`
- Modify: `src/Crystalfly.Core/Snapshots/NamedSnapshotService.cs`
- Create: `tests/Crystalfly.Core.Tests/Configuration/IniDocumentTests.cs`
- Create: `tests/Crystalfly.Core.Tests/Configuration/AppConfigServiceTests.cs`
- Create: `tests/Crystalfly.Core.Tests/Saves/SaveFileCodecTests.cs`
- Create: `tests/Crystalfly.Core.Tests/Saves/SaveGameEditorTests.cs`
- Modify: `tests/Crystalfly.Core.Tests/Snapshots/NamedSnapshotServiceTests.cs`

**Interfaces:**
- Produces `IniDocument.Parse(string)`, `AppConfigService.LoadAsync/SaveAsync`, `SaveGameEditor.Flatten/Rebuild`, and `NamedSnapshotService.ListSaveSlotsAsync/DecryptSaveAsync/UpdateSaveAsync`.
- `UpdateSaveAsync(instanceId, snapshotId, slotRelativePath, json, token)` accepts only the four exact game slots and refreshes named snapshot metadata hash after writing.

- [ ] **Step 1: Add failing core tests**

Cover malformed INI preservation, atomic AppConfig writes, encrypted save round trips, flatten/rebuild scalar edits, slot range rejection, path traversal rejection, and snapshot hash refresh after an edit.

- [ ] **Step 2: Run only the new core tests before migration**

Run: `dotnet test .\tests\Crystalfly.Core.Tests\Crystalfly.Core.Tests.csproj -c Release --filter "FullyQualifiedName~Configuration|FullyQualifiedName~Saves|FullyQualifiedName~NamedSnapshot"`

Expected: RED because the services have not yet been imported.

- [ ] **Step 3: Import the core services and harden containment**

Use atomic temporary writes, `FileOptions.WriteThrough`, the existing process guard, and a separator-aware base-path containment comparison. Keep the current instance LocalLow and named snapshot directory model; do not add a shared LocalLow path.

- [ ] **Step 4: Re-run the focused core tests**

Run: same command as Step 2.

Expected: PASS.

- [ ] **Step 5: Commit the core migration**

Run: `git add src/Crystalfly.Core tests/Crystalfly.Core.Tests && git commit -m "feat: add isolated config and save editing core"`

### Task 3: Integrate configuration and save editor view models

**Files:**
- Create: `src/Crystalfly.App/ViewModels/GameConfigViewModel.cs`
- Create: `src/Crystalfly.App/ViewModels/SaveEditorViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/MainViewModel.cs`
- Modify: `src/Crystalfly.App/ViewModels/LocalizationViewModel.cs`
- Create: `tests/Crystalfly.App.Tests/ViewModels/GameConfigViewModelTests.cs`
- Modify: `tests/Crystalfly.App.Tests/ViewModels/DialogViewModelTests.cs`

**Interfaces:**
- `GameConfigViewModel` owns one instance-local `AppConfig.ini`, preserves unknown INI fields and exposes dirty/save/reset state.
- `SaveEditorViewModel` initializes slots asynchronously, updates only `user1.dat`–`user4.dat`, and exposes `Entries`, `Slots`, `IsLoaded`, `IsDirty` and `SelectedSlot`.
- `MainViewModel` creates both models only after resolving the selected instance and cancels their work through its existing lifetime cancellation token.

- [ ] **Step 1: Add failing view-model tests**

Cover accessibility slider write-through, unknown INI value retention, four-slot limit, async loading state, save/reset behavior, current instance isolation, and cancellation propagation.

- [ ] **Step 2: Implement model integration without UI blocking**

Run parsing and save flattening on the existing asynchronous path; marshal only observable collection replacement back to the UI continuation. Do not call synchronous file I/O on an Avalonia event handler.

- [ ] **Step 3: Add localization keys**

Add paired `zh-CN` and `en-US` strings for configuration, save editor, slot selection, save/reset, busy, isolation and validation errors.

- [ ] **Step 4: Run focused App view-model tests**

Run: `dotnet test .\tests\Crystalfly.App.Tests\Crystalfly.App.Tests.csproj -c Release --filter "FullyQualifiedName~GameConfig|FullyQualifiedName~SaveEditor|FullyQualifiedName~DialogViewModel"`

Expected: PASS.

- [ ] **Step 5: Commit the view-model migration**

Run: `git add src/Crystalfly.App/ViewModels tests/Crystalfly.App.Tests/ViewModels && git commit -m "feat: add instance config and save editors"`

### Task 4: Restore window layout, loading animation and editor surfaces

**Files:**
- Create: `src/Crystalfly.App/Assets/knight-icon.png`
- Create: `src/Crystalfly.App/Assets/knight-walk.png`
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml`
- Modify: `src/Crystalfly.App/Views/MainWindow.axaml.cs`
- Modify: `src/Crystalfly.App/Styles/CrystalflyTheme.axaml`
- Create: `tests/Crystalfly.App.Tests/Ui/SaveEditorRenderingTests.cs`
- Modify: `tests/Crystalfly.App.Tests/Ui/LayoutRenderingTests.cs`
- Modify: `tests/Crystalfly.App.Tests/Ui/MainWindowStructureTests.cs`
- Modify: `tests/Crystalfly.App.Tests/Ui/ThemeRenderingTests.cs`

**Interfaces:**
- Root `LoadingContainer` has a centered `GlobalKnightLoadingViewport`; save editor has a corresponding local viewport.
- Code-behind starts and stops only the two named walk images, respects reduced motion, and modifies only image `RenderTransform`.
- Main window binds configuration and save editors through `MainViewModel`, with zero layout animation on containers.

- [ ] **Step 1: Add failing UI structure tests**

Assert both image assets are Avalonia resources, global and editor loading indicators are centered, the config page exposes save/reset, and the editor displays only current-instance slots.

- [ ] **Step 2: Integrate the source UI surgically**

Merge only editor/config sections and layout improvements into the current MainWindow. Keep the existing 0.3–0.6 launch warning, Mod health, preset, queue, protocol and update sections intact.

- [ ] **Step 3: Apply animation constraints**

Use a larger fixed image viewport centered with `HorizontalAlignment="Center"` and `VerticalAlignment="Center"`; pause the loop when the host is hidden or reduced motion is enabled; animate translation only.

- [ ] **Step 4: Run headless layout tests**


Run: `dotnet test .\tests\Crystalfly.App.Tests\Crystalfly.App.Tests.csproj -c Release --filter "FullyQualifiedName~LayoutRendering|FullyQualifiedName~MainWindowStructure|FullyQualifiedName~ThemeRendering|FullyQualifiedName~SaveEditorRendering"`

Expected: PASS.

- [ ] **Step 5: Commit the UI migration**

Run: `git add src/Crystalfly.App tests/Crystalfly.App.Tests/Ui && git commit -m "feat: restore editor surfaces and loading animation"`

### Task 5: Restore source-side catalog, dependency and snapshot safeguards

**Files:**
- Modify: `src/Crystalfly.Core/Catalog/CatalogProvider.cs`
- Modify: `src/Crystalfly.App/ViewModels/Dialogs/DependencyPlanDialogViewModel.cs`
- Modify: `src/Crystalfly.App/Views/Dialogs/DependencyPlanDialogView.axaml`
- Create: `src/Crystalfly.App/ViewModels/Dialogs/DependencyTreeConnectors.cs` only when it is referenced by the dialog.
- Modify: `tests/Crystalfly.Core.Tests/Catalog/CatalogProviderTests.cs`
- Modify: `tests/Crystalfly.Core.Tests/Catalog/EmbeddedCatalogTests.cs`

**Interfaces:**
- Remote catalog filtering keeps only known embedded builds, loader mappings and dependent Mod/speedrun content before cache persistence.
- Dependency dialog preserves node relationship details without changing queue execution or dependency install rules.

- [ ] **Step 1: Add failing catalog regression tests**

Cover remote build/loader filtering, zero-hash/unknown build rejection, and cache fallback behavior.

- [ ] **Step 2: Merge only source safeguards that remain absent from origin/main**

Compare every hunk to current catalog and dialog code; retain current 0.4/0.5 custom source, Mod health and preset behavior when a source hunk predates it.

- [ ] **Step 3: Run focused catalog and dialog tests**

Run: `dotnet test .\Crystalfly.slnx -c Release --filter "FullyQualifiedName~CatalogProvider|FullyQualifiedName~EmbeddedCatalog|FullyQualifiedName~DialogViewModel"`

Expected: PASS.

- [ ] **Step 4: Commit the safeguards**

Run: `git add src/Crystalfly.Core/Catalog src/Crystalfly.App/ViewModels/Dialogs src/Crystalfly.App/Views/Dialogs tests/Crystalfly.Core.Tests/Catalog tests/Crystalfly.App.Tests/ViewModels && git commit -m "fix: retain catalog and dependency safeguards"`

### Task 6: Produce, verify and publish the corrective release

**Files:**
- Modify: `Directory.Build.props`
- Modify: `README.md`
- Create: `docs/releases/0.6.1.md`
- Modify: `scripts/build-release.ps1`
- Modify: `scripts/build-and-install.ps1`
- Modify: `scripts/test-build-release.ps1`
- Modify: `scripts/test-build-and-install.ps1`

**Interfaces:**
- Version becomes `0.6.1`; release assets are `Crystalfly-0.6.1-win-x64-portable.zip`, `Crystalfly-0.6.1-win-x64-setup.exe`, `update-manifest.v1.json` and `SHA256SUMS.txt`.
- The installed application continues to use `D:\Program Files\Crystalfly` and preserves application data and instance roots.

- [ ] **Step 1: Update version and user documentation**

Describe the restored editor/config/animation behavior and state that all edits are instance-isolated.

- [ ] **Step 2: Run fresh full verification**

Run:

```powershell
dotnet restore .\Crystalfly.slnx
dotnet build .\Crystalfly.slnx -c Release --no-restore
dotnet test .\Crystalfly.slnx -c Release --no-build
pwsh -NoProfile -File .\scripts\test-build-release.ps1
pwsh -NoProfile -File .\scripts\test-build-and-install.ps1
git diff --check
```

Expected: zero build errors, all tests passing, scripts passing and no whitespace errors.

- [ ] **Step 3: Build and install 0.6.1**

Run: `pwsh -NoProfile -File .\scripts\build-and-install.ps1 -Version 0.6.1`

Expected: portable ZIP, installer, signed manifest and hashes are generated; existing application data remains intact.

- [ ] **Step 4: Smoke-test the installed application**

Verify the center loading animation, selected-instance configuration, exactly four save slots, non-blocking editor load, and preservation of existing instance/queue settings.

- [ ] **Step 5: Commit, PR, merge and release**

Run: create a merge-commit PR to `main`, tag `v0.6.1`, upload the four artifacts, re-download them and compare SHA-256 with `SHA256SUMS.txt`.
