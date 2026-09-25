# 速通工作台与社区直达设计

## 背景

现有速通页把环境列表、创建环境、RuntimePatches 配置、验证启动、LiveSplit 收藏和成绩动态分散在三列及底部 Tab 中。900×600 窗口下底部 Tab 会遮挡配置内容，成绩动态无数据时留下大面积空白。启动首页只有实例状态，没有 HK / Silksong 速通社区入口。

## 目标

1. 将速通页重构为以“当前环境下一步操作”为核心的速通工作台。
2. 在启动首页右侧提供 HK / Silksong 速通、资源、Discord 等社区直达入口。
3. 支持内置链接与用户自定义链接，并保持旧 settings.json 兼容。
4. 在 900×600、1280×720、1920×1080 下保持主要操作可见且不重叠。
5. 不改变速通环境创建、RuntimePatches 安装、验证、启动和成绩缓存的领域行为。

## 非目标

- 不在启动时联网验证社区链接。
- 不改变 Speedrun.com 成绩抓取、缓存和榜单解析协议。
- 不增加新的 UI 动画或外部 UI 框架。
- 不让用户自定义链接覆盖或删除内置链接。

## 信息架构

### 启动首页

启动页改为 `实例摘要与启动操作 | 启动预检与状态 | 社区直达` 三列。社区栏使用可滚动的普通内容区域，不覆盖启动预检。宽度不足时社区栏移入主内容下方，启动和预检优先保留。

社区链接按 Hollow Knight、Silksong、其他分组显示。每项包含 Lucide 图标、可读名称、来源域名 Tooltip 和 `AutomationProperties.Name`。内置链接先显示，自定义链接随后显示；自定义链接提供添加、编辑、删除按钮。

### 速通环境页

保留“环境 / 赛况”两个 Tab。Tab 切换条移入工作区顶部的普通布局流，去除底部覆盖式定位。

环境 Tab 的阅读顺序固定为：

1. 环境列表与创建。
2. 当前环境摘要（名称、版本、技术状态、校验并启动）。
3. 准备检查与报告状态。
4. RuntimePatches 配置，按计时辅助和存档状态分组。
5. LiveSplit 收藏与文件操作。

赛况 Tab 将监控状态、更新时间、筛选和刷新放在顶部；列表为空时使用内容流中的紧凑空状态，不让空状态占满星号高度。

## 数据模型与边界

在 `Crystalfly.Core.Configuration` 增加 `SpeedrunCommunityLinkDefinition`，字段为 `Name`、`Group`、`Url`、`IconKey`。`CrystalflySettings` 增加 `SpeedrunCommunityLinks`，默认值为空集合，保持现有配置反序列化兼容。

内置目录由 App 层的静态目录提供，包含已核验的 HK Speedrunning、HK-Resources、Hollow Knight / Silksong Speedrun.com、Hollow Knight 社区 Discord 和 HK Speedrunning Discord 入口。内置项目带稳定 ID，用户项目使用生成 ID。

新增 `SpeedrunCommunityLinksViewModel`，负责：

- 合并内置与用户项目并按分组暴露集合。
- 添加、编辑、删除自定义项目。
- 规范化名称、分组、图标和 URL。
- 通过现有 settings 保存队列异步持久化。

自定义 URL 必须是 HTTPS 绝对 URI，不得包含用户名或密码，长度限制为 2048 个字符；规范化后不得重复。图标仅允许固定 Lucide 图标键集合。非法或损坏的自定义项目在加载时丢弃，不阻止页面加载。

## 外链行为

所有社区项目复用现有 `OpenExternalUrl` 路径。打开前再次确认 HTTPS；Process.Start 失败写入现有 `ErrorMessage`。不对社区网站做后台请求，不因 OfflineMode 隐藏入口。

## 文件边界

- `src/Crystalfly.Core/Configuration/CrystalflySettings.cs`：持久化定义。
- `src/Crystalfly.App/ViewModels/SpeedrunCommunityLinksViewModel.cs`：社区目录状态和命令。
- `src/Crystalfly.App/ViewModels/MainViewModel*.cs`：facade 属性、初始化和保存联动。
- `src/Crystalfly.App/Views/MainWindow.axaml`：启动首页社区栏、速通工作台布局。
- `src/Crystalfly.App/Views/MainWindow.SpeedrunHandlers.cs`：编辑对话框与外链事件。
- `src/Crystalfly.App/ViewModels/LocalizationViewModel.cs`：中英文文案。
- `tests/Crystalfly.Core.Tests/Configuration/*`：设置往返与规范化测试。
- `tests/Crystalfly.App.Tests/ViewModels/*`：社区命令与保存测试。
- `tests/Crystalfly.App.Tests/Ui/*`：结构、可访问性和多窗口尺寸测试。

## 验证

先运行现有 App 测试作为基线，再新增针对模型、命令和 UI 的回归测试。完成后运行 App、Core、Steam 测试及 Release 构建；截图测试覆盖 900×600、1280×720 和 1920×1080 中文界面。验证启动、速通环境和成绩动态的既有行为不变。
