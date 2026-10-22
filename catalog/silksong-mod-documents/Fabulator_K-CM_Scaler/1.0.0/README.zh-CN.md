# CM_Scaler (ConfigurationManagerScaler)

一个简单的插件，用于缩放配置管理器界面以适应高分辨率显示器。

[BepInEx ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)的F1菜单以固定像素大小绘制——在1440p时为小尺寸，在4K时为极小尺寸。CM_Scaler会将CM的渲染过程封装在一个统一的缩放比例中，使整个窗口（文本、滑块、复选框）随你的分辨率扩大。不会修改任何 ConfigurationManager 文件。

## 设置（请在缩放窗口中直接编辑）

- **自动缩放**（默认开启）—— 根据您的分辨率进行适配：`screen height / 1080`，永不小于 1.0，上限为 2.5。
- **手动缩放**（0.5–3.0）—— 当自动缩放关闭时使用。

## 注意事项

- 需要 ConfigurationManager（mono/BepInEx 5）。没有它，插件将静默无操作。
- 本内容并非丝之歌专属——相同的 DLL 应能在任何运行 ConfigurationManager 的 BepInEx 5 游戏中使用。

## 法律声明与署名

包图标基于 [BepInEx](https://github.com/BepInEx/BepInEx) 的标志设计，仅用于识别。CM_Scaler 是一个独立的社区插件：它与 BepInEx 团队或 ConfigurationManager 作者无任何关联、未获其支持或背书，若出现任何问题，请向我反馈而非向他们报告。

BepInEx 采用 LGPL-2.1 许可证，ConfigurationManager 采用 LGPL-3.0 许可证。本插件不包含 BepInEx 或 ConfigurationManager 项目的任何代码或文件，仅在运行时通过其公开 API 进行链接，并在内存中对 ConfigurationManager 进行动态修补。

如果 BepInEx 的维护者希望此处不再使用其标志，请与我联系，将立即替换。
