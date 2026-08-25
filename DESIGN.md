# Crystalfly 设计

## 1. 目标

Crystalfly 是面向 Windows 10/11 x64 的 Hollow Knight 环境管理器。它把游戏版本、实例、Loader、Mod、LocalLow 存档、速通环境和 Steam 下载放在同一个可恢复的工作流中。

核心目标：

- 用“实例”隔离不同游戏版本、Loader、Mod 和存档状态。
- 让下载、安装、切换、恢复和卸载都具备可验证、可回滚的边界。
- 只在 catalog 明确验证后提供 Loader 和速通能力；未知游戏版本仍可作为 Vanilla 实例启动。
- 在离线、网络失败、Steam 重连和程序崩溃后保留可解释的状态。

非目标：

- 不分发第三方游戏、Loader 或 Mod 文件。
- 不把所有 Loader 或 Mod 版本视为兼容。
- 不用隐藏的全局状态替代实例级数据。

## 2. 用户工作流

应用启动后先进入实例选择。用户选择一个实例后，后续页面都在该实例上下文中工作；切换实例必须显式发生。

主要入口：

| 入口 | 责任 |
| --- | --- |
| 启动页 | 实例选择、启动预检、启动和问题处理 |
| 实例详情 | Loader、Mod、配置、日志、快照和整合包 |
| 下载中心 | 游戏版本、Mod 市场、下载队列、重试和历史 |
| 设置 | 网络线路、离线模式、主题、背景、更新和新手引导 |
| 速通环境 | 从 Vanilla 实例创建独立副本并校验 RuntimePatches |

启动前检查按阻断级别处理：游戏文件、Loader、事务、LocalLow 和进程冲突始终阻止启动；Mod 文件或依赖问题只有在用户明确确认后才允许强制启动。

## 3. 系统边界

```text
Crystalfly.App      Avalonia UI、ViewModel、导航、下载队列、更新和协议
        |
        +--> Crystalfly.Core   实例、catalog、Loader、Mod、事务、LocalLow、快照、速通
        |
        +--> Crystalfly.Steam  SteamKit2 登录、Manifest、Depot 和 CDN 下载
        |
        +--> Crystalfly.Updater 独立更新进程、资产校验、安装和恢复
        |
        +--> services/preset-share  可选的整合包分享服务
```

依赖方向保持单向：UI 编排领域服务，Core 保存业务不变量，Steam 只负责 Steam 传输和认证，Updater 不依赖桌面 UI。领域 ViewModel 通过 `XxxDependencies` 接收依赖，不在内部创建共享服务，也不引入 DI 容器。

## 4. 核心领域模型

- **Build**：由已验证文件指纹标识的不可变游戏构建；`latest` 和 `public` 只是指向 Build 的 channel。
- **Instance**：一个可启动的游戏目录及其 `.crystalfly-instance.json` 元数据。
- **Loader**：实例最多一个有效 Loader 状态：`Vanilla`、`ModdingApi`、`BepInEx`、`Conflict` 或 `Drifted`。
- **Mod receipt**：记录受管理 Mod 的版本、文件哈希、启用状态和归属，不以目录扫描结果代替收据。
- **Transaction**：对 Loader、Mod、LocalLow 或快照恢复的可恢复文件操作。
- **Named snapshot**：实例级、永久保留的存档快照；不是事务临时备份。
- **Speedrun environment**：由干净 Vanilla 实例复制出的独立副本，包含经过哈希校验的 RuntimePatches。

## 5. 关键状态与数据流

### 5.1 实例修改

所有会替换或删除文件的操作都遵循：

```text
Prepared -> Applying -> Committed
                 \-> RollingBack -> RolledBack
                                  \-> NeedsAttention
```

写入前先检查路径、文件类型、哈希和目标兼容性；目标文件先进入事务私有备份。提交后删除临时恢复点，回滚不确定时保留 journal 并禁止启动，不猜测文件状态。

### 5.2 LocalLow 隔离

运行游戏前，`InstanceRuntimeSession` 获取全局互斥体并确认没有其他 Hollow Knight 进程。它把选中实例的 LocalLow staged 到共享目录，退出后再以哈希校验的 capture 写回实例，并恢复原共享目录。缺失、变化或歧义的目录布局进入 `NeedsAttention`。

