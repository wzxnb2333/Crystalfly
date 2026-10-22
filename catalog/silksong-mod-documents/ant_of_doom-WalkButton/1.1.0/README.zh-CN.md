# 丝之歌-行走按钮模组  
一个简单的丝之歌模组。按住某个按钮即可缓慢移动到任意位置。默认按键是键盘 `L`，但你可以轻松将其更改为其他按键。

该功能在按键持续按住时生效，但你可以将其配置为切换模式使用。

# 安装  
将 `WalkButton.dll` 从 `ant_of_doom-WalkButton-1.1.0.zip` 中取出，放入 `BepInEx\plugins` 中。  

完成。

# 更改按钮

你可以通过至少运行一次模组来更改默认按钮，运行后模组会在 `BepInEx\config` 文件夹中创建一个配置文件。该配置文件名为 `org.sealofapproval.walkbuttonmod.cfg`。更改默认按钮非常简单，只需打开该文件，将第 `walkButton = L` 行中的 `L` 替换为你自己的选择即可。

在相同位置，你还可以更改切换/保持设置：将第 `mode = hold` 行中的 `hold` 替换为 `toggle`，或反之。