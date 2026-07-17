# Crystalfly

Crystalfly 是面向 Windows 10/11 x64 的《空洞骑士》版本、Loader、Mod、存档与速通环境管理器。界面采用单实例上下文：先选择游戏实例，再管理其 Loader、Mod、快照和日志，避免把不同版本的状态混在一起。

> 当前状态：首个源码版本。仓库暂不发布 Crystalfly 二进制 Release；可在本地生成便携包和安装包。

[English](#english)

![Crystalfly 启动预检](docs/screenshots/crystalfly-1280x720-zh.jpg)

## 功能

- 扫描用户选择的版本根目录，并把每个直接子目录作为独立实例。
- 识别 `1.2.2.1`、`1.4.3.2`、`1.5.78.11833` 和动态 `latest` 稳定通道。
- 完整复制实例；Loader、Mod 和存档互不共享。
- 事务化安装、切换、修复和卸载 Loader，检测冲突及文件漂移。
- 支持带 Crystalfly 清单的高级本地 Loader 导入，并永久标记为“未验证”。
- 搜索并按状态筛选 Mod，支持单项和批量启用、停用、更新、卸载，以及依赖解析、本地 ZIP/DLL 和离线包缓存。
- 启动前真实检查游戏文件、Loader、Mod 依赖、事务和实例 LocalLow；任一检查失败都会阻止启动。
- 在实例日志页查看 BepInEx、Modding API 和 `Player.log` 的最新内容及来源路径。
- 通过 SteamKit2 扫码登录并下载 public 分支历史 manifest；同一文件最多四路并发下载 Chunk，完成后生成 `steam_appid.txt` 以直接启动对应实例，refresh token 仅以当前 Windows 用户的 DPAPI 加密保存。
- 启动前切换实例 LocalLow，退出后写回，并恢复原共享数据。
- 创建永久命名快照；事务临时恢复点成功后自动清理。
- 创建独立速通副本，按模板部署速通工具，并在每次启动前写出验证报告。

## 兼容矩阵

| 游戏版本 | Loader | DebugMod |
| --- | --- | --- |
| `1.2.2.1` | Modding API v37 | `legacy/1.2.2.1` |
| `1.4.3.2` | Modding API v60 | `legacy/1.4.3.2` |
| `1.5.78.11833` | Modding API v77 | `legacy/1.5.78` |
| 当前已验证稳定版 | Modding API v78 或 BepInEx 5.4.23.4 | `latest` |

“当前稳定版”由远程 catalog 的 Steam public manifest 决定，不在界面或兼容逻辑中写死版本号。未知的新 manifest 可以下载并以原版启动，但 Crystalfly 会锁定 Loader 安装，直到 catalog 提供新的构建指纹和兼容清单。

- DebugMod：<https://github.com/wzxnb2333/New.HK.Debug/releases/tag/v1.4.10.5-r2>
- Modding API v78：<https://github.com/wzxnb2333/api/releases/tag/1.5.12620.0-78>
- Modding API v37：<https://github.com/wzxnb2333/api/releases/tag/1.2.2.1-37-windows>

## 启动

需要 [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)：

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet run --project '.\src\Crystalfly.App\Crystalfly.App.csproj'
```

首次使用：

1. 在“设置”中选择版本根目录，例如 `D:\HK_ver`。
2. Crystalfly 只扫描直接子目录，并忽略 `<版本根目录>\.crystalfly`。
3. 在“版本”中选择实例，再进入实例详情管理 Loader、Mod 和快照。
4. 启动游戏时不要同时运行其他《空洞骑士》进程。

## 启动预检与实例详情

选择实例后，启动页会检查游戏可执行文件、是否已有游戏进程、Loader 冲突或漂移、已启用 Mod 的依赖、待恢复事务和实例 LocalLow。全部通过后才能启动；操作执行期间和游戏运行期间会锁定导航及实例修改。

“Mods”页可按名称、ID 或版本搜索，并按全部、启用、停用、本地和可更新状态筛选。受 catalog 管理的 Mod 支持单项更新；勾选多项后可批量启用、停用、更新或卸载。本地 Mod 不提供自动更新。停用或卸载仍被其他已安装 Mod 依赖的项目时，界面会列出反向依赖并阻止操作，不会自动级联删除。

“日志”页会发现当前实例的 BepInEx、Modding API 和共享 `Player.log`，显示日志来源路径，并支持刷新和查看文件末尾内容。共享 `Player.log` 可能来自最近运行的实例，排查当前实例时应优先使用实例目录内的 Loader 日志。

## 速通环境

四个内置模板会创建专用完整副本，不会临时修改日常或练习实例。当前 catalog 没有完整、可信的规则修订与 Steam 文件白名单，因此模板明确显示“未验证”；不会伪造正式验证标记。规则与文件清单齐备后，远程 catalog 可在不更新客户端的情况下启用正式验证。

- `1.2.2.1` 单跑和比赛模板部署 ScreenShakeModifier，不支持 LoadNormaliser。
- `1.5.78` 单跑模板不部署 LoadNormaliser；该版本使用游戏内置的屏幕震动设置。
- 只有 `1.5.78` 比赛模板部署 LoadNormaliser，并可选择 `1`、`2`、`3` 或 `5` 秒。

正式可信模板验证失败时会阻止启动；当前未验证模板仍会生成报告并明确保留未验证状态。

验证报告是启动前文件完整性的时间点快照，不证明报告写出后文件仍未变化。

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
- 实例元数据、缓存、事务、LocalLow 和快照：`<版本根目录>\.crystalfly`
- 实例标识：`<实例目录>\.crystalfly-instance.json`

首次接管 LocalLow 前会保留完整共享备份。发生崩溃时，事务日志只在能够用阶段和文件哈希证明安全的情况下自动恢复；否则实例进入 `NeedsAttention` 并禁止启动。

## 构建与验证

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet test '.\Crystalfly.slnx' -c Release --no-build

pwsh -NoProfile -File '.\scripts\build-release.ps1'
```

脚本会自动查找 Inno Setup 6；自定义安装位置可传入 `-IsccPath '<ISCC.exe 路径>'`。本地输出位于 `artifacts`：self-contained publish、带 `portable.flag` 的便携 ZIP、Inno Setup 安装包和 `SHA256SUMS.txt`。首轮只公开源码；本地产物未做 Authenticode 签名，仅用于本机验证。详细设计见 [架构文档](docs/architecture.md)。

## 许可证

Crystalfly 使用 [GPL-3.0-only](LICENSE)。第三方游戏、Loader 和 Mod 不随仓库分发，仍受各自许可证约束。

## English

Crystalfly manages Hollow Knight versions, loaders, mods, saves, snapshots, Steam depot downloads, and dedicated speedrun environments on Windows 10/11 x64.

This is the first source release. Crystalfly binary releases are not published yet; the repository builds a local self-contained portable ZIP and an Inno Setup installer.

![Crystalfly launch checks](docs/screenshots/crystalfly-1280x720-zh.jpg)

### Highlights

- Discovers direct child directories under a user-selected version root and keeps each instance isolated.
- Recognizes `1.2.2.1`, `1.4.3.2`, `1.5.78.11833`, and a dynamic stable `latest` channel.
- Installs, switches, repairs, and removes mutually exclusive loaders through recoverable file transactions.
- Searches and filters mods by state, with single-item and batch enable, disable, update, and uninstall operations, dependency resolution, local ZIP/DLL imports, and an offline package cache.
- Runs real launch checks for game files, loader state, mod dependencies, pending transactions, and per-instance LocalLow state; launch stays blocked until every check passes.
- Displays detected BepInEx, Modding API, and `Player.log` files with their source paths and refreshable tail content.
- Imports local loaders only through a validated Crystalfly manifest and keeps them marked unverified.
- Uses SteamKit2 for QR authentication and public manifest downloads, with up to four concurrent chunk requests per file. Completed instances receive `steam_appid.txt` for direct launch. Refresh tokens are protected with Windows DPAPI for the current user.
- Swaps per-instance LocalLow data before launch, captures it after exit, then restores the original shared data.
- Creates persistent named snapshots and dedicated speedrun copies, provisions template-specific tools, and writes a report before every speedrun launch.

The current built-in speedrun templates are intentionally unverified because the catalog does not yet contain a trusted rules revision and complete Steam file allowlist. Unknown new public manifests remain launchable as vanilla, but loader installation stays locked until the catalog verifies the build.

### Launch checks and instance details

After an instance is selected, the launch page checks the game executable, running Hollow Knight processes, loader conflicts or drift, enabled mod dependencies, pending transaction recovery, and per-instance LocalLow readiness. Launch is enabled only when every check passes. Navigation and instance mutations are locked while an operation or the game is running.

The Mods page searches by name, ID, or version and filters installed mods by all, enabled, disabled, local, or update available. Catalog-managed mods support single-item updates; checked items can be enabled, disabled, updated, or removed in batches. Local mods do not receive automatic updates. Disabling or removing a mod that still has installed dependents lists those reverse dependencies and blocks the operation instead of cascading removal.

The Logs page discovers BepInEx, Modding API, and shared `Player.log` files, shows each source path, and reads refreshable tail content. The shared `Player.log` may belong to the most recently launched instance, so instance-local loader logs are the stronger source when diagnosing one instance.

### Speedrun environments

The four built-in templates create dedicated full copies and remain explicitly unverified until the catalog contains a trusted rules revision and complete Steam file allowlist.

- The `1.2.2.1` solo and race templates deploy ScreenShakeModifier and do not support LoadNormaliser.
- The `1.5.78` solo template does not deploy LoadNormaliser and uses the game's built-in screen shake setting.
- Only the `1.5.78` race template deploys LoadNormaliser, with selectable `1`, `2`, `3`, or `5` second variants.

A failed trusted-template report blocks launch. Current unverified templates still write a report and retain their unverified status.

Verification reports are pre-launch integrity snapshots. They do not attest that files remain unchanged after the report is written. The first release publishes source only; locally built binaries are not Authenticode-signed.

### Develop

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet test '.\Crystalfly.slnx' -c Release --no-build
dotnet run --project '.\src\Crystalfly.App\Crystalfly.App.csproj'
```

### Release build

```powershell
pwsh -NoProfile -File '.\scripts\build-release.ps1'
```

The script automatically locates Inno Setup 6 from `PATH` or its standard install directories. Pass `-IsccPath '<path to ISCC.exe>'` for a custom location. Outputs under `artifacts` include the self-contained publish, portable ZIP, installer, and `SHA256SUMS.txt`.

Application settings use `%LOCALAPPDATA%\Crystalfly`, or `Data` beside the executable when `portable.flag` exists. Per-instance state always stays under `<version-root>\.crystalfly`.

Crystalfly is licensed under [GPL-3.0-only](LICENSE). Hollow Knight, loaders, and mods are not redistributed by this repository and retain their own licenses.
