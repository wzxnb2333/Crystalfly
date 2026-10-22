# 更新日志

本项目的所有显著变更都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)，  
且本项目遵循 [Semantic Versioning](https://semver.org/spec/v2.0.0.html)。

## [1.1.1] - 2026-5-2

### 其他

- 这与 1.0.9 相同

## [1.0.9] - 2025-10-27

### 其他

- 上载至 Thunderstore 的 Peglin 模组数据库

## [1.0.8] - 2025-9-30

### 其他

- 已上传至 Thunderstore 的《空洞骑士：丝之歌》模组数据库

## [1.0.7] - 2025-6-19

### 其他

- 上载至 Thunderstore 的 PEAK 模组数据库

## [1.0.6] - 2025-3-9

### 其他

- 上载至以下 Thunderstore 模组数据库：
  - Gloomwood
  - R.E.P.O.

## [1.0.5] - 2024-5-25

### 修改内容

- AutoHookGenPatcher 现在也支持 .NET Standard 2.0（此前仅支持 2.1）

## [1.0.4] - 2024-5-2

### 修复内容

- Thunderstore 描述中不再出现第二个单词也大写的情况

### 其他

- 已上传至 Thunderstore 的 Lethal Company 模组数据库

## [1.0.3] - 2024-4-12

### 修复

- 处理在读取程序集时出现 BadImageFormatException 的错误情况

## [1.0.2] - 2024-4-5

### 修复

- 若存在两个名称相同的非插件程序集，处理该错误

## [1.0.1] - 2024-4-4

### 修改内容

- 现在使用 Thunderstore CLI 来构建 Thunderstore 包
- 更新了 README，更清晰地说明了 AutoHookGenPatcher 的功能

### 修复内容

- 修复了一个 bug，该 bug 导致当 MMHOOK 组件已存在时，AutoHookGenPatcher 无法生成新的 MMHOOK 组件版本
- README 中的少量拼写错误已修正

### 其他内容

- 已上传至 Thunderstore 的 Plasma Mod Database

## [1.0.0] - 2024-4-2

### 修改内容

**首次发布 — 与原始 HookGenPatcher 的差异**

- 不再仅针对固定的文件列表生成 MMHOOK 文件，AutoHookGenPatcher 会从已安装的插件中获取 MMHOOK 文件引用，并在可能的情况下生成这些 MMHOOK 文件。
- AutoHookGenPatcher 使用缓存文件来快速检查内容是否仍为最新状态，而无需每次都检查每个 MMHOOK 文件的信息。
- 引擎生成现已支持多线程，意味着生成多个 MMHOOK 文件所需时间更短。例如，在我的机器上，为 Lethal Company 的 `Managed` 目录中每一个文件生成 MMHOOK 文件，现在仅需约 **22.5 秒**，而没有多线程时则需要 **40.0 秒**。