# Crystalfly

**简体中文** | [English](README.en.md)

Crystalfly 是面向 Windows 10/11 x64 的《空洞骑士》游戏版本、Loader、Mod、存档与速通环境管理器。启动页是实例选择与管理的唯一入口；真正的游戏版本下载位于“下载 → 游戏版本”。界面采用单实例上下文，避免把不同实例的 Loader、Mod 和存档状态混在一起。

> 当前版本：`1.1.3`。提供 Windows x64 本地未签名便携包与安装包。Steam 游戏下载支持跨文件 Chunk 调度和跨版本复用缓存。

![Crystalfly 启动预检](docs/screenshots/crystalfly-1280x720-zh.jpg)
![选择实例](docs/screenshots/crystalfly-select-instance-1280x720-zh.jpg)

### 界面验收截图

![启动问题红框](docs/screenshots/crystalfly-launch-issues-1280x720-zh.jpg)
![启动问题确认](docs/screenshots/crystalfly-launch-issues-overlay-1280x720-zh.jpg)
![已安装 Mod 完整性](docs/screenshots/crystalfly-installed-mod-health-1280x720-zh.jpg)
![离线模式与下载线路](docs/screenshots/crystalfly-1920x1080-zh.jpg)
![Mod 市场列表](docs/screenshots/crystalfly-mod-market-list-1280x720-zh.jpg)
![Mod 详情](docs/screenshots/crystalfly-mod-market-detail-1280x720-zh.jpg)
![安装目标实例](docs/screenshots/crystalfly-mod-install-overlay-1280x720-zh.jpg)
![实例详情](docs/screenshots/crystalfly-instance-detail-900x600-zh.jpg)
![实例配置编辑](docs/screenshots/crystalfly-instance-config-1280x720-zh.jpg)
![实例存档编辑](docs/screenshots/crystalfly-save-editor-1280x720-zh.jpg)
![依赖关系图](docs/screenshots/crystalfly-dependency-graph-1280x720-zh.jpg)

## 功能

