---

# ⚔️ 已解锁能力  
本模组会从游戏开始就为你提供所有能力。可通过进入 BepInEx 配置文件，并将 true 到 false 的设置进行调整，来决定哪些能力立即生效，哪些则延迟获得。

## 🔧 安装  
1. 下载 BepInEx  
2. 将 BepInExPack 文件夹中的文件放入你的可执行文件目录，路径应为 steamapp/common/hollow knight silksong/  
3. 下载此模组  
4. 将名为 (AbilitiesUnlocked.dll) 的 DLL 文件放入你可执行文件目录下的 BepInEx 文件夹内的 plugins 文件夹中。即：steamapps\common\Hollow Knight Silksong\BepInEx\plugins  
4.1 如果没有 plugin 文件夹，就直接从我提供的要求链接下载 BepInEx（如果你是从 nexus 下载的）。或者，只需用你当前的 BepInEx 安装版本运行一次游戏即可。  
5. 完成

## 📒 注意

- 我开启了一次新游戏，并且可以使用所有能力，因此我认为它应该能正常工作。我稍后会进行进一步的测试运行。
- 理论上，该模组仅在 HeroController 开始时将 bool 类型的 hasXYZ 设置为 true。这些值不应被再次设置为 false，且在通过正常方式获得能力时也不会导致游戏崩溃。
然而由于你可能可以访问所有区域，我不确定是否有可能造成任何破坏。因此建议自行承担风险使用。