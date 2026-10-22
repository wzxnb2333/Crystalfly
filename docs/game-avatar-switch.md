# 启动页头像切换（2026-09-28）

在启动页左侧点击小骑士切换到丝之歌，头像变为大黄蜂；再次点击切回空洞骑士。原顶部独立游戏按钮移除，当前游戏名称放在头像下方。按钮支持键盘聚焦，悬停提示与辅助技术名称随中英文设置更新。

正常动效使用 180 ms 透明度交叉淡化和轻微缩放，不使用全屏加载遮罩。减少动效、关闭动效或系统禁用动画时立即切换。切换中禁止重复操作与启动，原有忙碌、游戏运行和未保存存档保护保持生效；两款游戏原有目录、实例、筛选恢复逻辑继续使用。

## 资源来源

新增 Assets/hornet-icon.png 从本机 Steam 正式安装的 Hollow Knight Silksong.exe 图标资源提取，为带透明通道的 128×128 PNG，仅用于标识所选游戏。角色与原始美术权利归 Team Cherry；项目代码许可证不替代第三方美术资源的授权。

SHA-256：70B6F63CB0C514DD933C31D8DB53B4B0059F5DFC6A8EEE1904F5081805B92D78。

## 验证

以下定向检查退出码 0，25 项通过，0 失败、0 跳过：

~~~powershell
dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~GameAvatarSwitchTests|FullyQualifiedName~MultiGameWorkspaceUiTests|FullyQualifiedName~MultiGameFeatureTests|FullyQualifiedName~PageTransitionMotionTests|FullyQualifiedName~MainWindowCodeBehindTests'
~~~

覆盖真实控件的鼠标往返点击、游戏状态与头像同步、忙碌/运行/未保存存档拦截、中英文提示更新、动效配置与即时关闭、持久化游戏选择，以及原有页面动效和工作区恢复。采用 Avalonia Headless，无截图和图像识别；不宣称完成视觉观感验收。

Windows x64 自包含验证构建退出码 0：

~~~powershell
dotnet publish src/Crystalfly.App/Crystalfly.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/game-avatar-validation-win-x64 -v:minimal
git -c core.safecrlf=false diff --check
~~~

启动：

~~~powershell
& 'E:/AI_collection/crystalfly/artifacts/game-avatar-validation-win-x64/Crystalfly.App.exe'
~~~

验证目录带 portable.flag，使用独立 Data，首次打开需要登记游戏目录。没有覆盖正式安装，没有提交或推送。构建摘要与验证结果在该目录的 verification.json。
