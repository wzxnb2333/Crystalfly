using System.Globalization;

namespace Crystalfly.App.ViewModels;

/// <summary>
/// Presentation-only descriptions. Never rewrite JSON paths, values, or Mod-owned keys.
/// Field references: HKSM/data/SaveLoader.java in KayDeeTee/Hollow-Knight-SaveManager,
/// and hk.py in Asamo096/hk-editer. The Chinese wording is maintained locally.
/// </summary>
internal static class SaveFieldLocalization
{
    internal sealed record Field(string Name, string Description);

    private static readonly IReadOnlyDictionary<string, Field> PlayerFields = new Dictionary<string, Field>(StringComparer.Ordinal)
    {
        ["geo"] = new("吉欧", "当前持有的游戏货币数量。"),
        ["health"] = new("当前生命值", "当前剩余的普通面具生命值，与最大生命值分开保存。"),
        ["maxHealth"] = new("最大生命值", "当前普通面具生命值上限，可能受到护符等效果影响。"),
        ["maxHealthBase"] = new("基础生命上限", "基础普通面具生命值上限，与当前生命值及临时效果分开保存。"),
        ["healthBlue"] = new("蓝色生命值", "当前额外的蓝色面具生命值。"),
        ["heartPieces"] = new("面具升级记录", "存档中的面具升级计数；与碎片收集进度和当前生命值分开保存。"),
        ["heartPieceCollected"] = new("面具碎片进度", "当前一组面具碎片的收集进度。"),
        ["maxHealthCap"] = new("生命上限限制", "游戏保存的最大生命值限制。"),
        ["MPCharge"] = new("当前灵魂", "主灵魂容器中当前储存的灵魂量。"),
        ["maxMP"] = new("主灵魂容量", "主灵魂容器的容量。"),
        ["MPReserve"] = new("储备灵魂", "额外灵魂容器中当前储存的灵魂量。"),
        ["MPReserveMax"] = new("储备灵魂容量", "额外灵魂容器的总容量，与当前储备量分开保存。"),
        ["vesselFragments"] = new("容器碎片进度", "灵魂容器碎片的收集进度。"),
        ["vesselFragmentCollected"] = new("容器碎片收集记录", "游戏记录的灵魂容器碎片收集状态。"),
        ["MPReserveCap"] = new("储备灵魂容量限制", "游戏保存的额外灵魂容器容量限制。"),
        ["nailDamage"] = new("骨钉伤害", "骨钉的基础伤害记录；其他伤害加成由游戏另行计算。"),
        ["nailSmithUpgrades"] = new("骨钉强化次数", "在骨钉匠处完成的骨钉强化次数。"),
        ["ore"] = new("苍白矿石", "当前持有的苍白矿石数量。"),
        ["simpleKeys"] = new("简单钥匙", "当前持有的简单钥匙数量。"),
        ["rancidEggs"] = new("腐臭蛋", "当前持有的腐臭蛋数量。"),
        ["dreamOrbs"] = new("梦境精华", "当前记录的梦境精华数量。"),
        ["grubsCollected"] = new("已救幼虫", "已经解救的幼虫数量；各场景的幼虫状态另有记录。"),
        ["grubRewards"] = new("幼虫奖励进度", "幼虫爷爷已经发放奖励的进度。"),
        ["completionPercentage"] = new("游戏完成度", "存档记录的完成百分比；游戏也会根据实际收集与进度重新计算。"),
        ["playTime"] = new("游玩时间", "此存档记录的游玩时长，单位为秒。"),
        ["permadeathMode"] = new("钢铁之魂模式状态", "永久死亡模式的内部状态；字段类型和状态值随存档记录保留。"),
        ["isInvincible"] = new("无敌状态", "游戏保存的无敌状态开关。"),
        ["hasDash"] = new("蛾翼披风（冲刺）", "是否已经获得普通冲刺能力。"),
        ["canDash"] = new("当前允许冲刺", "当前是否允许使用冲刺；这是状态字段，与是否获得披风分开保存。"),
        ["hasShadowDash"] = new("暗影披风", "是否已经获得暗影冲刺能力。"),
        ["hasWalljump"] = new("螳螂爪（蹬墙跳）", "是否已经获得蹬墙跳能力。"),
        ["hasWallJump"] = new("螳螂爪（蹬墙跳）", "是否已经获得蹬墙跳能力；保留存档原有的字段大小写。"),
        ["hasDoubleJump"] = new("帝王之翼（二段跳）", "是否已经获得空中再次跳跃的能力。"),
        ["hasSuperDash"] = new("水晶之心（超级冲刺）", "是否已经获得蓄力超级冲刺能力。"),
        ["hasAcidArmour"] = new("伊思玛的眼泪", "是否已经获得在酸液中游动的能力。"),
        ["hasLantern"] = new("光蝇灯笼", "是否持有用于照亮黑暗区域的灯笼。"),
        ["hasDreamNail"] = new("梦之钉", "是否已经获得梦之钉。"),
        ["dreamNailUpgraded"] = new("觉醒的梦之钉", "是否已经升级梦之钉。"),
        ["hasDreamGate"] = new("梦之门", "是否已经获得设置和使用梦之门的能力。"),
        ["hasTramPass"] = new("电车通行证", "是否持有电车通行证。"),
        ["hasLoveKey"] = new("爱之钥", "是否持有爱之钥。"),
        ["hasKingsBrand"] = new("王之印记", "是否已经获得王之印记。"),
        ["hasSlykey"] = new("店主的钥匙", "是否持有交给斯莱的店主钥匙。"),
        ["hasWhiteKey"] = new("典雅的钥匙", "是否持有典雅的钥匙。"),
        ["hasCityKey"] = new("城市纹章", "是否持有城市纹章。"),
        ["hasGodfinder"] = new("神明调谐器", "是否已经获得神明调谐器。"),
        ["hasSimpleKey"] = new("简单钥匙持有状态", "存档中的简单钥匙持有标记；数量可能另有字段记录。"),
        ["fireballLevel"] = new("复仇之魂／暗影之魂等级", "0：未获得；1：复仇之魂；2：暗影之魂。"),
        ["quakeLevel"] = new("荒芜俯冲／黑暗降临等级", "0：未获得；1：荒芜俯冲；2：黑暗降临。"),
        ["screamLevel"] = new("嚎叫幽灵／深渊尖啸等级", "0：未获得；1：嚎叫幽灵；2：深渊尖啸。"),
        ["hasCyclone"] = new("旋风劈砍", "是否已经学会旋风劈砍骨钉技艺。"),
        ["hasDashSlash"] = new("冲刺劈砍", "是否已经学会冲刺劈砍骨钉技艺。"),
        ["hasUpwardSlash"] = new("强力劈砍", "是否已经学会强力劈砍骨钉技艺。"),
        ["hasNailArt"] = new("已获得骨钉技艺", "是否已经获得骨钉技艺的总标记；各招式另有开关。"),
        ["charmSlots"] = new("护符槽位数", "可用于装备护符的槽位总数。"),
        ["charmSlotsFilled"] = new("已占用护符槽位", "当前装备的护符占用的槽位数。"),
        ["charmsOwned"] = new("已拥有护符数量", "已收集护符的数量记录；每枚护符另有获得标记。"),
        ["overcharmed"] = new("护符过载状态", "当前是否处于护符过载状态。"),
        ["canOvercharm"] = new("允许护符过载", "是否已经解锁护符过载。"),
        ["royalCharmState"] = new("王魂／虚空之心状态", "王魂碎片、完整王魂和虚空之心共用的进度状态。"),
        ["grimmChildLevel"] = new("格林之子等级", "格林之子的升级阶段，与剧团任务进度有关。"),
        ["fragileHealth_unbreakable"] = new("坚固心脏升级", "易碎心脏是否已经升级为坚固心脏。"),
        ["fragileGreed_unbreakable"] = new("坚固贪婪升级", "易碎贪婪是否已经升级为坚固贪婪。"),
        ["fragileStrength_unbreakable"] = new("坚固力量升级", "易碎力量是否已经升级为坚固力量。"),
        ["brokenCharm_23"] = new("易碎心脏已损坏", "易碎心脏是否处于损坏状态。"),
        ["brokenCharm_24"] = new("易碎贪婪已损坏", "易碎贪婪是否处于损坏状态。"),
        ["brokenCharm_25"] = new("易碎力量已损坏", "易碎力量是否处于损坏状态。"),
        ["hasMap"] = new("已获得地图", "是否已经获得地图的总标记；各区域地图另有字段。"),
        ["hasQuill"] = new("羽毛笔", "是否持有用于更新地图的羽毛笔。"),
        ["mapCrossroads"] = new("遗忘十字路地图", "是否已经获得遗忘十字路的地图。"),
        ["mapGreenpath"] = new("苍绿之径地图", "是否已经获得苍绿之径的地图。"),
        ["mapFogCanyon"] = new("雾之峡谷地图", "是否已经获得雾之峡谷的地图。"),
        ["mapFungalWastes"] = new("真菌荒地地图", "是否已经获得真菌荒地的地图。"),
        ["mapCity"] = new("泪水之城地图", "是否已经获得泪水之城的地图。"),
        ["mapWaterways"] = new("皇家水道地图", "是否已经获得皇家水道的地图。"),
        ["mapMines"] = new("水晶山峰地图", "是否已经获得水晶山峰的地图。"),
        ["mapDeepnest"] = new("深邃巢穴地图", "是否已经获得深邃巢穴的地图。"),
        ["mapCliffs"] = new("呼啸悬崖地图", "是否已经获得呼啸悬崖的地图。"),
        ["mapOutskirts"] = new("王国边缘地图", "是否已经获得王国边缘的地图。"),
        ["mapRestingGrounds"] = new("安息之地地图", "是否已经获得安息之地的地图。"),
        ["mapAbyss"] = new("古老盆地地图", "是否已经获得古老盆地的地图。"),
        ["mapRoyalGardens"] = new("王后花园地图", "是否已经获得王后花园的地图。"),
        ["mapZone"] = new("当前地图区域编号", "游戏内部使用的地图区域编号。"),
        ["respawnScene"] = new("重生场景", "下次重生使用的内部场景名称；保留游戏识别的原始字符串。"),
        ["respawnMarkerName"] = new("重生位置标记", "重生场景中的位置标记名称。"),
        ["respawnType"] = new("重生方式", "游戏内部记录的重生方式编号。"),
        ["respawnFacingRight"] = new("重生时面向右侧", "重生后的角色朝向。"),
        ["atBench"] = new("坐在长椅上", "当前是否处于长椅休息状态。"),
        ["dreamGateScene"] = new("梦之门场景", "梦之门所在的内部场景名称。"),
        ["dreamGateX"] = new("梦之门横坐标", "梦之门在对应场景中的横向位置。"),
        ["dreamGateY"] = new("梦之门纵坐标", "梦之门在对应场景中的纵向位置。"),
        ["shadeScene"] = new("暗影所在场景", "死亡后留下的暗影所在的内部场景名称。"),
        ["shadePositionX"] = new("暗影横坐标", "死亡后留下的暗影在场景中的横向位置。"),
        ["shadePositionY"] = new("暗影纵坐标", "死亡后留下的暗影在场景中的纵向位置。"),
        ["geoPool"] = new("暗影保管的吉欧", "死亡后等待从暗影处取回的吉欧数量。"),
        ["metStag"] = new("已遇见鹿角虫", "是否已经遇见鹿角虫。"),
        ["openedTown"] = new("德特茅斯鹿角站", "是否已经开放德特茅斯的鹿角虫车站。"),
        ["openedCrossroads"] = new("十字路鹿角站", "是否已经开放遗忘十字路的鹿角虫车站。"),
        ["openedGreenpath"] = new("苍绿之径鹿角站", "是否已经开放苍绿之径的鹿角虫车站。"),
        ["openedFungalWastes"] = new("王后驿站鹿角站", "是否已经开放王后驿站的鹿角虫车站。"),
        ["openedRuins1"] = new("城市仓库鹿角站", "是否已经开放城市仓库的鹿角虫车站。"),
        ["openedRuins2"] = new("国王驿站鹿角站", "是否已经开放国王驿站的鹿角虫车站。"),
        ["openedRestingGrounds"] = new("安息之地鹿角站", "是否已经开放安息之地的鹿角虫车站。"),
        ["openedDeepnest"] = new("遥远村庄鹿角站", "是否已经开放遥远村庄的鹿角虫车站。"),
        ["openedRoyalGardens"] = new("王后花园鹿角站", "是否已经开放王后花园的鹿角虫车站。"),
        ["openedHiddenStation"] = new("隐藏鹿角站", "是否已经开放古老盆地的隐藏鹿角站。"),
        ["openedStagNest"] = new("鹿角虫之巢车站", "是否已经开放鹿角虫之巢的车站。"),
        ["hasJournal"] = new("猎人日志", "是否已经获得猎人日志。"),
        ["killedFalseKnight"] = new("已击败假骑士", "假骑士的击败记录。"),
        ["killedMantisLord"] = new("已击败螳螂领主", "螳螂领主的击败记录。"),
        ["killedMageLord"] = new("已击败灵魂大师", "灵魂大师的击败记录。"),
        ["killedDungDefender"] = new("已击败粪虫防御者", "粪虫防御者的击败记录。"),
        ["killedBrokenVessel"] = new("已击败残破容器", "残破容器的击败记录。"),
        ["killedInfectedKnight"] = new("已击败残破容器", "残破容器的击败记录。"),
        ["killedHollowKnight"] = new("已击败空洞骑士", "空洞骑士的击败记录。"),
        ["killedFinalBoss"] = new("最终首领击败记录", "存档中的最终首领击败状态。"),
        ["hornet1Defeated"] = new("已击败苍绿之径的大黄蜂", "苍绿之径大黄蜂战斗的完成标记。"),
        ["hornet2Defeated"] = new("已击败王国边缘的大黄蜂", "王国边缘大黄蜂战斗的完成标记。"),
        ["killedLurien"] = new("已解除卢瑞恩封印", "守望者卢瑞恩对应的守梦者进度。"),
        ["killedMonomon"] = new("已解除莫诺蒙封印", "教师莫诺蒙对应的守梦者进度。"),
        ["killedHegemol"] = new("已解除赫拉封印", "游戏以此内部字段记录野兽赫拉对应的守梦者进度。"),
        ["dreamersDefeated"] = new("已完成守梦者数量", "守梦者完成数量记录，各守梦者另有状态字段。"),
        ["colosseumBronzeCompleted"] = new("勇士的试炼已完成", "愚人斗兽场第一项试炼的完成记录。"),
        ["colosseumSilverCompleted"] = new("征服者的试炼已完成", "愚人斗兽场第二项试炼的完成记录。"),
        ["colosseumGoldCompleted"] = new("愚人的试炼已完成", "愚人斗兽场第三项试炼的完成记录。"),
        ["trinket1"] = new("漫游者日记", "当前持有、可出售给遗物搜寻者的漫游者日记数量。"),
        ["trinket2"] = new("圣巢印章", "当前持有、可出售的圣巢印章数量。"),
        ["trinket3"] = new("国王雕像", "当前持有、可出售的国王雕像数量。"),
        ["trinket4"] = new("奥秘蛋", "当前持有、可出售的奥秘蛋数量。")
    };

