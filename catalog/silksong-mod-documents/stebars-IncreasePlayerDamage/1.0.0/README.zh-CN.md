将玩家造成的伤害乘以 2。倍率数值可以进行配置。

安装方法：
1. 安装 BepInEx 5。说明见：https://docs.bepinex.dev/articles/user_guide/installation/index.html（已测试于 5.4.23.3；其他版本也可能可用）。
2. 解压 DoubleDamage.zip，并将 DoubleDamage.dll 放入 BepInEx/plugins 文件夹中。
3. 启动游戏。玩家的伤害应被翻倍。
4. 运行游戏一次后，会在 BepInEx/config/stebars.silksong.double-damage.cfg 处生成一个配置文件。编辑该配置文件以更改伤害倍率。修改配置后，重启游戏以应用变更。

将倍率设置为 1 时，伤害将与原版游戏相同。你可以将其设为小数，例如 1.5（仅增加 50% 的伤害）或 0.5（造成一半伤害，因此蚂蚁需要 8 次攻击才会死亡）。

该设置将适用于玩家造成的所有伤害（如钉子、工具等），但不适用于敌人造成的伤害。倍率在所有其他修饰效果之后应用。不会作用于某些环境障碍物（例如妹妹·碎裂藤蔓制作的藤蔓）。

源代码：https://github.com/StephenBarnes/Silksong-DoubleDamage
