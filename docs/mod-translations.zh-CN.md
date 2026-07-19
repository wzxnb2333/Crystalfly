# 中文 Mod 市场数据

`catalog/mod-translations.zh-CN.v1.json` 是中文界面的 Mod 名称、说明和标签显示数据。官方 HK ModLinks 仍负责 Mod ID、版本、Loader、依赖、下载地址和 SHA-256；本文件不替代官方目录。

## 来源与回退

数据由维护者提供的 `Mod对照表  有中文说明，可下载.xlsx` 导入，导入日期和源文件不会写入运行时目录。程序启动时使用内嵌基线，随后尝试读取本地缓存并请求 GitHub `main/catalog/mod-translations.zh-CN.v1.json`。远程内容通过校验后原子写入缓存；远程失败时回退到有效缓存，再回退到内嵌基线。缺少翻译的 Mod 使用官方英文名称和说明。

## 更新命令

```powershell
pwsh -NoProfile -File .\scripts\import-mod-translations.ps1 `
  -InputPath 'D:\path\to\Mod对照表  有中文说明，可下载.xlsx' `
  -OfficialCatalogPath "$env:LOCALAPPDATA\Crystalfly\catalog\hk-modlinks.v77.json" `
  -OutputPath .\catalog\mod-translations.zh-CN.v1.json
```

导入脚本使用 PowerShell 和 ZIP/XML 标准库，不引入 Excel 依赖。脚本验证工作表、表头、重复名称、官方 ID、标签映射并按 ID 稳定排序；版本、依赖、英文说明和下载链接只用于核对或被忽略，不写入翻译目录。

中文市场搜索同时匹配中文名称、中文说明、中文标签和官方英文名称、ID、版本、英文说明及原始标签。未提供拼音和人工别名。
