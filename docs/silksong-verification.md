# 丝之歌 / Thunderstore 验证记录

核验日期：2026-09-28（Asia/Shanghai）；Steam 原始证据时间为 2026-09-27 17:25 / 17:36 UTC。本文件描述实际验证范围，不将版本指纹匹配等同于全部模组兼容或完整游玩验收。

## 真实 Steam 核验

使用现有授权 Steam 会话读取 PICS，解析 App 1030300、Windows Depot 1030301 的公开版 Manifest。候选历史 Manifest 实际从 Steam 下载成功后才收录。分别下载 EXE、UnityPlayer.dll、globalgamemanagers、Assembly-CSharp.dll，经 Steam 内容/分块校验后计算联合指纹和读取实际版本。

| 构建 | Manifest | 核验结果 |
| --- | --- | --- |
| 1.0.30000 | 4421626056705534276 | 当次 public；实际版本及文件指纹一致 |
| 1.0.29980 | 468692862190470536 | 当次可访问历史稳定版；实际版本及文件指纹一致 |

两版 EXE SHA-256 相同：5957B04989DE6E242938F5EE0FC745A5B1AF42DA8CCA44B0E6F8DAF30D836496。

两版 UnityPlayer SHA-256 相同：5EADAEC116A9810C68A3C67BC9AD07C6F233BADEB06533987CE27DEFF471B21D。

globalgamemanagers：

- 1.0.30000：6B68A9CAFD0856EFC8357AF92F939E80564C361F77FB2C61AB067BB59356DA49
- 1.0.29980：3C85E9B49150463F2C8898E79E56D469CC0998399EDEC704BEE678F2BCF8607C

因此不能仅靠 EXE 判断这两版。证据在忽略的 artifacts/silksong-verification/public/evidence.json 与 history/evidence.json，不含登录令牌。历史版核验为指纹文件下载，不宣称历史版全量下载或所有历史版本已实际启动。

## 2026-10-06 历史版本扩充边界

