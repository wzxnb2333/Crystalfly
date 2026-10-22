# 启动页对齐与等宽修整

日期：2026-09-28。

## 调整结果

- 启动页内容使用同一个滚动容器；窄窗口中的社区卡片紧随实例卡片，不再占据固定的底部区域、截断实例状态卡片。
- 卡片使用 16 DIP 内边距和 16 DIP 间距；引导卡片与实例卡片等宽、左右对齐。
- 引导的三个操作、启动问题入口、查看详情使用 160 × 36 DIP 按钮；左侧启动、选择实例、实例设置保持等宽，统一为 44 DIP 高。
- 四项检查使用相同的图标、名称、160 DIP 状态列；状态右边缘与查看详情按钮对齐。
- 宽度不足 1180 DIP 时，社区卡片与主卡片等宽，两款游戏的社区链接各占一个等宽列；更宽窗口中社区使用 272 DIP 侧栏，与第一张卡片顶边对齐。调整窗口宽度不会重建或移动控件的父容器。
- 启动有问题时不再同时显示“就绪”徽章；英文关闭引导按钮改为简洁的“Got it”，避免等宽按钮内文字截断。

## 修改入口

- src/Crystalfly.App/Views/MainWindow.axaml：卡片、按钮、检查行及社区结构。
- src/Crystalfly.App/Views/MainWindow.axaml.cs：按窗口宽度调整社区所在行列。
- src/Crystalfly.App/Styles/CrystalflyTheme.axaml：启动页局部尺寸样式。
- src/Crystalfly.App/ViewModels/LocalizationViewModel.cs：英文引导按钮文字。
- tests/Crystalfly.App.Tests/Ui/LaunchLayoutAlignmentTests.cs：布局回归。
- tests/Crystalfly.App.Tests/Ui/LayoutRenderingTests.cs：左侧操作测试明确定位操作区，避免将游戏切换头像计入三个操作按钮。

## 验证

以下最终检查退出码均为 0：

~~~powershell
dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~LaunchLayoutAlignmentTests|FullyQualifiedName~Launch_rail_actions_are_vertical_equal_width_buttons|FullyQualifiedName~GameAvatarSwitchTests|FullyQualifiedName~PageTransitionMotionTests|FullyQualifiedName~MainWindowStructureTests.Launch' --logger 'trx;LogFileName=launch-alignment.trx' --results-directory artifacts/launch-alignment-tests
dotnet publish src/Crystalfly.App/Crystalfly.App.csproj -c Release -r win-x64 --self-contained true --no-restore -o artifacts/launch-alignment-win-x64 -v:minimal
git -c core.safecrlf=false diff --check
~~~

20 项定向测试通过，0 失败、0 跳过。新增 8 项布局用例覆盖 900、1100、1180、1280、1600 DIP 窗宽，中英文、浅色和深色组合，以及窗口反复缩放、关闭引导与状态恢复。直接验证控件边界、卡片间距、按钮尺寸和文字适配、社区等宽列、滚动到底可访问最后一项。另验证头像切换、既有页面动效和启动页结构。

第一轮布局检查发现英文关闭引导文字在固定宽度按钮内截断，修正文案后上述检查全部通过。未执行截图、图像识别、桌面视觉验收或游戏启动；没有执行全量测试。

## Windows x64 验证版

~~~powershell
& 'E:/AI_collection/crystalfly/artifacts/launch-alignment-win-x64/Crystalfly.App.exe'
~~~

此目录为自包含便携构建，带 portable.flag。Data/settings.json 沿用上一轮 silksong-save-fix-win-x64 验证版的设置，保留游戏目录和当前实例选择。未复制或改写游戏存档，未覆盖正式安装，未提交、推送或发布。测试报告在 artifacts/launch-alignment-tests/launch-alignment.trx，构建摘要在验证版目录的 verification.json。
