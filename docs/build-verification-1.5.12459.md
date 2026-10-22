# Hollow Knight 1.5.12459 原版身份校验

校验时间：2026-09-27 09:20:43 UTC。

## 来源与方法

- Steam App ID：`367520`；Windows Depot ID：`367521`。
- Steam Manifest ID：`4709992615819941531`。
- 通过拥有访问权限的 Steam 会话取得原始 manifest，复用 Crystalfly 的下载和分块校验实现下载下列四个文件，共 39,671,996 字节。
- 下载后重新计算每个文件的 SHA-1，与 Steam manifest 一一匹配，再计算用于 catalog 的 SHA-256。
- 原版 `Assembly-CSharp.dll` 包含 UTF-16 版本字符串 `1.5.12459`。
- 游戏文件、账户资料及访问凭据不随源码分发。

## 原版文件证据

| 文件 | 字节数 | Steam SHA-1 | SHA-256 |
| --- | ---: | --- | --- |
| `hollow_knight.exe` | 672256 | `93434BAE8C23D93A287A2E1ECCED3537F2A3D4BC` | `7961BBA7243CE322163AD6B0DDFD6EF701413020B92AD0D98502526A396171B0` |
| `hollow_knight_Data/globalgamemanagers` | 1620756 | `B756B8596CA2C5ACF0CC83E17423258DD583FE26` | `C9720F4B34131058C97766A9A92A1C4FFCE3B407647DE192C6D4DF46253EAFA9` |
| `hollow_knight_Data/Managed/Assembly-CSharp.dll` | 3642368 | `38C37CF234E9C8D8E521B47A0C700A66B0E4D3ED` | `4E5B6A6E2B66A3BCB814FE263178B30554D897AD96DEE59EAC9800902DCD8AFE` |
| `UnityPlayer.dll` | 33736616 | `6D1D824936F145FEFAF6FFE077F6552D39D50719` | `8F2D601F8D3C7F4D29D80BA786C0BE873102BB7E6041EB03964A90B99724D90B` |

## 识别与兼容边界

catalog 使用可执行文件、UnityPlayer 和 globalgamemanagers 的联合指纹识别版本。`1.5.12459` 与 `1.5.12620.0` 的前两个文件哈希相同，globalgamemanagers 不同；只凭可执行文件或 UnityPlayer 不能区分这两个构建。Assembly-CSharp 的哈希作为额外原版证据记录，不替代既有联合指纹规则。

本次将 `1.5.12459` 加入公开及内置 catalog，删除专用“未验证，仅限原版”下载占位项，继续沿用普通实例的 BepInEx 安装和修复流程。当前没有匹配的 Modding API 包，因此不把该版本加入 v77 或 v78 的兼容清单：

- Modding API v77 对应 `1.5.78.11833`。
- 当前 [Modding API v78 发布包](https://github.com/wzxnb2333/api/releases/tag/1.5.12620.0-78) 明确对应 `1.5.12620.0`。其原版 Assembly-CSharp 为 3,652,608 字节、SHA-256 `E9048EF6A633970F735E01EC166D3959F610EAEA7A88D827D48D67B1E5FB87BD`，与本次下载的 12459 原版不相同。
- [上游兼容性问题](https://github.com/hk-modding/api/issues/179)记录了 v77 与 12459 的不兼容。

本次验证范围是原版构建身份、catalog 关联和下载入口；未进行完整游戏启动或 Mod 运行验证，也未构建、发布针对 12459 的 Modding API 移植包。

## 本轮验证命令与结果

从仓库根目录执行以下相关回归，均退出 0。Core 通过 7 项，App 通过 3 项；未运行全量测试。

```powershell
dotnet test tests/Crystalfly.Core.Tests -c Release --filter 'FullyQualifiedName~Load_returns_verified_official_fallback_data|FullyQualifiedName~Public_catalog_matches_the_embedded_fallback_and_schema_is_present|FullyQualifiedName~Catalog_contains_all_supported_loader_and_debugmod_release_assets|FullyQualifiedName~BuildFingerprintServiceTests|FullyQualifiedName~Verified_12459' --verbosity minimal
dotnet test tests/Crystalfly.App.Tests -c Release --filter 'FullyQualifiedName~Verified_12459_download|FullyQualifiedName~Download_build_search_keeps_every_catalog_build|FullyQualifiedName~DownloadBuildUiTests' --verbosity minimal
git -c core.safecrlf=false diff --check
```

本地临时探针还对已下载的四个 Steam 文件运行了生产代码 `BuildFingerprintService.CalculateAsync` / `FindBuild`，实际返回构建 `1.5.12459` 和 manifest `4709992615819941531`，退出 0。探针和游戏文件仅留在忽略的 `artifacts/verify-12459` 中，不是源码依赖。

验证版构建命令如下，退出 0，0 警告、0 错误。官方错误报告配置由本机文件显式注入，不作为普通源码构建的默认接收地址。

```powershell
. ./scripts/build-release.ps1
$reportingProperties = @(Get-ErrorReportingBuildProperties -ConfigPath './config/error-reporting.official.json')
dotnet build src/Crystalfly.App/Crystalfly.App.csproj -c Release --no-restore -o artifacts/diagnostics/verified-12459 @reportingProperties --verbosity quiet
```
