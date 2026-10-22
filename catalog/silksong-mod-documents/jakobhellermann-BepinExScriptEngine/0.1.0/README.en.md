# ScriptEngine from BepinEx.Debug

https://github.com/BepInEx/BepInEx.Debug

> Loads and reloads BepInEx plugins from the BepInEx\scripts folder. User can reload all of these plugins by pressing the keyboard shortcut defined in the config. Shortcut is F6 by default.
Very useful for quickly developing plugins as you don't have to keep reopening the game to see your changes.

> Remember to clean up after the old plugin version in case you need to. Things like harmony patches or loose GameObjects/MonoBehaviours remain after the plugin gets destroyed. Loose gameobjects and monobehaviours in this case are objects that are not attached to the parent scriptengine gameobject. For example: