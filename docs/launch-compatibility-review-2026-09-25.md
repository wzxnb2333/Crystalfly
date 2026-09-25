# 接管版本、加载器、模组与启动可靠性复查

日期：2026-09-25。本文记录磁盘游戏接管复查之后，针对其他用户环境的第二轮专项检查。

## 已复现并修复

| 场景 | 修复前行为 | 当前行为 |
| --- | --- | --- |
| 预检成功后磁盘断开、Loader 凭据被占用或 JSON 损坏 | 继续沿用上次的可启动状态，或 JSON 异常直接逃出命令 | 每次重查先清空旧结果；失败保留具体错误，普通启动和强制启动均不能绕过未完成的检查 |
| Windows 无法创建游戏进程 | Win32Exception 未捕获；已经切入的隔离存档未立即恢复，互斥锁影响后续启动 | 捕获原生启动错误；恢复原始共享存档、保留实例存档并释放会话，支持再次尝试 |
| 游戏版本与 Loader 不匹配、已启用模组要求另一个 Loader | 只看 Loader 类型，错误组合也能获得正常启动许可 | 对照 Loader 支持的构建和模组记录中的 Loader 身份；停用模组不参与兼容性阻断 |
| 接管外部 Loader | 接管记录容易被当成兼容性证明 | 明确显示兼容性未验证警告；确认绑定到实例、构建和 Loader 身份；原有模组警告确认记录继续兼容 |
| 软件打开期间游戏被外部更新，或核心数据文件消失 | 启动仍使用已扫描的旧版本，只检查 exe 是否存在 | 实际启动前重算游戏指纹，并检查 globalgamemanagers 与目录中已知构建要求的 UnityPlayer.dll |
| Loader 凭据为空、包含 null 或使用不支持的版本 | 空凭据可被判定正常；损坏内容造成空引用异常 | 空文件清单判定为漂移；无效结构、SHA-256、重复条目和不支持的 schema 明确报为数据错误 |
| BepInEx 实例保留已经停用的 Modding API 模组 | 误报两个 Loader 冲突 | 排除 Mods/Disabled 中的停用模组，仍检查活动目录和实际 Loader 文件 |
| 接管 Modding API 后游戏主程序集被覆盖 | 只跟踪 MMHOOK，Assembly-CSharp.dll 变化未被识别 | 新接管记录同时保存已存在的 Assembly-CSharp.dll 指纹 |
| 模组主凭据丢失，但 .bak 仍存在 | 原有模组被当成未接管内容，依赖和归属信息丢失 | 使用既有原子 JSON 恢复路径读取备份；卸载同时清理主凭据和备份，避免重新出现 |
| 旧版模组记录的文件列表损坏 | 迁移时发生 ArgumentNullException 或 NullReferenceException | 迁移前校验旧版文件列表，保留明确的数据错误 |
| 接管模组重新关联目录条目时写入失败 | 先删旧记录，再写新记录，可能失去唯一有效记录 | 使用现有文件事务同时更新新记录和备份、移除旧记录，失败保持原状态 |

旧版重新关联留下的孤立备份，仅当一个有效主凭据明确接管同样的文件、哈希、目录和启用状态时，才会保留原字节并改名为 `.relinked-<id>.bak`。不明确的交叉归属仍报错，不自动猜测或删除内容。

Loader 安装记录新增可选的 `SupportedBuildIds`，用于保留本地安装清单的构建范围。旧记录缺少此字段仍可读取；官方记录优先采用当前目录中的兼容性清单。

## 回归验证

本轮新增 33 个回归案例：App 18 个、Core 15 个。先运行修复前用例确认故障，再执行修复后的专项与全量验证。

- `LaunchReliabilityTests`：18 个通过，包含中文和空格路径、普通/强制启动、磁盘不可用、凭据占用及损坏、兼容性检查、确认后重试、外部更新、核心文件缺失。
- 原生启动失败注入覆盖错误码 5、193、740、1223；每种错误均连续尝试两次，并验证共享存档、实例存档及事务状态。
- Loader 与既有启动预检专项：89 个通过。
- 模组凭据迁移及重新关联专项：15 个通过。
- 最终全量验证：1,656 个通过，0 失败、0 跳过；Core 763、App 787、Steam 68、Updater 38。
- 首轮全量运行中，App 有 14 个旧测试因反射调用未补齐新增可选参数而失败；修正测试辅助方法后，实例状态与启动可靠性专项 152 个通过，随后重新完成上述全量验证。首轮失败记录保留以便追溯。
- Release 构建成功，0 警告、0 错误；差异格式检查通过。

本轮实际执行的验证命令及退出码：

```powershell
# 退出码 0；0 警告、0 错误
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
# 首轮退出码 1；上述测试辅助方法的问题随后修复
dotnet test '.\Crystalfly.slnx' -c Release --no-build --logger 'trx;LogFilePrefix=launch-final' --results-directory artifacts/bug-review/launch-compatibility/full
# 退出码 0；152 个通过
dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~MainViewModelStateTests|FullyQualifiedName~LaunchReliabilityTests' --logger 'trx;LogFileName=app-state-fixed.trx' --results-directory artifacts/bug-review/launch-compatibility
# 退出码 0；脚本逐个运行全部四个测试项目，共 1,656 个通过，并完成发布构建及本地覆盖安装
pwsh -NoProfile -File ./scripts/build-and-install.ps1 -UnsignedLocal
# 退出码 0
git -c core.safecrlf=false diff --check
```

详细测试记录位于 `artifacts/bug-review/launch-compatibility/`。

## 本地覆盖安装

已通过现有脚本覆盖 `D:\Program Files\Crystalfly`，保持版本号 1.1.5。构建及安装流程退出码为 0，未执行公开发布。

- 发布目录的 247 个文件与安装目录逐一进行 SHA-256 比较，全部一致。
- 安装后的 App 与 Core 程序集哈希均与覆盖前不同，确认新修复已安装；不以未变的版本号代替更新验证。
- 原有设置文件的存在状态与 SHA-256 均保持不变；安装目录没有 `portable.flag`。
- Windows x64 ZIP 与安装程序均匹配本轮 `SHA256SUMS.txt`。
- 安装证据保留在 `artifacts/bug-review/launch-compatibility/local-install/` 的 `build-and-install.log`、`result.json`、`before.json` 与 `after.json`；安装后核验退出码为 0。

启动已安装版本：

```powershell
& 'D:\Program Files\Crystalfly\Crystalfly.App.exe'
```

## 验证边界

- Windows 启动错误通过进程创建入口注入，实际执行了存档切入、切出和互斥锁释放；没有启动真实游戏或修改真实玩家存档。
- 未在其他玩家的实体电脑上实测，也未逐一执行所有第三方模组。通过文件、依赖和版本检查，不代表模组运行逻辑必然无错。
- 保留原有明确确认后的强制启动能力；兼容性警告的确认不改变 Loader 的未验证身份。
- Assembly-CSharp.dll 的新增指纹跟踪适用于本轮之后生成的接管记录；不会补造历史接管时的文件哈希。
- 不做提交或公开发布。
