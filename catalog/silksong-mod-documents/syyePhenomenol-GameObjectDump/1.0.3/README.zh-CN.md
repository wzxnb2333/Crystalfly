# Silksong.GameObjectDump

用于将 GameObjects 导出到文本文件的辅助模组。

支持功能：
- 使用 Dump() 扩展方法导出一个 GameObject、可枚举的 GameObjects 或整个场景的 GameObjects。
- 在 DumpOptions 中对特定组件和 FsmStateActions 进行过滤。
- 在 LoggableRegistry 中注册特定类型的自定义日志处理器。

使用后为释放部分内存，请调用 `ClearReflectionCaches()`。