### 5.3 下载队列

下载先经过 URL 策略、声明大小或可信 Content-Length、SHA-256 和 ZIP 路径校验，再写入按哈希复用的缓存。依赖链按 Loader、前置、主 Mod 串行；无关安装组最多三路并发。网络错误最多自动重试三次，校验和兼容性错误直接保留为失败任务。

### 5.4 Catalog 与信任

信任优先级为：内置官方 catalog、官方 Hollow Knight XML、命名空间隔离的用户 HTTPS 源。远程 catalog 必须先完成 schema、哈希、URL 和引用校验，成功后才原子替换缓存。用户源不能新增 Build、Loader、channel 或已验证速通状态。

## 6. 持久化布局

```text
<version-root>/
  <instance>/.crystalfly-instance.json
  .crystalfly/
    catalog/ downloads/ packages/ transactions/
    instances/<instance-id>/
      local-low/ mods/ presets/ snapshots/ speedrun-reports/
    local-low/
      shared-backup/ takeover.json transactions/<session-id>/journal.json
```

安装模式设置位于 `%LOCALAPPDATA%\Crystalfly`；便携模式使用程序旁的 `Data`。应用更新只替换程序边界，不触碰实例目录、缓存、存档和 LocalLow。

整合包 JSON 限制为 128 KiB、1000 个条目，并绑定精确 Build 与 Loader package ID。分享服务只保存校验后的 JSON 和删除 token 的 SHA-256，分享码有效期为 180 天；全局离线模式下禁用远程分享，但本地导入导出仍可用。

## 7. UI 与代码组织

- `Crystalfly.App` 使用 Avalonia、Semi.Avalonia 和 CommunityToolkit.Mvvm。
- `MainViewModel` 持有共享状态和导航，领域逻辑拆为 `SettingsViewModel`、`InstancesViewModel`、下载、Mod、速通等领域 ViewModel。
- ViewModel 之间通过 MainViewModel 的回调或事件联动，避免反向引用主 ViewModel。
- 页面保持现有 `CurrentPage + IsVisible` 导航模式；迁移绑定时保留 MainViewModel facade，降低 UI 和测试的破坏范围。
- 动画遵循系统偏好，支持跟随系统、减少动效和关闭；状态反馈必须可见且与实际事务状态一致。

## 8. 安全与可靠性不变量

1. 未通过路径校验、哈希校验或 ZIP traversal 检查的内容不得安装。
2. 事务、LocalLow 会话和更新操作必须可恢复；不确定状态宁可阻止启动，也不覆盖用户数据。
3. Steam 凭据和 refresh token 只保存在当前 Windows 用户的 DPAPI 存储中。
4. 更新清单先验证 Ed25519 签名，再使用版本、大小和 SHA-256 字段；安装期间保持资产读共享锁。
5. 离线模式阻断远程读写和 Steam 队列，但不阻断本地实例、快照和 JSON 导入导出。

## 9. 验证策略