- **新手引导**：首次启动弹出 8 步向导（导入游戏 → 选择实例 → 安装 Loader → 添加 Mod → 启动 → 更多功能），之后可从“设置 → 常规”随时重新打开。
- **游戏目录自动发现**：扫描 Steam 库之外，可递归扫描所有本地磁盘自动发现非 Steam 的 Hollow Knight 目录，扫描时实时显示进度。
- 管理多个游戏目录，并把活动目录自身或其直接子目录识别为独立实例；Steam 安装通过库目录自动发现，确认后才登记或迁移。
- 启动时在后台扫描实例与版本文件，不等待远程目录或 Steam 自动重连；顶部设置使用文字 Tab，“选择实例”入口与页面、卡片和操作反馈采用快速弹簧动效，并支持跟随系统、减少动效或关闭。
- 使用自绘标题栏和原生可缩放边框；Windows 11 明确请求系统圆角，Windows 10 保持兼容。
- 识别 `1.2.2.1`、`1.4.3.2`、`1.5.78.11833` 和动态 `latest` 稳定通道。
- 启动页的实例入口可直接选择、进入设置、完整克隆或永久删除实例；删除会先检查游戏进程、下载任务与文件事务，确认后同时移除游戏目录和实例状态。
- 事务化安装、切换、修复和卸载 Loader，检测冲突及文件漂移。
- 支持带 Crystalfly 清单的高级本地 Loader 导入，并永久标记为“未验证”。
- 在“下载 → Mod 市场”中搜索在线 Mod、查看详情并选择目标实例；在实例的“已安装 Mod”页通过信息、打开目录、启停和卸载快捷操作管理单个 Mod，也可多选后批量处理。
- Mod 市场支持“最近新增/最近更新”筛选，并按需加载经过清理的 README 与最新发行说明；内容使用 ETag 原子缓存，离线时读取最后有效缓存。
- 官方受管理 Mod 可从详情页按原启停状态重装或修复，并可定位、事务删除当前实例隔离 LocalLow 中的全局设置。
- 设置页可使用绑定精确游戏构建与 Modding API Loader 的 HTTPS 自定义 ModLinks 完整替换官方源；自定义内容始终显示为未验证。
- 安装前展示 Loader、递归前置与主 Mod；确认后加入后台下载队列，同一依赖链串行，独立安装组最多三路并发。
- 安装前检查游戏版本、精确 Loader ID 和完整依赖闭包；官方 `1.5.78` ModLinks 包可在当前稳定版的 Modding API v78 环境中安装，其余 Modding API v37/v60/v77/v78、BepInEx 及游戏版本组合仍保持隔离。
- 主动扫描受管理与外部 Mod，显示文件缺失、修改、额外文件和未接管状态；外部 Mod 可由用户一键接管，接管后自动匹配目录条目（名称 + Loader 家族 + 版本）以打通依赖解析，唯一匹配自动改写、歧义时由用户选择；取消接管后下次选中实例会重新提示。
- 启动页持续用红框显示 Mod 完整性和依赖问题。只有 Mod 文件与依赖问题允许用户确认后强制启动；游戏文件、Loader、事务、LocalLow 和进程冲突始终阻止启动。
- 支持固定 Mod；批量卸载和无用前置建议会跳过固定项，单独卸载前需先取消固定。
- 全局离线模式会断开 Steam 登录会话，使目录、翻译、自定义目录、Mod 和 Steam 下载只使用已验证缓存；网络队列等待恢复在线，不影响本地实例管理。
- 在实例日志页查看 BepInEx、Modding API 和 `Player.log` 的最新内容及来源路径。
- Steam 登录支持**账号密码**与扫码两种方式；Steam Guard 验证码（邮箱码 / 手机码）通过对话框收集，绑定了手机令牌的账号优先走设备确认。通过 SteamKit2 下载 public 分支与任意手动输入的 Windows Depot Manifest；自动跟随 Windows 系统代理并使用 WebSocket 登录通道，代理变化或非主动断线会暂停 Steam 队列并重连已保存账号。加速器环境下无 HTTPS 内容服务器时自动回退 HTTP 服务器。未验证历史版本仅允许原版启动，目录后续收录相同文件指纹时会自动升级为正式构建。整个 Depot 最多十六路并发下载 Chunk，已验证 Chunk 在当前游戏目录中以 20 GiB 上限跨版本、跨实例复用，完成后生成 `steam_appid.txt`；refresh token 与记住的凭据仅以当前 Windows 用户的 DPAPI 加密保存。
- 设置页可在 GitHub 直连、智能选择、`gh-proxy.org`、`gh-proxy.com`、`ghproxy.net` 与 `ghfast.top` 间切换并分别测试延迟；镜像仅代理官方 GitHub 目录和 GitHub 托管安装包，智能选择会按首包延迟优先使用可用线路，请求失败时依次切换并最终回退 GitHub 直连。Steam、自定义目录及其他下载地址保持原线路，包校验规则不变。
- 设置页支持全局背景图片与当前实例独立覆盖，可调节图片不透明度；实例移除独立背景后自动恢复全局背景。
- 启动前切换实例 LocalLow，退出后写回，并恢复原共享数据。
- 创建永久命名“存档快照”；快照仅包含实例的非日志 LocalLow，事务临时恢复点成功后自动清理。
- 在实例设置中编辑当前实例隔离的 `AppConfig.ini`；未知配置项会原样保留，写入采用原子替换。
- 在当前实例或其命名快照中编辑 `user1.dat` 至 `user4.dat`；解密和展开异步执行，空存档会显示明确状态，不会阻塞主窗口。
- 在实例详情创建追加或精确整合包，支持复制、导入导出、分享码、按依赖顺序应用，以及恢复应用前启停和安装状态；固定 Mod 及其传递依赖不会被精确模式停用。
- 创建独立速通副本，按模板部署速通工具，并在每次启动前写出验证报告。
- 支持严格校验的 `crystalfly://` 外部命令和单实例转发；安装包会注册协议，所有修改状态的外部命令均先展示摘要并确认。
- 每天检查一次 Ed25519 签名稳定更新清单，支持立即更新、稍后和跳过版本；安装模式静默运行 Inno 安装包，便携模式使用同卷备份与替换并保留 `Data`。

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

