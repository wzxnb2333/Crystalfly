# Changelog

## 版本 1.2.1
- 现在使用发布页面中的正确二进制文件

## 版本 1.2.0
- 迁移至 [Silksong.I18N](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/) 以支持可翻译文本  
- 消除由本模组引起的 `DontDestroyOnLoad` 警告（其他模组仍可能产生此类警告）

## 版本 1.1.2
- 将本地化文件包含在 thunderstore 包中

## 版本 1.1.1
- 更新 README.md
- 添加 CHANGELOG.md

## 版本 1.1.0
- 将按钮互换选项的切换开关改为按钮互换设置
- 按钮互换选项将在会话间保存
- 修复在控制器菜单中更改游戏手柄皮肤时无控制器连接的情况下的 `NullReferenceException` 问题
- 迁移为使用可翻译文本（模组现在依赖 SimpleSilksongLocalizer）