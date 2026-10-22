# 脱敏错误报告 / Sanitized error reports

## 用户说明

配置了接收地址的官方版本默认开启“自动发送错误报告”。首次安装或升级时会出现简短说明，可直接关闭，也可在 **设置 → 常规** 关闭；关闭状态随设置保存，重启和升级不会主动恢复。读取设置失败时暂停云端发送。未配置接收地址的源码、测试及 Fork 构建仅保留本地脱敏报告。

报告用于诊断 **Crystalfly 自身** 的启动、界面、后台任务、目录刷新、下载、安装和存档操作错误。取消、输入校验失败、正常离线不报告。致命错误尽力落盘，应用按原有方式退出，下一次允许联网时补发。系统强制终止、磁盘不可写等情况不能保证落盘。

| 发送字段 | 明确排除 |
| --- | --- |
| 应用版本及提交标识、系统版本与架构 | 用户名、计算机名、设备唯一标识 |
| 固定操作分类、时间、严重程度 | 存档内容、Steam 账号、密码、令牌 |
| 异常类型、错误码、程序集/类型/方法堆栈 | 原始异常消息、完整路径、命令行、环境变量 |
| 单份报告随机 ID、错误指纹 | 游戏/Mod 原始日志、截图、内存转储 |

原始异常的 Message、Data、ToString() 不参与报告；堆栈不带源文件路径或行号。不启用自动会话、性能、指标、额外日志或用户行为追踪。不创建持续的用户/设备标识。

### 接收方、地区及保留期

官方接收方为 **GlitchTip 官方托管（Burke Software）**，项目位于 **美国纽约 DigitalOcean NYC1（app.glitchtip.com）**。官方政策说明事件自动保留 **90 天**、数据库备份保留 **7 天**；以接收方后续发布的政策为准。维护者后台需要登录，目前仅项目所有者可访问；不会自动创建公开 GitHub Issue。

这是“脱敏报告”，不宣称完全匿名。HTTPS 连接不可避免地向接收方暴露连接 IP，托管方可能按其隐私政策处理 IP 等网络信息。关闭开关仅清理客户端待发送缓存，**不会删除已经送达云端的数据**。如需删除已送达数据，请联系维护者，提供应用版本、出错时间或导出报告中的随机报告 ID；不需要提交存档或令牌。

