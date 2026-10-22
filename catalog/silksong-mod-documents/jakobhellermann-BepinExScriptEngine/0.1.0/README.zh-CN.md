# ScriptEngine 来自 BepinEx.Debug

https://github.com/BepInEx/BepInEx.Debug

> 从 BepInEx\scripts 文件夹中加载和重新加载 BepInEx 插件。用户可以通过配置文件中定义的键盘快捷键重新加载所有这些插件。默认快捷键为 F6。

> 开发插件时非常有用，因为无需反复重启游戏以查看修改效果。

> 如果需要，记得在旧版本插件结束后进行清理工作。例如，和谐补丁或松散的 GameObjects/MonoBehaviours 在插件被销毁后仍会残留。此类松散的游戏对象和单个脚本组件是指未附加到父脚本引擎游戏对象上的对象。例如：