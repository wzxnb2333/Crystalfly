# Crystalfly

Windows 上的《空洞骑士》版本、加载器、模组、存档与速通环境管理器。

当前仓库包含可独立测试的核心库，提供：

- 带版本号的 JSON 数据模型与原子写入；
- 实例目录扫描、sidecar 元数据与构建指纹；
- 动态 `latest` 通道解析；
- 远程 catalog、缓存和内嵌官方数据的分层合并；
- 自定义 catalog 仅可新增条目，不能覆盖官方数据。

## 开发

需要 .NET SDK 10：

```powershell
dotnet restore Crystalfly.slnx
dotnet build Crystalfly.slnx --no-restore
dotnet test Crystalfly.slnx --no-build
```

## English

Crystalfly manages Hollow Knight versions, loaders, mods, saves, and speedrun environments on Windows.

The repository currently contains the independently testable core library: versioned JSON models, atomic persistence, instance discovery and fingerprints, dynamic channel resolution, and layered catalogs with protected official entries.

Development requires .NET SDK 10. Use the commands above to restore, build, and test the solution.
