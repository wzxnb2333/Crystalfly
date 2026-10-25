# 使用与维护指南

[返回首页](../README.md) · [English](user-guide.en.md)

本页收录兼容性、安装规则、存储位置与开发构建细节。首次使用请先看首页的快速上手。

## 兼容矩阵

| 游戏版本 | Loader | DebugMod |
| --- | --- | --- |
| `1.2.2.1` | Modding API v37 或 BepInEx 5.4.23.4 | `legacy/1.2.2.1` |
| `1.4.3.2` | Modding API v60 或 BepInEx 5.4.23.4 | `legacy/1.4.3.2` |
| `1.5.78.11833` | Modding API v77 或 BepInEx 5.4.23.4 | `legacy/1.5.78` |
| `1.5.12459` | BepInEx 5.4.23.4；暂无匹配的 Modding API | — |
| 当前已验证稳定版 | Modding API v78 或 BepInEx 5.4.23.4 | `latest` |

`1.5.12459` 已于 2026-09-27 根据 Steam 原版 manifest 和文件哈希完成身份校验，不再作为“未验证，仅限原版”下载项。参见[校验证据](build-verification-1.5.12459.md)。这不表示 Modding API 兼容：v77 对应 `1.5.78.11833`，当前 v78 发布包对应 `1.5.12620.0`，均不能套用于 `1.5.12459`。

“当前稳定版”由远程 catalog 的 Steam public manifest 决定，不在界面或兼容逻辑中写死版本号。普通实例的所有游戏版本均可安装和修复 BepInEx，包括自定义版本和未知 Steam manifest；旧远程目录的版本白名单不会重新锁定 BepInEx。不会仅因尚未核验加载器兼容性而警告或阻止启动；Modding API 和具体模组仍按各自兼容清单检查，正式速通环境保持不可安装模组加载器。

- DebugMod：<https://github.com/wzxnb2333/New.HK.Debug/releases/tag/v1.4.10.5-r2>
- Modding API v78：<https://github.com/wzxnb2333/api/releases/tag/1.5.12620.0-78>
- Modding API v37：<https://github.com/wzxnb2333/api/releases/tag/1.2.2.1-37-windows>

## 启动预检、Mod 市场与实例详情

选择实例后，启动页会检查游戏可执行文件、进程、Loader、Mod 依赖与文件哈希、待恢复事务和实例 LocalLow。Mod 文件或依赖问题可在明确确认后强制启动；游戏文件、Loader、事务、LocalLow 和进程冲突始终阻止启动。问题变化会让“不再提醒”指纹自动失效，红色问题框始终保留。

“下载 → Mod 市场”负责发现在线 Mod：可按关键词、游戏版本、Loader、来源和标签筛选，查看描述、作者、依赖、集成、仓库及精确兼容范围，再选择一个兼容实例安装。Vanilla 实例可在确认后先安装目录指定的精确 Loader，再重新验证并安装 Mod；Loader 冲突、文件漂移和正式速通实例不可作为安装目标，未知构建仍按具体包的兼容范围判断。

选择目标实例后会先显示 Loader、全部递归前置和主 Mod 的安装计划。确认只负责加入后台队列，不会锁住市场。每条依赖链按 Loader、前置、主 Mod 顺序执行；互不相关的安装组最多并行三个网络任务。游戏运行时可继续下载，写入目标实例前会等待游戏退出。网络错误自动重试三次；哈希、清单和兼容性错误不会盲目重试。关闭程序时会保存未完成及失败任务；未完成任务会在重启后继续，失败任务会保留并等待手动重试。

实例的“已安装 Mod”页只管理当前实例内的 Mod，可按名称、ID 或版本搜索，并按全部、启用、停用、本地和可更新状态筛选。每项提供信息、打开目录、启停和卸载快捷操作；进入多选后可全选、取消选择，并批量启用、停用、更新或卸载。卸载前会展示依赖影响树；依赖修复会列出需要重新启用或下载安装的项目，无法安全修复时明确阻止操作。本地 Mod 不提供自动更新。

