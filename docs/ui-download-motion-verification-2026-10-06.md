# 下载界面与动效验证（2026-10-06）

## 实际交付

- 下载页标题、搜索工具栏和版本列表对齐；保留账号和版本白色卡片，压紧间距。
- 登录与队列批量操作按钮等宽，版本行在悬停、选中及键盘焦点时保持内容位置。
- 下载失败显示固定中文摘要和处理建议；原始错误与复制操作移入默认折叠详情。
- 丝之歌显示动态最新版入口和固定的 1.0.30000、1.0.29980；文件核验标识匹配内置游戏、Depot、Manifest 与文件指纹，本地标识匹配同游戏实例且目录存在。
- 市场详情进入／返回、封面加载、下载阶段文字增加短过渡；保留减少／关闭动效，进度更新不反复触发，隐藏、解绑和关闭时取消并复位。

关键实现：`src/Crystalfly.App/Views/MainWindow.axaml`、`src/Crystalfly.App/Services/MotionCoordinator.cs`、`src/Crystalfly.App/ViewModels/MainViewModel.cs`。

## 最终验证

下列两次定向回归均退出码 0，共 26 项通过、0 失败；不运行全量测试。

```powershell
dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~DownloadBuildUiTests|FullyQualifiedName~Failed_download_requires_close_confirmation|FullyQualifiedName~DownloadQueueGroupItemViewModelTests|FullyQualifiedName~Silksong_download_projection_keeps_fixed_builds_and_requires_matching_local_instances' --logger 'trx;LogFileName=ui-queue-versions-20261006.trx' --results-directory artifacts/test-results/ui-queue-versions-20261006

dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~PageTransitionMotionTests|FullyQualifiedName~GameAvatarSwitchTests' --logger 'trx;LogFileName=motion-avatar-final-20261006.trx' --results-directory artifacts/test-results/motion-avatar-final-20261006
```

第一条 11/11 通过；第二条 15/15 通过。初次组合回归的 6 个新动效测试因测试窗口没有名称作用域失败，修复测试夹具后通过，未修改生产头像／加载动画的查找行为。首轮市场 `ThunderstoreUiTests` 的 12 项均通过，结果保留于 `artifacts/test-results/ui-download-motion-20261006/ui-download-motion-20261006.trx`；该组合运行整体退出码为 1，不能当作整轮通过证据。

XAML XML 解析与相关文件 `git diff --check` 通过。没有截图、图像识别或真实桌面视觉验收。

## 本地覆盖

```powershell
pwsh -NoProfile -File .\scripts\build-and-install.ps1 -UnsignedLocal
```

退出码 0。Windows x64 构建和安装器成功，覆盖到 `D:\Program Files\Crystalfly`。发布目录 `artifacts/publish/win-x64` 的 248 个文件与安装文件 SHA-256 全部一致；证据保存在 `artifacts/ui-versions-motion-install-verification-20261006.json`。安装包：`artifacts/installer/Crystalfly-1.1.5-win-x64-setup.exe`。

启动命令：

```powershell
& 'D:\Program Files\Crystalfly\Crystalfly.App.exe'
```

## 未完成边界

本轮新增历史版本数为 0：已知可用 Manifest 在隔离的 60 秒超时和 CDN 直连探针中均于清单下载阶段超时，未取得指纹文件。没有将候选 Manifest 伪标为已核验，也没有部署探针的超时／代理改动。详见 `docs/silksong-verification.md` 的 2026-10-06 段落。

没有可靠逐版本官方发布日期，实际日期留空。文件已核验不表示模组兼容或完整游戏启动验收。本轮未修改用户游戏、存档或重试真实失败队列；未提交、推送或发布。
