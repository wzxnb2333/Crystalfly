# 2026-09-25 缺陷复查记录

本轮重点检查更新恢复、文件事务、存档编辑、快照、下载队列、Steam 下载路径和预设分享服务。修复以下 9 类问题，并补充可复现旧行为的回归用例。

## 已修复问题

### 1. 更新恢复误读日志及链接路径越界

- 触发条件：同一父目录有多个便携安装；备份文件中存在同名 `operation.json`；恢复目录或其关键文件被替换为符号链接。
- 原行为：其他安装的日志会阻挡当前恢复；递归扫描会把备份文件当成日志；链接路径可能导致访问恢复目录之外的文件。
- 修复：只枚举直接子目录内的顶层日志；独立验证日志所属安装并按目标筛选；恢复与完成阶段校验关键路径的 reparse point。保留其他安装的恢复现场。
- 代码：[PortableUpdateInstaller.cs](../src/Crystalfly.Updater/PortableUpdateInstaller.cs)。Updater 新增 4 个用例，覆盖兄弟安装、同名文件及链接路径。

### 2. 存档数字精度与属性路径丢失

- 触发条件：存档包含超出 double 精确表示范围的整数、高精度小数、大指数，或包含点号、方括号、引号、空名称的属性。
- 原行为：即使没有修改数字，保存也可能改变其值；特殊属性名称会与层级路径混淆，导致错改或丢失字段。
- 修复：使用 JSON 数字原始表示完成读取与重建；编辑值必须是有效 JSON 数字；特殊属性使用带 JSON 转义的方括号路径，并支持嵌套数组和对象。
- 代码：[SaveGameEditor.cs](../src/Crystalfly.Core/Saves/SaveGameEditor.cs)。回归覆盖数字往返、非法数字拒绝、特殊键及嵌套数据。

### 3. 主 JSON 缺失时无法使用备份

- 触发条件：主文件不存在，但 `.bak` 仍有效。
- 原行为：读取直接抛出文件不存在异常；设置加载不能恢复备份。
- 修复：主文件缺失或 JSON 损坏时，在存在备份的前提下尝试备份；没有备份时仍返回真实异常，不吞掉取消或其他 I/O 错误。
- 代码：[AtomicJsonStore.cs](../src/Crystalfly.Core/Serialization/AtomicJsonStore.cs)。同时覆盖底层读取和实际设置加载。

### 4. 下载队列初始化失败后无法正常重试

- 触发条件：读取队列后，归一化状态写盘失败，例如队列文件被占用。
- 原行为：内存已标记初始化完成，重试直接返回，已有任务可能无法调度。
- 修复：先成功持久化，再发布初始化状态、待处理分组及调度器；初始化期间网络状态变化另行同步。
- 代码：[DownloadQueueService.cs](../src/Crystalfly.App/Downloads/DownloadQueueService.cs)。回归通过真实文件锁制造失败，验证解锁后可重试且任务只启动一次。

### 5. 文件事务恢复可能删除或写入目录之外

- 触发条件：终态日志的恢复目录被修改；回滚目标或备份路径被替换为链接；备份内存在同名 `journal.json`；主日志丢失但备份存在。
- 原行为：部分终态直接清理未验证的目录；回滚可能沿链接写入外部文件；递归扫描误读数据文件；备份日志无法被发现。
- 修复：所有状态分支先验证完整日志及目标、备份、恢复路径；拒绝链接路径；不安全日志保留为 `NeedsAttention`；只扫描顶层日志并识别 `.bak` 恢复入口。
- 代码：[FileTransaction.cs](../src/Crystalfly.Core/Transactions/FileTransaction.cs)。新增 6 个回归用例，检查外部文件保持不变及恢复现场保留。

### 6. 切换存档失败后保存到错误槽位

- 触发条件：已加载并编辑一个槽位，再切换到损坏存档，或切换操作被取消。
- 原行为：保存目标已切换，编辑内容仍来自旧槽位，后续保存可能覆盖另一个存档。
- 修复：加载成功后才提交槽位和编辑数据；失败恢复选择；用加载版本抑制过期异步结果；加载期间禁止保存，保存期间新增的修改保留未保存状态。
- 代码：[SaveEditorViewModel.cs](../src/Crystalfly.App/ViewModels/SaveEditorViewModel.cs)。新增真实加密存档回归，覆盖损坏读取和取消两种失败。

