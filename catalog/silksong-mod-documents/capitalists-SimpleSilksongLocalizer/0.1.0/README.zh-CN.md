# 简单的丝之歌本地化模组
![icon](icon.png)

该模组使得替换文本、添加可翻译文本以及添加整个新语言变得（相对）简单。

## 安装

1. 下载并安装 [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)。  
2. 下载此模组并放置到 `<BepInEx Folder>/plugins`

## 使用方法  
本模组的使用方式围绕创建翻译表展开。

翻译表是一个 XML 文档，包含多个键（用于查找）和值（显示给用户）对，格式如下：  
```xml
<entries>
    <entry name="KEY">Value</entry>
    <entry name="Hello">World!</entry>
</entries>
```


### 添加本地化目录  
如果路径符合以下条件，模组会自动将该目录注册为 *本地化目录*：  
`<BepInEx folder>/plugins/*/SSL.Localization/`

例如，对于如下结构：  
```
└── BepInEx
    └── plugins
        ├── AmazingMod
        │   └── SSL.Localization
        │       └── EN
        │           └── MainMenu
        ├── Mod
        │   └── Sub
        │       └── SSL.Localization
        ├── Mod1
        ├── MyMod
        │   └── SSL.Localization
        └── MyOtherMod
            └── Nope

```
  
只有 `MyMod/SSL.Localization` 和 `AmazingMod/SSL.Localization` 被注册为 *本地化目录*。

可通过代码调用  
`SimpleSilkongLocalizerPlugin.AddLocalizationDirectory(string localizationDirectory)`  
来额外注册目录。（这需要创建一个使用本模组作为依赖的 C# 项目）

### 替换游戏文本  
要替换文本，必须创建一个包含相同键的条目表。  
该表还必须以与原表相同的名称命名，但需稍作调整。

游戏原始的翻译表存储在名为 `<language code>_<sheet title>` 的文件中，  
但本模组会查找 *本地化目录* 中名为 `<language code>/<sheet title>` 的文件。

示例：将主菜单上的“Start Game”文本替换为英文版的“Don't Start Game”。  
该文本通常保存在 `EN_MainMenu` 表中  

`<localization directory>/EN/MainMenu`  
```
<entries>
    <entry name="MAIN_START">Don&apos;t Start Game</entry>
</entries>
```
请注意：`&apos;` 用于插入撇号 `'`，因为 [it cannot be used directly for XML values, along with some other characters](https://stackoverflow.com/questions/1091945/what-characters-do-i-need-to-escape-in-xml-documents)。

### 添加新的可翻译文本键  

1. 在本地化目录中的某处创建一个新的表（e.g、`EN/Sheet1`）  
2. 在你的 *本地化目录* 中创建 `LocalizationSettings.json`  
3. 在 `LocalizationSettings.json` 中将 `CustomSheets` 键设置为一个包含你希望添加的表路径的数组。  

```json
{
  "CustomSheets": [
    "Sheet1",
    "subdirectory/mysheet"
  ]
}
```
  

#### 备用语言  
如果你未为所有可用语言（默认：DE、EN、ES、FR、IT、JA、KO、PT、RU、ZH）提供翻译表，  
并且希望当某语言缺失时显示其他语言的文本而非用户难以理解的键值对字符串：

请设置 `Fallback` 键。这允许你指定在文本缺失时使用的备用语言，  
以及排除某些语言不显示来自备用语言的文本。  

```json
{
  "CustomSheets": [
    "Sheet1",
    "subdirectory/mysheet"
  ],
  "Fallback": {
    "Language": "EN",
    "Excluded": []
  }
}
```