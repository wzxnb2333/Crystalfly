# 额外工具颜色  
一个为丝之歌制作的模组，为游戏添加了新的组合工具类型。

## 特性  
- 4 新增工具类型  
  - 绿色：蓝色 + 黄色  
  - 橙色：黄色 + 红色  
  - 紫色：蓝色 + 红色  
  - 粉色：红色 + 技能  
- 工具类型重塑  
  - 按 F1 打开 BepInEx 配置管理器，调整游戏中的工具类型  
  - 当你打开背包时，工具类型会刷新，但如果工具处于无效插槽中，则不会自动卸下

## 限制
- 颜色为红色、粉色或白色的工具（技能类）无法设置为除 3 之外的任何类型。这是为了避免由于纹章中非攻击槽位意外绑定攻击类工具而引发的一些异常行为。

## 计划功能  
- 3 新增工具类型  
  - 浅蓝色：蓝色 + 技能  
  - 浅黄色：黄色 + 技能  
  - 彩虹色：所有基础类型  
- 从配置管理器切换至 Mod Menu Mod 进行设置  
  - 增加每个存档的独立设置  
- 外观随机化  
- 纹章槽外观随机化  
- 模组工具与纹章外观随机化集成

## 模组工具  
**本部分旨在为添加自定义工具的模组开发者提供指导。**

如果你想添加一把粉色工具，首先需要决定它是否具有使用次数限制（如同红色工具），还是无限制使用（意味着会像技能一样消耗丝线）。

对于具有使用次数限制的工具，将其默认类型设置为 red（1）。对于无限制使用的工具，将其默认类型设置为 skill（0）。

然后，请应用以下后缀，将字符串替换为你自定义工具的内部 ID：
```
[HarmonyPostfix]
[HarmonyPatch(typeof(ExtraToolColors.ExtraToolColors), nameof(ExtraToolColors.ExtraToolColors.GetChangedTools))]
public static void GetChangedToolsPostfix(ref Dictionary<string, ToolItemType> __result)
{
    string toolName = "INSERT_INTERNAL_TOOL_NAME_HERE";
    if (__result.ContainsKey(toolName))
        __result[toolName] = ExtraToolColors.ExtraToolColors.Pink;
    else
        __result.Add(toolName, ExtraToolColors.ExtraToolColors.Pink);
}
```