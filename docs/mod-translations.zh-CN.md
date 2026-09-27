# 中文 Mod 市场数据

`catalog/mod-translations.zh-CN.v1.json` 是 Crystalfly 自建的中文 Mod 名称、说明和标签显示数据。译文只以官方 [HK ModLinks](https://github.com/hk-modding/modlinks) 的英文元数据为原文；官方目录仍负责 Mod ID、版本、Loader、依赖、下载地址和 SHA-256。

## 加载与回退

程序启动时使用程序集内嵌基线，随后尝试读取本地缓存并请求 GitHub `main/catalog/mod-translations.zh-CN.v1.json`。远程内容通过校验后原子写入缓存；远程失败时回退到有效缓存，再回退到内嵌基线。缺少翻译的 Mod 使用官方英文名称和说明。

## 维护与校验

2026-09-27 同步的官方 ModLinks 快照共 675 个 Mod。此前补齐了缺失的 23 个条目和 `LLM-Assisted` 标签，但“675/675 条目齐全”不等于“675 个名称均已汉化”：其中仍有 432 个主名称只有英文。现已逐项依据官方名称与说明补充中文名称，675 个主名称均含有中文，11 个标签均有中文显示。

中文模式优先显示中文名称，并保留官方英文名称作为次要信息，便于搜索和对照。开发库及专有名称可保留原名，并配以中文用途说明；这属于模组市场信息汉化，不代表将各个 Mod 的游戏内界面也翻译成中文。

维护译文时，仅允许依据官方 ModLinks 的当前 `Manifest/Name`、`Description` 和 `Tags` 独立编写中文内容。不得导入第三方翻译表、下载链接、版本、依赖或其他安装元数据。

```powershell
curl.exe -L --fail --output "$env:TEMP\ModLinks.xml" `
  'https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml'
pwsh -NoProfile -File .\scripts\validate-mod-translations.ps1 `
  -OfficialModLinksPath "$env:TEMP\ModLinks.xml"
```

校验脚本要求译文目录与官方 ModLinks 的当前 Mod ID 一一对应，并检查格式版本、标签键、字段长度及名称中的中文字符；JSON Schema 与内嵌目录测试也拒绝纯英文主名称。标签键从传入的官方 XML 提取，以便在上游新增标签时及时发现缺译。字符检查只能发现未汉化名称，译名准确性仍须根据官方原文人工审阅。市场投影测试覆盖 ModCommon、ModConsole、ModScript、ModTerminal、MoreLocations、MoreMasks 和 MoreStags 的中文主名称、英文原名保留和中英文搜索。

中文市场搜索同时匹配中文名称、中文说明、中文标签和官方英文名称、ID、版本、英文说明及原始标签；不提供拼音和人工别名。
