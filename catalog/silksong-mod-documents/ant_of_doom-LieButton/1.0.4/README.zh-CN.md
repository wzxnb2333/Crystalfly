# 丝之歌-躺下按钮模组  
一个简单而可爱的丝之歌模组，非常适合主播使用。点击某个按键即可躺下或起身（类似开关功能）。默认按键为 `L`，但你可以在配置文件中轻松将其更改为其他按键，甚至可以设置为控制器按钮。配置文件中还提供了一个选项，使大黄蜂在按下 `Attack` 或 `Jump` 时自动起身。

无已知Bug。

# 安装  
将 `LieButton.dll` 从 `ant_of_doom-LieButton-1.0.4.zip` 中取出，并放入 `BepInEx\plugins` 中。  

完成。

# 更改按钮

你可以通过至少运行一次模组来更改默认按钮，运行后模组会在 `BepInEx\config` 文件夹中创建一个配置文件。该配置文件名为 `org.sealofapproval.liebuttonmod.cfg`。更改默认按钮非常简单，只需打开该文件，将第 `lieButton = L` 行中的 `L` 替换为你自己的选择即可。

在相同位置，你还可以通过将第 `wakeOnAttackOrJump = false` 行中的 `false` 更改为 `true` 来启用 wakeOnAttackOrJump 功能。