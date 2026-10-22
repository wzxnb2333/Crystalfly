# 丝之歌存档与加载器误报修复（2026-09-28）

## 原因与修复

1. 旧实现将共享目录写为 Team Cherry/Silksong；真实 Windows Steam 目录是 Team Cherry/Hollow Knight Silksong。空目录被当作有效存档基线，导致隔离状态显示就绪，却没有接管游戏实际读取的位置。
2. 真实根目录包含账号 ID 和 default 子目录。编辑器与命名快照原来只接受根目录 user1.dat 至 user4.dat，漏掉了账号档。现已支持账号/default 各自的四槽列表、编辑、导入导出、备份、快照与恢复，保留子目录结构，并继续拒绝路径穿越、未知子目录和重解析点。
3. 本次本机日志包含 Steam failed to initialize；最近写入的是 default 存档，账号目录中的旧档仍存在。启动检查在存在账号存档但 Steam 未运行时要求确认，并提示先启动 Steam，避免误认为存档被清空。Steam 进程存在不代表初始化必然成功，本轮没有把它当作成功证明。
4. 未收录构建使用 BepInEx 被基础检查直接标为“不支持”，与允许独立注入加载器的策略冲突。现改为“兼容性尚未验证”提示；冲突、文件漂移和依赖错误仍保留原检查。接管文件不等同于已验证兼容性。

## 数据保护与迁移

- 修改前备份相关共享存档与旧管理状态，并逐文件校验 SHA-256；本机备份位于忽略的 artifacts/silksong-save-recovery-20260928-205317。
- 旧错误路径的接管记录、共享备份、事务日志与空基线移入 local-low-legacy-silksong，不删除；迁移有持久化标记，支持重试。活动事务、取消或异常路径不会被强行跳过。
- 仅重新初始化旧的空实例基线；任何已有非空私有存档保留。正常空白游戏实例不会在每次启动被重新灌入旧档。
- 本机受影响实例的四个账号档已按原始字节复制到正确隔离目录，并建立命名快照。default 档保留，不以账号档自动覆盖。共享源目录 238 个文件校验保持不变。
- 不转换较新版本的存档，不取消既有的降级导入确认。较新存档能解码不代表旧游戏能正常读取。

## 验证记录

修改前复现 4 项失败：未知构建 BepInEx 误报、接管后同类误报、错误共享路径、账号/default 八槽遗漏。

修复后以下定向检查退出码均为 0：

~~~powershell
dotnet test tests/Crystalfly.Core.Tests -c Release --no-restore --filter 'FullyQualifiedName~LaunchPreflightEvaluatorTests|FullyQualifiedName~SilksongSaveTransferTests|FullyQualifiedName~SilksongSavePathMigrationTests|FullyQualifiedName~NamedSnapshotServiceTests|FullyQualifiedName~MultiGameTests|FullyQualifiedName~LocalLowIsolationServiceTests'
dotnet test tests/Crystalfly.App.Tests -c Release --no-restore --filter 'FullyQualifiedName~SaveEditorViewModelTests|FullyQualifiedName~MultiGameFeatureTests|FullyQualifiedName~MainViewModelStateTests'
~~~

Core：71 通过；App：213 通过；共 284，通过后未重复全量测试。TRX 保存在上述备份目录的 tests 子目录。另以本机真实收据验证 BepInEx 5.4.23.2 不再被判为“不支持”，四个真实账号档字节一致且能安全编解码往返。

Windows x64 自包含构建退出码 0：

~~~powershell
dotnet publish src/Crystalfly.App/Crystalfly.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/silksong-save-fix-win-x64 -v:minimal
git -c core.safecrlf=false diff --check
~~~

启动修复版：

~~~powershell
& 'E:/AI_collection/crystalfly/artifacts/silksong-save-fix-win-x64/Crystalfly.App.exe'
~~~

此验证版保留旧测试版的设置与游戏目录登记，包含头像切换更新，使用 portable.flag。请先启动并登录原 Steam 账号。不要再使用修复前的验证版操作已迁移的隔离目录。正式安装未覆盖，没有提交或推送。

本轮未启动游戏、未做截图或图像识别，因此不声明游戏内选档、Steam Cloud 同步或旧版读取新版存档已通过验收。
