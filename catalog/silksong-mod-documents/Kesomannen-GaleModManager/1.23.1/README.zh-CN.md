![Gale](https://raw.githubusercontent.com/Kesomannen/gale/master/images/banner.png)

[![Thunderstore Version](https://img.shields.io/thunderstore/v/Kesomannen/GaleModManager?style=flat)](https://thunderstore.io/c/lethal-company/p/Kesomannen/GaleModManager/)
[![Discord](https://img.shields.io/discord/1288196347597688912?style=flat&label=discord)](https://discord.gg/sfuWXRfeTt)
[![GitHub License](https://img.shields.io/github/license/Kesomannen/gale?style=flat)](https://github.com/Kesomannen/gale?tab=GPL-3.0-1-ov-file#readme)

一款为 [Thunderstore](https://thunderstore.io) 设计的强大模组管理器，基于 [Svelte](https://kit.svelte.dev/) 和 [Tauri](https://tauri.app/) 构建。

## 特性

- 支持在 Thunderstore 和 Hexium 上的 150+ 游戏，包括 Lethal Company、R.E.P.O 以及 Risk Of Rain 2
- 直观且响应迅速的界面
- 极小的下载大小和资源占用
- 功能丰富的模组配置编辑器
- 自动同步配置文件

[...and more](https://github.com/Kesomannen/gale/wiki/Features)

## 安装

> [!WARNING]
> Gale 的唯一官方来源是 [Github](https://github.com/Kesomannen/gale)、[Thunderstore](https://thunderstore.io/c/lethal-company/p/Kesomannen/GaleModManager/) 和 [Hexium](https://hexium.gg/mod-manager)。任何声称提供官方下载的其他网站均不合法。特别是，**galemodmanager.com** 是非官方的，并提供了严重过时的应用版本！

### Windows

<details>
  <summary>
    <b>手动安装（Thunderstore）</b>
  </summary>
  
  - 前往 [Thunderstore page](https://thunderstore.io/c/lethal-company/p/Kesomannen/GaleModManager/) 并点击 _手动下载_。
  - 解压下载的 .zip 文件（例如通过右键选择 _全部提取_）。
  - 在解压后的文件夹中运行 `Gale_X.X.X_x64_en-US.msi` 文件。
</details>

<details>
  <summary>
    <b>手动安装（Github）</b>
  </summary>
  
  - 前往 [Releases](https://github.com/Kesomannen/gale/releases)。
  - 下载您所需版本的 `Gale_X.X.X_x64_en-US.msi` 文件（推荐下载最新版本）。
  - 运行已下载的文件。
</details>

<details>
  <summary>
    <b>Scoop</b>
  </summary>
  
  Gale 可作为官方 [Scoop](https://scoop.sh/) 应用在 [games bucket](https://github.com/Calinou/scoop-games) 中获取：

```powershell
scoop bucket add games
scoop install gale
```

</details>

<details>
  <summary>
    <b>WinGet</b>
  </summary>
  
  Gale 可作为 [WinGet](https://learn.microsoft.com/en-us/windows/package-manager/winget/) 应用获取：

```powershell
winget install Kesomannen.Gale
```

</details>

> [!NOTE]
> 您可能会收到提示“Windows 已保护您的电脑”。此时，请点击 `More Info` 并选择 `Run Anyway`。

> [!TIP]
> 如果您对这款应用的安全性存疑，建议通过像 [VirusTotal](https://www.virustotal.com) 这样的服务进行运行检测。

### Linux

<details>
  <summary>
    <b>Arch</b>
  </summary>
  
  Gale 可作为 **社区维护** 的 AUR 包获取：[gale](https://aur.archlinux.org/packages/gale)（源码编译）和 [gale-bin](https://aur.archlinux.org/packages/gale-bin)（预构建版）。
  
  示例安装命令：
  
  ```bash
  yay -S gale-bin
  ```

> [!WARN]
> **不要** 使用应用内的更新器，而是通过 AUR 更新该应用。

</details>

<details>
  <summary>
    <b>Debian</b>
  </summary>

Gale 可作为 .deb 包在 [Releases](https://github.com/Kesomannen/gale/releases) 中获取。下载后，使用以下命令安装：

```bash
sudo dpkg -i Gale_X.X.X_x64_en-US.deb
```

更新 Gale 可通过应用内的更新器界面完成。

</details>

<details>
  <summary>
    <b>Fedora</b>
  </summary>

Gale 可作为 .rpm 包在 [Releases](https://github.com/Kesomannen/gale/releases) 中获取。下载后，使用以下命令安装：

```bash
sudo rpm -i Gale_X.X.X_x64_en-US.rpm
```

更新 Gale 可通过应用内的更新器界面完成。

</details>

<details>
  <summary>
    <b>Flatpak</b>
  </summary>

Gale 可作为独立托管的 Flatpak 包获取：

```bash
flatpak install https://kesomannen.com/com.kesomannen.gale.flatpakref
```

更新该应用可通过 `flatpak update com.kesomannen.gale` 完成。

</details>

<details>
  <summary>
    <b>AppImage</b>
  </summary>

跨发行版的 AppImages 可在 [Releases](https://github.com/Kesomannen/gale/releases) 中获取。下载后，使其可执行并运行：

```bash
chmod +x Gale_X.X.X_x64_en-US.AppImage
./Gale_X.X.X_x64_en-US.AppImage
```

更新 Gale 可通过应用内的更新器界面完成。

</details>

---

想从源码构建吗？请参见 [wiki](https://github.com/Kesomannen/gale/wiki/building-from-source)。

## 截图

_资料页_

![screenshot](https://raw.githubusercontent.com/Kesomannen/gale/master/images/screenshots/screenshot1.png)

_Thunderstore 浏览器_

![screenshot](https://raw.githubusercontent.com/Kesomannen/gale/master/images/screenshots/screenshot2.png)

_模组配置编辑器_

![screenshot](https://raw.githubusercontent.com/Kesomannen/gale/master/images/screenshots/screenshot3.png)

_模组包导出_

![screenshot](https://raw.githubusercontent.com/Kesomannen/gale/master/images/screenshots/screenshot4.png)

## Credits

Material icons licensed under [Apache 2.0](https://www.apache.org/licenses/LICENSE-2.0.html).

Thanks to Ebkr for helping to navigate the Thunderstore API and BepInEx, and of course making the original mod manager!

---

Still have questions? See the [FAQ](https://github.com/Kesomannen/gale/wiki/faq) or a [detailed list of features](https://github.com/Kesomannen/gale/wiki/Features).