“整合包”保存精确游戏构建、Loader 和受管理 Mod 版本。本地或外部 Mod 只记录名称与文件哈希，不包含文件、下载地址或本地路径。追加模式只安装或启用缺失项；精确模式还停用未列出且未固定的 Mod。应用计划进入现有下载队列，前置、启用和停用保持依赖顺序；恢复点在队列真正开始修改实例时捕获，整个整合包组与同一实例的其他修改互斥，恢复写入前会完成固定项、收据和文件健康预检。整合包 JSON 上限为 128 KiB、1000 个条目，可本地导入导出或使用 12 位分享码；离线时本地导入导出仍可使用。

Loader 兼容按精确包 ID 判断，不会把所有 Modding API 或 BepInEx 版本视为等价。Crystalfly 安装的 Loader 可修复和卸载；手动安装且能确认版本的 BepInEx 标记为外部所有，仅允许安装完全匹配的插件，不会修复、卸载、覆盖或接管 BepInEx 本体。手动安装的 Modding API 因缺少原版程序集备份会保持 `Drifted`。

“日志”页会发现当前实例的 BepInEx、Modding API 和共享 `Player.log`，显示日志来源路径，并支持刷新和查看文件末尾内容。共享 `Player.log` 可能来自最近运行的实例，排查当前实例时应优先使用实例目录内的 Loader 日志。

## 速通环境

三个内置 RuntimePatches 模板会从用户选定的干净 Vanilla 实例创建完整副本，不修改原实例。客户端固定使用 AssemblyPatches v1.0.2 的 Windows 发布包，并分别验证 ZIP 与内部 `Assembly-CSharp.dll` 的 SHA-256。

- 支持 `1.2.2.1`、`1.4.3.2` 与 `1.5.78`；不安装 Modding API、BepInEx 或 LoadNormaliser。
- 所有开关默认关闭。`1.2.2.1` 不提供 `FasterIntroSkip`；`1.5.78` 不提供 `ScreenShakeModifier`。
- `FasterIntroSkip` 与 `MiniSaveStates` 会显示规则警告，但具体分类是否合法仍以 SRC 公告为准。
- 1.5.78 速通环境可在 MiniSaveStates 单槽与 MultiSaveStates 十槽之间切换；切换会重新安装对应的 Assembly-CSharp，两个程序集不会叠加。
- MiniSaveStates 原作者仓库：<https://github.com/Shichien/MiniSavestates>。

启动前会检查核心游戏指纹、RuntimePatches DLL、实例隔离配置、Loader/Mod 标记、事务和 LocalLow 状态。技术错误会阻止启动，规则警告不会。PNG、贴图和普通额外文件不参与阻断。旧模板实例保留文件并标记为已过期，需要重新创建。

验证报告是启动前文件完整性的时间点快照，不证明报告写出后文件仍未变化。

## Steam 下载与离线使用

Steam 登录支持账号密码、扫码与 Steam Guard 验证。下载可选择 public 分支或手动输入 Windows Depot Manifest；public 会解析为实际 Manifest，修复使用实例已记录的精确 Manifest。未核验版本保留真实身份，后续目录匹配其文件指纹时可升级为已核验构建。

下载跟随 Windows 系统代理，代理变化或非主动断线会暂停 Steam 队列并重连已保存账号。整个 Depot 最多十六路并发下载 Chunk，已验证 Chunk 在当前游戏目录中以 20 GiB 上限跨版本、跨实例复用。记住的凭据与 refresh token 只以当前 Windows 用户的 DPAPI 加密保存。

全局离线模式会断开 Steam 会话，目录与安装仅使用有效缓存，网络任务等待恢复在线。GitHub 线路选择和镜像只作用于官方 GitHub 目录及 GitHub 托管包；Steam、自定义目录与其他下载地址保持原线路，包校验规则不变。