### 7. 快照编辑失败后数据与校验信息不一致

- 触发条件：已写入快照存档，但写 `snapshot.json` 失败；或目标存档路径经过符号链接。
- 原行为：存档已改变，元数据仍保存旧 hash，快照不能恢复；链接目标可能被覆盖。
- 修复：存档和快照编辑接入已有文件事务；修改前恢复待处理事务并验证路径与原 hash；暂存修改后的存档及元数据，再共同提交，失败时回滚。
- 代码：[NamedSnapshotService.cs](../src/Crystalfly.Core/Snapshots/NamedSnapshotService.cs)。回归覆盖元数据写入失败回滚、链接拒绝，以及成功编辑后 hash 匹配和快照恢复。

### 8. Steam 下载 staging 链接绕过目录边界

- 触发条件：staging 或其子目录是指向外部目录的符号链接。
- 原行为：仅做字符串范围校验，下载或清理临时块文件时可能访问外部路径。
- 修复：解析下载目标时检查路径及祖先的 reparse point，拒绝链接。
- 代码：[DownloadPath.cs](../src/Crystalfly.Steam/Downloads/DownloadPath.cs)。新增 3 个路径用例及 1 个下载集成用例，验证拒绝发生在下载块和删除临时文件之前。

### 9. 分享服务请求体限制在完整缓冲后才生效

- 触发条件：请求没有可信的 `Content-Length`，请求体持续超过 128 KiB。
- 原行为：先读取完整文本，再检查大小，超大请求仍会被全部缓冲。
- 修复：逐块累计字节数，超限立即取消读取并返回 413；在限额内完整解码 UTF-8，保留跨块中文字符。
- 代码：[http.ts](../services/preset-share/src/http.ts)。新增超限停止读取和 UTF-8 跨块两项回归。

## 验证结果

本轮先运行基线，再为缺陷补充失败回归，修复后执行定向验证及最终全量检查。所有下列最终命令退出码均为 0。

| 检查 | 最终结果 |
| --- | --- |
| Release 解决方案构建 | 0 警告，0 错误 |
| Crystalfly.App.Tests | 760 / 760 通过 |
| Crystalfly.Core.Tests | 724 / 724 通过 |
| Crystalfly.Steam.Tests | 68 / 68 通过 |
| Crystalfly.Updater.Tests | 38 / 38 通过 |
| .NET 合计 | 1590 / 1590 通过，0 失败，0 跳过 |
| preset-share | 16 / 16 通过；TypeScript 类型检查通过 |
| 发布与安装脚本测试 | 两个现有验证脚本均通过 |
| diff 格式检查 | 通过 |

.NET 基线为 1551 个通过用例，本轮最终增加 39 个；分享服务由 14 个增加至 16 个。

仓库根目录执行的最终命令：

```powershell
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet test '.\Crystalfly.slnx' -c Release --no-build --logger 'trx;LogFilePrefix=final' --results-directory artifacts/bug-review/final
pwsh -NoProfile -File scripts/test-build-release.ps1
pwsh -NoProfile -File scripts/test-build-and-install.ps1
git -c core.safecrlf=false diff --check
```

在 `services/preset-share` 执行：

```powershell
npm test -- --reporter=default --reporter=json --outputFile=../../artifacts/bug-review/service-tests.json
npm run typecheck
```

本地证据保存在忽略目录 `artifacts/bug-review/`：基线位于 `baseline/`，失败复现与定向验证位于 `reproductions/`、`fixed/`，最终 .NET TRX 位于 `final/`，服务结果为 `service-tests.json`。

## 验证边界

- 使用测试夹具验证文件占用、取消、损坏数据、链接路径、失败回滚和恢复；未进行真实断电实验。
- 未登录真实 Steam 账号、下载真实 CDN 游戏内容，亦未连接生产分享服务或 Redis；对应验证使用现有测试替身。
- App 验证包含现有 headless UI 测试，未进行真实游戏存档人工往返或桌面端完整手工操作验收。
- 发布和安装脚本只运行其测试脚本；未生成发布包、覆盖已安装应用、提交或发布代码。
