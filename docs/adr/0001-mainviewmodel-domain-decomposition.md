# Decompose MainViewModel into domain ViewModels

MainViewModel（主文件 5158 行 + 8 个 partial）已成为承载下载、模组、设置、实例、速通、协议、更新等多领域的 God object，任何领域改动都有高回归风险。我们决定按业务领域将其拆分为独立领域 ViewModel，并划成三个并行方向（下载中心、模组管理、其余领域），每个方向独立分支 + 独立 agent 并行推进，通过 partial 文件与导航模式隔离冲突。

选择系统拆分而非渐进优化或重写：渐进优化无法打破 God object 的职责边界；重写会丢弃已验证的事务、信任与回滚逻辑。拆分后的领域 VM 各自可独立理解与测试，为后续功能增强铺平道路。