- Core：事务、路径安全、目录哈希、catalog、Loader/Mod 兼容性和 LocalLow 恢复测试。
- App：Avalonia Headless UI 测试、ViewModel 状态测试、导航和布局回归测试。
- Steam：认证、DPAPI、Manifest 和下载错误路径测试。
- Updater：签名、资产校验、安装边界、回滚和健康握手测试。
- 发布前执行：

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet build '.\Crystalfly.slnx' -c Release --no-restore
dotnet test '.\Crystalfly.slnx' -c Release --no-build
```

## 10. 相关文档

- [架构细节](docs/architecture.md)
- [领域 ViewModel 规范](docs/domain-viewmodel-guidelines.md)
- [产品事实](docs/product-facts.md)
- [目录格式与翻译](docs/mod-translations.zh-CN.md)
- [构建与发布脚本](scripts/build-release.ps1)

## 11. UI 设计美学

### 11.1 设计方向

Crystalfly 的视觉方向是“安静、可靠、带有游戏气质的桌面工具”。界面首先服务于重复操作和状态判断，其次才表达 Hollow Knight 的品牌氛围。整体避免营销页式的大标题、装饰性卡片堆叠和过度动效；使用紧凑的信息层级、稳定的布局和明确的操作反馈。

关键词：

- **沉静**：低饱和的深色基础、克制的高亮色和充足但不过量的留白。
- **清晰**：标题、状态、操作和危险提示有稳定的字号、颜色与位置关系。
- **精致**：细边框、轻微阴影、统一圆角、Lucide 图标和高质量素材共同建立质感。
- **可信**：成功、进行中、警告、阻断和未验证状态不能只依赖颜色，必须同时使用文字、图标或结构变化表达。

### 11.2 视觉语言

- **色彩**：背景使用中性黑灰作为基底，面板使用相邻但可区分的层级色；蓝绿色用于主要强调，琥珀色用于警告，红色只用于阻断或破坏性操作，避免整页被单一色相占据。
- **材质**：优先使用半透明深色面板、细描边和轻微高光；不使用渐变球、气泡、玻璃拟态大面积铺底或纯装饰渐变。
- **圆角**：工具栏、输入框和卡片统一使用不超过 8px 的圆角；按钮和列表项尺寸稳定，悬停不改变布局尺寸。
- **图标**：工具操作优先使用 Lucide 图标；仅图标按钮必须提供可识别的悬停提示，文字按钮只用于明确命令。
- **插图与图片**：启动页和实例相关页面优先展示真实的游戏或应用素材，让当前实例、版本或状态在首屏可感知；图片不使用过度模糊、过暗或只提供氛围的裁切。

### 11.3 页面构图

- **启动页**：以当前实例为首要视觉信号，左侧或主区域展示实例名称、游戏版本、Loader 和健康状态；右侧提供启动和实例操作。预检问题必须在首屏形成连续的状态带，而不是隐藏在深层弹窗。
- **实例详情**：采用“摘要 + 分段内容 + 就地操作”结构。摘要区域固定显示当前实例、Build、Loader、Mod 健康和 LocalLow 状态；详情页使用 Tab 或明确分区承载配置、Mod、快照、日志和整合包。
- **下载中心**：以队列和进度为主，不使用大面积空白卡片。状态、速度、剩余时间、重试和取消操作在同一行或同一组内保持可扫描。
- **设置页**：使用分组列表和紧凑表单；危险设置与普通偏好分离，网络线路、离线模式、更新和外观设置保持稳定顺序。
- **弹窗与覆盖层**：只承载需要用户确认或补充信息的短流程。标题说明当前动作，正文说明影响，底部操作按“取消 / 次要动作 / 主要动作”排列；长内容回到页面，不把完整管理流程塞进弹窗。

页面分区不嵌套卡片。卡片只用于单个重复条目、对话框和真正需要边界的工具；整页分区使用全宽背景带或无框约束布局。

### 11.4 字体与排版

- 正文使用清晰的无衬线字体，标题、正文、辅助说明和数字状态建立明确的字号阶梯。
- 不用随视口宽度缩放字号；桌面窗口缩放通过布局、列宽和换行适配。
- 标题保持短而具体，避免在紧凑面板中使用英雄式大字。
- 版本号、文件名、哈希、路径和下载速度使用等宽或数字稳定的排版，避免状态刷新导致跳动。
- 所有文本必须在父容器内完整显示；按钮和列表项允许自然换行，禁止文字覆盖相邻控件。

### 11.5 动效与反馈

- 动效用于页面切换、列表插入、状态变化和操作确认，不用于持续装饰。
- 使用现有 `MotionCoordinator` 和系统动效偏好；减少动效时缩短或移除过渡，关闭动效时保留状态变化本身。
- 交互反馈分为即时反馈、进行中反馈和最终结果：点击后立即改变控件状态，后台任务显示进度，完成或失败显示可定位的结果。
- 加载状态保持固定尺寸，避免 spinner、文字或进度条出现时页面跳动。

### 11.6 可访问性与验收

- 所有可操作元素支持键盘焦点和合理的 Tab 顺序；焦点环在深色背景上具有足够对比度。
- 图标按钮有可读名称，状态颜色有文字或图标补充，错误提示不只使用红色。
- 1280x720、1920x1080 和 900x600 窗口下检查首屏层级、文本溢出、弹窗边界、列表密度和按钮可达性。
- Avalonia Headless 测试验证关键控件存在、绑定路径稳定和阻断状态可见；截图验收验证真实素材加载、窗口缩放和主要流程无重叠。
