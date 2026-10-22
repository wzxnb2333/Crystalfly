# 阴茎插头歌

为《空洞骑士：丝之歌》添加振动功能，让大黄蜂更加充满激情。

讽刺的是，该功能并未进行振动编码！

![see git.gay/danatron1/ButtplugSong for a fancy list of features](Images/features.png)

## 要求

你需要：

* 一个振动玩具（[compatible device list](https://iostindex.com/?filter0ButtplugSupport=4)）
* 一份 [Hollow Knight: Silksong](https://store.steampowered.com/app/1030300/Hollow_Knight_Silksong/)
* 一个丝之歌模组管理器，例如 [r2modman](https://thunderstore.io/c/hollow-knight-silksong/p/ebkr/r2modman/)（免费）
	* 或者手动使用 BepInEx 配置丝之歌模组。不过模组管理器会为你完成这些操作！
* 已安装并启用 [Intiface Central](https://intiface.com/#intiface-central)（免费）
* 一台配备蓝牙接收器的 PC，或者一台已安装 Intiface Central 的智能手机并配备 [relaying](https://intiface.com/docs/intiface-central/ui/app-modes-repeater-panel)

## 安装说明

1. 安装一个模组管理器，例如 [r2modman](https://thunderstore.io/c/hollow-knight-silksong/p/ebkr/r2modman/)。（其他选项可用，但不推荐使用 Cogfly，因为它会阻止 nsfw 模组的安装）  
2. 在本模组的 [thunderstore page](https://thunderstore.io/c/hollow-knight-silksong/p/danatron1/Buttplug_Song/) 上点击“通过模组管理器安装”  
3. 在 Intiface Central 中开启服务器，扫描设备，并确保你的玩具已出现在列表中。如果你使用手机作为蓝牙中继，请按照要求部分链接的中继说明进行操作。  
4. 通过模组管理器启动游戏。如果一切正常，模组将在右上角显示界面，且你的玩具会出现在网络选项卡下。

## 调试

* 确保模组以及丝之歌均为最新版本。  
* 关闭 VPNs。（默认情况下，模组会尝试连接到 `localhost:12345`，但此行为可进行配置）  
* 确认振动器已开启，并在适用时正在搜索蓝牙连接。  
* 如果使用 Lovense Connect，请停止！该产品已停用。请改用 Intiface Central 的中继模式（参见要求部分）。  
* 确保 Intiface Central 已打开，你已经启动了服务器，并点击了“开始扫描”。玩具应在 Intiface 中显示出来。  
* 尝试安装 [ModList](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/Modlist/) 以确认模组能否正常加载。

（备注：电池显示功能无法工作是一个已知问题。该功能的实现异常困难，目前已被搁置。你仍然可以通过 Intiface 应用查看电池电量）

如果你仍遇到问题（e.g，模组崩溃），请联系我（danatron1），并发送你 BepInEx 文件夹中的 `LogOutput.txt` 调试日志文件。路径应类似如下：

`C:\Users\YourNameHere\AppData\Roaming\r2modmanPlus-local\HollowKnightSilksong\profiles\Default\BepInEx`

我在做什么呢。