来源（核对于 2026-09-27）：
- [托管架构、地区和保留政策](https://glitchtip.com/documentation/hosted-architecture/)
- [隐私政策](https://glitchtip.com/legal/privacy/)
- [套餐与配额](https://glitchtip.com/pricing/)

### 本地控制与限量

- 离线模式完全阻止错误上报网络请求，并取消正在发送的请求；关闭上报还清除待发送缓存。
- 每份报告（及单个发送 envelope）不超过 64 KiB；SDK 待发送缓存最多 20 份、保留不超过 7 天。
- 同一错误指纹在同版本下每 24 小时最多记录一次；每客户端 UTC 日最多 5 份。限额包含本地记录，跨重启保存；删除本地数据会重置限额。
- 本地脱敏历史位于应用数据目录 diagnostics/reports，最多 20 份 / 7 天，用于 **导出本地错误报告** 及限流。它不是另一条发送队列；关闭期间的本地历史不会在重新开启时补发。
- 唯一上传队列由 Sentry SDK 管理，位于 diagnostics/cache；发送前再次检查字段白名单、大小和时效。关闭应用期间不运行清理，下次启动/读取时清理过期数据；运行期间每 15 分钟清理。
- 上报请求 5 秒超时；服务不可用时退避。遵守 Retry-After / X-Sentry-Rate-Limits；上报自身的错误不会再次上报。不会影响下载、安装、存档的业务结果。
- 托管免费套餐为整个组织每月 1,000 事件，用户共享配额；本地限额不能保证整个项目永远不触达云端限额。首期未购买付费套餐。

## 构建、Fork 和自建

普通 dotnet build/run 和发布脚本默认无接收地址；不读取 SENTRY_DSN 环境变量。公开仓库中的 config/error-reporting.official.json 只包含 **公开写入 DSN** 和政策说明，没有管理令牌。必须显式传入配置文件才能生成官方上报构建：

    pwsh -File scripts/build-release.ps1 -ErrorReportingConfig config/error-reporting.official.json

本地无签名验证包可另加 -UnsignedLocal。安装到既有位置时，先关闭 Crystalfly：

    pwsh -File scripts/build-and-install.ps1 -ErrorReportingConfig config/error-reporting.official.json -UnsignedLocal

这两个脚本仍然默认跳过全量测试。测试及 Fork 不应选择官方配置；复制该 JSON 并替换为自己的 **公开 HTTPS DSN、region、retention** 即可接自建 GlitchTip 或其他兼容接收端。SDK 不跟随重定向，不接受含管理密码的 DSN。DSN 是公开的写入地址，不能用隐藏客户端配置防止滥用；维护者应在服务端控制配额、限制访问并必要时轮换 DSN。

高级构建使用 CrystalflyErrorReportingDsn、CrystalflyErrorReportingRegion、CrystalflyErrorReportingRetention 三个 MSBuild 属性。提交标识由现有 AssemblyInformationalVersion 注入。未配置 DSN 时仍可正常编译、运行及导出报告。

诊断入口为 Core 的 IErrorReporter；业务只传异常和固定 ErrorOperation。应用层使用独立 SentryClient，不初始化全局 SentrySdk，不把 Core 绑定到供应商。先创建白名单模型并写入有界本地历史，再由 BeforeSend 替换 SDK 富化后的事件，传输层在缓存重放时再次校验。测试注入假 HTTP 处理器，不依赖正式后台。

## 针对性验证

    dotnet test tests/Crystalfly.Core.Tests -c Release --filter "FullyQualifiedName~ErrorReportTests|FullyQualifiedName~CrystalflySettingsStoreTests"
    dotnet test tests/Crystalfly.App.Tests -c Release --filter "FullyQualifiedName~Diagnostics|FullyQualifiedName~ErrorReportingUiTests"
    pwsh -File scripts/test-error-reporting-config.ps1
    pwsh -File scripts/test-build-release.ps1
    pwsh -File scripts/test-build-and-install.ps1

CrashReportingTests 使用隔离子进程触发真正的未处理异常，验证退出、SDK 落盘和重启重放。人工托管联通验证使用单独的临时目录及明确传入的 DSN，仅发送固定构造错误：

    dotnet run --project tests/Crystalfly.Diagnostics.Probe -c Release -- smoke artifacts/diagnostics/cloud-validation <PUBLIC_DSN>

同一目录的重复执行受限流；联通验证只在首次接入或接收配置变更时执行，不加入每轮开发/CI/发布流程，不把游戏/Mod 日志作为测试输入。

## English notice

Official builds with an explicitly configured destination enable sanitized Crystalfly error reporting by default. A one-time notice and Settings → General offer an immediate opt-out. Opt-out survives restarts and upgrades; unreadable preferences suspend uploading. Source, test and fork builds have no destination by default and can still export local sanitized reports.

Reports contain only app version/commit, OS version/architecture, fixed operation, timestamp/severity, exception type/code, assembly/type/method stack, a per-report random ID and error fingerprint. They exclude raw exception messages/data, local paths, usernames, machine/device identifiers, arguments, environment variables, credentials, saves, game/Mod logs, screenshots and dumps. No performance, session, metrics or extra log collection is enabled. Fatal errors are saved best-effort and replayed on restart without suppressing the fatal exception.

The official recipient is GlitchTip hosted by Burke Software in New York, USA (DigitalOcean NYC1). Published event retention is 90 days; database backups are retained for 7 days. The provider may process connection IP addresses, so these reports are sanitized, not fully anonymous. Reports remain in the private maintainer dashboard; public issues are never automatically created. Disabling reporting cancels pending requests and clears the local send queue, but cannot remove events already received by the host.

Each report/envelope is limited to 64 KiB; the single SDK send queue and separate export history each keep at most 20 reports for 7 days. Identical errors are limited to once per release per 24 hours, with a maximum of 5 reports per client UTC day. The quota persists across restarts, includes local-only reports, and resets if local data is deleted. Local history is not replayed when reporting is re-enabled. Offline mode makes no reporting requests. Timeout, network failure and rate limits only affect diagnostics; retry failures do not recursively report themselves.

The free hosted allowance is 1,000 events/month shared by the entire organization, not per user. Only the public ingestion DSN belongs in release configuration. Forks should supply their own HTTPS DSN, region and retention JSON explicitly using -ErrorReportingConfig, or omit it to disable cloud reporting. Management tokens and passwords never belong in the client or repository. The synthetic cloud probe is an explicit one-time integration check, not a routine build or CI step.
