namespace Crystalfly.App.ViewModels;

/// <summary>Keys checked against PlayerData metadata in Steam Silksong 1.0.30000.</summary>
internal static class SilksongSaveFieldLocalization
{
    private static readonly IReadOnlyDictionary<string, SaveFieldLocalization.Field> Fields =
        new Dictionary<string, SaveFieldLocalization.Field>(StringComparer.Ordinal)
        {
            ["health"] = new("当前生命", "当前剩余的普通生命值。"),
            ["maxHealth"] = new("最大生命", "当前普通生命上限。"),
            ["maxHealthBase"] = new("基础生命上限", "基础生命值，不等同于临时加成。"),
            ["healthBlue"] = new("蓝色生命", "额外蓝色生命值。"),
            ["heartPieces"] = new("面具碎片进度", "已收集的面具碎片计数。"),
            ["geo"] = new("念珠", "当前持有的念珠数量；丝之歌中的 geo 不表示本体的吉欧。"),
            ["ShellShards"] = new("贝壳碎片", "当前工具材料数量。"),
            ["silk"] = new("当前丝线", "当前可用丝线量。"),
            ["silkMax"] = new("丝线上限", "丝线容量。"),
            ["silkRegenMax"] = new("丝线恢复上限", "游戏记录的丝线恢复上限。"),
            ["silkParts"] = new("丝线升级记录", "丝线容量升级的内部计数。"),
            ["silkSpoolParts"] = new("丝轴碎片进度", "丝轴碎片的内部计数。"),
            ["IsSilkSpoolBroken"] = new("丝轴损坏状态", "丝轴当前是否损坏。"),
            ["hasDash"] = new("冲刺能力", "是否已取得冲刺。"),
            ["hasWalljump"] = new("蹬墙跳能力", "是否已取得蹬墙跳。"),
            ["hasDoubleJump"] = new("二段跳能力", "是否已取得二段跳。"),
            ["hasNeedolin"] = new("针琴能力", "是否已取得针琴。"),
            ["hasBrolly"] = new("滑翔能力", "是否已取得滑翔相关能力。"),
            ["hasHarpoonDash"] = new("钩针冲刺能力", "对应能力的取得标记。"),
            ["hasNeedleThrow"] = new("投针能力", "投针技能的取得标记。"),
            ["hasThreadSphere"] = new("丝线球技能", "丝线球技能的取得标记。"),
            ["hasSilkSpecial"] = new("丝技解锁", "是否已取得丝技。"),
            ["silkSpecialLevel"] = new("丝技等级", "丝技的内部等级记录。"),
            ["hasParry"] = new("格挡能力", "格挡技能的取得标记。"),
            ["hasChargeSlash"] = new("蓄力斩能力", "蓄力斩的取得标记。"),
            ["CurrentCrestID"] = new("当前纹章", "当前装备纹章的原始 ID；保留内部字符串。"),
            ["PreviousCrestID"] = new("上次纹章", "上次装备纹章的原始 ID。"),
            ["IsCurrentCrestTemp"] = new("临时纹章状态", "当前纹章是否由临时状态指定。"),
            ["ToolPouchUpgrades"] = new("工具袋升级", "工具袋的升级计数。"),
            ["ToolKitUpgrades"] = new("工具套件升级", "工具套件的升级计数。"),
            ["respawnScene"] = new("重生场景", "死亡或读档后使用的内部场景名称。"),
            ["respawnMarkerName"] = new("重生位置标记", "场景内部的位置标记，需与重生场景匹配。"),
            ["mapZone"] = new("所在地图区域", "游戏内部地图区域标识。"),
            ["atBench"] = new("长椅休息状态", "是否处于长椅休息状态。"),
            ["version"] = new("存档游戏版本", "生成存档的游戏版本；修改此字段不会转换存档格式。"),
            ["profileID"] = new("存档槽位", "游戏记录的槽位编号。"),
            ["playTime"] = new("游玩时间", "游戏保存的累计游玩时间。"),
            ["permadeathMode"] = new("永久死亡模式", "对应游戏模式的内部状态。")
        };
    private static readonly IReadOnlyDictionary<string, string> Collections = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Tools"] = "工具解锁记录", ["ToolEquips"] = "工具装备", ["ExtraToolEquips"] = "额外工具装备",
        ["ToolLiquids"] = "工具液体记录", ["Collectables"] = "收集物记录", ["QuestCompletionData"] = "任务完成记录",
        ["QuestRumourData"] = "任务传闻记录", ["scenesVisited"] = "已访问场景", ["scenesMapped"] = "已绘制场景",
        ["scenesEncounteredBench"] = "已发现长椅场景", ["scenesEncounteredCocoon"] = "已发现茧场景",
        ["CompletedEndings"] = "已完成结局"
    };
    public static SaveFieldLocalization.Field? Find(string path)
    {
        if (path == "sceneName") return new("当前场景", "当前保存位置的内部场景名称。");
        var key = path.StartsWith("playerData.", StringComparison.Ordinal) ? path[11..] : path;
        if (Fields.TryGetValue(key, out var field)) return field;
        var separator = key.IndexOfAny(['[', '.']);
        if (separator > 0 && Collections.TryGetValue(key[..separator], out var title))
            return new(title + " · " + key[separator..], "集合中的原始成员；保留字段类型、顺序及未知成员，不自动补全任务或物品。");
        return null;
    }
}
