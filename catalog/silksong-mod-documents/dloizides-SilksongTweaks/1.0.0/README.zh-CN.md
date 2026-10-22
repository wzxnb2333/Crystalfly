# 丝之歌调整

为 **空洞骑士：丝之歌** 提供四个提升体验的调整，整合在一个模组中，仅需一个面板即可设置。

在游戏中按 **`F8`**。所有设置立即生效。

- **返回死亡地点** — 在长椅处重生后，将自动返回死亡位置，并附带可取消的倒计时
- **保留念珠** — 再次死亡不再破坏你的第一个茧
- **最大面罩** — 提升大黄蜂的最大生命值（1–20）
- **受到伤害** — 调整承受伤害的数值，从无敌状态到比原版更难

👉 **[Full usage guide](docs/USAGE.md)**

## 安装

**使用模组管理器（最简单）。** 通过 Thunderstore 使用 r2modman 或 Gale 安装，BepInEx 会自动作为依赖项被引入——无需其他操作。

**手动安装。**

1. 安装 [BepInEx 5 for Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/BepInExPack_Silksong/)
   — 解压后，将位于 `Hollow Knight Silksong.exe` 旁边的 `BepInExPack` 文件夹的全部内容复制进去。
2. 运行游戏一次以让 BepInEx 创建其所需文件夹，然后退出。
3. 将 `SilksongTweaks.dll` 放入 `BepInEx/plugins/` 中。
4. 启动游戏并按下 `F8`。

**卸载：** 删除 `SilksongTweaks.dll`。其他内容均不受影响。

## 为什么需要另一个模组？

因为无法判断其他模组是否真正生效。

被本模组替代的模组会愉快地报告“已加载”，然后完全不执行任何操作——从外部看，一个正常工作的模组和一个静默失效的模组看起来一模一样。本模组为每个设置项提供状态显示：是否成功挂钩游戏，以及最后一次实际执行操作的时间。

| 标记 | 含义 |
|---|---|
| `ACTIVE · fired 12s ago` | 正常运行，并已执行过操作 |
| `ACTIVE · not yet fired` | 已完成挂钩，尚未触发 |
| `OFF` | 你已将其关闭 |
| `UNAVAILABLE · <reason>` | 无法挂钩游戏，原因如下 |

当游戏更新导致某个设置项失效时，其余三项仍会正常运行，并明确指出这一点。

## 设计说明

- **你的存档文件永远不会被写入。** 最大生命值是通过拦截游戏的“读取”操作实现的，而不是存储新的数值。卸载后会留下一个原始存档，没有任何需要回滚的内容。
- **不会对编译器生成的代码进行钩子绑定。** 如 `<Die>d__1101` 这类迭代器状态机，其内部携带的数字会在每次构建时被编译器重新生成，因此一旦游戏补丁更新，这些钩子就会静默失效。所有钩子都针对稳定且命名明确的方法。
- **钩子目标的唯一真实来源。** `HookTargets.cs` 同时被运行时和一个测试程序使用，该测试会读取你当前安装的 `Assembly-CSharp.dll` 文件，并在发现任何目标位置发生偏移时直接报错。更新丝之歌后运行此测试，即可明确指出哪些部分出错了。
- **禁用不会自动撤销补丁。** 补丁始终保留在系统中，仅通过布尔值短路跳过执行，因为运行时的卸载操作是已知的不稳定来源。

## 构建

需要 .NET SDK。游戏会提供引用程序集。

```bash
dotnet build src/SilksongTweaks -c Release
dotnet test  tests/SilksongTweaks.Tests
```

如果丝之歌未位于默认路径：

```bash
dotnet build src/SilksongTweaks -c Release -p:GameDir="D:\path\to\Hollow Knight Silksong"
```

`tools/ApiInspector` 会在不运行游戏的情况下读取游戏程序集，以查找钩子目标：

```bash
dotnet run --project tools/ApiInspector -- dump "<ManagedDir>" '^PlayerData$' 'health'
dotnet run --project tools/ApiInspector -- xref "<ManagedDir>" '^maxHealth$'
```

---

## Credits

Silksong Tweaks is an **independent implementation**, written against the game's own API. None
of the mods below were decompiled and none of their code is used. They came first, they proved
these ideas were worth having, and they deserve the credit for them:

- **[Xiaohai (XiaohaiMod)](https://thunderstore.io/c/hollow-knight-silksong/p/XiaohaiMod/ReBack/)**
  — *ReBack*, for returning to the death location
- **[BlueRaja](https://thunderstore.io/c/hollow-knight-silksong/p/BlueRaja/rosaries_never_permanently_lost/)**
  — *rosaries never permanently lost*, for merging death cocoons
- **[Ericky1694](https://thunderstore.io/c/hollow-knight-silksong/p/Ericky1694/CustomDifficulty/)**
  — *CustomDifficulty*, for health and damage tuning

Built on [BepInEx](https://github.com/BepInEx/BepInEx) and
[HarmonyX](https://github.com/BepInEx/HarmonyX). Not affiliated with Team Cherry.

## Licence

[MIT](LICENSE).
