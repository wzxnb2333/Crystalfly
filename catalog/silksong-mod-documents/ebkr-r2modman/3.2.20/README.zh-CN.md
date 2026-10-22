> [!WARNING]
> 近来出现了大量声称是官方的第三方分发网站。
>
> 获取 r2modman 的唯一合法途径为：
>
> - [Thunderstore](https://thunderstore.io/package/ebkr/r2modman/)
> - [GitHub](https://github.com/ebkr/r2modmanPlus/releases/latest)
> - [ebkr.dev](https://r2builds.ebkr.dev/)（用于托管的 Flatpak 构建版本）

# r2modman

[![Discord](https://img.shields.io/discord/727304496522461185?label=r2modman%20Discord&style=for-the-badge)](https://discord.gg/jE2zWHY)

[![GitHub](https://img.shields.io/github/license/ebkr/r2modmanPlus?color=orange&style=for-the-badge)](https://github.com/ebkr/r2modmanPlus)

| [Features](#features) | [What is a mod manager?](#what-is-a-mod-manager) | [Installing](#installing) | [Help](#help) | [Feedback and suggestions](#feedback-and-suggestions) | [Changelog](#changelog) | [Screenshots](#screenshots) |
|---|---|---|---|---|---|---|

## 特性  
- 支持《风险之雨》2、《戴森球计划》、《瓦尔海姆》、《GTFO》、《BONEWORKS》等游戏  
- 简洁直观的用户界面，旨在让模组制作尽可能简单  
- 更安全的模组安装方式，允许你通过 Steam 正常游玩游戏  
- 模组配置文件，可快速轻松地在不同模组组合之间切换  
- 可导出配置文件，方便与朋友分享你的模组和设置  
- 可直接从管理器下载并安装模组  
- 查看并更新过时的模组  
- 可直接在管理器中编辑配置文件  
- 自动更新功能  
- 更多特性！

## 什么是模组管理器？
其实很简单，模组管理器是一个应用程序，用于更方便地控制你已安装的模组。

你可以通过一次点击来选择更新、启用/禁用甚至卸载模组，同时保持这些模组在另一个配置文件中可用。

## 安装

### Windows
1. 在 Thunderstore 上点击“手动下载”。
2. 在下载的 **.zip** 文件中，运行 "r2modman Setup X.X.X.exe"（其中 X.X.X 为当前版本）。
3. 按照安装程序的步骤进行操作。

### Linux

如果您使用的是 Linux 系统，请不要安装 r2modman 的 `.exe` 文件。请使用以下任一方法：

#### Steam Deck

如果希望在游戏模式下使用 r2modman，Steam Deck 依赖于 Flatpak 安装。

如果您更倾向于在桌面模式下运行管理器，可以使用 Flatpak（推荐）或 AppImage。

#### Flatpak 和 AppImage

1. 在 Thunderstore 上点击“手动下载”。
2. 解压下载的 **.zip** 文件。
2. 在解压后的文件夹中，将包含：
  - 一个 `flatpak-setup.sh` 文件。如果您希望通过 Flatpak 安装 r2modman，请运行此脚本。（推荐）。
  - 一个 AppImage 文件用于常规使用。该文件可能需要标记为可执行文件（`chmod +x <filename>.AppImage`）。

`flatpak-setup.sh` 脚本包含：
```bash
flatpak remote-add --if-not-exists r2builds http://r2builds.ebkr.dev/flatpak/r2modman.flatpakrepo
flatpak install io.github.ebkr.r2modman
```

您需要具备提升权限（sudo）才能运行 `flatpak-setup.sh` 脚本或上述命令。

如果您不想使用 sudo/提升权限来安装 Flatpak，可以手动执行以下操作：
```bash
flatpak remote-add --user --if-not-exists r2builds http://r2builds.ebkr.dev/flatpak/r2modman.flatpakrepo
flatpak install --user io.github.ebkr.r2modman
```


#### 平台特定版本

_如果希望安装平台特定版本，则可以在 ebkr/r2modmanPlus 仓库的最新 GitHub 发布版本中找到_

平台版本：
- deb
- rpm
- pacman
- tar.gz

您可在此处访问发布文件列表：https://github.com/ebkr/r2modmanPlus/releases/latest

_关于 Linux 版本的问题，应提交至 [r2modman discord](https://discord.gg/jE2zWHY)。_

#### 注意
- 临时解决方案以强制在 Linux 系统上使用 Proton
    - 在游戏目录中放置一个 `.forceproton` 文件，直到问题有解决方案为止

## 更新  
r2modman 在使用过程中会自动下载可用的更新。

如果已下载更新，将在关闭应用程序后进行安装。

## 帮助  
### 管理器错误：  
1. 检查 [wiki](https://github.com/ebkr/r2modmanPlus/wiki)。  
2. 如果无法找到解决方案，请加入社区模组 Discord 并在相应的频道中寻求帮助。

### 模组错误：  
1. 加入相关的社区模组 Discord，并在适当的频道中寻求帮助。

## 反馈与建议  
欢迎提供尽可能多的反馈，也完全接受批评。  

欢迎提出各种建议，目前已有部分建议被纳入管理器！  
从简单的功能（例如始终展开卡片），到较大的功能（例如基于代码的配置文件导出）均有涵盖。  

在提出新功能时，唯一需要考虑的是：该功能对那些计算机经验较少的用户可能产生的影响。

## 截图

游戏选择

![](https://r2modman-readme-assets.ebkr.dev/GameSelection.png)

已安装模组视图

![](https://r2modman-readme-assets.ebkr.dev/InstalledList.png)

在线模组

![](https://r2modman-readme-assets.ebkr.dev/OnlineList.png)

配置编辑器

![](https://r2modman-readme-assets.ebkr.dev/ConfigEditor.png)

配置文件

![](https://r2modman-readme-assets.ebkr.dev/ProfileSelection.png)