公开版本列表仅用于发现候选 Manifest，不作为收录证据。本轮从 SteamDB 页面记录了 Windows Depot 1030301 的候选清单：4421626056705534276、468692862190470536、3703686001292550981、3853982342899707391、3545882420322545098、426651197780377263、3690203822520536668、5977483240701257214、6773297648406051922、3900764848237536293、8642535143474926050、539129767115354441、3229726349000518284。已核验的前两项仍按上表处理，其余只进入应用的“未核验历史 Manifest”下载入口；日期是 SteamDB 的首次出现日期，不是游戏版本号。此前公开列表中标注的 1.0.28324、1.0.28497、1.0.28650 仍需真实文件下载后再确认。列表来源为 [Silksong-Resources manifests](https://github.com/hk-speedrunning/Silksong-Resources/blob/main/manifests.md) 及用户提供的 SteamDB 页面。

复用 Crystalfly 自有 DPAPI Steam 会话，在 Windows Depot 1030301 对已核验控制 Manifest 468692862190470536 做了延长 SteamKit CDN 超时（60 秒）和 CDN 直连两次诊断（约 62.7 秒、63.3 秒）。Steam 返回的去敏内容服务器为 `cache2-tyo3.steamcontent.com`，没有 Valve 代理服务器；两次均在 `SteamKit2.CDN.Client.DownloadManifestAsync` 阶段以 `TaskCanceledException` 结束，目标目录没有文件。因此本轮没有把任何新候选写入 catalog，也没有把 Manifest 名称或公开版本号当作实际文件版本。候选历史版仍需一次真实 Steam Manifest 访问、四个指纹文件下载及实际版本读取后才可收录。

下载列表会继续保留动态 `public` 入口和现有两个固定核验版本（1.0.30000、1.0.29980）。固定条目的“文件已核验”只表示内置基线的 Depot、Manifest 与 EXE/UnityPlayer/globalgamemanagers/Assembly-CSharp 联合证据匹配，不表示模组兼容或完整启动验收；日期没有可靠官方逐版本证据时保持空白。

## 2026-10-06 历史 Manifest 接续验收

用户提供的 SteamDB 快照包含 13 条 Windows Depot 1030301 清单：两个已有核验版本按 Manifest 去重，另 11 条显示为“版本与文件未核验”的历史入口，保留动态 public。支持按 Manifest ID 搜索、选择并加入同游戏的下载队列；首次出现日期单独展示，不作为发布日期。手动清单弹窗跟随当前游戏显示 AppID/Depot，并将候选与已收录构建区分。

接续时复现了真实下载入口的阻塞：`UniqueInstanceName` 使用 `Hollow Knight: Silksong`，其中冒号被 Windows 目录名检查拒绝，下载命令捕获异常后队列为空。共用函数改用游戏定义中已有的安全目录名 `Hollow Knight Silksong`，覆盖普通版本与历史入口，保留实例路径校验和名称冲突处理。新增断言确认没有隐藏错误、目标目录及 Manifest 正确，两种游戏都能跳过已存在文件。

最终定向回归退出码 0：17 通过、0 失败、0 跳过，TRX 位于 `artifacts/test-results/manifest-history-final/manifest-history-final.trx`。

```powershell
dotnet test '.\tests\Crystalfly.App.Tests' -c Release --no-restore --filter 'FullyQualifiedName~Silksong_history|FullyQualifiedName~Silksong_download_projection|FullyQualifiedName~DownloadBuildUiTests|FullyQualifiedName~Download_instance_name|FullyQualifiedName~Steam_download_command_enqueues_selected_build|FullyQualifiedName~Historical_manifest_dialog' --logger 'trx;LogFileName=manifest-history-final.trx' --results-directory artifacts/test-results/manifest-history-final
```

此前失败结果保留在 `artifacts/test-results/manifest-history-resume/manifest-history-resume.trx`；该结果不作为通过证据。此次接续没有访问 Steam CDN、下载游戏或做图像识别，因此不宣称 11 条候选的实际下载可用性、真实版本号或模组兼容性已核验。

本地覆盖命令 `pwsh -NoProfile -File .\scripts\build-and-install.ps1 -UnsignedLocal` 退出码 0，安装到 `D:\Program Files\Crystalfly`，版本 1.1.5。发布目录的 248 个文件与安装文件 SHA-256 全部一致，哈希检查退出码 0；记录位于 `artifacts/manifest-history-install-verification-20261006.json`，构建日志位于 `artifacts/manifest-history-build-install-20261006.log`，安装器为 `artifacts/installer/Crystalfly-1.1.5-win-x64-setup.exe`。`git -c core.safecrlf=false diff --check` 退出码 0。未提交或推送，未做真实桌面视觉验收。

启动命令：

```powershell
& 'D:\Program Files\Crystalfly\Crystalfly.App.exe'
```

## 2026-10-06 历史版本显示调整

按用户要求，13 条清单依据 [Silksong-Resources manifests](https://github.com/hk-speedrunning/Silksong-Resources/blob/main/manifests.md) 对应 12 个版本，历史列表直接显示版本号，移除历史条目的“未核验”标注。1.0.28714 保留“新 Unity／旧 Unity”两个入口，中英文切换会更新名称；按版本号、Manifest ID 和首次出现日期搜索仍可用。下载任务继续使用独立的 Manifest 身份，两种 Unity 构建不会合并。本轮未新增文件指纹或模组兼容性记录。

定向回归退出码 0：15 通过、0 失败、0 跳过，结果位于 `artifacts/test-results/manifest-version-labels/manifest-version-labels.trx`。

```powershell
dotnet test '.\tests\Crystalfly.App.Tests' -c Release --no-restore --filter 'FullyQualifiedName~Silksong_history|FullyQualifiedName~Silksong_download_projection|FullyQualifiedName~DownloadBuildUiTests|FullyQualifiedName~Download_instance_name|FullyQualifiedName~LocalizationViewModelTests' --logger 'trx;LogFileName=manifest-version-labels.trx' --results-directory artifacts/test-results/manifest-version-labels
```

本地覆盖命令 `pwsh -NoProfile -File .\scripts\build-and-install.ps1 -UnsignedLocal` 退出码 0；已正常关闭旧进程，更新 `D:\Program Files\Crystalfly`，发布目录的 248 个文件与安装文件 SHA-256 全部一致，哈希检查退出码 0。构建日志和安装证据分别位于 `artifacts/manifest-version-labels-build-install-20261006.log`、`artifacts/manifest-version-labels-install-verification-20261006.json`。`git -c core.safecrlf=false diff --check` 退出码 0，未提交或推送，未做图像识别或真实桌面视觉验收。启动方式同上。

## 2026-10-06 版本行提示精简

移除“首次出现（UTC）”字样与“已有本地版本”提示，保留现有日期值以及发布日期显示。此次仅调整版本行模板，不改动日期数据、搜索或下载目标。已有无截图 UI 回归确认两个提示不存在、发布日期标签及两种日期值仍显示，同时检查选择、搜索与列表对齐。

```powershell
dotnet test '.\tests\Crystalfly.App.Tests' -c Release --no-restore --filter 'FullyQualifiedName~DownloadBuildUiTests' --logger 'trx;LogFileName=version-row-hints.trx' --results-directory artifacts/test-results/version-row-hints
```

退出码 0：1 通过、0 失败、0 跳过；结果位于 `artifacts/test-results/version-row-hints/version-row-hints.trx`。

本地覆盖命令 `pwsh -NoProfile -File .\scripts\build-and-install.ps1 -UnsignedLocal` 退出码 0，旧应用正常退出后安装成功。发布目录的 248 个文件与安装文件 SHA-256 全部一致，哈希检查退出码 0，记录位于 `artifacts/version-row-hints-install-verification-20261006.json`；构建日志位于 `artifacts/version-row-hints-build-install-20261006.log`。`git -c core.safecrlf=false diff --check` 退出码 0。未做图像识别或真实桌面视觉验收，未提交或推送。启动方式同上。

## 真实模组流程

从实际 Thunderstore 分块目录读取 455 个包；验证 gzip、身份、版本与下载大小。下载并安装以下完整依赖集合到本地 Steam 游戏的独立测试副本：

| 包 | 版本 |
| --- | --- |
| hk_speedrunning-DebugMod | 1.1.2 |
| silksong_modding-BepInExPack_Silksong | 1.0.3 |
| MonoDetour-MonoDetour | 0.7.16 |
| MonoDetour-MonoDetour_BepInEx_5 | 0.7.16 |
| SFGrenade-WavLib | 1.1.9 |
| silksong_modding-I18N | 1.1.1 |
| silksong_modding-ModMenu | 0.8.0 |
| silksong_modding-Modlist | 0.3.1 |
| silksong_modding-UnityHelper | 1.2.0 |

安装后所有收据/文件校验通过。实际启动丝之歌进程，日志记录 DebugMod、ModMenu、I18N 等加载和 Chainloader startup complete。随后按依赖顺序卸载全部九包；受管注入文件与有效载荷清除，配置保留，原版联合指纹仍匹配。

证据：artifacts/silksong-verification/thunderstore/plan.json、bepinex-validation.log、unity-validation.log。安装、启动、存档和卸载探针均返回退出码 0。测试期间未读取图片或截图。

实际边界：测试副本日志出现 BepInEx Unity 日志写入器初始化错误、游戏早期单例未就绪提示，以及 Steam SDK 初始化失败；这些没有阻止上述链式加载或样本生成。没有把这些日志描述为无错误运行；Steam Overlay、游戏内 Steam 成就/云同步、完整游玩和全部第三方模组兼容性仍未验收。

## 真实编码存档样本

测试插件调用游戏 1.0.30000 自身的 CreateSaveGameData 与 GetBytesForSaveJsonStatic，生成四份 51,526 字节的测试文件。这是游戏编码器生成的样本，不是手工合成的编码文件，也不是用户游玩存档。没有声称通过游戏 UI 保存过四个进度档。

四槽经 Crystalfly 导入、解码和完整字节往返验证；修改念珠字段后重新读取、导出，再恢复命名快照，JSON 与原始内容一致。隔离会话退出后共享 user*.dat 哈希恢复原值；一次进程退出时间差留下的隔离日志也实际完成恢复。测试插件已从测试实例移除，用户原 Steam 安装未改动。

用户及游戏生成的原始数据仅保留在忽略的 artifacts 下，不纳入 Git、发布包或测试源码。仓库回归使用合成小型数据，保留未知键、数组、类型及中文内容。

## 针对性回归

所有命令均在仓库根目录执行；未运行全量解决方案测试。

| 范围 | 最终结果 | 退出码 |
| --- | --- | ---: |
| Core：目录、迁移、指纹、实例、加载器、Thunderstore、存档 | 236 通过，0 失败 | 0 |
| App：多游戏、市场、队列、存档、既有状态与无截图 UI 绑定 | 278 通过，0 失败 | 0 |
| App：通用队列、按游戏等待退出、未完成任务重启恢复 | 49 通过，0 失败 | 0 |
| Steam：Depot 下载及库发现 | 25 通过，0 失败 | 0 |
| Windows x64 self-contained publish | 成功 | 0 |

Core/App TRX：artifacts/silksong-verification/tests/core.trx、app.trx、queue.trx。合计 588 项针对性测试通过。Steam 使用本轮已通过的对应测试结果；相关代码和输入之后未改动。

执行命令：

~~~powershell
dotnet test tests/Crystalfly.Core.Tests -c Release --no-restore --filter 'FullyQualifiedName~ThunderstoreTests|FullyQualifiedName~MultiGameTests|FullyQualifiedName~SilksongSaveTransferTests|FullyQualifiedName~NamedSnapshotServiceTests|FullyQualifiedName~EmbeddedCatalogTests|FullyQualifiedName~LoaderManagerTests|FullyQualifiedName~BuildFingerprintServiceTests|FullyQualifiedName~SaveFileCodecTests|FullyQualifiedName~ModInstallServiceTests|FullyQualifiedName~LaunchPreflightEvaluatorTests|FullyQualifiedName~InstanceCloneServiceTests|FullyQualifiedName~InstanceImportServiceTests|FullyQualifiedName~InstanceDeletionServiceTests|FullyQualifiedName~CatalogProviderTests|FullyQualifiedName~CrystalflySettingsStoreTests' --logger 'trx;LogFileName=core.trx' --results-directory artifacts/silksong-verification/tests -v:q

dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~MultiGameFeatureTests|FullyQualifiedName~ThunderstoreViewModelTests|FullyQualifiedName~SteamDownloadQueue|FullyQualifiedName~SaveEditorViewModelTests|FullyQualifiedName~MainViewModelStateTests|FullyQualifiedName~DownloadBuildUiTests|FullyQualifiedName~ThunderstoreUiTests|FullyQualifiedName~CatalogPackageQueueExecutorTests' --logger 'trx;LogFileName=app.trx' --results-directory artifacts/silksong-verification/tests -v:q

dotnet test tests/Crystalfly.Steam.Tests -c Release --no-restore --filter 'FullyQualifiedName~SteamDepotDownloadServiceTests|FullyQualifiedName~SteamLibraryDiscovery' -v:q

dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~DownloadQueueServiceTests' --logger 'trx;LogFileName=queue.trx' --results-directory artifacts/silksong-verification/tests -v:q

dotnet publish src/Crystalfly.App/Crystalfly.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/silksong-validation-win-x64 -v:minimal
git -c core.safecrlf=false diff --check
~~~

重点覆盖旧 JSON 默认游戏、设置备份、跨游戏同名版本、队列游戏固定、损坏目录回退、GZip/v1/429、离线无请求、版本锁定/依赖冲突、升级降级/修复/启停、标准目录路由、ZIP 越界、内容摘要漂移、配置保留、文件锁导致安装失败后的全部文件及收据恢复、取消、克隆及删除清理恢复、四槽存档和未保存编辑保护。

未做真实磁盘耗尽或系统权限破坏性测试；文件失败路径通过真实文件占用及 I/O 故障回归覆盖。没有做视觉验收或截图巡检，也没有增加新的逐轮全量检查流程。

## 验证版启动

输出目录：artifacts/silksong-validation-win-x64，入口 Crystalfly.App.exe。添加 portable.flag，使用该目录下独立 Data，避免自动复用正式安装设置。本次构建不注入官方错误上报 DSN；不包含游戏、模组 ZIP、测试存档或验证探针。

~~~powershell
& 'E:/AI_collection/crystalfly/artifacts/silksong-validation-win-x64/Crystalfly.App.exe'
~~~

这是本地验证构建，没有自动覆盖 D:/Program Files/Crystalfly，也没有创建 Git 提交、推送或公开 Release。使用说明及迁移路径见 [功能文档](silksong-thunderstore.md)。
