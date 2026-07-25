# 中文 Mod 市场数据

`catalog/mod-translations.zh-CN.v1.json` 是 Crystalfly 自建的中文 Mod 名称、说明和标签显示数据。译文只以官方 [HK ModLinks](https://github.com/hk-modding/modlinks) 的英文元数据为原文；官方目录仍负责 Mod ID、版本、Loader、依赖、下载地址和 SHA-256。

## 加载与回退

程序启动时使用程序集内嵌基线，随后尝试读取本地缓存并请求 GitHub `main/catalog/mod-translations.zh-CN.v1.json`。远程内容通过校验后原子写入缓存；远程失败时回退到有效缓存，再回退到内嵌基线。缺少翻译的 Mod 使用官方英文名称和说明。

## 维护与校验

维护译文时，仅允许依据官方 ModLinks 的当前 `Manifest/Name`、`Description` 和 `Tags` 独立编写中文内容。不得导入第三方翻译表、下载链接、版本、依赖或其他安装元数据。

```powershell
curl.exe -L --fail --output "$env:TEMP\ModLinks.xml" `
  'https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml'
pwsh -NoProfile -File .\scripts\validate-mod-translations.ps1 `
  -OfficialModLinksPath "$env:TEMP\ModLinks.xml"
```

校验脚本要求译文目录与官方 ModLinks 的当前 Mod ID 一一对应，并验证 schema、标签键和中文字段。中文市场搜索同时匹配中文名称、中文说明、中文标签和官方英文名称、ID、版本、英文说明及原始标签；不提供拼音和人工别名。
