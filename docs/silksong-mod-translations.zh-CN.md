# 丝之歌 Mod 市场与文档汉化

中文界面优先使用 Crystalfly 维护的译名、简介和分类，英文界面保留作者原文。名称在市场、详情、已安装列表、安装预览和卸载确认中统一显示；完整 `Owner-Name` 身份始终保留。汉化不修改下载地址、依赖、安装收据、模组文件或游戏内菜单。

详情页保留英文原名，原文简介可展开阅读。README 与更新日志使用现有 Markdown 阅读组件，可切换「中文／原文」，不必先安装模组。为了支持离线阅读，文档图片显示为链接，HTML 图片不会自动加载。

## 冻结基线与实际范围

冻结日期为 2026-10-05。官方来源为 [Thunderstore 丝之歌社区目录](https://thunderstore.io/c/hollow-knight-silksong/api/v1/package/)。最新版本按最高的可用版本选择，包含弃用包、加载器、依赖库及整合包。

| 内容 | 基线数量 |
| --- | ---: |
| 包名称与非空简介 | 463 |
| 分类 | 14 |
| 非空 README | 460 |
| 非空更新日志 | 133 |
| 上游没有提供的更新日志 | 328 |
| 上游提供的空文档 | 5 |

名称和简介已逐项维护。593 份非空文档使用本地模型辅助生成，并经过技术文本保留检查；正文尚未全部人工精校，界面明确标注这一点。原文、代码、命令、配置键、链接、版本号、署名和许可是核对依据。缺失与空文档不计入翻译覆盖数；下载失败也不能计入。

完整基线、来源摘要和逐包文档状态保存在 `catalog/silksong-translation-baseline.v1.json`。缺失清单和实际校验结果见 `docs/silksong-mod-translations.validation.json`，其中 `errors` 必须为空。

文档目录在 `.gitattributes` 中禁止自动换行转换，保证 Windows 和其他系统检出的原文与译文摘要一致；不要对原文运行格式化工具。

## 数据与更新

- `catalog/silksong-mod-translations.zh-CN.v1.json`：译名、简介、分类、版本与文档 SHA-256。
- `catalog/silksong-translation-overrides.zh-CN.json`：人工维护的名称和简介。
- `catalog/silksong-mod-documents/Owner-Name/版本/`：原文 `.en.md` 和中文 `.zh-CN.md`，按需读取。
- 名称按完整包身份匹配；简介按身份、版本、原文摘要匹配；文档还校验原文与译文文件摘要。无匹配译文的旧版本或改变后的原文回退作者内容，并显示说明。

内置索引立即可用，随后异步尝试仓库 `main/catalog/` 的在线更新。只接受有效且不早于现有基线的索引，合并历史版本后原子保存。文档下载后先校验摘要，再缓存。网络失败保留有效数据；损坏的译文更新可回退到相同原文的有效内置译文。离线模式不发送请求。

缓存位于应用数据目录 `thunderstore/silksong/translations/`，与市场目录、下载包及空洞骑士译文分开。源码构建和本地覆盖可以立即使用内置译文；未推送到仓库的内容不会自动出现在在线更新端。

## 新模组和版本的维护步骤

以下命令从仓库根目录运行，使用项目内或系统现有 Python，不安装全局依赖。

```powershell
curl.exe --fail --location --compressed 'https://thunderstore.io/c/hollow-knight-silksong/api/v1/package/' --output artifacts/new-silksong-catalog.json
py scripts/silksong-translation-snapshot.py --snapshot artifacts/new-silksong-catalog.json --output artifacts/new-silksong-translations --extract
```

为新快照使用新输出目录。工具只通过 HTTPS 读取官方元数据及 ZIP 文档，复用范围缓存并校验 ZIP 结构，不安装模组、不执行包内代码。抓取结果的 `failed` 必须处理后再交付；`missing` 表示官方包内不存在对应文件。

1. 根据原文在 overrides 中添加或校正完整身份对应的中文名称和简介；库名称保留专有名字，并描述用途。新分类的译文维护在索引的 `categoryNames` 中，组装工具会保留它。
2. 执行索引组装，将原文复制至文档目录；旧版本的译文保持独立。新版本没有译文时不能宣称覆盖完成。
3. 翻译或校对对应 `.zh-CN.md`，完整保留技术标识与许可信息，不推测原文没有说明的功能。
4. 运行校验，更新译文摘要；再运行受影响的 Core、App 测试。提交文档与索引时保持一致。

```powershell
py scripts/build-silksong-translations.py --snapshot artifacts/new-silksong-translations
py scripts/validate-silksong-translations.py --update-hashes
py scripts/validate-silksong-translations.py
dotnet test tests/Crystalfly.Core.Tests -c Release --filter 'FullyQualifiedName~SilksongModTranslationSourceTests'
dotnet test tests/Crystalfly.App.Tests -c Release --filter 'FullyQualifiedName~ThunderstoreTranslationTests|FullyQualifiedName~ThunderstoreUiTests|FullyQualifiedName~FontRenderingTests'
```

## 可选的离线初稿工具

`scripts/translate-silksong-local-llm.py` 仅为维护者工具，只允许连接 `127.0.0.1` 上的 llama.cpp 服务。模型、推理程序和缓存均放在忽略的 `artifacts/` 下，不随 Crystalfly 发布，也没有客户端自动翻译服务或云端翻译请求。

本轮使用 [Qwen3-4B-Instruct-2507](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507) 的本地量化模型与 [llama.cpp](https://github.com/ggml-org/llama.cpp)。维护者可换用自己的本地模型，人工校对要求保持不变。待译文档始终作为数据，不启用工具执行能力。

```powershell
# 在本地启动自己的 llama-server 后，仅用于生成待校对初稿：
py scripts/translate-silksong-local-llm.py --cache artifacts/silksong-local-drafts
```

工具保护技术片段并在本地还原；丢失占位符、截断输出或无法生成中文会记为失败。运行完成后应检查缓存目录中的 `report.json`，必要时用 `--only-failures` 重试。初稿成功与自动检查通过不等于逐份人工审核完成。

已有中文文件默认保留。只有明确重新生成初稿时使用 `--overwrite`；它可能替换此前的人工校对，因此先保留修改。