    private static readonly string[] CharmNames =
    [
        "", "蜂群集结", "任性的指南针", "幼虫之歌", "坚硬外壳", "巴德尔之壳",
        "亡者之怒", "快速聚集", "生命血之心", "生命血核心", "防御者纹章",
        "吸虫之巢", "苦痛荆棘", "骄傲印记", "稳定之体", "沉重之击",
        "锋利之影", "蘑菇孢子", "修长之钉", "萨满之石", "灵魂捕手",
        "噬魂者", "发光子宫", "易碎／坚固心脏", "易碎／坚固贪婪", "易碎／坚固力量",
        "骨钉大师的荣耀", "乔尼的祝福", "乌恩之形", "蜂巢之血", "舞梦者",
        "冲刺大师", "快速劈砍", "法术扭曲者", "深度聚集", "蜕变挽歌",
        "王魂／虚空之心", "飞毛腿", "梦之盾", "编织者之歌", "格林之子／无忧旋律"
    ];

    public static Field? Find(string path)
    {
        if (path == "sceneName")
        {
            return new("当前场景", "存档记录的内部场景名称，保存时保留原始字符串。");
        }
        var key = path.StartsWith("playerData.", StringComparison.Ordinal) ? path[11..] : path;
        if (PlayerFields.TryGetValue(key, out var field))
        {
            return field;
        }
        // Only recognize exact playerData members or a flat player-data document.
        // A similarly named nested Mod field must never inherit a game's description.
        foreach (var (prefix, action) in CharmActions)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && id > 0 && id < CharmNames.Length)
            {
                return new($"{CharmNames[id]} · {action}", $"护符编号 {id} 的{action}记录；获得、装备和槽位消耗分别保存。");
            }
        }
        if (key.StartsWith("equippedCharms[", StringComparison.Ordinal) && key.EndsWith(']')
            && int.TryParse(key.AsSpan(15, key.Length - 16), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            return new($"已装备护符 · 第 {(long)index + 1} 项", "已装备护符列表中的编号；此数值是护符 ID，不是护符数量。");
        }
        return null;
    }

    private static readonly (string Prefix, string Action)[] CharmActions =
    [
        ("gotCharm_", "已获得"),
        ("equippedCharm_", "已装备"),
        ("charmCost_", "槽位消耗"),
        ("newCharm_", "新获得提示")
    ];
}
