# CrestsEvolve · 空洞骑士：丝之歌 纹章强化 Mod（发布包）

- 包版本：0.58.9（构建日期：2026-08-17）
- 包结构：1 个前置 + 4 个附属插件（共 5 个独立 BepInEx 插件）
- 运行环境：BepInEx 5 / HarmonyX / netstandard2.1 / 《空洞骑士：丝之歌》（Unity 6000.0.50f1，Mono）

## 包内容

本包 `BepInEx\plugins\` 下：

| 插件 | 插件 GUID | 说明 |
|---|---|---|
| CrestsEvolve | `local.crestsevolve` | 前置：事件 / 伤害 / 资源 / 无敌 / 绑定 / 白槽 / HUD 宿主 / FoundationServices |
| CrestsEvolve.LegendsNeverDie | `local.crestsevolve.legendsneverdie` | 附属 1：16 个天赋符文 + 纹章功能 + 蓝血染色 |
| CrestsEvolve.RageOfTheGoner | `local.crestsevolve.rageofthegoner` | 附属 2：亡怒 / 超级亡怒 |
| CrestsEvolve.ToolsAscension | `local.crestsevolve.toolsascension` | 附属 3：36 个工具等级 / 白槽 / 白色工具 |
| CrestsEvolve.InsectMayCry | `local.crestsevolve.insectmaycry` | 附属 4：快捷换纹章 + D/V/N 动作 |

另含：`config/`（数值参考 + 游戏绑定表）、`Textures/`（HUD 图标贴图）、`languages/`（中英文案）、`THIRD_PARTY_NOTICES.md`（第三方声明）。

## 安装

1. 先安装 BepInExPack Silksong（5.4.x）。
2. 安装依赖 mod（本包不包含，需另行安装）：
   - Needleforge 0.9.0
   - Silksong FsmUtil 0.3.17
   - 如需 Prepatcher 投影相关能力：SilksongPrepatcher 1.4.0
3. 备份游戏 `BepInEx\plugins` 下已有的 `CrestsEvolve*` 目录。
4. 把本包内的 `BepInEx` 文件夹合并到游戏根目录（保留原有文件，覆盖同名）。
5. 启动游戏。首次启动会在 `BepInEx\config\` 生成 5 个 `.cfg` 配置文件，可用 BepInEx 配置管理器（F1）调整。

## 配置说明

- `BepInEx\plugins\CrestsEvolve\balance.example.json`：人类可读的数值参考表（运行时数值以 F1 配置为准）。
- `BepInEx\plugins\CrestsEvolve\game-bindings.json`：游戏内部成员绑定表；运行时实际读取 `BepInEx\config\CrestsEvolve\game-bindings.json`。
- `BepInEx\plugins\CrestsEvolve\Textures\*.png`：HUD 状态图标外部贴图，可自行替换。
- `BepInEx\plugins\CrestsEvolve\languages\*.json`：中英文案覆盖。

## 回滚

删除游戏 `BepInEx\plugins` 下的 `CrestsEvolve`、`CrestsEvolve.LegendsNeverDie`、`CrestsEvolve.RageOfTheGoner`、`CrestsEvolve.ToolsAscension`、`CrestsEvolve.InsectMayCry` 五个目录，并删除 `BepInEx\config\CrestsEvolve` 与 5 个对应 `.cfg`，即可完全恢复原版。

## 功能说明

四个附属 mod 的完整功能清单见 [功能说明.md](./功能说明.md)。

## 本包验证状态

- Release 构建：0 警告 0 错误
- `dotnet test`：1121 通过 / 0 失败
- 契约校验：PASSED（4 个既有警告：3 个兼容性空 catch + game-bindings 占位符）
- 程序集审计（插件 GUID / 补丁唯一性）：PASSED
- 说明：大量功能已接线并通过本地构建/测试，部分已实机验证；仍有部分功能处于“待本机游戏内验收”状态，具体见功能说明中的标注。发布前请按仓库 `docs/01_开发计划.md` 的验收清单逐项确认。
