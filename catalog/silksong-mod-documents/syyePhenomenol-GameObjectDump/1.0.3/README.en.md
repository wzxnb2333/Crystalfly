# Silksong.GameObjectDump

Helper mod for dumping GameObjects to a text file.

Supports:
- Dumping a GameObject, enumerable of GameObjects or an entire scene's GameObjects, using the Dump() extension.
- Filtering for certain Components and FsmStateActions (in DumpOptions).
- Registering custom log handlers for certain types (in LoggableRegistry).

To free up some memmory after use, call `ClearReflectionCaches()`.