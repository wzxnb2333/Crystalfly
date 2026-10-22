# AssetHelperMenu

一个添加模组菜单的插件，该菜单暴露了与 [AssetHelper](https://github.com/silksong-modding/Silksong.AssetHelper) 相关的开发者工具。

此处的所有工具均可在 DebugTools.Dev 命名空间中找到 AssetHelper，更详细的文档可在 [AssetHelper docs](https://docs.silksong-modding.org/Silksong.AssetHelper/api/Silksong.AssetHelper.Dev.DebugTools.html) 中获取。

附加说明：
* 本插件中的任何函数都可能因执行计算和处理而冻结游戏。
* 当前场景层级导出提供当前活动场景中游戏对象的层级结构。
路径在此处指的是包内的路径，是使用 AssetHelper 请求资源所需的路径；如果在运行时进行了修改，这些路径可能与场景中的路径不一致。