1. 首次启动会弹出新手引导向导；从启动页进入“选择实例”，扫描 Steam 库、全盘扫描或添加自选游戏目录；首次扫描结果需要确认后才登记。
2. Crystalfly 会递归扫描磁盘上的游戏目录，并忽略 `.crystalfly`、重解析点、无权限目录和未完成下载。
3. 从启动页打开实例选择，选中实例后可进入设置管理 Loader、游戏配置、“已安装 Mod”、“整合包”和“存档快照”；当前实例及快照的四个存档槽可在“存档快照”中编辑。
4. 需要下载游戏时进入“下载 → 游戏版本”；需要查找在线 Mod 时进入“下载 → Mod 市场”；进度、速度、取消与重试位于“下载 → 下载队列”。
5. 启动游戏时不要同时运行其他《空洞骑士》进程。

## 启动预检、Mod 市场与实例详情

选择实例后，启动页会检查游戏可执行文件、进程、Loader、Mod 依赖与文件哈希、待恢复事务和实例 LocalLow。Mod 文件或依赖问题可在明确确认后强制启动；游戏文件、Loader、事务、LocalLow 和进程冲突始终阻止启动。问题变化会让“不再提醒”指纹自动失效，红色问题框始终保留。

“下载 → Mod 市场”负责发现在线 Mod：可按关键词、游戏版本、Loader、来源和标签筛选，查看描述、作者、依赖、集成、仓库及精确兼容范围，再选择一个兼容实例安装。Vanilla 实例可在确认后先安装目录指定的精确 Loader，再重新验证并安装 Mod；Loader 冲突、漂移、未知构建和正式速通实例不可作为安装目标。

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

启动前会检查核心游戏指纹、RuntimePatches DLL、实例隔离配置、Loader/Mod 标记、事务和 LocalLow 状态。技术错误会阻止启动，规则警告不会。PNG、贴图和普通额外文件不参与阻断。旧模板实例保留文件并标记为已过期，需要重新创建。

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
- 实例元数据、缓存、事务、LocalLow 和快照：`<当前游戏目录>\.crystalfly`
- 实例标识：`<实例目录>\.crystalfly-instance.json`

首次接管 LocalLow 前会保留完整共享备份。发生崩溃时，事务日志只在能够用阶段和文件哈希证明安全的情况下自动恢复；否则实例进入 `NeedsAttention` 并禁止启动。

## 构建与验证

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet test '.\Crystalfly.slnx' -c Release --no-build

pwsh -NoProfile -File '.\scripts\build-release.ps1' -Version '1.1.3'

# 无更新签名的本地构建与固定目录覆盖；不会生成 update-manifest.v1.json。
pwsh -NoProfile -File '.\scripts\build-and-install.ps1' -Version '1.1.3' -UnsignedLocal
```

脚本会自动查找 Inno Setup 6；自定义安装位置可传入 `-IsccPath '<ISCC.exe 路径>'`。发布构建从已忽略的 `.env.update-signing` 读取 `CRYSTALFLY_UPDATE_SIGNING_KEY`，并使用 `tools/Crystalfly.ReleaseTool` 生成签名更新清单；私钥文件不得提交。仅本地验收时可显式传入 `-UnsignedLocal`，此模式不会生成 `update-manifest.v1.json`，不得将其作为公开 Release 上传。`build-and-install.ps1` 会从 `Directory.Build.props` 读取版本号，执行完整 Release 构建和测试，验证产物后以管理员权限静默更新 `D:\Program Files\Crystalfly`，最后核对已安装版本。运行中的 Crystalfly 会使流程停止，不会强制关闭程序。安装包默认安装到 `D:\Program Files\Crystalfly`，需要管理员权限。便携 ZIP 可直接解压到其他目录。本地输出位于 `artifacts`：self-contained publish、独立更新程序、带 `portable.flag` 的便携 ZIP、Inno Setup 安装包、`update-manifest.v1.json` 和 `SHA256SUMS.txt`。产物尚未使用 Authenticode 签名；客户端仍会验证更新清单的 Ed25519 签名及资产 SHA-256、大小和版本。详细设计见 [架构文档](docs/architecture.md)。

## 许可证

Crystalfly 使用 [GPL-3.0-only](LICENSE)。第三方游戏、Loader 和 Mod 不随仓库分发，仍受各自许可证约束。
