# Radiance 更新日志 / Changelog

## 0.2.1

### 修复 / Fixed

- **辐光光球伤害恢复**：修复启用原版辐光修复后，光球在对象池复用时可能持续无伤害的问题。
- **Radiant Orb damage restore**: Fixed an issue where, after enabling the vanilla Radiance fix, pooled orbs could remain harmless when reused.

### 0.2.0

### 新增 / Added

- **游戏内配置面板**：按 F6（默认，可在配置文件中更改）打开/关闭配置界面，实时调整模组选项。
  - **In-game config panel**: Press F6 (default, configurable in config file) to toggle the config panel and adjust mod options in real time.

- **永恒之光 (Immortal Light)**：引入一代中强力的 mod boss「永恒之光」
  - **Immortal Light**: Introduces the powerful HK1 mod boss "Immortal Light"

- **原版 Bug 修复**：可选修复原版无上辐光 (Absolute Radiance) 的部分 bug：
  - 光球消散后仍造成伤害
  - 死亡准备 → 最终冲击 过渡期玩家仍可受伤
  - **Vanilla bug fixes**: Optional fixes for Absolute Radiance bugs:
    - Orbs still dealing damage after dissipating
    - Player taking damage during Death Ready → Final Impact transition

- **丝绸资源限制**：进入 Radiance 场景时，将丝绸上限限制为 9 点，超出部分自动移除。
  - **Silk cap**: When entering the Radiance scene, silk is capped at 9 (matching HK1 challenge), excess silk is removed.

### 改进 / Improved

- **皮肤 Mod 兼容**：为 tk2d 图集添加 (HK1) 后缀命名空间，以兼容 Patchwork 等皮肤 mod。
  - **Skin mod compatibility**: Added (HK1) suffix namespace to tk2d collections for Patchwork skin mod compatibility.

- **Mac 系统适适配**：运行时 shader 兼容处理，尽可能适配 Mac 平台（作者无 Mac 设备，无法实测，欢迎反馈）。
  - **Mac support**: Runtime shader compatibility for Mac (author has no Mac to verify; feedback welcome).

- **配置界面增强**：为所有配置项添加中英双语描述，支持滚动查看，优化窗口布局。
  - **Config UI enhancements**: Bilingual descriptions for all options, scroll view, improved layout.
