# 丝之歌多版本管理与 Thunderstore

本次扩展面向 Windows x64，同时保留《空洞骑士》原有工作流。标题仍为 Crystalfly。游戏文件、用户存档和 Steam 凭据不随源码或验证构建分发。

## 使用入口

1. 在内容区域顶部选择「空洞骑士」或「丝之歌」。两款游戏分别保存版本库、当前实例、实例搜索、市场搜索与筛选。
2. 本地导入：在「设置 → 游戏目录」扫描 Steam 库或选择本地游戏目录。下载新副本：在「下载 → 游戏版本」先选择版本库目录，再选公开版、已核验历史版本，或输入 Manifest。
3. 下载创建独立实例。队列固定记录目标游戏、实例、App/Depot 与实际 Manifest；之后切换游戏不改变任务目标。
4. 在丝之歌「模组市场」刷新 Thunderstore，搜索/分类/排序，选择包与版本，点击安装预览。预览列出依赖及版本变化；对未声明兼容性的组合确认一次后执行。
5. 实例「已安装模组」支持启停、版本锁定、修复、卸载、预设导入导出。更新使用最新版本；降级通过详情版本选择后重新预览。仍被其他包依赖的包不能直接卸载或禁用。
6. 可粘贴丝之歌 Thunderstore 包链接，或导入标准 ZIP。ZIP 导入要求已有目录以解析依赖；首次完全离线且没有目录时给出说明。离线安装仅使用已校验的完整缓存。
7. 在「存档」中编辑当前实例或命名快照，支持四个槽位、DAT 导入导出、中文/原始字段搜索。修改前自动备份；切换实例/游戏前需保存或重置未保存编辑。

两款游戏共享 Steam 登录、主题、网络和错误报告设置。丝之歌不显示空洞骑士的 Modding API、正式速通环境模板或本体存档词库；社区和榜单页面保留。

## 游戏与版本身份

| 游戏 | GameId | Steam App | Windows Depot | 可执行文件 |
| --- | --- | ---: | ---: | --- |
| 空洞骑士 | hollowKnight | 367520 | 367521 | hollow_knight.exe |
| 丝之歌 | silksong | 1030300 | 1030301 | Hollow Knight Silksong.exe |

身份由 GameDefinition 集中管理。构建/通道查找以 GameId + ID 为键；同名跨游戏版本、加载器及模组不能相互匹配。public 在下载时解析实际 Manifest，不能将一个静态历史 Manifest 永远标为最新。

首批丝之歌构建为 1.0.30000 与 1.0.29980，详见 [核验记录](silksong-verification.md)。手动 Manifest 下载保留真实 Manifest，未匹配受信任构建时显示未验证；修复仍使用该实例记录的精确 Manifest。加载器可安装不代表所有第三方模组对所有游戏版本兼容。

扫描、启动、删除和存档占用检查按目标游戏的进程区分。丝之歌启动携带对应 Steam App 环境标识。丝之歌完整克隆复制游戏文件、受管模组/配置、当前隔离存档、备份与命名快照，并重新绑定快照路径；不会改变源实例。删除同时清理游戏目录、登记元数据及该实例独立状态，清理中断保留恢复日志。

## 旧数据迁移与存储

- 旧 JSON 未写 GameId 时按空洞骑士读取，包括实例、构建、收据、预设、快照和未完成队列。保留原实例 ID、路径与收藏。
- 首次读取旧设置时先保留 settings.json.pre-multigame.bak，再通过 AtomicJsonStore 原子写入 GameWorkspaces。不会为迁移移动游戏目录。
- 空洞骑士既有状态路径保持不变。新增字段在后续正常写入时保存；未修改的旧实例/快照无需重新写一遍。
- 实例登记元数据仍在版本库的 .crystalfly/instances/ID；丝之歌的 LocalLow、快照与存档恢复状态在 .crystalfly/games/silksong/instances/ID。实例 ID 仍全局唯一。
- 丝之歌共享 Steam 存档根目录为用户配置文件下 AppData/LocalLow/Team Cherry/Hollow Knight Silksong，存档位于账号 ID 或 default 子目录；空洞骑士继续使用 Team Cherry/Hollow Knight。首次接管前保留共享目录备份，退出后回收该实例存档。旧版本错误使用的 Team Cherry/Silksong 备份与空基线按[修复说明](silksong-save-profile-fix.md)保留并迁移。
- Thunderstore 目录与包缓存位于应用数据目录 thunderstore/silksong。安装收据位于每个实例的 .crystalfly-thunderstore/installed.json，禁用文件保存在其 disabled 子目录。
- 回退应用前先退出游戏和 Crystalfly，备份当前设置与各版本库的 .crystalfly。保留新数据；若恢复旧设置备份，应先切回空洞骑士。没有自动逆向迁移。

