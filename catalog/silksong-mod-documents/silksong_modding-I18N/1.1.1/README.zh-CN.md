# Silksong.I18N

一个空洞骑士：丝之歌模组，可自动加载其他模组的本地化文本。

## 为用户翻译模组

要翻译一个模组，你需要找到该模组的翻译文件夹。翻译文件夹应位于名为 `BepInEx/plugins` 的子文件夹中。如果该模组使用了 I18N（国际化），它会有一个与主模组文件 `.dll` 并列的 `languages`、`Languages` 或 `LANGUAGES` 文件夹。该文件夹应包含一个或多个以语言命名的 `.json` 文件：`en.json`、`fr.json` 等。大小写不重要。

支持的语言包括：`en.json`（英语）、`fr.json`（法语）、`de.json`（德语）、`zh.json`（简体中文）、`es.json`（西班牙语）、`ko.json`（韩语）、`ja.json`（日语）、`it.json`（意大利语）、`pt.json`（巴西葡萄牙语）和 `ru.json`（俄语）。目前，除非有其他模组提供支持，否则你无法将模组翻译成这些语言之外的语言。

要为模组添加你的语言，首先需要创建一个翻译文件。复制该模组中的任意一个 `.json` 语言文件（选择行数最多的那个），并将其重命名为对应语言的两位代码。例如，如果你想将英文模组翻译为巴西葡萄牙语，则应复制模组中的 `en.json` 文件，并将其重命名为 `pt.json`。

然后，在你的文本编辑器（在 Windows 上为 Notepad）中打开新创建的翻译文件，将每行**冒号之后（`:`）** 的文本进行翻译。**不要修改冒号之前（`:`）的文本！** 冒号之前的文本是模组用来识别每一行内容的关键。

例如，在以下翻译文件中：

```json
{
    "TRANSLATION_KEY": "Localized text!",
    "CUSTOM_TOOL_NAME": "Super Cool Tool",
    "CUSTOM_TOOL_DESC": "The name of this tool rhymes. How amusing.",
    "SOME_DIALOGUE": "Hi! I’m an NPC…<hpage>And I’m Hornet.<page>Yay!"
}
```

你应该将 `Localized text!`、`Super Cool Tool`、`The name of this tool rhymes. How amusing.` 和 `Hi! I’m an NPC…<hpage>And I’m Hornet.<page>Yay!` 的文本替换为对应语言的翻译。**不要修改** `TRANSLATION_KEY`、`CUSTOM_TOOL_NAME`、`CUSTOM_TOOL_DESC`、`SOME_DIALOGUE`，以及任何冒号（`:`）、直引号（`"`）、行尾逗号（`,`）或花括号（`{` 和 `}`）。

要在游戏中使用你的翻译，需在主菜单中将游戏语言更改为你的语言。

## 使用方法（针对模组开发者）

如果您的模组需要本地化文本才能正常运行，请通过在模组的 `BaseUnityPlugin` 类中添加以下属性将其作为依赖项：

```cs
[BepInDependency("org.silksong-modding.i18n")]
```


此外，如果您计划将模组发布到 Thunderstore，您需要在模组的 Thunderstore  manifest 中添加此依赖项。为此，请在 `thunderstore.toml` 文件中 `[package.dependencies]` 定义之后添加如下内容：

```toml
silksong_modding-I18N = "1.0.2"
```


或者，如果您没有 `thunderstore.toml` 文件，也可以通过将 `"silksong_modding-I18N-1.0.2"` 添加到您 `dependencies` 数组中来将其作为依赖项加入您的 Thunderstore  
`manifest.json` 文件。

此模组无需作为引用添加到您的项目中。

### 设置默认语言

建议为您的模组设置一个默认语言，当游戏当前语言不被支持或缺少翻译时使用。默认语言应是您模组最能良好支持的语言。

您可以直接在 C# 代码中通过向模组的程序集添加 `System.Resources.NeutralResourcesLanguage` 属性来设置模组的默认语言。若要将模组的默认语言设为英文，可以在源码中添加如下属性：

```cs
[assembly: System.Resources.NeutralResourcesLanguage("EN")]
```


任意位置均可生效，但推荐位置是 `Properties/AssemblyInfo.cs`。

或者，您也可以在模组的项目文件中定义 `NeutralLanguage` 属性。若要将默认语言设为英文，可在项目文件中添加如下内容：

```xml
<PropertyGroup>
    <NeutralLanguage>EN</NeutralLanguage>
</PropertyGroup>
```


#### 格式字符串与默认语言

在某些情况下，可能希望定义一种形式为 `{1} {0}` 的语言字符串，并将其作为 `string.Format` 的第一个参数传递。这可能导致其他语言出现语法问题；在这种情况下，使用形式 `{0} - {1}` 可能更合适，例如 `Bellway - Bilewater` 在任意语言中显得不自然，而 `Bilewater Bellway` 在英文中是自然的。然而，如果希望在英文中保持正确的语序，则不能将 EN 作为默认语言。为实现此目标，推荐采用以下方案：

- 将默认语言设为任意语言（如 `DEFAULT`）。
- 使用 `default.json` 存储默认格式字符串和英文文本（或任何其他希望使用的语言）。
- 使用 `en.json` 存储仅被覆盖的格式字符串。

### 添加本地化文本

