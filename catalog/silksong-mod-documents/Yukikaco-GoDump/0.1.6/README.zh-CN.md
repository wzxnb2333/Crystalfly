# GoDump

# 默认键位  
- F1 打开菜单  
- F3 收集日志  
- F4 导出数据  
- isDrawRectBorder 默认为 true

# TriMay CustomKnight-创作者-修复了较少的崩溃  
- https://github.com/TriMay/CustomKnight-Creator-Fixed/releases/latest

# GoDump 教程  
- https://youtu.be/oDoj9dUOMwo

# 自定义骑士创建教程  
- https://youtu.be/PU3y3FwSlJU  
- 在红色矩形区域内绘制精灵图

# 导出 Texture2D 对齐精灵用于自定义骑士创建器  
- https://github.com/asd9176506911298/AssetStudio/releases/latest  
- https://github.com/asd9176506911298/CustomKnight-Creator-Fixed/releases/latest  
- https://youtu.be/gcKnJstZ41Q

# Unity 探索脚本窥视 TK2D 的图集  
- https://youtu.be/oDoj9dUOMwo  
```cs
foreach (var x in Resources.FindObjectsOfTypeAll<tk2dSpriteAnimation>())
{
    if(x.name == "Knight") // Anim Name
    {
        Inspect((x.FirstValidClip.frames[0].spriteCollection.materials.SyncRoot));
    }
}
```

# 大陆图与单个精灵图
- https://drive.google.com/drive/folders/1VWTdghNgBii1IAiS12Xq19v4njXN9p_L

# 每个首领角色精灵图（剧透）
<details>
<summary>每个首领角色精灵图（剧透）</summary>

| 首领名称               | 动画名称                        |
|------------------------|---------------------------------|
| **苔藓母亲**           | 苔骨母亲动画                   |
| **铃铛巨兽**           | 骨巨兽动画                     |
| **蕾丝**               | 蕾丝动画                       |
| **失落的蕾丝**         | 失落蕾丝动画                   |
| **第四合唱团**         | 不跳过 tk2d                    |
| **狂暴飞虫**           | 骨飞虫巨人动画                 |
| **姐妹碎刺**           | 碎刺女王动画                   |
| **骷髅暴君**           | 骷髅国王动画                   |
| **寡妇**               | 旋转者动画                     |
| **沼泽翼**             | 蛊毒蚊动画                     |
| **最后的审判者**       | 最后的审判者动画               |
| **巨大珊瑚壳钻机**     | 珊瑚珊瑚钻机巨人动画          |
| **幽灵**               | 幽灵动画                       |
| **特罗比奥**           | 特罗比奥动画                   |
| **受折磨的特罗比奥**   | 受折磨的特罗比奥动画          |
| **齿轮舞者**           | 钟表舞者动画                  |
| **第二哨兵**           | 歌之骑士动画                  |
| **爬父**               | 爬父动画                       |
| **乌鸦人**             | 陪审员动画                     |
| **乌鸦人匕首**         | 陪审员动画                     |
| **小乌鸦人**           | 小陪审员动画                   |
| **狂怒珊瑚飞虫**       | 珊瑚珊瑚钻机动画              |
| **塞斯**               | 塞斯动画                       |
| **大丝母**             | 丝首领动画 / 丝首领 DeathEmbrace 动画 |
| **雏菊舞者**           | 雏菊舞者动画                  |
| **卡雷米塔**           | 猎手女王动画                   |
| **甲壳之王哈恩**       | 珊瑚之王动画                  |
| **奈利斯**             | 花之首领动画                  |
| **格罗尔大法师**       | 沼泽巫师动画                  |
| **厨师卢戈利**         | 蚂蚁饲养员厨师动画            |
| **卵母体**             | 板状飞虫卵母体                |
| **巨跳蚤**             | 巨跳蚤动画                    |
| **加尔蒙与扎扎**       | 加尔蒙动画                    |
| **边缘的守望者**       | 珊瑚战士灰色动画              |
| **苍白舞台**           | 雏菊舞台白色动画              |
| **前兄弟西尼斯和格隆** | 船坞守卫动画                  |
| **被撕裂的存在**       | 指挥官动画                    |
| **戈尔，流放者**       | 骨猎人陷阱者动画              |
| **伏特维姆**           | 电巢动画                      |
| **沙克拉**             | 地图绘制者动画                |
| **钉压者**             | 钉压者首领动画                |
| **火焰之父**           | 不跳过 tk2d                   |
| **铃铛吞噬者**         | 巨型千足虫动画                |
| **失落的加尔蒙**       | 加尔蒙黑色丝线动画            |
| **拳头罪人**           | 第一编织者动画                |
</details>

# 每个纹章精灵（提示）
<details>
<summary>每个纹章精灵（提示）</summary>

| 纹章名称 | 动画名称 |
| :--- | :--- |
| **收割者** | 大黄蜂 CrestWeapon 斧头动画 |
| **流浪者** | 大黄蜂 CrestWeapon 刀刃动画 |
| **野兽** | 大黄蜂 CrestWeapon 武士动画 |
| **女巫** | 大黄蜂 CrestWeapon 捆绑鞭动画 |
| **建筑师** | 大黄蜂 CrestWeapon 钻头长矛动画 |
| **萨满** | 大黄蜂 CrestWeapon 萨满动画 |

</details>

# 问题求助链接  
- 模组开发 Discord: https://discord.gg/hkmodding  
- GoDump 问题：https://discord.com/channels/879125729936298015/1417543334788075581  
- 丝之歌皮肤聊天：https://discord.com/channels/879125729936298015/1416781980728557661