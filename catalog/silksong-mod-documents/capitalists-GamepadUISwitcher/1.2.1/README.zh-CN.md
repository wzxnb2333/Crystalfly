# 游戏手柄 UI 切换器  
一个允许你覆盖游戏手柄 UI 提示的模组。

## 特性

- 允许在以下手柄之间切换提示
    - PlayStation（DualShock 4，DualSense）
    - Steam Deck
    - Switch（Joycons，Pro 控制器）
    - Xbox（360，One，Series X）

- 交换面按按钮，例如：A <-> B，X <-> Y / Cross <-> Circle，Square <-> Triangle

## 安装

### 简单方式
使用 r2modman 或 Gale。

### 手动安装
1. 下载 [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/) 并将其解压到游戏文件夹，位于游戏可执行文件旁边  
2. 下载 [Silksong.I18N](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/) 并将其解压到 `<game folder>/BepInEx/plugins` 内部  
3. 下载 [GamepadUISwitcher.zip](https://github.com/capitalistspz/GamepadUISwitcher/releases/latest) 并将其解压到 `<game folder>/BepInEx/plugins` 内部  
4.  
    - （Windows）正常运行游戏  
    - （Linux/MacOS）运行 `run_bepinex.sh`

## 配置

从游戏的主菜单或暂停屏幕中使用 `Options` -> `Controller`。
![Switch Pro Menu](https://raw.githubusercontent.com/capitalistspz/GamepadUISwitcher/refs/heads/master/dist/preview/menu-switch-pro.jpg)

或者，如果你安装了 `BepInEx.ConfigurationManager`，可以使用其菜单（默认：F1）。

## 预览

![Switch In-game](https://raw.githubusercontent.com/capitalistspz/GamepadUISwitcher/refs/heads/master/dist/preview/ingame-switch.jpg)

![DualShock 4 Menu](https://raw.githubusercontent.com/capitalistspz/GamepadUISwitcher/refs/heads/master/dist/preview/menu-ds4.jpg)

![DualShock 4 Remap](https://raw.githubusercontent.com/capitalistspz/GamepadUISwitcher/refs/heads/master/dist/preview/remap-ds4.jpg)
