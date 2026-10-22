# 背景模糊优化

一个专为《空洞骑士：丝之歌》制作的模组，用于提升背景模糊效果的质量，并支持部分自定义设置。

## 特性

游戏原生以固定 360p 分辨率渲染背景。虽然由于叠加的模糊效果，这一点并不明显，但在移动时会显得明显且令人分心，出现闪烁现象，如下图所示：

之前：

https://github.com/user-attachments/assets/83eef05f-16b2-497f-8402-fe771f915fc8

之后：

https://github.com/user-attachments/assets/aebc2904-8367-448e-a53f-d6d412184327

图像对比：

https://compare.promptingpixels.com/a/k6FwSSq

该模组允许通过以下方式调整模糊效果：
- 提高分辨率
- 增加模糊强度
- 完全禁用该效果

## 安装

### 简单方式

使用 r2modman 或 Gale。这将正确处理依赖项。

### 手动安装

1。下载 [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/) 并将其解压到游戏文件夹，位于游戏可执行文件旁边  
2。下载 [BepinExConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepinExConfigurationManager/)  
3。下载 [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/) 及其依赖项  
3。从 [github](https://github.com/alexkiri/BackgroundBlurImprovedPlugin/releases)、[thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/alexkiri/BackgroundBlurImproved/) 或 [nexusmods](https://www.nexusmods.com/hollowknightsilksong/mods/661) 下载，并将其解压到 `<game folder>/BepInEx/plugins`  
4。  
    - （Windows）正常运行游戏  
    - （Linux/MacOS）运行 `run_bepinex.sh`

## 配置

以下任一方法均可使用：
- 使用 `ModMenu` 并在游戏内选项 -> 模组中访问
- 使用 `BepinExConfigurationManager` 并默认按 F1 打开
- 手动编辑位于 `<game folder>/BepInEx/config` 内部的 `io.github.alexkiri.backgroundblurimproved.cfg` 文件

结合使用这些参数可以使游戏画面看起来一致，但完全消除令人分心的闪烁现象。

- `ApplyBlurPreset`
  - 允许从 UI 中应用预设（仅 `BepinExConfigurationManager` 支持）
- `BlurTextureHeight`
  - 提高显示背景模糊效果的渲染目标分辨率
  - 提升效果质量
  - 降低效果强度
  - 对性能影响极小
- `BlurPassCount`
  - 增加背景模糊效果的渲染次数
  - 可增强效果强度
  - 对性能影响中等至显著，值大于 10 不推荐使用
- `BlurEffectEnabled`
  - 可完全禁用背景模糊效果
  - 不推荐，部分源纹理分辨率较低，若移除模糊效果，背景将显得不一致
- `BloomResolution`
  - 指定发光效果的分辨率
  - 可选择低分辨率 -> 1/4 主分辨率，或高分辨率 -> 1/2
- `BloomBlurIterations`
  - 发光效果的迭代次数

由于本模组启用后，主菜单中的“模糊质量”设置已无实际效果，因此已从主菜单中移除该选项。

## 其他重要说明

使用大量 `BlurPassCount` 会导致着色器略微偏移背景，通过的次数越多问题越明显。唯一的解决方法是使用 [renodx](https://github.com/clshortfuse/renodx/wiki/Mods)（请按照安装说明操作，并查找 Silksong）。该模组通过添加 HDR 和 16 位渲染显著提升了游戏画面效果，同时未破坏原作的艺术意图。强烈推荐使用。

## 待办事项
- [ ] 将滑块更新为使用 [WiderSliderElement](https://github.com/kaycodes13/Silksong.EdgeDetection/blob/main/Menu/WiderSliderElement.cs)，在 ModMenu 中显示效果更佳
- [ ] 使用 [Silksong.I18N](https://github.com/silksong-modding/Silksong.I18N) 添加翻译
- [ ] 更新背景绘制逻辑，以支持多个背景图层并以不同强度进行模糊处理