安装包会注册严格校验的 `crystalfly://` 协议。外部请求通过单实例转发，修改状态前必须展示摘要并确认。应用每天检查一次签名稳定更新清单；安装版运行安装包，便携版使用同卷备份与替换并保留 `Data`。

## 高级本地 Loader

本地 Loader 必须由一个 JSON 清单和同目录 ZIP 组成。界面只接受该清单，不接受裸 Loader ZIP。示例：

```json
{
  "schemaVersion": 1,
  "id": "community-loader",
  "name": "Community Loader",
  "version": "1.0.0",
  "loaderState": "moddingApi",
  "packageFile": "CommunityLoader.zip",
  "sizeBytes": 123456,
  "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  "supportedBuildIds": ["1.5.78.11833"],
  "managedFiles": ["hollow_knight_Data/Managed/Assembly-CSharp.dll"]
}
```

`loaderState` 只能是 `moddingApi` 或 `bepInEx`。清单路径、ZIP 大小和 SHA-256 会在修改实例前验证；本地来源始终显示“未验证”。

## 数据位置

- 安装模式设置：`%LOCALAPPDATA%\Crystalfly`
- 便携模式设置：程序旁 `Data`（存在 `portable.flag` 时）
- 实例元数据、缓存、事务、LocalLow 和快照：`<当前游戏目录>\.crystalfly`
- 实例标识：`<实例目录>\.crystalfly-instance.json`

首次接管 LocalLow 前会保留完整共享备份。发生崩溃时，事务日志只在能够用阶段和文件哈希证明安全的情况下自动恢复；否则实例进入 `NeedsAttention` 并禁止启动。

## 构建与验证

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet build '.\Crystalfly.slnx' -c Release --no-restore

pwsh -NoProfile -File '.\scripts\build-release.ps1' -Version '1.1.8'

# 无更新签名的本地构建与固定目录覆盖；不会生成 update-manifest.v1.json。
pwsh -NoProfile -File '.\scripts\build-and-install.ps1' -Version '1.1.8' -UnsignedLocal

# 仅在需要完整回归时显式加上 -RunTests；两个构建脚本均支持。
pwsh -NoProfile -File '.\scripts\build-release.ps1' -Version '1.1.8' -RunTests
```

日常开发只执行一次与改动相关的检查，优先按测试名称筛选；文档和脚本改动只运行相应轻量检查。代码与输入未变化时复用已通过的结果，不自动重复全量回归、安装验收或开启下一轮复查。完整测试保留在 CI 合并检查中。

两个构建脚本默认跳过测试套件，只构建发布所需项目；显式传入 `-RunTests` 才会先构建完整解决方案并运行全部测试。打包文件一致性、签名和校验和检查仍然保留。

脚本会自动查找 Inno Setup 6；自定义安装位置可传入 `-IsccPath '<ISCC.exe 路径>'`。发布构建从已忽略的 `.env.update-signing` 读取 `CRYSTALFLY_UPDATE_SIGNING_KEY`，并使用 `tools/Crystalfly.ReleaseTool` 生成签名更新清单；私钥文件不得提交。仅本地验收时可显式传入 `-UnsignedLocal`，此模式不会生成 `update-manifest.v1.json`，不得将其作为公开 Release 上传。`build-and-install.ps1` 会从 `Directory.Build.props` 读取版本号，执行 Release 构建，验证产物后以管理员权限静默更新 `D:\Program Files\Crystalfly`，最后核对已安装版本。运行中的 Crystalfly 会使流程停止，不会强制关闭程序。安装包默认安装到 `D:\Program Files\Crystalfly`，需要管理员权限。便携 ZIP 可直接解压到其他目录。本地输出位于 `artifacts`：self-contained publish、独立更新程序、带 `portable.flag` 的便携 ZIP、Inno Setup 安装包、`update-manifest.v1.json` 和 `SHA256SUMS.txt`。产物尚未使用 Authenticode 签名；客户端仍会验证更新清单的 Ed25519 签名及资产 SHA-256、大小和版本。详细设计见 [架构文档](architecture.md)。
