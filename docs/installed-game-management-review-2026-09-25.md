# 2026-09-25 安装版磁盘游戏托管复查

本次检查安装模式下的磁盘游戏发现、接管、版本识别、目录切换、重启恢复，以及迁移后的存档、快照和 Loader 状态。此前的通用缺陷复查保留在 `bug-review-2026-09-25.md`，两份记录的测试计数对应各自完成时的代码。

## 已修复问题

### 1. 迁移游戏目录时遗漏托管数据

- 原行为仅移动游戏目录，没有迁移同级 `.crystalfly/instances/<id>`。隔离存档、命名快照、Loader 备份和 Mod 元数据可能留在原版本目录。
- 现在先复制并校验托管状态，按文件清单和 SHA-256 验证内容，再发布状态与游戏目录。目标状态已存在时拒绝覆盖；游戏发布失败则撤回已发布的状态副本，保留源游戏和源状态。
- 快照的 `SourcePath`、`SnapshotPath` 与 Loader 的 `BackupRoot` 会在私有暂存目录中校验并重定位；元数据备份同步使用有效的新路径。越界路径和不支持的元数据版本会在移动源文件之前被拒绝。
- 新增真实 Loader 安装→迁移→卸载、命名快照创建→迁移→恢复回归。源目录清理失败时保留剩余源数据，返回明确的清理失败结果。
- 代码：`GameDirectoryMigrationService.cs`；测试：`GameDirectoryMigrationServiceTests.cs`。

### 2. 手动复制游戏共用实例身份

- 原行为直接信任复制过来的标记，同一版本目录中的原游戏和副本可能共用实例 ID、隔离存档和托管状态。
- 现在结合元数据记录的原目录和原标记确认归属，为副本生成独立 ID；同一根目录的并发发现串行登记，避免创建多个身份。
- 标记备份同步保存当前身份，防止副本丢失主标记后恢复成原游戏 ID。回归覆盖副本排序在原游戏之前、并发扫描、删除副本保留原存档，以及副本标记恢复。
- 代码：`InstanceImportService.cs`、`InstanceSidecar.cs`。

### 3. 一个损坏实例导致正常游戏全部消失

- 损坏的实例标记、元数据或 Loader 收据原本可能中断整批扫描。
- 现在逐实例收集问题，继续显示正常游戏，并显示出错目录与部分失败状态。取消仍然传播，不会被转换成成功扫描。
- 根目录恢复或访问失败时清除过期实例和选中项，避免继续操作已经不可用的游戏。
- 代码：`InstanceDiscoveryResult.cs`、`InstanceImportService.cs`、`MainViewModel.cs`；测试包含损坏标记、损坏 Loader 元数据和失效刷新。

### 4. 主文件缺失时丢失托管关联

- 修正实例标记、实例元数据及 Loader 收据外层的存在性判断，允许现有 JSON 备份恢复逻辑处理主文件缺失。
- 迁移后的 Loader 测试实际删除主收据，再验证备份可读；随后破坏主收据并执行卸载，确认原始程序集内容恢复。
- 市场安装的 headless UI 回归分别覆盖主标记丢失但备份有效时完成安装，以及主标记和备份全部丢失时安全阻止安装。
- 代码：`InstanceSidecar.cs`、`LoaderManager.cs`。

### 5. 磁盘文件变化后继续使用旧版本标签

- 已登记实例会重新计算版本指纹，更新能够验证的版本；实际文件不匹配时不再沿用已知版本标签。此前未知的版本也能够随版本索引（catalog）更新而识别。
- Steam manifest 身份仍保留其未验证语义；版本索引尚未加载或离线不可用时，不因空索引而清除已有身份。
- 代码：`InstanceImportService.cs`；测试覆盖未知转已知、内容变化降级、manifest 身份及空版本索引。

### 6. 注册失败和磁盘断开留下错误界面状态

