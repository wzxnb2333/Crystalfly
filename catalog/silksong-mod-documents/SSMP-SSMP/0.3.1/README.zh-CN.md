# SSMP <img src="https://i.imgur.com/7mpq81u.png" width="52" align="right">

## 什么是丝之歌多人模式？
正如名称所暗示的，丝之歌多人模式（SSMP）是热门2D动作冒险游戏《空洞骑士：丝之歌》的一个多人游戏模组。
该模组的主要目的是让人们可以创建游戏并邀请他人加入自己的冒险旅程。
模组有一个专门的 [Discord server](https://discord.gg/KbgxvDyzHP)，您可以在其中提问或一般性地讨论该模组。
此外，您还可以提交建议或错误报告。最新的公告将发布在该页面。

## 安装  
### Thunderstore  
当使用与 Thunderstore 兼容的安装程序时，一切应能自动完成。  

### 手动安装  
请参考 [Github repository](https://github.com/Extremelyd1/SSMP) 以了解手动安装模组的详细步骤。

## 使用方法  
该模组可通过游戏主菜单中的“开始多人模式”选项访问。  
打开多人模式菜单后，你将看到一个控制面板，从中可以选择在线对战或局域网对战。

### Steam 支持  
SSMP 完全支持 Steam 网络功能。  
这意味着你可以轻松地创建游戏并邀请朋友通过 Steam 画面上层进行连接，无需担心端口转发或 IP 地址问题。  
- **主机模式**：要创建一个 Steam 联机房间，只需在主机菜单中选择“Steam”选项。  
- **邀请朋友**：在主机状态下，你可以使用 Steam 画面上层邀请好友，或在游戏中使用 `/invite` 命令进行邀请。  
- **加入游戏**：好友可以直接通过他们的“好友列表”加入你的游戏，或接受你的邀请。

### 匹配服务（MMS）  
模组内置了一个由我们的匹配服务驱动的服务器浏览器。  
- **房间浏览器**：你可以在多人模式菜单中直接浏览公开房间。  
- **洞穿 NAT**：MMS 实现了 NAT 洞穿功能，使玩家能够无需端口转发即可直接连接（点对点 P2P）。

### 直连与局域网  
对于偏好传统连接方式的用户，你仍然可以通过 IP 地址进行直连。  
- **主机模式**：在路由器上将端口（默认为 `26960`）转发到你的设备。  
- **局域网**：在同一局域网内对战可直接使用，无需额外设置。  
- **VPN**：可以使用诸如 [Hamachi](https://vpn.net) 等工具在互联网上模拟局域网环境。

当你开始创建或加入服务器时，模组会提示你选择一个存档文件使用。  
该存档文件仅用于本地，不会与服务器同步。

### 命令  
模组提供了一个聊天窗口，允许用户输入命令。  
聊天输入可通过键绑定（默认为 `Y`）打开，支持以下命令：  
- `/help`：显示所有可用命令列表。  
- `/list`：列出当前连接玩家的名称。  
- `/invite` 或 `/inv`：打开 Steam 的邀请对话框，邀请好友加入你的房间。  
- `/addon <enable|disable|list> [addon(s)]`：启用、禁用或列出客户端插件。  
- `/set <setting name> [value]`：读取或写入指定名称和值的设置项。  
- `/skin <skin ID>`：更改当前玩家使用的皮肤 ID。  
- `/team <None|Moss|Hive|Grimm|Lifeblood>`：更改玩家所属队伍。  
- `/announce <message>`：向所有连接玩家广播一条聊天消息（仅管理员可用）。  
- `/kick <auth key|username|ip address>`：踢出某位玩家（仅管理员可用）。  
- `/ban <auth key|username>`：封禁某位玩家（仅管理员可用）。  
- `/unban <auth key>`：解除封禁某位玩家（仅管理员可用）。  
- `/banip <auth key|username|ip address>`：封禁某个 IP 地址（仅管理员可用）。  
- `/unbanip <ip address>`：解除封禁某个 IP 地址（仅管理员可用）。  
- `/copysave <from> <to>`：将一个玩家的存档数据复制到另一个玩家（目前已禁用）。  
- `/debug`：输出各种调试信息至日志。

### 认证/授权  
每位用户都会本地生成一个认证密钥，用于身份验证和权限控制。  
该密钥可用于白名单管理，并授权特定用户加入服务器或执行需要更高权限的命令。

- `/whitelist [args]`：管理白名单，支持以下选项：  
  - `on|off`：启用/禁用白名单。  
  - `add|remove [name|auth key]`：添加/移除用户。  
  - `clear [prelist]`：清空白名单。  
- `/auth <name|auth key>`：授权某位玩家。  
- `/deauth <name|auth key>`：取消授权某位玩家。

### 独立服务器  
可以在 Windows、Linux 和 Mac 上运行独立服务器。  
最新版本的服务器可从 [releases page](https://github.com/Extremelyd1/SSMP/releases) 下载。  
请确保下载与你操作系统匹配的正确版本。

独立服务器可通过 .NET 9.0 运行。  
如果你仅关心运行该服务器，只需下载最新的 [.NET 9.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) 即可。

安装完 .NET 运行时后，可在终端中使用 `./SSMPServer [port]` 启动可执行文件。  
端口参数是可选的，若未指定则默认为 `26960`。

服务器会读取或创建一个名为 `consolesettings.json` 的控制台设置文件，可用于修改服务器的默认启动配置。  
这包括在命令行中未指定时使用的默认端口。  
该文件中的另一项设置（`fullSynchronisation`）目前未使用。

服务器还会读取或创建一个名为 `serversettings.json` 的服务器设置文件，可用来调整以下部分列出的服务器配置。  
你也可以通过命令行运行“settings”命令来修改设置。  
除了上述命令外，独立服务器还支持以下命令：  
- `exit`：优雅退出服务器并断开所有用户连接。  
- `log [log level(s)]`：调整输出到控制台的日志消息级别。

### 设置  
存在大量可配置的设置项，可以改变模组的工作方式。

以下值可以被上述 `/set` 命令读取和修改。  
所有设置名称均为不区分大小写，但为清晰起见以大写形式书写。  
- `IsPvpEnabled`：是否启用玩家对玩家的伤害。  
  - 别名：`pvp`  
- `AlwaysShowMapIcons`：玩家的地图位置是否始终在游戏地图上共享。  
  - 别名：`globalmapicons`  
- `OnlyBroadcastMapIconWithWaywardCompass`：玩家的地图位置仅在装备了“迷途指南针”饰品时才被共享。  
  注意：如果地图位置始终共享，则此设置无效果。  
  - 别名：`compassicon`, `compassicons`, `waywardicon`, `waywardicons`  
- `DisplayNames`：是否显示头顶名称。  
  - 别名：`names`  
- `TeamsEnabled`：是否启用玩家团队功能。  
  同一队伍内的玩家无法互相造成伤害。  
  团队可在客户端设置菜单中选择。  
  - 别名：`teams`  
- `AllowSkins`：是否允许使用玩家皮肤。  
  若禁用，玩家将无法本地使用皮肤，也无法将其传输给其他玩家。  
  - 别名：`skins`  
- `NeedleDamage`：玩家针击造成的伤害掩码数量。  
  - 别名：`needledmg`  
  - 默认值：1  
- `NeedleStrikeDamage`：针刺技能造成的伤害掩码数量。  
  - 别名：`needlestrikedmg`, `strikedmg`, `artdmg`  
  - 默认值：2  
- `VoltFilamentDamage`：使用电压丝线的丝线技能额外造成的半掩码伤害数量。  
  伤害在应用修饰后向下取整。  
  - 别名：`voltfilamentdmg`, `voltdmg`, `filamentdmg`, `voltmodifier`, `voltmod`  
  - 默认值：1  
- `ShamanDamage`：在祭司纹章上使用的丝线技能额外造成的半掩码伤害数量。  
  伤害在应用修饰后向下取整。  
  - 别名：`shamandmg`, `shamanmodifier`, `shamanmod`  
  - 默认值：1  
- `CrossStitchDamage`：十字缝合技能造成的伤害掩码数量。  
  - 别名：`crossstitchdmg`, `stitchdmg`, `parrydmg`  
  - 默认值：1  
- `PaleNailsDamage`：苍白指甲技能造成的伤害掩码数量。  
  - 别名：`palenailsdmg`, `palenaildmg`, `paledmg`  
  - 默认值：1  
- `RuneRageDamage`：符文狂怒技能造成的伤害掩码数量。  
  - 别名：`runeragedmg`  
  - 默认值：1  
- `SharpDartDamage`：锐刺镖技能造成的伤害掩码数量。  
  - 别名：`sharpdartdmg`, `dartdmg`  
  - 默认值：1  
- `SilkSpearDamage`：丝矛技能造成的伤害掩码数量。  
  - 别名：`silkspeardmg`, `speardmg`  
  - 默认值：1  
- `ThreadStormDamage`：线风暴技能造成的伤害掩码数量。  
  - 别名：`threadstormdmg`, `stormdmg`  
  - 默认值：1  
- `WardingBellDamage`：护盾铃铛造成的伤害掩码数量。  
  - 别名：`wardingbelldmg`, `bindbelldmg`, `belldmg`  
  - 默认值：1  
- `ClawMirrorDamage`：基础爪镜造成的伤害掩码数量。  
  - 别名：`clawmirrordmg`, `mirrordmg`, `mirror1dmg`  
  - 默认值：1  
- `ClawMirrorUpgradedDamage`：升级版爪镜造成的伤害掩码数量。  
  - 别名：`clawmirrorupgradeddmg`, `mirror2dmg`  
  - 默认值：1  
- `SawtoothCircletDamage`：锯齿圈饰造成的伤害掩码数量。  
  - 别名：`sawtoothcircletdmg`, `circletdmg`, `umbrelladmg`  
  - 默认值：1  
- `PoisonBrewDamage`：毒蝇酒产生的云造成的伤害掩码数量。  
  - 别名：`poisonbrewdmg`, `brewdmg`, `fleabrewdmg`  
  - 默认值：1  
- `StraightPinDamage`：直针造成的伤害掩码数量。  
  - 别名：`straightpindmg`, `pindmg`, `pindamage`  
  - 默认值：1  
- `ThreefoldPinDamage`：三重针造成的伤害掩码数量。  
  - 别名：`threefoldpindmg`, `threepindmg`, `threepindamage`  
  - 默认值：1  
- `LongpinDamage`：长针造成的伤害掩码数量。  
  - 别名：`longpindmg`  
  - 默认值：1  
- `TacksDamage`：钉子造成的伤害掩码数量。  
  - 别名：`tackdmg`, `tacksdmg`, `tackdamage`  
  - 默认值：1  

### 皮肤系统  
当前皮肤系统尚未完全实现。  
虽然可以通过命令系统更改皮肤 IDs，但很可能无法正常工作。

## Patreon  
如果你喜欢这个项目并希望参与其开发，可以考虑在  
[Patreon](https://www.patreon.com/Extremelyd1) 上成为支持者。你将获得开发动态、抢先体验以及新功能的早期访问权限。此外，你还将获得一个在 Discord 服务器中的角色，可访问专属频道。

## Copyright and license
HKMP is a game modification for Hollow Knight that adds multiplayer.  
Copyright (C) 2026  Extremelyd1

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
    USA
