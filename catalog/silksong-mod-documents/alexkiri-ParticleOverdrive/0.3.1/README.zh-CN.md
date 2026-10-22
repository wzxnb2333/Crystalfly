# 粒子狂飙

一个专为《空洞骑士：丝之歌》制作的模组，可将粒子数量提升到荒谬的程度

https://github.com/user-attachments/assets/55179fb4-7e0b-4c08-a33d-08f62880fcee

## 安装

### 简单方式

使用 r2modman 或 Gale。

### 手动安装

1. 下载 [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/) 并解压到游戏可执行文件旁边的游戏文件夹  
2. 下载 [BepinExConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/)  
2. 从 [github](https://github.com/alexkiri/ParticleOverdrivePlugin/releases)、[thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/alexkiri/ParticleOverdrive/) 或 [nexusmods](https://www.nexusmods.com/hollowknightsilksong/mods/736) 下载，并解压到 `<game folder>/BepInEx/plugins`  
3.  
    - (Windows) 正常运行游戏  
    - (Linux/MacOS) 运行 `run_bepinex.sh`

## 配置

使用 `BepinExConfigurationManager`（默认按 F1 打开）实时调整参数，或编辑位于 `<game folder>/BepInEx/config` 内部的 `io.github.alexkiri.particleoverdrive.cfg` 文件

- `ParticlesMultiplier`:
    - 增加粒子数量
    - 降低性能
    - 默认值：VanillaHigh
    - 可接受值：最高可达 16x
- `ExtremeMultiplier`:
    - 允许极高的数值
    - 建议在使用此倍率时禁用 `UpgradeAllParticleSystems` 选项，否则游戏将无法正常游玩
    - 可极大降低性能
    - 仅在“高级设置”中可用
- `UpgradeAllParticleSystems`:
    - 将所有粒子系统升级为受倍率影响
    - 需要退出并重新加载才能生效

启用本模组后，主菜单中的“粒子效果”设置将无效。