- 在登记到设置之前检查游戏完整性、路径边界及游戏目录/元数据目录写权限，避免创建元数据失败后仍保存一个虚假的成功登记。
- 磁盘不可用时清除游戏列表、计数和选择；磁盘恢复后可以重新发现相同 ID。目录激活任务处理取消和预期 I/O 异常。
- 安装模式回归实际执行登记、持久化、释放实例、重新创建应用视图模型并加载，验证目录和身份保持一致。
- 代码：`InstanceDirectory.cs`、`InstancesViewModel.cs`、`MainViewModel.cs`；测试：`InstalledGameManagementTests.cs`。

### 7. 目录切换时旧扫描结果覆盖当前目录

- 扫描固定根目录并记录请求版本，丢弃过期扫描结果。Loader、Mod 管理器从实例所属路径解析托管目录，不再依赖可能已经切换的当前根目录。
- 回归在扫描暂停期间切换根目录，验证旧结果不会成为新目录中的可操作实例，也不会在新目录创建旧实例状态。
- 代码：`MainViewModel.cs`；测试：`InstalledGameManagementTests.cs`。

### 8. 符号链接和目录联接绕过托管边界

- 扫描遍历进入每一层之前验证祖先路径；手动接管、标记读写、迁移及恢复入口同样校验 reparse point。
- 对不安全的链接目录跳过或明确拒绝；测试验证外部目录内容保持不变。
- 代码：`GameDirectoryTreeScanner.cs`、`GameDirectoryScanner.cs`、`InstanceDirectory.cs`、`InstanceSidecar.cs` 及应用恢复入口。

## 验证

各问题先用失败回归确认，再修复并执行定向测试。最终验证于 2026-09-25 完成，下列最终命令退出码均为 0。

| 检查 | 最终结果 |
| --- | --- |
| Release 解决方案构建 | 0 警告，0 错误 |
| Crystalfly.Core.Tests | 748 / 748 通过 |
| Crystalfly.App.Tests | 769 / 769 通过 |
| Crystalfly.Steam.Tests | 68 / 68 通过 |
| Crystalfly.Updater.Tests | 38 / 38 通过 |
| .NET 合计 | 1623 / 1623 通过，0 失败，0 跳过 |
| 市场安装标记/备份两分支定向 UI 回归 | 2 / 2 通过 |
| diff 格式检查 | 通过 |

本专项相对此前已完成的 1590 个 .NET 用例新增 33 个，并加强现有迁移、副本身份恢复和市场安装用例。最终完整构建后没有再修改产品代码或测试代码。

仓库根目录执行：

```powershell
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet test tests/Crystalfly.App.Tests -c Release --no-build --filter FullyQualifiedName~Market_install_missing_sidecar_uses_backup_or_fails_safely --logger 'trx;LogFilePrefix=marker-recovery-ui' --results-directory artifacts/bug-review/disk-management
dotnet test '.\Crystalfly.slnx' -c Release --no-build --logger 'trx;LogFilePrefix=final' --results-directory artifacts/bug-review/disk-management/final-validated
git -c core.safecrlf=false diff --check
```

最终完整回归证据位于忽略目录 `artifacts/bug-review/disk-management/final-validated/`，失败复现与定向验证证据位于其父目录。中途的失败复现、旧用例前提调整和文件占用导致的构建重试不计为通过证据；最终采用等待前一测试进程结束后串行构建、定向验证和完整回归的结果。

## 验证边界

- 使用临时游戏文件和独立存档目录，未修改用户实际游戏、存档或 Steam 库。安装路径与托管状态的分离经过代码检查和安装模式视图模型测试。
- 磁盘断开/重连使用目录移走/放回模拟；跨卷迁移使用注入的跨卷判定执行真实复制、校验和删除分支。未进行真实拔盘、断电或跨物理磁盘硬件验收。
- 迁移保持先复制再发布、最后清理源文件。游戏与状态是两个目录，进程在发布窗口中被强杀仍可能留下暂存/重复状态，尚不能宣称具有完整的断电事务恢复保证。
- 未对用户已存在的历史异常数据批量迁移或修复，也未验证通过资源管理器任意移动整个托管根目录后的所有绝对路径。游戏迁移应使用应用提供的迁移流程。
- 最终结果仅代表所列自动化检查；未启动真实游戏、登录 Steam 或执行完整桌面人工验收。未生成发布安装包、覆盖当前已安装程序、提交或发布代码。源码修复需更新已安装程序后生效。