要为模组添加本地化文本，请在模组程序集旁边创建一个名为 `languages` 的目录。在此目录中，可以添加名为 `{lang}.json` 的文件，其中 `{lang}` 是游戏使用的两位语言代码。语言文件以不区分大小写的方式加载。如果您的模组是 `YourName.YourModName`，以下文件结构将定义英文和法文的本地化文本：

```
BepInEx/
    plugins/
        silksong_modding-I18N/
            Silksong.I18N.dll
        YourName.YourModName/
            YourName.YourModName.dll
            languages/
                en.json
                fr.json
```


游戏支持的语言代码如下：`EN`（英语）、`FR`（法语）、`DE`（德语）、`ZH`（简体中文）、`ES`（西班牙语）、`KO`（韩语）、`JA`（日语）、`IT`（意大利语）、`PT`（巴西葡萄牙语）和 `RU`（俄语）。

本地化文本文件的内容是将翻译键映射到本地化文本值的 JSON 对象。例如，`en.json` 的内容可能是：

```json
{
    "TRANSLATION_KEY": "Localized text!",
    "CUSTOM_TOOL_NAME": "Super Cool Tool",
    "CUSTOM_TOOL_DESC": "The name of this tool rhymes. How amusing.",
    "SOME_DIALOGUE": "Hi! I’m an NPC…<hpage>And I’m Hornet.<page>Yay!"
}
```


如果您的模组定义了默认语言，而游戏当前语言没有对应的翻译文件，则会使用默认语言的翻译文件。如果模组为游戏语言提供了翻译文件但部分键缺失，则缺失的键将从默认语言的翻译文件中加载。

假设您的模组默认语言是 `EN`，且 `fr.json` 内容如下：

```json
{
    "CUSTOM_TOOL_NAME": "Outil super cool",
    "CUSTOM_TOOL_DESC": "Le nom de cet outil ne rime pas. Ce n’est pas drôle."
}
```


那么键 `TRANSLATION_KEY` 和 `SOME_DIALOGUE` 将从 `en.json` 中加载。

**强烈建议**：您永远不要在模组使用的任何翻译键中包含冒号字符（`:`），因为如果用户不了解 JSON 语法或其文本编辑器未提供 JSON 语法高亮，可能会因此产生困惑。

#### 将本地化文本包含在项目文件中

为了将本地化文本添加到您的模组包中，您需要更新您的 `.csproj` 文件。找到包含 `Binaries` 项的 `ItemGroup` 并编辑它，通过为 `languages/*.json` 添加新条目来包含您的语言文件。同时，请将每个条目的 `PackDir` 属性全部设置为以 `plugins` 开头。最终结果应如下所示：

```xml
<ItemGroup>
  <!-- Replace `PackDir="/"` with `PackDir="plugins"`: -->
  <Binaries Include="$(TargetPath)" LocalDir="/" PackDir="plugins" />
  <Binaries Include="$(TargetDir)/$(TargetName).pdb" LocalDir="/" PackDir="plugins" />
  <!-- Add this line: -->
  <Binaries Include="languages/*.json" LocalDir="languages" PackDir="plugins/languages" />
</ItemGroup>
```


其中 `LocalDir` 是本地安装模组时作为后构建步骤复制文件的位置。  
`PackDir` 是在 Thunderstore 包中复制文件的位置。由于 Thunderstore 安装规则，当您希望在安装包中包含嵌套目录（如此处）时，这两个路径通常会不同。

如果您使用的是较旧版本的丝之歌插件模板创建模组，则还需要更新 Copy 目标以处理 `LocalDir` 和 `PackDir` 属性。您可以参考更更新的  
[template](https://github.com/silksong-modding/Silksong.Modding.Templates/blob/61ab392917e46cb272ed1c38ee27cd4b79c91754/Silksong.Modding.Templates/content/SilksongPlugin/Silksong.Plugin.1.csproj#L57-L75)  
以查看必要的修改内容。

### 使用本地化文本

您的模组的本地化文本将被加载到一个名为 `Mods.{id}` 的表中，其中 `{id}` 是您模组 BepInEx 插件的唯一 ID。如果您的模组是 `YourName.YourModName`，该表名称将是 `Mods.YourName.YourModName`。

要访问本地化文本，可以使用 `TeamCherry.Localization.LocalisedString` 和 `TeamCherry.Localization.Language` 类。请注意，`LocalisedString` 类名采用澳大利亚英语拼写（“localised”），但命名空间 `TeamCherry.Localization` 并未如此。确保任何使用本地化文本的文件都导入了 `TeamCherry.Localization` 命名空间，例如：

```cs
using TeamCherry.Localization;
```


然后，您可以创建一个带有正确表名和键名的 `LocalisedString` 来访问您模组的本地化文本。如果您的插件是 `YourModPlugin` 且定义了一个名为 `CUSTOM_TOOL_NAME` 的键，则可以这样访问该键：

```cs
new LocalisedString($"Mods.{YourModPlugin.Id}", "CUSTOM_TOOL_NAME");
```


或者，您可以直接调用 `Language.Get` 来访问由模组加载的字符串。如果您的插件是 `YourModPlugin` 且定义了一个名为 `CUSTOM_TOOL_DESC` 的键，则可以将其作为 `string` 访问，如下所示：

```cs
Language.Get("CUSTOM_TOOL_DESC", $"Mods.{YourModPlugin.Id}")
```