## Thunderstore 安装规则

使用固定 hollow-knight-silksong 社区的 package-listing-index，读取 GZip 索引与分块；索引不可用时兼容社区 v1 接口。只有完整目录通过验证才替换缓存，失败使用上一份完整缓存；损坏缓存可回退原子备份。429 遵守 Retry-After。

目录保留每包所有版本、完整身份、依赖、分类、弃用状态与原始作者内容。默认隐藏弃用包，已安装包和依赖仍参与解析。中文界面使用独立维护的译名、简介、分类及匹配版本的文档，保留原文入口；英文界面使用作者内容。具体基线、覆盖数量和维护方法见[丝之歌 Mod 汉化说明](silksong-mod-translations.zh-CN.md)。

- 普通模组：优先复用满足要求的已安装依赖，缺失时选最新可用版本。
- 整合包：按照声明的精确依赖版本解析。
- 锁定版本：不会静默升级或降级；冲突在写入前报告。
- 兼容性：已验证、未验证、已知不兼容三态；不凭名称推断游戏/架构兼容性。未知组合需确认，明确不兼容规则阻止安装。
- 固定加载器为 silksong_modding-BepInExPack_Silksong-1.0.3，内部运行时单独记录为 BepInEx 5.4.23.4。旧 BepInEx-BepInExPack_Silksong 的 5.4.2303、5.4.2304、5.4.75304 依赖通过显式迁移表映射；其他名称不会模糊合并。

| 包内目录/内容 | 安装位置 |
| --- | --- |
| plugins、普通插件及资源 | BepInEx/plugins/包完整名称 |
| patchers | BepInEx/patchers/包完整名称 |
| core | BepInEx/core/包完整名称 |
| monomod、根目录 .mm.dll | BepInEx/monomod/包完整名称 |
| config | BepInEx/config；保留已有用户配置 |
| 纯依赖整合包 | 保存收据与依赖，不制造空 DLL |
| 已批准的加载器包 | 专用适配器处理 core 和允许的根目录注入文件 |

下载、ZIP 检查和整个依赖集合暂存完成后，使用既有 FileTransaction 同时提交文件与收据。每包记录全部文件归属及 SHA-256；文件冲突、越界路径、重解析点、错误架构和包内元数据不一致会阻止安装。卸载不删除用户配置。

普通 Thunderstore 包没有发布方摘要时，检查 HTTPS 来源、逐跳重定向、声明大小、ZIP 结构、名称、版本和依赖，记录本地 SHA-256。同一包版本出现不同字节内容时停止覆盖；本地摘要不是发布方签名。原有可信 catalog 的预期哈希仍严格验证。专用加载器额外固定已核验摘要。

## 存档写入边界

沿用经过实际游戏编码样本核验的封装/AES 编解码。生命、丝线、念珠、贝壳碎片、技能、工具、纹章、任务及场景字段采用丝之歌专用词库；未知字段显示原始名称。编辑保持键、类型、嵌套数组及未知字段。

可写入条件为文件能完整往返、JSON 结构重建不丢失、游戏未运行。损坏/未知格式只读并说明原因；切换到损坏槽位失败不会把上一槽位的编辑写到错误文件。导入先核对游戏身份；较新或无法比较的版本需要确认风险，不做格式转换。写入前备份、提交前重新解码、提交后复核，失败回滚。

## 验证与构建

参见 [验证命令、结果与实际边界](silksong-verification.md)。本次不创建丝之歌正式速通模板，不支持 Xbox WGS 或非 Windows 下载，不接管其他模组管理器的协议关联。未自动提交、推送、发布或覆盖本地正式安装。

官方规则参考：

- [Thunderstore 丝之歌游戏定义及安装路由](https://github.com/thunderstore-io/ecosystem-schema/blob/master/games/data/hollow-knight-silksong.yml)
- [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/1.0.3/)
