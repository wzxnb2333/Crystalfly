# Changelog

# 4.1.1
- 为旧方法添加了过时标签

## 4.1.0
- 添加了新的 GetAudioClip 和 GetSprite 版本，它们接受 Assembly 作为输入参数

## 4.0.0
- 将 GetAudioClip 方法拆分为创建 GetAudioClipFromStream
- 新增一个辅助类：Needolin

## 3.1.1
- 调整了内部代码，以期避免触发 Thunderstore 的安全过滤机制

## 3.1.0
- 修复 EnemyDamageLogger 的BUG

## 3.0.0
- 修复 GetAudioClip 的日志记录问题  
- 添加 GetSprite  
- 将 ClassIntegrations 修改为明确允许 null 输入

## 2.0.2
- 修复了 TOML 配置以包含依赖项

## 2.0.1
- 修复了清单以包含依赖项

## 2.0.0
- 添加了 GetAssetBundle、GetAudioClip、DamageEnemy、GetEnemy、GetTools、EnemyDamageLogger、Components
- 改进了文档说明
- 修改了部署方式以包含 XML 文档说明

## 1.2.0
- 添加了 NotchCosts 和计算

## 1.1.4
- 修改了 ClassIntegrations 以支持静态类（null 输入）

## 1.1.3
- 修复了命名空间错误