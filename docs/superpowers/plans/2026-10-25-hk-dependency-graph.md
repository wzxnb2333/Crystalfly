# HK 模组依赖图修复计划

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 补全可确定的依赖关系，让卡片可以越过坐标原点自由拖动，并将详情改为画布内的小窗。

**Architecture:** 复用现有 Avalonia 图模型和安装记录。安装记录保留已安装版本的依赖；同版本目录补充遗漏，缺失依赖按目录递归展开。世界坐标允许负数，绘制坐标相对于图形边界归一化，拖动卡片不改变视口平移。

**Tech Stack:** .NET 10、Avalonia、xUnit、Avalonia.Headless。

**Scope:** 不改变安装、启停、卸载策略，不将新版目录的依赖强加给已安装旧版本，不覆盖现有工作区修改。

- [x] 添加依赖来源、递归缺失依赖、负坐标往返、拖动不平移的失败回归。
- [x] 修复关系构建和世界坐标/绘制坐标，保留布局保存与恢复。
- [x] 图视图占满宽度；右下角可收起详情窗，展示依赖与被依赖关系；列表保留原详情。
- [x] 改善默认缩放和连线方向提示，执行定向测试并检查中英文、紧凑尺寸截图。
- [x] 根据用户补充，增强浅色主题辅助文字、通用边框、图形边框和连线对比度；使用浅灰画布区分白色卡片。

## 验证

49 项定向测试通过，覆盖依赖来源、递归与循环依赖、已安装旧版本的关系保留、负坐标拖动与保存、负坐标连线像素、缩放和平移、按钮可读性及浅色资源对比度。最后的浮窗空状态与列表切换变更后，另复跑 6 个深浅色和尺寸组合，全部通过。`git diff --check` 通过。

浅色资源回归要求：辅助文字对比白色卡片至少 7:1；图形边框至少 3:1；连线对比画布至少 4.5:1。这些是资源颜色检查，不代表整个软件通过无障碍认证。

## 截图

- [用户提供的原图](../../../artifacts/hk-dependency-graph/before-user.png)
- [浅色修改前](../../../artifacts/hk-dependency-graph/light-before/hk-graph-1100-SimplifiedChinese-light-default.png)
- [浅色修改后](../../../artifacts/hk-dependency-graph/after/hk-graph-1100-SimplifiedChinese-light-default.png)
- [深色修改后](../../../artifacts/hk-dependency-graph/after/hk-graph-1100-SimplifiedChinese-dark-default.png)
- [900×600 英文浅色详情](../../../artifacts/hk-dependency-graph/after/hk-graph-900-English-light-expanded.png)
- [900×600 中文深色缺失依赖](../../../artifacts/hk-dependency-graph/after/hk-graph-900-SimplifiedChinese-dark-missing.png)

截图由 Avalonia Headless 渲染并目视检查。未修改用户游戏数据、未更新已安装版本、未运行全量回归或实机桌面验收。目录没有提供的依赖关系仍无法推断；缺失节点的递归关系采用目录版本，不作为已安装版本的事实。
