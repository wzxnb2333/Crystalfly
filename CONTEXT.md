# Crystalfly

Crystalfly 是 Windows 上管理 Hollow Knight 游戏实例、加载器、模组、速通环境与游戏资产下载的桌面工具。

## Language

**主视图模型（MainViewModel）**：
应用根视图模型，当前承载下载、模组、设置、实例、速通等多个业务领域的逻辑。
_Avoid_: God object、巨型 VM

**领域视图模型（domain ViewModel）**：
按业务领域从主视图模型拆分出的独立视图模型，每个负责一个清晰领域，可独立理解和测试。
_Avoid_: feature VM、子 VM

**下载中心（Download Center）**：
管理游戏资产下载队列的领域，涵盖下载、进度、重试、历史与错误。
_Avoid_: downloads、下载队列页

**模组管理（Mod Management）**：
管理已安装模组的领域，涵盖健康、依赖、冲突、更新与详情。
_Avoid_: mods 页、已安装模组

**VM 拆分（ViewModel decomposition）**：
将主视图模型按业务领域拆分为独立领域视图模型的架构实践，通过 partial 文件与导航模式保持一致性。
