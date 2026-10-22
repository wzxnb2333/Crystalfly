### v1.11.18

- **修复了打完黑寡妇后、从梦境回到神社时钟上的丝不断裂的问题**  
- 修复了从黑寡妇梦境返回后铃铛上的丝线未能正确连接的问题

之前打完黑寡妇、进梦境、再回到神社，钟上的丝一直没有任何变化，拉杆不出现，神社也就没法激活。现在回到神社时会正常出现带光效的丝线，大约 2 秒后丝线爆断，露出拉杆，敲钟即可激活神社。  
之前，在击败黑寡妇后进入梦境并返回神社时，铃上的丝线始终没有变化，杠杆从未出现，神社也无法激活。现在，返回神社后，发光的丝线如预期般显现，约两秒后断裂，杠杆随之出现，摇响铃铛即可激活神社。

---

### v1.11.17

- **移除了额外手动生成的货币拾取点**
- **移除了额外的手动生成货币拾取物品**

移除了预置在各场景的额外货币拾取点（额外钥匙点）列表——现阶段的触发点已经够多了，这些额外点会拉低难度并造成游玩疲倦。生成与拾取机制保留。  
移除了场景中硬编码的额外货币拾取点（额外关键点）——场景中已经足够多的触发点，这些额外内容降低了游戏难度并增加了重复性。拾取和生成机制本身得以保留。

---

### v1.11.16

- **物品随机计算逻辑改为主动放置（按点位可达性）——测试版**  
- **物品随机化现在使用主动放置（可达性驱动）——BETA**

物品随机的计算逻辑已重构：关键物品改为按点位可达性主动放置，现在会具体到部分点位的条件判定。但大部分点位的条件数据尚未写完，因此当前仅为测试版，可能会出现严重的卡关问题，请谨慎游玩。  
物品随机化逻辑已重新设计：关键物品现在会根据点位的可达性进行主动放置，并且放置过程会考虑部分点位的条件。然而，大多数点位的条件数据尚未完善，因此这是一个 Beta 版本，可能会导致严重的软锁——请谨慎游玩。

---

### v1.11.15

- **三旋律触发点纳入预生成映射键（可外部配置）**  
- **三个旋律触发器现在会预先生成映射键（外部配置）**

三个官方旋律剧情点的奖励不再每次现抽，而是随开种一起预分配：同一随机种子下结果确定，可按键分配奖励。键集通过随插件发布的 `collectible_scene_map.txt`（`event:` 前缀行）外置，修改该文件即自动重建映射（内容并入配置指纹）。  
三个原版旋律遭遇场景不再在运行时掉落奖励——奖励在种子生成时已预先分配，因此相同的种子始终产生相同的结果，钥匙可按钥匙进行分配。钥匙集合可通过随附的 `collectible_scene_map.txt`（以 `event:` 开头的行）外部配置；编辑该文件会自动重建映射关系（文件内容会被整合进配置哈希中）。

---

### v1.11.14

- **三旋律官方剧情改为随机触发点**  
- **三个旋律故事场景现在会随机发放奖励**

三首旋律（指挥家、图书管理员、建筑师）的官方获得剧情首次触发时，改为发放随机奖励而非原版旋律：获得弹窗与官方发放均被拦截，不写入 `HasMelody*` 字段；琴照弹、演出正常走完，剧情不会被跳过也不会卡死。完成一次后不再重复触发，重进场景时机关（圆柱、踏板）保持完成态，不会反复抬板拉回演出。  
三个旋律故事场景（指挥家、图书管理员、建筑师）现在在首次进入时会随机奖励，而非原版旋律：获取弹窗和官方授予都会被拦截，但不会写入 `HasMelody*` 字段——场景仍完整播放，无跳过或软锁现象。完成一次后将不再触发，再次进入房间时一切保持已完成状态（圆柱体和压力板始终处于下落状态，不会重复出现抬板的剧情片段）。

---

- 修复迷障幽影 BOSS 战格挡替换随机奖励后卡死的问题
- 修复了当影首领格挡被替换为随机奖励时出现的软锁定问题

修复了迷障幽影（shadowboss）首领战中，格挡动作被替换为随机奖励后流程卡住、无法继续战斗的问题。发放随机奖励后现在能正常还原状态并继续战斗。
修复了当影首领的格挡被替换为随机奖励时战斗挂起的问题——奖励发放后，战斗状态已正确恢复，战斗可以正常进行。

---

### v1.11.13

- **修复二段跳相关的漂浮状态问题**  
- 修复了漂浮/滑行状态干扰双跳的问题

拿到二段跳后，跳跃和漂浮的判定不再互相干扰，二段跳落地后不会莫名其妙进入漂浮状态，操作手感恢复正常。  
双重跳跃与浮空/滑行能力不再相互干扰——成功进行双重跳跃不会意外使你陷入浮空状态，因此控制手感再次变得自然。

---

- **原生点位面具集齐动画不再卡死**  
- 修复了在原生点位上掩体装配动画卡顿的问题

从原生点位或随机发放获得面具碎片、集齐四格触发合成时，动画能正常播放完整，不再卡在半路导致无法操作。  
收集面具碎片以组装新面具现在能够流畅地播放完整动画，无论是从原生点还是随机奖励获得——不再在中途出现软锁问题。

---

- **任务物品不再参与随机**  
- 任务物品不再进入随机池

铃铛、羽毛、针、袍子等剧情与任务相关的物品保持原版行为，不会当作随机奖励发放出来，也不会被模组额外处理弄丢。  
故事与任务物品（铃铛、羽毛、别针、斗篷等）保持原版行为——它们不会作为随机奖励出现，也不会被模组影响。

---

- **跳蚤不再多余生成**  
- **跳蚤不再重复生成**

修复了同一位置反复排出多只跳蚤的问题，进入场景后跳蚤的分布与预先安排一致，不会再冒出多余的个体。  
修复了同一地点反复生成多只跳蚤的问题——进入场景后将仅显示之前分配好的跳蚤，不再额外生成。

---

- **拾取点状态正确持久化，切档不再丢记录**  
- **拾取状态现在能正确保持，并在存档切换时存活**

修复了切换或读取存档后，已拾取的记录被清空、导致重进场景时捡过的点又冒出来的问题。现在每次切档都会载入对应档位的记录，四个存档位各自独立、互不干扰。  
修复了在切换存档时收集物品记录被清空的问题，导致进入场景后拾取点会重新生成。现在每个存档槽位都能正确加载自身的记录，并保持完全独立。

---

### v1.11.12

- **修复原生点位面具碎片和丝轴碎片的持久化**  
- 修复了原生的纹章碎片/丝线卷轴拾取不会持久化的问题

重新进入场景后，原生点位的面具碎片、丝轴碎片不再反复生成，拾取后的销毁状态按当前存档正确保存。  
重新进入场景时，原生的面具碎片和丝线卷轴不再刷新——它们的收集状态现在会正确保存到当前存档中。⟧K0000⟧

---

- **四个存档档位各自独立保存进度与设置**  
- 每个存档槽位现在都保留自己的进度和设置

随机进度与配置按游戏存档位隔离保存，每档独立保留自己的进度和设置；切换、新建或读取存档时立即切换到对应数据，互不串档。  
随机进度和配置现在按存档槽隔离，每个存档槽都保持独立状态；切换到、创建或加载一个存档时，会立即替换为该存档槽的数据，不会出现跨存档槽的混合。

---

- **调整了部分图标与判定逻辑**  
- **调整了若干图标以及部分拦截逻辑**

- **重复物品发放 200 念珠补偿**  
- **重复物品现在会获得 200 念珠**

### v1.11.10

- **修复方向图标后处理，上/左/右劈三方向现在都正确显示**  
- 修复了方向图标，使上/左/右的斜线均能正确显示

- **所有奖励都会弹左下角横幅（和右上列表一致）**  
- 所有奖励现在都显示左下角的横幅，与右上角的列表一致

- **修复抗寒、游泳拿到后重启/切场景会丢失的问题**  
- **修复了冷抗性和游泳属性在重启或场景切换后丢失的问题**

- **权限值写入和本地保存绑定在一起，不会再乱**  
- 权限写入与本地存档现已关联在一起

- **开局物品数量滑块现在会重置为 0**  
- **The starting-item-count slider now resets to 0**

- **洗牌更彻底，同类奖励不再扎堆**  
- **Rewards are shuffled more thoroughly, less same-type clustering**

- **修复蓝血奖励卡住，现在能正常攒够转蓝血形态**  
- **Fixed blue-blood reward getting stuck; it now queues and turns into lifeblood form**

### v1.11.9

- **调整了面板相关内容**  
- **Adjusted panel-related content**

- **增加了左下角获得提示**  
- **Added a pickup notification in the bottom-left corner**

- **覆盖了大多数物品的图标**  
- **Added icons for most items**

- **修复了面具获得四格卡住且无法提升上限的问题**  
- **Fixed mask fragment assembly hanging and not increasing max health**

### v1.11.7

- **新增：识别原生物品并弹出对应官方弹窗**  
- **New: Native items now recognized and shown with their matching popup**

映射到四心、遥观仪、永绽花、法术等原生物品时，会识别出具体物品并弹出该物品对应的官方获得弹窗，不再共用错误画面。  
When a mapping lands on a native item such as a heart, Farsight, White Flower or a spell, the mod now recognizes it and plays that item's own official popup instead of a wrong generic screen.

---

- **新增：分裂能力正确绑定对应能力并弹窗**  
- **New: Split abilities now correctly grant and show their popup**

冲刺/飞针/漂浮/壁跳的左右分裂权限在收到时会实际赋予对应方向，并弹出带左/右标记的官方弹窗；劈方向与回血权限同步校正。  
Dash/harpoon/float/wall-jump left & right splits now truly grant their direction and show the official popup with the LEFT/RIGHT marker; slash and heal permissions were corrected too.

--- 

- **原生跳蚤救走记录持久化**  
- **Native flea rescue records now persist**

原生跳蚤位替换为拾取点后，救走的跳蚤记录会正确持久化保存。记录以触发救走时的人物坐标写入存档，重进场景时按坐标容差判重，已救走的跳蚤不再反复生成。  
Flea rescue points replaced with pickups now correctly persist their collected state. The save records the hero's position at the moment of rescue, and re-entering a scene matches against it with coordinate tolerance, so rescued fleas never respawn.

---

- **场景跳蚤自动生成显示**  
- **Fleas now automatically appear in scenes**

进入场景时，只要该场景的触发点映射到跳蚤救援，就会在对应点位自动生成跳蚤显示，无需固定点位。  
On entering a scene, any trigger point mapped to a flea rescue now automatically spawns the flea at that spot — no fixed placement needed.

---

- **修复种子在游玩中被自动更换**  
- **Fixed the seed being silently re-rolled during play**

修复了种子在未重新开档的情况下也会被自动更换、导致当前存档的映射与实际发放不一致的问题。种子确定后不再变化。  
Fixed a bug where the seed could be re-rolled even without starting a new save, making the current save's mappings drift from what is actually granted. The seed now stays fixed once set.

---

### v1.11.3

- **新增自定义游泳与抗寒全屏弹窗**  
- **Added custom fullscreen popups for Swimming and Cold Resistance**

游泳与抗寒获得时现在会弹出官方全屏获得画面，完整保留黑幕演出，名称与描述按能力自定义并附中英双语文案。  
Swimming and Cold Resistance now play the official fullscreen banner (full blackout showcase intact), with the name and description customized per ability and provided in both languages.

---

- **解决发放与演出时序问题，弹窗不再被覆盖**  
- **Fixed grant/show timing so popups are no longer overwritten**

修复了奖励发放与弹出画面之间的时序问题：此前官方演出会在设置后再次覆盖文本，导致文案显示不对；现已协调时序并缓存文本写入，弹窗稳定显示正确内容。  
Fixed the timing between granting and showing: the native showcase could re-write the text after it was set, breaking the copy. Timing is now coordinated and text writes cached, so banners reliably keep the correct text.

---

- **校正分裂能力的实际方向**  
- **Corrected the actual direction of split abilities**

校对了分裂能力各方向的发放，左右、上下不再发反，拿到的东西和描述一致。  
Split abilities now grant the intended direction every time — left/right and up/down match their description.

---

- **修复拾取点生成钱堆的问题**  
- **Fixed pickups spawning coin piles**

修复了部分拾取点被替换后错误生成钱堆、导致奖励不对应的问题。  
Fixed some pickup points being replaced but incorrectly spawning coin piles, so the reward no longer matches what was drawn.

---

- **修复怪物随机与陷阱随机开关失效**  
- **Fixed the Enemy Random / Trap Random toggles losing effect**

修复了怪物随机与陷阱随机的开关在游戏中突然失效、变回原版行为的问题。  
Enemy and trap randomization no longer silently switch back to vanilla while playing.

---

### v1.11.2

- **修复纹章进化弹窗与伊娃场景拦截**  
- **Fixed the crest evolution popup and Eva scene interception**

纹章进化现在会正常弹出官方获得画面；伊娃（织女）场景的几个互动点也会正确拦截并触发随机奖励，不再漏触发。  
Crest evolution now shows the official popup; the Eva scene interactions are properly intercepted and grant random rewards — no more missed triggers.

---

- **新增丝之心与旋律弹窗，三旋律加入随机池**  
- **Added Silk Heart & melody popups; the three melodies join the random pool**

三颗丝之心与三首旋律获得时都会弹出官方全屏画面；三首旋律现在也会作为随机奖励发放。  
All three Silk Hearts and the three melodies now show the official fullscreen banner, and the melodies are part of the random pool.

---

- **管道加入随机池，修正管道车站图标**  
- **Pipes join the random pool; pipe station icon fixed**

管道现在会作为随机奖励出现在任意地方；管道车站在地图上显示的图标也已修正。  
Pipes now appear as random rewards anywhere, and the pipe station marker on the map is fixed.

---

- **修正各地区地图对应**  
- **Fixed region maps showing the wrong area**

打开地图时不再显示成其他地区的图，缩略图与全图都与当前所在区域一致。  
The map now always shows the region you're actually in — no more mismatched thumbnails or full maps.

---

- **调整收费机机制**  
- **Reworked toll machines**

收费机现在可以正常花钱购买解锁后续流程，同时额外获得一份随机奖励。  
Toll machines can be paid as normal to unlock progression, and now also grant a random reward.

---

- **修复跳蚤音效与奖励分布**  
- **Fixed flea sound and reward distribution**

跳蚤获得时音效与救援进度恢复正常，并调整了跳蚤在随机池中的发放顺序。  
Flea pickups now play the correct sound and advance the rescue quest; their order in the reward pool was adjusted.

---

- **重复能力不再弹原版获得画面**  
- **Repeated abilities no longer play the vanilla popup**

抽到已经拥有的能力时改为静默发放，不再重复弹原版获得动画。  
Drawing an ability you already own now grants it silently instead of replaying the vanilla banner.

---

- **修复部分种子计算问题**  
- **Fixed some seed calculation issues**

修正了部分种子判定错误，避免出现"算出能开局的种子实际走不通"的误判。  
Fixed misjudgements that could hand you a seed which turns out to be unplayable.

---

- **修复部分纹章场景随机拦截失效**  
- **Fixed crest scenes that failed to intercept**

少量教堂/纹章演出场景偶尔会漏拦、直接发放原版纹章；现已全部接管，统一改为随机奖励。  
A few chapel/crest scenes could occasionally hand out the vanilla crest; they are now all intercepted and replaced with random rewards.

---

- **修复设置**  
- **Fixed settings**

修复了部分设置项修改后不生效、以及重启后丢失的问题。  
Some settings would not take effect or got lost on restart — fixed.

---

- **修复种子计算**  
- **Fixed seed calculation**

修复了种子计算流程的几处问题，手动输入种子与一键随机的结果都更可靠。  
Fixed several issues in the seed calculation flow; manually entered and auto-generated seeds are now more reliable.

- **修复怪物随机与陷阱随机中途失效**  
- **修复了怪物随机与陷阱随机在游戏中突然失效、变回原版行为的问题。**  
Enemy and trap randomization no longer switch back to vanilla halfway through a run.

---

- **修复特殊拾取物的对应效果**  
- **修复了地图机、门锁、钥匙孔等特殊拾取点拿到对应成果后，实际效果不生效的问题。**  
Special pickups such as map machines, door locks and key holes now actually apply their effect after being collected.

---

- **校正分裂能力的实际方向**  
- **校对了分裂能力各方向的发放，左右、上下不再发反，拿到的东西和描述一致。**  
Split abilities now grant the intended direction every time — left/right and up/down match their description.

---

- **设置统一写入全局配置文件**  
- **所有设置集中保存到同一个全局配置文件，重启游戏后保持上次的状态。**  
All settings are now saved into one unified config file, so your choices persist across restarts.

---

### v1.11.1

- **跳蚤救援点全面修复：原生跳蚤位现替换为普通拾取点，且跳蚤获得时附带正确音效与任务进度**  
- **Flea rescue points fully fixed: native flea positions are now replaced with regular pickups; collecting a flea now plays the correct sound and advances the rescue quest**

此前原生跳蚤（如骨林、珊瑚等区域的跳蚤救援）虽已替换为拾取点，但拾取时既无音效也无法推进跳蚤救援任务进度，甚至部分点位未能正确生成拾取点。现已全面修正：所有原生跳蚤救援位均改为可交互的拾取点，拾取时触发原生跳蚤救援音效，并正确增加 `SavedFleasCount` 及对应的 `SavedFlea_*` 持久化标记，跳蚤救援任务可正常完成。  
Native flea rescue points (e.g., in Boneforest, Coral, etc.) were previously replaced with pickups, but picking them up lacked sound and did not advance the flea rescue quest progress. Some positions also failed to spawn pickups correctly. This is now fully fixed: all native flea rescue spots are now interactive pickups that play the proper flea rescue sound, correctly increment `SavedFleasCount` and set the corresponding `SavedFlea_*` persistent flags, allowing the flea rescue quest to complete normally.

---

- **面具碎片与丝轴碎片重力异常修复**  
- **Fixed gravity abnormalities after picking up Mask Fragments and Spool Parts**

此前拾取面具碎片或丝轴碎片后，角色有时会残留错误的重力数值（表现为下落缓慢、漂浮或无法正常跳跃），现已修复：在碎片随机化替换的静默流程中，强制恢复默认重力并清除残留的输入锁与控制权锁定，确保角色物理状态完全正常。  
After picking up a Mask Fragment or Spool Part, the player could sometimes be left with incorrect gravity (slow falling, floating, or inability to jump). This is now fixed: the silent flow for fragment randomization now forcibly resets gravity and clears any lingering input blockers and control locks, ensuring the hero’s physics return to normal.

---

- **面具碎片四格动画卡死问题修复**  
- **Fixed soft‑lock during the four‑piece Mask Fragment acquisition animation**

当面具碎片集齐四格触发合成动画时，若该碎片来自随机池发放（而非原生拾取），动画可能卡在“Heart Container UI”的中间状态，导致角色无法操作。现已拦截并静默跳过该动画流程中的卡点动作（`Wait`、`AnimatorPlayStateWait`、`ListenForAnimationEvent` 等），并补发必要的完成事件，使合成动画能正常收尾，不再卡死。  
When the four‑piece mask assembly animation triggers from a fragment obtained through the random pool (rather than a native pickup), the animation could hang in the “Heart Container UI” middle state, leaving the player unable to act. The mod now intercepts and silently skips the blocking actions (`Wait`, `AnimatorPlayStateWait`, `ListenForAnimationEvent`, etc.) within that flow and fires the required completion events, allowing the assembly animation to finish cleanly without soft‑locking.

---

- **额外纹章槽位解锁演出卡死修复**  
- **Fixed soft‑lock when unlocking extra crest slots (ExtraToolSlotUIMsg)**

织女（Eva）场景中解锁额外纹章槽位（黄槽/蓝槽）时，官方全屏弹窗演出可能卡在“BG”图层无响应，导致画面停滞且无法操作。现已修复：额外槽位解锁时直接调用原生 `ExtraToolSlotUIMsg.Spawn`，同时补全必要的依赖资源（字体、动画、音效），并在演出结束后自动还原输入状态与暂停控制，不再出现卡死。  
When unlocking extra crest slots (yellow/blue) in the Eva scene, the native fullscreen popup (`ExtraToolSlotUIMsg`) could hang on the “BG” layer, freezing the screen and blocking input. This is now fixed: the unlock directly calls `ExtraToolSlotUIMsg.Spawn` with all necessary dependencies (fonts, animations, sounds) loaded, and properly restores input and pause state after the popup finishes, eliminating the soft‑lock.

---

- **劈方向权限（上/左/右劈）标反问题修复**  
- **Fixed reversed labels for slash direction permissions (up/left/right)**

此前“全随机模式”下，拾取“上劈权限”实际解锁的是下劈，“左力建议”解锁右劈，“右力建议”解锁左劈。现已修正：权限物品与解锁方向正确对应，拾取后实际生效的方向与物品描述一致。  
In “Full Random Mode”, picking up “Up Slash Permission” would actually unlock down slash, “Left Slash” unlocked right, and “Right Slash” unlocked left. This is now corrected: each permission item now correctly unlocks its intended direction, matching the item description.

---

- **权限类奖励读档后丢失的问题修复**  
- **Fixed permission rewards (heal, directional splits) being lost after loading a save**

移动方向权限（冲刺左/右、飞针左/右、漂浮左/右、壁跳左/右）及回血权限，在获得后若保存游戏并重新读档，之前解锁的权限会全部丢失。现已修复：读取存档时，根据 `ItemGivenCounts` 记录重新恢复所有已获得的权限状态，确保永久生效。  
Directional permissions (dash left/right, harpoon left/right, float left/right, wall jump left/right) and heal permission could be lost after saving and reloading – all previously unlocked permissions would revert. This is now fixed: on save load, the mod restores all unlocked permissions from the `ItemGivenCounts` records, ensuring they persist permanently.

---

- **部分奖励图标缺失或显示错误修复**  
- **Fixed missing or incorrect icons for several rewards**

商店中部分虚拟奖励（如随机念珠、随机蓝血）图标不显示或显示为空白；地图/车站奖励图标偶发错位；近期物品列表中某些图标（如“跳蚤救援”）未能正确加载。现已统一修正，所有奖励图标均从游戏资源中正确获取并显示。  
Some virtual shop rewards (e.g., random coin pouch, random blue health) had missing or blank icons; map/station reward icons occasionally displayed incorrectly; a few icons in the recent items list (e.g., “Flea Rescue”) failed to load. All icons are now correctly fetched from game resources and displayed consistently.

---

### v1.10.9

- **跳蚤加入随机池**（全游戏共30个，按顺序发放）  
- **Fleas added to the random pool** (30 in total, awarded sequentially)

- 念珠堆和跳蚤现在可以直接出现在任意场景，无需先经过原有区域  
- Coin piles and rescue fleas can now spawn directly in any scene without needing to visit their original areas first

- 跳蚤救援点已替换为普通拾取点；若拾取点的奖励映射到跳蚤，则该点直接显示为跳蚤  
- Flea rescue points have been replaced with regular pickups; if the mapped reward is a flea, the pickup appears as a flea instead

目前仅一种跳蚤和钱堆类型，后续会扩展更多念珠和跳蚤变体。  
Only one flea type and rosary type are implemented for now; more coin pile and flea variants will follow.

---

### v1.10.7

- **修复丝之心获得弹窗**  
- **Fixed Silk Heart acquisition popup**

此前丝之心获得时图标显示错误，且偶发不弹出；现已修复为正确显示官方 prompt_silkheart 图标，所有三颗丝之心均正常弹出官方全屏获得弹窗。  
Silk Heart popups previously showed the wrong icon and could fail to appear; they now correctly display the official prompt_silkheart icon, and all three Silk Hearts trigger the official fullscreen banner as intended.

- **修复面具碎片与丝轴碎片相关的一系列问题**  
- **Fixed a range of issues with Mask Fragments and Spool Parts**

- 碎片拾取后不再残留无敌状态或导致角色物理异常（漂浮、无惯性、下落缓慢）。  
- 碎片获得动画在随机模式下被替换时，现在会干净利落地跳过，不再卡半路。  
- 碎片计数与原生升级流程完全同步，获得后能正确推进面具合成与丝轴容量提升。  
- Fragment pickups no longer leave the player invincible or cause physics issues (floating, no momentum, slow falling).  
- Fragment acquisition animations are now skipped cleanly when replaced by randomization, instead of hanging midway.  
- Fragment counts stay fully in sync with the native upgrade flow, so mask assembly and spool capacity upgrades progress correctly.

---

- **新增永绽花（白花）与遥观仪（远视）弹窗，对应剧情点改为随机奖励**  
- **Added popups for White Flower (Everbloom) and Constructed Farsight (Spyglass); associated story points now give random rewards**

遥观仪（belltown_room_spare 收藏板合成展示品）和永绽花（memory_red 红色记忆剧情收集品）现已接入官方全屏获得弹窗。对应的原生剧情触发点已全面拦截，首次触发时改为发放随机奖励，同时阻断原生字段写入；已获得的存档不会重复触发。  
Constructed Farsight (the display piece assembled in belltown_room_spare) and the White Flower (the red memory story collectible) now trigger official fullscreen acquisition popups. Their native story trigger points are now fully intercepted: on first interaction they grant a random reward instead, blocking the vanilla field writes. Saves that already have them will not trigger again.

---

- **修复未开启全随机模式时游泳与抗寒能力被错误分裂的问题**  
- **Fixed swimming and cold resistance being incorrectly split when Full Random Mode is off**

此前即使全随机模式未开启，游泳权限与抗寒权限依然作为独立奖励出现在随机池中，导致玩家获得了这两个能力却无法下水或抵御寒冷。现已修正：这两项能力仅在开启全随机模式时才会作为分裂权限出现，非全随机模式下保持原生行为（有披风即抗寒，有游泳即入水）。  
Previously, even with Full Random Mode disabled, swimming and cold resistance permissions could still appear as separate rewards in the pool, causing players who drew them to be unable to swim or survive cold areas. This is now fixed: these split permissions only appear when Full Random Mode is enabled. In non‑full‑random runs, they retain vanilla behaviour (cold resistance with the cloak, swimming with the swimming ability).

---

- **花芯（Wild Bloom Core）加入随机奖励池**  
- **Wild Bloom Core added to the random reward pool**

花芯现作为有限奖励（全游戏共 1 个）纳入预生成映射池。抽到时触发生成演出并正常推进任务线，不再依赖固定位置拾取。  
The Wild Bloom Core is now a limited reward (1 per run) in the pre‑generated mapping pool. Drawing it triggers the spawn performance and advances the quest line as normal, no longer bound to a fixed pickup location.

---

- **尝试将所有钱堆替换为随机拾取点（实验性，可能存在较大问题）**  
- **Experimental: attempted to replace all coin piles with random pickups (may have issues)**

所有场景中的可打碎钱堆现尝试改为随机拾取点，触碰时触发随机奖励而非获得念珠。该功能目前为实验性实现，因钱堆的跨场景生成依赖模板场景（template scene），实际游玩中可能出现钱堆不生成、或只在特定场景生成的情况。此功能仍在调试中，建议谨慎使用并欢迎反馈。  
All breakable coin piles in every scene have been tentatively converted into random pickups – touching them now grants a random reward instead of coins. This is still experimental: because coin pile generation across scenes relies on template scenes, they may fail to appear in some areas or only spawn in specific scenes. Use with caution and feedback is welcome while this feature is being worked on.

### v1.10.6  
- 修复伊娃场景：2 个纹章升级、2 个额外槽位、1 个最终获得  
- Fixed the Eva scene: 2 crest upgrades, 2 extra crest slots and 1 final acquisition  

此前伊娃场景的这几个获取点没有做好拦截与随机触发，现已全部接管拦截并正常触发随机奖励。  
These pickup points in the Eva scene previously lacked proper interception and random triggering; they are now all intercepted and correctly trigger random rewards.

- 修复教堂在已获得纹章情况下门强制打开但无交互点的问题  
- Fixed chapel interaction point missing after the gate was force-opened with the crest already obtained  

已通过随机池提前获得纹章的玩家，此前进教堂时门虽被强制打开，神社却因原生"已解锁即关闭"判定而无交互点，演出无法触发；现已保持交互点可用，且演出结束后纹章替换演出与画面渐亮完整播放（玩家依然不会真正获得该纹章）。  
For players who obtained a crest early through the random pool, chapel gates were forced open but the shrine had no interaction point due to the native "already unlocked → shut down" check. The interaction point now stays available, and the full crest-replacement cutscene with screen fade-in plays afterwards (the player still does not actually keep the crest).

- 分裂出的能力数量配额提升为 2  
- Split ability count quota raised to 2  

技能物品与全部方向权限（冲刺/钩爪/滑翔/爬墙左右）的数量配额由 1 提升为 2，与其他能力的双倍发放一致。  
Skill items and all directional permissions (dash/harpoon/float/walljump left & right) are now split into 2 copies instead of 1, matching the double grant used by other abilities.

- 最近物品列表参考一代 RecentItemsDisplay mod 的样式做了调整  
- Recent Items list adjusted with reference to the HK1 RecentItemsDisplay style  

如样式存在争议，会改为自创样式。  
Should the styling be disputed, it will be replaced with an original design.

---

### v1.10.3  
- 最近物品列表全新改版  
- Reworked the Recent Items list  

右上角常驻显示最近获得的物品：图标 + 物品名 + 来源区域，样式与游戏本体一致。文字随游戏语言自动切换，任何物品都能正确显示。  
The top-right corner now keeps a live list of recently obtained items: icon, item name and the area it came from, matching the game's own look. Text follows your game language automatically, and every kind of item displays correctly.

- 列表会记住最近拿到的物品  
- The list remembers what you picked up  

保存最近 10 条记录；再次进游戏后拿到新物品时会连同历史一起显示。  
The last 10 entries are stored in your save; they reappear alongside new pickups when you play again.

- 设置中的开局能力限制此前不生效  
- Starting ability restrictions did not actually apply  

相关设置现在会正常阻止对应能力被发放。  
These settings now properly block the corresponding abilities from being granted.

---

### v1.10.1  
- 修复方向类权限左右颠倒  
- Fixed directional permissions being left/right inverted  

向左/向右类权限此前可能发成相反方向导致动作无法使用，受影响的存档重置种子即可恢复。  
Directional permissions could be granted for the opposite side, making the action unusable. Reset your seed to fix affected saves.

- 修复面具碎片、丝轴碎片、丝之心的获得弹窗  
- Fixed acquisition popups for Mask Fragments, Spool Parts and Silk Hearts  

- 回血限制覆盖所有回血途径  
- Heal restrictions now cover every heal source  

野兽纹章、巫妪绑定等特殊回血此前不受限制，现已全部纳入；椅子休息照常回满。  
Special heals (Beast crest fury, Witch bind) previously ignored the restriction; all heals are now gated. Bench rest still refills normally.

- 二段跳与抗寒拆分为两个奖励  
- Double jump and cold resistance split into two rewards  

原来拿到二段跳就能在寒冷区域存活，现在寒冷抗性是独立奖励。  
Owning double jump no longer grants survival in cold areas - cold resistance is now its own reward.

- 新增游泳奖励  
- New Swimming reward  

没抽到游泳时下水会受伤并回到检查点。  
Entering water without Swimming deals damage and sends you back to the last checkpoint.

- 修复漂浮途中转身可无限滑翔  
- Fixed infinite gliding by turning around mid-glide

- 读档后正确恢复各项权限  
- 读档后权限恢复正常  

- 移除 LimitSilkHeart 配置项，丝之心数量由丝线恢复上限设置控制  
- 移除 LimitSilkHeart 设置项；丝之心数量由丝线恢复上限设定控制  

### v1.9.9  
- 猎人日志纳入随机奖励池，获得时使用原生弹窗  
- 猎人日志加入随机奖励池，获取时通过原生弹窗提示  

halfway_01 努的剧情赠送（解锁图鉴系统）现在会被拦截并改为发放随机奖励：原生获得弹窗与 hasJournal 字段写入均被阻断，猎人日志本身作为有限奖励进入预生成映射池。抽到时正确写入 hasJournal 并弹出官方全屏获得弹窗（Set Journal 分支文案与 Journal_Prompt 官方图标自动填入），图鉴击杀记录从此生效。  
Nuu 的剧情赠送（位于 halfway_01，用于解锁图鉴系统）现已被拦截并转换为随机奖励发放：原生获得弹窗及 hasJournal 字段写入均被阻断，猎人日志本身作为有限奖励进入预生成映射池。抽中时正确设置 hasJournal 并弹出官方全屏提示（Set Journal 分支文本与 Journal_Prompt 官方图标自动填充），图鉴击杀记录自此生效。  

- 获取判定强制修正：未随机时保持可触发  
- 获取判定强制修正：未随机状态下仍可触发  

老存档已原生持有日志的情况下，努的对话原本会因 hasJournal=true 直接跳过赠送流程导致随机永不触发。现在获取判定被接管：随机未触发时强制走无日志分支（可反复对话触发），已触发后放行原生判定——已抽到的存档正常持有日志，未抽到则幂等拦截不重复发放。  
在旧存档已拥有猎人日志的情况下，Nuu 的对话曾因 hasJournal=true 直接跳过赠送流程，导致随机奖励无法触发。现获取判定被接管：未随机时强制进入无日志分支（可重复触发对话），已触发后恢复原生判定——已抽中的存档正常保留日志，未抽中的则通过幂等拦截避免重复发放。  

### v1.9.7  
- 丝之心纳入随机奖励池（共 3 颗）  
- 丝之心加入随机奖励池（共计 3 颗）  

三处丝之心梦境剧情点（钟之野兽、蕾丝塔、守卫）的过场演出现在会被统一拦截并改为发放随机奖励，原剧情仅触发一次且跨存档持久保存。丝之心本身作为有限奖励进入预生成映射池，抽到时正确提升丝线恢复上限（silkRegenMax + 1），与原生三颗丝之心的成就判定完全一致。  
钟之野兽、蕾丝塔、守卫三个剧情点中的丝之心梦境过场演出现已统一拦截并改为发放随机奖励，原剧情仅触发一次且跨存档持久保留。丝之心作为有限奖励进入预生成映射池，抽中时正确提升丝线恢复上限（silkRegenMax + 1），与原生三颗丝之心的成就判定完全一致。  

- 新增官方获得弹窗统一集合，全屏弹窗全面统一  
- 新增官方获得弹窗统一集合，全屏弹窗实现标准化  

StartingAbilityPicker 新增 OfficialMsgHelper 统一入口，收录三类官方原生弹窗：UI Msg Get Item 全屏黑幕获得弹窗（丝之心/能力/旋律）、额外工具槽解锁弹窗、纹章获得弹窗。物品随机化与技能随机化的所有弹窗调用已迁移至该集合，消除重复实现；弹窗文字一律由游戏语言表自动填入，图标使用官方素材。  
新增 OfficialMsgHelper 统一入口（位于 StartingAbilityPicker），涵盖三类官方原生弹窗：UI Msg Get Item 全屏黑幕提示（丝之心/能力/旋律）、额外工具槽解锁弹窗、纹章获得弹窗。所有物品与技能随机化中的弹窗调用均已迁移至该集合，消除重复代码；弹窗文本均从游戏语言表自动填充，图标采用官方素材。  

- 修复丝之心获得弹窗不显示与图标错误  
- 修复丝之心获得弹窗未显示及图标错误  

丝之心分支此前未接入官方弹窗映射导致回落为小横幅，现已正确映射到官方 Set Silk Heart 分支（"已汲取，恢复甲壳内灵丝再生的能力"文案自动填入），并将图标修正为官方 prompt_silkheart 图标。  
丝之心分支此前未接入官方弹窗映射，导致显示为小横幅，现已正确映射至官方 "Set Silk Heart" 分支（文本“已汲取，恢复甲壳内灵丝再生的能力”自动填充），图标更正为官方 prompt_silkheart 图标。  

### v1.9.5  
- 额外纹章槽位纳入随机范围，获得时使用原生弹窗  
- 额外纹章槽位加入随机奖励池，获取时通过原生弹窗提示  

额外槽位（UnlockCrestSlot）现在作为有限奖励进入预生成映射池。抽到时触发原生解锁演出弹窗，并阻断原生字段写入，避免重复获得。  
额外槽位（UnlockCrestSlot）现已作为有限奖励进入预生成映射池。抽中时触发原生解锁演出弹窗，并阻止原生字段写入，防止重复获取。  

- 修复面具碎片与丝轴碎片的收集 Bug，销毁状态持久保存  
- 修复面具碎片与丝轴碎片收集问题，销毁状态持久保存  

根治了收集碎片后角色物理异常（悬浮、无惯性、下落缓慢）：静默流程结束后正确终止收集点上卡死的残留演出 FSM，控制权、输入锁与重力全部恢复正常。同时场景级销毁标记写入存档持久保存，重进场景后已收集的世界点不再重生。  
修复了收集碎片后角色出现的物理异常（悬浮、无惯性、下落缓慢）：静默流程结束后正确终止收集点上残留的演出 FSM，控制权、输入锁定与重力均恢复正常。同时将场景级销毁标记写入存档，重进场景后已收集的世界点不再重新生成。  

- 修复所有技能触发点的持久保存与拦截  
- 修复所有技能触发点的持久化与拦截机制  

全部技能获得点（含 UI 弹窗类演出）现在统一走拦截与持久化标记：首次触发即发放随机奖励并永久关闭，跨会话生效，不再出现重复触发或漏拦。  
所有技能获取点（包括 UI 弹窗类演出）现已统一采用拦截与持久化机制：首次触发即发放随机奖励并永久关闭，效果跨会话持续，不再出现重复触发或遗漏拦截。  

- 初步加入房间随机与物品随机的种子可行性计算  
- 初步实现房间与物品随机化的种子可行性评估  

生成种子时会进行可达性与条件模拟以剔除死种。由于信息收集与游戏机制的限制，目前尚无法完全保证活种判定准确（可能误判活种或漏过死种），后续版本会持续完善。  
生成种子时将执行可达性与条件模拟以过滤无效种子。受限于信息获取能力及游戏机制，当前活种判断尚未完全准确（可能出现误判活种或遗漏死种），后续版本将持续优化。  

### v1.9.4  
- 技能神龛持久化：已触发即永久关闭  
- 技能神龛实现持久化：首次触发后永久禁用  

技能神龛（织女、深渊等 11 处）现在和教堂纹章一样，首次触发随机奖励后即标记为已使用，再次进入同一场景时不可交互，避免重复触发。重置种子或重置存档可恢复所有神龛。  
技能神龛（织女、深渊等 11 处）现与教堂纹章行为一致：首次触发随机奖励后即标记为已使用，再次进入同一场景时无法交互，避免重复触发。重置种子或重置存档可恢复所有神龛。  

- 初步引入逻辑判定，可能存在误判  
- 初步加入逻辑判断，可能产生误判  

生成种子时新增简单模拟流程，检查所有区域是否可达、点位条件是否满足。无法通过的种子会被自动跳过并重新生成。此功能尚在早期，可能会有活种误判或死种漏网，后续会持续优化。  
生成种子时新增简易模拟流程，验证各区域是否可达及关键点位条件是否满足。不符合要求的种子将被自动跳过并重新生成。该功能仍处于初期阶段，可能存在活种误判或死种遗漏，后续将持续改进。  

- 修复 Slab 区三把钥匙无法正常获得的问题  
- 修复 Slab 区三把钥匙无法正常获取的问题  

此前 Slab 区三把钥匙（怠惰之钥、异端之钥、叛教之钥）只设了开门标记，没有弹窗和背包反馈。现在发放钥匙时会同时触发原生物品获得弹窗，并正确设置开门标记，钥匙获得和开门功能均恢复正常。  
此前 Slab 区三把钥匙（怠惰之钥、异端之钥、叛教之钥）仅设置了开门标记，未提供弹窗或背包反馈。现发放钥匙时将同时触发原生物品获取弹窗，并正确设置开门标记，钥匙获取与开门功能均已恢复正常。

三个板状钥匙（懒惰之钥、异端之钥、背教之钥）此前仅设置解锁标志，而不会显示弹窗或物品栏反馈。现在它们会触发原生的“获得物品”弹窗，并正确设置解锁标志——弹窗与门解锁行为均已按预期正常工作。

---

### v1.9.3  
修复面具丝轴判定问题  
修正面具碎片与丝轴部件的拦截处理

---

### v1.9.2  
---重大更新---

#### 1. 全面校正所有获得弹窗（纹章/法术/能力），并将触发点改造为随机奖励  
**1. Fully revamped all acquisition popups (crests, spells/skills) and converted all trigger points into random rewards.**

- **纹章获得弹窗**：当从随机奖励池中抽到纹章时，现在会正确弹出原生“获得纹章”弹窗（`ToolCrestUIMsg`），弹窗结束后自动装配至当前纹章槽，演出流程与神社获取完全一致（此前纹章仅静默解锁，无任何反馈）。  
  **Crest acquisition popup**: 当从随机奖励池中抽到纹章时，现在会正确弹出原生“获得纹章”弹窗（`ToolCrestUIMsg`），弹窗结束后自动装配至当前纹章槽，演出流程与神社获取完全一致（此前纹章仅静默解锁，无任何反馈）。

- **能力（法术）获得弹窗**：所有能力（如疾风步、飞针、灵丝风暴等）获得时统一使用原生能力弹窗（`SkillGetMsg` / `PowerUpGetMsg`），完整显示图标、名称与描述，不再回退到自定义提示。  
  **Ability (spell) acquisition popup**: 所有能力（e.g, Swift Step, Harpoon Dash, Thread Storm）获得时均使用原生能力弹窗（`SkillGetMsg` / `PowerUpGetMsg`），完整显示图标、名称与描述，不再回退至自定义提示。

- **触发点平替（纹章教堂 & 技能地区）**：  
  - 教堂纹章神社演出（6 座）已全面拦截原生纹章解锁与弹窗，改为触发一次随机奖励；教堂门开关由模组直接接管，已平替的教堂门永久关闭，未平替的始终开启，不再受原生逻辑干扰。  
  - 技能获得地区演出（11 处，如织女、深渊、摇篮等）同样拦截原生技能获取，改为随机奖励，同时阻断自动装配与字段写入，避免重复获得。  
  **Trigger point replacement (crest chapels & skill regions)**:  
  - 六座教堂神社演出现已全面拦截——原生纹章解锁与弹窗被替换为单次随机奖励。教堂门状态由模组直接管理：已平替的教堂门永久关闭，未平替的保持开启，不再受原生逻辑影响。  
  - 技能获取区域演出（11 地点，e.g, 织女神社、深渊、摇篮）同样被拦截：不再授予技能，而是发放随机奖励，并阻断自动装配与字段写入，以防止重复获得。

- **丝轴碎片/面具碎片世界触发点**：  
  现已完全接管 `PrefabCollectable` 世界触发点（如“丝之歌丝轴”）。拾取时不再增加碎片计数，而是从预生成映射顺序中依次发放奖励；原生 UI 动画流程被静默跳过且不卡死（角色无敌、输入锁等全部恢复正常）。  
  **World trigger points for Spool Parts / Mask Fragments**:  
  `PrefabCollectable` 世界触发点（e.g, “丝之歌丝轴”）现已完全接管。拾取时不再增加碎片计数，而是从预生成映射顺序中依次发放奖励；原生 UI 动画被静默跳过且不卡死（角色无敌、输入锁等均恢复正常）。

---

#### 2. 部分系统调整与修复  
**2. Partial system adjustments and fixes**

- **灵丝获得/消耗随机化** 改为默认关闭，与原生行为一致，需在辅助设置中手动开启。  
  **Silk gain/spend randomization**: 现默认关闭，与原生行为一致，玩家需在辅助设置中手动开启。

- **修复多个卡死/无敌问题**：出梦境后角色卡住无法操作、丝轴道具后无敌残留、黑寡妇梦境卡死等均已修复。  
  **Fixed multiple soft-lock/invincibility issues**: 离开梦境后角色卡死、使用丝轴道具导致的永久无敌、黑寡妇梦境卡死等问题均已解决。

- **配置面板同步**：修复了面板开关与实际运行时状态不一致的问题（物品随机、纹章随机、怪物随机等），现在面板显示与真实生效状态完全对齐。  
  **Config panel sync**: 修复了面板开关与实际运行状态不一致的问题（物品随机、纹章随机、怪物随机等），当前面板显示已与实际运行状态完全同步。

---

#### 3. 确认：所有法术（能力）和纹章均已纳入预生成映射表（PreGeneratedMap）  
**3. Confirmation: All spells (abilities) and crests are now included in the pre-generated mapping (PreGeneratedMap).** `ItemRandomizer.BuildRewardPools()` 中，所有能力虚拟奖励（`hasDash`, `hasNeedleThrow` 等）和纹章（`ToolCrest` 类型）均已加入有限奖励池。  
- `PreGeneratedMap.BuildAllMappings()` 遍历该池，按次数（能力默认 2 次，纹章 1 次）填充所有映射键（拾取点、Lore、商店、车站、顺序碎片等）。  
- 因此，**所有能力和纹章**都作为固定奖励实体写入 `global_save.json` 的 `PreGeneratedMappings` 字段，同一种子下每局游戏的分配完全可预测且固定。  
  （在 `ItemRandomizer.BuildRewardPools()` 中，所有能力虚拟奖励（`hasDash`, `hasNeedleThrow` 等）和纹章（`ToolCrest` 类型）均已加入有限奖励池。  
  `PreGeneratedMap.BuildAllMappings()` 遍历该池，并根据其限制次数（能力默认 2 次，纹章 1 次），分配至所有映射键（拾取点、Lore、商店、车站、顺序碎片等）。  
  因此，**所有能力和纹章**均作为确定性奖励实体写入 `PreGeneratedMappings` 字段中的 `global_save.json`，确保在相同种子下每局游戏的分配完全可预测且固定。）

## v1.9.1

### - 修复：丝轴等道具导致的无敌问题

修复了获得丝轴碎片等道具后角色会卡在无敌状态的问题。

Fixed: an issue where obtaining spool parts and similar items could leave the player stuck invincible.

---

### - 修复：游戏声音消失

修复了跳过开场教学后游戏完全没有声音的问题。

Fixed: the game going completely silent after skipping the opening tutorial.

---

### - 调整：移除没有用的检查点随机和对应权限

移除了大部分检查点的随机奖励和对应权限，只保留车站收费机和日志石碑。被移除的种类包括：门锁（监狱锁等）、地图机、织女雕像、通告板、钥匙孔、休息点、银行门、罐子、普通交互点。这些地方恢复原版行为，交互即用，不再触发随机奖励，也不会因为没有权限被卡住。

Changed: removed the pointless checkpoint randomization and its permissions. Random rewards and unlock permissions are now kept only for station toll machines and lore tablets. Removed: door locks (prison locks etc.), map machines, weaver shrines, quest boards, key receptacles, rest spots, bank doors, pots, and ordinary inspect points. These places now behave as in vanilla — interact and use directly, no random reward, nothing blocked.

---

### - 修复：货币随机数量异常

修复了随机念珠金额异常的问题。此前本该在 0 到 90 之间均匀分布，实际几乎只拿到个位数金额；现在金额均匀分布。

Fixed: abnormal random bead amounts. They were supposed to spread evenly between 0 and 90 but almost always gave single digits. Amounts now spread evenly.

## v1.8.10

### - 新增：开局种子自动生成

进入设置页面时，种子输入框会自动填入一个经过验证、可以正常开局的随机种子，无需再手动点击"开始随机"。种子生成并验证完成之前，"确认并开始游戏"、经典和钢魂按钮会暂时不可用，避免用尚未确定的种子开局。想自己指定种子的话，直接在输入框里输入即可，自动生成会立即停止，以你输入的种子为准。

新功能：当打开设置页面时，将自动生成一个经过验证的可玩种子，因此无需手动点击“开始随机”。在种子生成和验证期间，确认/经典/钢制启动按钮将暂时不可用，以防止使用未确认的种子启动游戏。如果你想设定自己的种子，只需将其输入到输入框中——生成过程立即停止，并生效你的种子。

---

### - 调整：开始新游戏后自动跳过全部开场动画和教学流程

无论选择随机模式、经典模式还是钢魂模式，开始新游戏后都会自动跳过开场动画和开头教学部分，直接进入第一幕开始游玩，全程无需手动操作。这一调整只影响新开局的跳过过程，游戏过程中的其他剧情动画不受影响。

更改：开始新游戏时，现在会自动跳过所有模式（随机、经典和钢铁）的整个开场动画以及启动教程，直接进入第 1 关卡，无需手动操作。仅影响新游戏介绍画面；游戏中其他剧情动画保持不变。

---

### - 修复：配置面板开关与实际生效状态不一致

此前配置面板初始化时会把部分随机选项（如技能类物品随机、遗物随机）悄悄改回关闭，导致用默认设置直接进入游戏后，捡到工具遗物、念珠串等原生拾取点时直接获得原物，而不是随机奖励。现在面板上的开关状态与模组实际运行状态完全一致，不再出现"面板显示和实际效果对不上"的情况。

修复：配置面板的切换不再与实际生效的状态出现偏差。此前，在初始化过程中，面板可能会静默地关闭某些随机化选项（技能物品随机、圣物随机），导致默认设置下的原生拾取项（如工具圣物和念珠）获得原始物品而非随机奖励。现在，面板的开关状态与运行时的实际状态始终一致。

---

### - 调整：灵丝获得/消耗随机默认关闭

灵丝获得/消耗随机化（获得或消耗灵丝时数量随机变动）现改为默认关闭，与原版行为一致。想体验该功能的玩家，可在开始游戏前的辅助设置页面中打开"丝获得随机"开关。

更改：丝线获取/消耗的随机化现在默认关闭，与原版行为一致。希望启用该功能的玩家可以在开始游戏前，在辅助设置页面开启“丝线获取随机”开关。

---

### - 修复：黑寡妇梦境不再卡死

在部分剧情（如黑寡妇处）获得梦境回忆后，捡起梦境记忆球时游戏可能永远卡住：回忆动画不播放、无法离开梦境、后续剧情全部中断。现在记忆球的交互、回忆动画、离开梦境和之后的剧情都能正常推进。

修复：从某些剧情事件中获得的梦境（e.g. 女寡妇）不再卡住。此前，拾取记忆水晶后，梦境会永久滞留——没有任何动画，也无法退出，后续剧情也永远不会继续。现在，与水晶互动、观看记忆动画、离开梦境以及后续剧情均能正常进行。

---

### - 修复：自己发放的苔莓不再被再次随机

从奖励池中抽到苔莓浆果时，现在可以正常拿到浆果（计数增加并弹出提示）。此前这种由模组自己发放的苔莓会被误当成新的随机触发点，再次随机成其他物品，导致拿到手的东西和奖励不符。

修复：由奖励池自身发放的苔藓果不再进行随机重置。此前，模组发放的苔藓果可能被误认为是新的触发点，并被随机替换为其他物品，导致你实际获得的奖励与你抽到的内容不一致。现在，这类苔藓果的行为已与普通拾取物相同。

---

### - 修复：面具碎片、丝轴碎片不再卡升级

商店和奖励池中抽到面具碎片、丝轴碎片时，现在会走完整的原生获取流程：碎片计数正确增加，面具合成、丝轴上限提升等升级步骤都能照常推进。此前这些碎片的计数和原生合成流程脱节，可能出现拿到了碎片但升级迟迟不触发、计数错乱的情况。

修复：从商店或奖励池获得的面罩碎片和丝线部件现在使用完整的原生获取流程。碎片数量正确增加，相关的升级（面罩组装、丝线卷轴盖提升）也正常进行。此前碎片数量可能与原生升级流程不同步，导致升级被卡住或计数错误。

---

### - 修复：碎片获得动画在随机模式下不再卡死

丝之歌碎片、面具碎片的原生获取演出（获取动画、UI 弹窗、合成特效等）在随机模式下被替换时，现在会干净利落地跳过而不是卡在半路：不播放残缺动画、不虚增碎片计数和上限，所有相关流程都能正常走完。此前的随机替换偶发导致这类动画流程卡住，现已在各个触发渠道（世界触发点、拾取、商店发放等）彻底解决。

修复：当碎片被随机化替换时，原生的碎片获取动画不再卡顿。spool/掩码碎片获取的相关性能（获取动画、UI弹窗、组装效果）现在能够干净地跳过，而不会中途停止——没有半播放的动画，没有虚幻的碎片数量或上限，所有相关流程均能正常完成。此问题已在所有触发通道中得到解决（世界触发、拾取物品、商店奖励等）。

---

## v1.8.9

### - 新增：对随机种子进行了初步的逻辑处理

现在随机化不再只是把物品随机打散，而是加入了一层逻辑判断：会根据区域之间的互通条件（哪些区域需要什么能力才能进入）做可达性验证。点"开始随机"时，会先生成随机种子，用这套逻辑判断物品分配后所有区域是否都能走通，分配不可行的种子会自动跳过重新生成，直到找到逻辑上可行的种子才应用。判断依据的是游戏实际生成的物品分配。

新功能：随机化现在包含基本的逻辑处理。不再只是简单地打散物品，而是加入了一层可达性验证，基于区域间的互通条件（每个区域需要哪些能力才能进入）。点击“开始随机”后，系统会先生成一个随机种子，并根据游戏实际生成的物品分配情况检查所有区域是否都能到达；若某种子导致区域无法通行，则自动跳过并重新生成，直到找到逻辑上可行的种子才生效。

---

### - 新增：种子未确定时不能开始游戏

点"开始随机"后、逻辑验证完成前，点击"确认并开始游戏"、经典或钢魂模式都不会开始游戏，避免使用未经逻辑验证的种子开局。

新功能：在种子尚未确定之前，无法启动游戏。点击“开始随机”后，在逻辑验证完成前，“确认并开始游戏”、“经典模式”或“钢魂模式”按钮将被禁用，以防止使用未经过逻辑校验的种子进行游戏。

---

### - 新增：经典和钢魂模式关闭全部随机化

经典和钢魂模式现在会关闭所有随机化功能（物品、纹章、技能、遗物、陷阱、房间、敌人、全随机、纹章诅咒等），按原版流程进行。随机化设置只在随机模式下生效。

新功能：经典模式与钢魂模式现已禁用全部随机化功能（包括物品、纹章、技能、遗物、陷阱、房间、敌人、全随机、纹章诅咒等），并按照原版流程运行。随机化配置仅在“随机模式”中生效。

---

### - 调整：上劈、左劈、右劈默认开启且始终可调整

上劈、左劈、右劈三个方向在菜单中默认是开，且无论是否开启全随机模式都可以自行调整。此前非全随机模式下这三个按钮会被锁定。

变更：上劈、左劈、右劈三个方向在菜单中的默认状态为“开启”，并且无论是否启用全随机模式，均可随时手动调整。此前在非全随机模式下，这三个操作键会处于锁定状态。

---

### - 调整：能力类物品数量翻倍

回血、上劈、左劈、右劈以及各技能能力的发放数量翻倍，每个能力会出现在两处检查点。

变更：所有能力类物品（包括回血、上劈、左劈、右劈及各类技能）的数量已翻倍。每种能力将出现在两个检查点中。

## v1.8.8

### - 修复：获得技能或能力后左上角状态栏不再消失

此前获得丝之矛等技能或能力后，左上角的血条等状态栏会一直消失，无法恢复。现在获得技能或能力后，状态栏会正常恢复显示；获得的是技能时，技能会直接出现在对应的法术槽中。

Fixed: the HUD (health bar and the rest of the top-left status) no longer stays hidden after gaining a skill such as Silk Spear or an ability. The HUD returns to normal once the get-prompt ends, and skills are placed into their matching spell slot automatically.

---

### - 修复：离开梦境后不再卡在无法操作、无法起身的状态

此前从任何梦境离开并回到主世界后，角色有时会卡住——站着但无法移动、无法操作，也起不来。现在离开梦境后会自行恢复，角色可以正常移动和操作，不再需要重开游戏或读档。

Fixed: after leaving any dream and returning to the main world, the character no longer gets stuck standing but unable to move or act. The character now recovers on its own after a dream, so you can move and play normally again without reloading.

---

### - 修复：丝轴碎片和面具碎片拾取后不再不发放任何东西

现在拾取丝轴碎片或面具碎片会正常触发随机奖励，不会再出现捡了碎片却什么奖励都没有的情况。此前触发奖励偶尔会落空，本次已解决。

Fixed: picking up a spool part or a mask fragment now properly triggers a random reward. They no longer occasionally grant nothing when collected.

---

### - 修复：进入梦境后当前纹章不再被换掉

此前一进入梦境，当前装备的纹章会被悄悄替换成另一个随机纹章。现在进入梦境后纹章保持原样，不会在进出梦境时被意外换掉。

Fixed: the equipped crest is no longer silently swapped for a random one when entering a dream. Your crest now stays as it was through dream entry and exit.

---

### - 修正：纹章随机替换现在只在教堂场景发生

此前纹章替换可能在任何场景（包括剧情和梦境）发生。现在只在教堂场景里才会替换纹章，其他地方都不会受到影响。

Fixed: crest randomization now only happens in chapel scenes. It no longer occurs in story or dream scenes or anywhere else outside chapels.

## v1.8.7

### - 新增：面具碎片、丝轴碎片、苔莓全部纳入随机奖励

现在游戏中的面具碎片、丝轴碎片和苔莓均已改为随机奖励触发点。触碰这些碎片或苔莓时，不再直接获得原物，而是从奖励池中随机抽取一件物品或能力。面具碎片共有 20 处、丝轴碎片共有 18 处、苔莓共有 6 处，这些触发点已预先生成对应的奖励，并写入了映射表。游玩过程中会按先后顺序依次发放：第一个拾取的点获得该点预定的奖励，第二个拾取的点获得下一个预定的奖励，不会重复、也不会遗漏。

新增：面具碎片、丝轴碎片和苔莓均改为随机奖励触发点。触碰这些碎片或苔莓时，不再直接获得原物，而是从奖励池中抽取一件随机物品或能力。面具碎片共有 20 处、丝轴碎片共有 18 处、苔莓共有 6 处，这些触发点已预先生成对应的奖励，并写入了映射表。游玩过程中会按先后顺序依次发放：第一个拾取的点获得该点预定的奖励，第二个拾取的点获得下一个预定的奖励，不会重复、也不会遗漏。

苔莓拾取后还会记录所在区域，之后该区域不再长出苔莓，与已收集的效果相同。

新增：苔莓被拾取后会记录其所在区域，此后该区域不会再生成苔莓，效果等同于已被采集。

目前大部分类型的随机化已经实现，但部分触发点的具体位置尚未最终确认，实际游玩中若发现异常，欢迎反馈。

新增：大多数拾取类型已实现随机化，但部分触发点的精确位置仍待最终确定。在实际游玩过程中如发现异常情况，欢迎提供反馈。

## v1.8.6

### - 新增：石板区三把钥匙加入随机池---五圣钟统一解锁，最终审判挑战直接可进

石板区（监狱）的三把钥匙——怠惰之钥、异端之钥、叛教之钥——现在作为随机奖励进入奖励池。此前它们在石板区的固定位置拾取，现在改为从奖励池中抽取，每局各可获得一次。三把钥匙集齐后，用以开启最终审判的大门。

新增：三个石板钥匙（懒惰之钥、异端之钥、背教之钥）现已成为随机池中的奖励。它们过去只能在石板区域的固定位置拾取；现在，每个钥匙将在每局游戏中通过随机池获得一次。收集全部三把钥匙将开启最终审判之门。

游戏中的五座圣钟分布在五个区域。由于房间随机化后位置难以逐个寻找，新增一条统一的"圣钟解锁"奖励：抽到它即同时点亮五座圣钟，最终审判挑战可以直接进入，不再需要逐地寻找。

新增：已添加五个铃铛圣龛的单次解锁功能。绘制该物品将同时点亮全部五个圣龛，从而可直接进入最终试炼挑战，无需在打乱房间中逐一寻找每个圣龛。

---

---

### - 修正：奖励图标全面校正与补全，包括模式图标

- 方向权限类奖励（冲刺、针、漂浮、壁跳、上劈、左右劈、回血等）现在显示对应的技能图标，不再共用同一张图。
- 无图标类的奖励全部补齐图标：灵丝、甲壳、蓝血、货币、心、线轴、最大灵丝、纹章插槽等均有独立图标。
- 石碑类（地方志）按类型区分图标：蘑菇石碑、收费机/长椅、地图机、门锁/钥匙机等各配对应图标，不再统一显示。
- 车站、地图类奖励使用车站与地图图标。
- 修复了部分图标在近期物品栏中的颠倒或错位。
- 商店中的权限类图标不再遮挡画面，尺寸已缩小。

新增：奖励图标现已完全修正并完善。在模式下，icon.Direction-权限类奖励会显示与其对应技能的图标；所有 null-图标虚拟奖励（丝线、贝壳碎片、蓝色血液、升级物品、纹章槽位等）均已拥有正确的图标；剧情奖励将根据其交互类型匹配相应的图标（地图机器、收费长椅、平板、门锁）。站点/地图类奖励也使用了正确的图标。部分在最近项目栏中出现翻转或偏移的图标已修复，商店中的权限图标不再遮挡视野。

---


### - 修复：启动与日志报错不再刷屏

- 修复了启动时反复出现的若干异常日志。
- 修复商店相关报错：购买物品时不再产生"必须用 ScriptableObject.CreateInstance 实例化"的警告；商店购买"权限"类物品时名称与描述不再为空。
- 修复"物品状态低于 0"、石碑/铭文状态读取报错：改为直接读取真实状态名，不再触碰游戏原生的报错路径。
- 阻止第三方模组 FsmMaster 每帧访问不存在的 Game Manager 导致的日志刷屏。
- 日志中不再出现 GameManager.instance 空访问调用栈。

新增：清理了各种启动和运行时错误日志的冗余信息——商店购买警告、“物品状态小于”等消息。0错误，EnemyRando配置写入错误，以及FsmMaster“未找到游戏管理器”的大量错误均已修复。

---



---

## v1.8.5

### - 新增：上锁的门、收费机等世界设施纳入随机奖励

此前游戏中的部分重要设施（上锁的门、收费机、地图机、任务板、神龛等）没有进入随机奖励体系。现在它们全部纳入：每个设施第一次使用时，会额外触发一次随机奖励；此后该设施暂时无法使用，需要从奖励池中抽到对应这一地点的"使用许可"，才能恢复原版功能。同一个地点只会触发一次随机奖励，获得许可后永久恢复正常使用。

新增：上锁的门、收费机和其它世界设施现已纳入随机奖励系统。每个设施在首次使用时，将额外触发一次随机奖励；此后该设施将暂时无法使用，需从奖励池中抽到对应位置的“使用许可”，才能恢复原版功能。同一地点仅触发一次随机奖励，获得许可后永久恢复正常使用。

---

### - 修正：地方志（石碑）内容全面校对

游戏中的地方志（石碑、铭文）文本已全部逐条校对，共 50 余篇，作为奖励加入随机池。阅读石碑时不再显示该石碑原文，而是触发一次随机奖励；从随机池中抽到"地方志"奖励时，会以游戏原生对话框弹出其中一篇，文字与原版一致，无乱码、无缺漏。

修正：所有地方志（石碑、铭文）文本均已全面校对——共 50+ 条内容已加入随机池作为奖励。阅读石碑时不再显示原文，而是触发一次随机奖励；当从奖励池中抽到“地方志”奖励时，将通过游戏原生对话框弹出其中一篇，文字与原版完全一致，无乱码、无缺漏。

## v1.8.4

### - 新增：一键随机种子（开始随机按钮）

主页面种子输入框下方新增“开始随机”按钮。点击后会自动生成一个随机种子填入输入框，并同步应用到技能随机和物品随机模块，立即生效。不想手动填种子的玩家，可以直接用这个按钮开一局完全随机的游戏，再配合“确认并开始游戏”即可开始。

New: a "Start Random" button has been added below the seed input field on the main page. One click generates a random seed, fills it into the input field, and applies it to both the skill randomizer and item randomizer modules instantly. Players who do not want to type a seed manually can simply use this button to start a fully random run.

---

### - 修复：部分详情页"返回主页面"按钮失效

- 修复了部分详情页（如开局、技能、纹章、完成度、房间、陷阱、怪物、物品等页面）的“返回主页面”按钮点击无反应的问题。此前这些按钮缺少页面跳转的逻辑绑定，现已统一修复，所有详情页的返回按钮都能正常回到主页面。
- 同时将各详情页的“返回主页面”按钮整体上移，避免与页面底部区域重叠遮挡。

- Fixed the "Back to Main Page" buttons on several details pages (startup, skills, crests, completion, room, traps, enemies, items, etc.) having no response when clicked. These buttons previously lacked the page-switching logic binding; they are now unified so every details page returns to the main page correctly.
- The "Back to Main Page" buttons on all details pages have also been moved upward to avoid overlapping with elements near the bottom of the page.

---

### - 修复：经典模式/钢魂模式按钮未启动对应模式

- 修复了模式选择页“经典模式”和“钢魂模式”按钮点击后不开始游戏的问题。此前这两个按钮缺少开始游戏的调用逻辑接口，点击无任何反应。现已补齐：经典模式以无永久死亡模式开始新游戏，钢魂模式以永久死亡模式开始新游戏，与游戏原版模式一一对应。

- Fixed the "Classic Mode" and "Steel Mode" buttons on the mode selection page not starting the game. These two buttons previously lacked the game-starting logic call and did nothing when clicked. The call interfaces are now restored: Classic Mode starts a new game without permadeath, and Steel Mode starts a new game with permadeath, matching the vanilla game modes.

---

### - 修复：配置面板初始开关状态与实际默认值不一致

- 修复了配置面板首次打开时部分开关显示状态与模组实际默认值不一致的问题。此前“怪物随机”“陷阱移动”“回血权限”等开关在面板中默认显示为关闭，而实际代码默认值为开启；“上劈权限”则相反。现已全部对齐，面板首次打开即显示与代码一致的真实状态，不再误导玩家。

- Fixed the config panel showing toggle states that did not match the mod's actual default values on first open. Previously toggles like "Enemy Random", "Trap Movement" and "Heal Permission" displayed as OFF while the code defaults were ON, and "Up Slash Permission" was the other way around. All states are now aligned, so the panel shows the true state on first open and no longer misleads players.

---

### - 同步代码内部默认字段与外部配置

- 将代码内部的默认字段值与外部 UI 配置对齐：物品随机内部默认状态修正为默认开启，陷阱随机保持默认关闭。现在首次启动模组时，代码默认值、外部配置面板显示状态与运行时的实际开关完全一致。
- 说明：物品随机在权威配置中本就默认为开启，本次将共享内存中的默认字段同步为开启，避免出现“面板显示开启而内部逻辑默认关闭”之类的偏差；陷阱随机关闭状态无改动。

- Synced the internal default fields to match the external configuration: item randomization internal default is now ON, while trap randomization stays OFF. On first launch, the code defaults, the config panel display, and the actual runtime logic are all consistent.
- Note: item randomization was already ON by default in the authoritative config; this update only syncs the shared-memory default field to ON as well, eliminating any mismatch between what the panel shows and what the code defaults to. Trap randomization stays OFF as before.

## v1.8.2

### - 新增：强制所有商人显示

现在，所有区域的沙克拉（Mapper）商人都被强制保留在现场，不再因为剧情推进或游戏逻辑而离开。只要你进入对应区域，商人们就会一直站在原位，可以正常对话、正常购买物品，不会再出现“到了地方却发现商人已经走了”的情况。

新功能：所有沙克拉（Mapper）商人现在都被强制保留于原地。无论剧情如何发展，他们始终位于初始位置，并可正常进行对话与购物，不再出现进入区域后发现商人已离开的情况。

此改动仅影响商人的“是否在场”，不改变对话内容、商店货物和任务流程。商人在场时仍遵循原版逻辑正常服务，其余随机机制（物品随机、商店随货、车站、地图等）完全不受影响。

---

### - 修复：能力分裂导致技能无法使用

- 修复了开启能力/方向权限分裂后，拾取技能物品却无法获得对应技能的问题。现在分裂模式下，拾取到技能会正确解锁对应方向并可正常使用，不再出现“获得了却没效果”的情况。
- 相关方向权限物品（冲刺左/右、飞针左/右、漂浮左/右、壁跳左/右、上劈/左劈/右劈/回血权限）在分裂模式下仍会正常出现，解锁后即时生效。
- 修复了开启能力/方向权限分裂后，拾取技能物品却无法获得对应技能的问题。现在分裂模式下，拾取到技能会正确解锁对应方向并可正常使用，不再出现“获得了却没效果”的情况。
- 方向权限物品（冲刺左/右、飞针左/右、漂浮左/右、壁跳左/右、上劈/左劈/右劈/回血权限）在分裂模式下仍会正常出现，解锁后即时生效。

---

### 修复：长凳翻译相关问题

- 修复了部分长凳名称无法正确翻译/不显示的问题（Benchwarp 传送菜单）。现在长凳名称会根据当前语言环境正确显示中文或英文，传送菜单刷新即时同步翻译。
- 修复了部分长凳名称在 Benchwarp 传送菜单中无法正确显示或未被翻译的问题。现在长凳名称将根据当前语言环境准确显示为中文或英文，并在菜单刷新后立即同步更新。

## v1.8.1

### - 所有检查点统一映射管理：商店、日志、车站、拾取点全纳入一张表

现在，商店、日志石碑、地图购买、车站激活和拾取点不再分散处理，而是整合进同一个系统。游戏启动时，会根据当前种子为每一个检查点预先分配好奖励，并写入 `global_save.json` 的 `PreGeneratedMappings` 字段中。无论你走到哪里，碰到的是拾取物、石碑、地图商人还是车站收费机，每次交互得到的物品或能力都是固定且可预期的——只要种子不变，结果就不变。

所有检查点现在都统一在一个映射系统下：商店、传说石板、地图购买、车站激活以及拾取物品，不再单独处理。启动时，模组会根据当前种子为每个检查点预先分配奖励，并将其存储在 `PreGeneratedMappings` 字段中，位于 `global_save.json` 内。无论你前往何处——拾取物品、铭文石板、地图商人或过路费机器——只要种子保持不变，每一次互动都将产生固定且可预测的奖励。

你可以随时打开 `global_save.json`，查看每个检查点对应的是什么奖励，从而规划路线，优先拿取自己想要的东西。商店也完全纳入此系统：每个商店的 12 个槽位在启动时就被确定下来，哪些槽位卖什么物品、价格多少，都写在映射表里。同一个种子下，所有商店的货物完全固定，不会因读档或切换场景而改变。重置种子或重置种子世界时，整张映射表会被清空并重新生成，所有检查点的奖励都会重新分配。

你可以随时打开 `global_save.json` 以查看每个检查点将提供什么内容，从而规划你的路线并优先获取你想要的奖励。商店也完全集成：每家商店中的所有 12 占位槽在启动时即被确定——哪些物品出售、售价是多少——都写入了映射表中。在同一种子下，商店库存完全固定，无论重新加载存档或切换场景都不会发生变化。重置种子或重置种子世界将清除整个映射表，并从头开始重新生成所有检查点奖励。

这意味着，今后所有的奖励来源（拾取、阅读石碑、购买地图、开通车站、商店购物）都共享同一个“奖励池”，并且所有分配结果都由种子决定，完全可预测，不再依赖运气。

这意味着所有奖励来源（拾取物品、剧情内容、地图购买、站点解锁以及商店购买）现在共享一个统一的奖励池，所有结果均由种子决定——完全可预测，不涉及运气。

---

### - 剩余奖励改为无限虚拟奖励，货币出现率更高

当真实物品（如武器、技能、地图等）被分配完毕后，剩下的检查点会获得四种无限虚拟奖励：灵丝（3格）、甲壳（300）、随机蓝血（1～6格）和随机货币（50～300念珠）。其中，随机货币的出现频率远高于其他三种，让你在探索过程中总能获得一些钱，不至于空手而归。

在所有真实物品（武器、技能、地图等）分配完毕后，剩余的检查点会获得四种无限虚拟奖励：丝线（3 单位）、贝壳碎片（300）、随机蓝色生命值（1–6 剂量）和随机金币（50–300 Geo）。其中，随机金币出现的频率远高于其他奖励，确保你在探索过程中始终能获得一定数量的货币。

---

### - 全随机模式下方向权限物品也会出现在映射中

开启“全随机模式”后，原本会被禁用的方向动作（如左冲刺、右冲刺、上劈等）会以“方向权限物品”的形式出现在检查点奖励中。你拾取它们后，就能逐步解锁对应的方向能力。这些物品同样被写入预生成映射表，你可以提前知道哪个点会给哪个方向权限。

当“全随机模式”开启时，原本被锁定的方向性动作（例如左跃、右跃、上斩等）会作为“方向权限物品”出现在检查点奖励中。拾取这些物品将逐步解锁对应的方向能力。这些物品也包含在预生成的映射表中，因此你可以提前查看哪个检查点会提供何种权限。

## v1.8.0

### - 地图与车站加入随机池

现在，游戏中的地图和车站均被纳入随机奖励池。购买地图或首次激活车站时，原本固定的解锁行为会被替换为一次随机奖励。地图本身仍会正常解锁，车站也会正常开通，但玩家会额外获得一个随机物品或能力。

地图和车站现在都被加入到随机奖励池中。当购买地图或首次激活车站时，原有的固定解锁机制将被替换为一次随机奖励。地图本身仍然会正常解锁，车站也会正常开放，但玩家将额外获得一个随机物品或能力。

地图共有 28 张，车站共有 10 个。所有地图购买和车站激活的交互点均被视为独立的检查点。每个检查点只能触发一次奖励。触发后，对应的收费机或商店交互将自动禁用，以防止重复使用。

共有 28 张地图和 10 个车站。每一个地图购买与车站激活的交互点均被视为独立的检查点。每个检查点仅能触发一次奖励。触发后，对应收费机或商店的交互将自动禁用，以防止重复操作。

开通车站时，除随机奖励外，还会自动解锁铃兽总开关（UnlockedFastTravel），确保快速旅行功能在车站解锁后立即可用。

激活车站时，除了随机奖励之外，还将自动解锁铃兽总开关（UnlockedFastTravel），以确保快速旅行功能在车站开启后立刻生效。

这些检查点的奖励与种子绑定。重置种子世界后，所有地图和车站的奖励状态将被重置，玩家可重新领取。

这些检查点的奖励与种子相关联。重置种子世界后，所有地图和车站的奖励状态都将被清除，玩家可以再次获取。

---

### - 预生成映射表：启动时生成全部检查点奖励

游戏启动时，会根据当前种子为所有检查点（包括拾取点、石碑交互点、地图购买点、车站激活点）预先分配奖励，并将映射关系写入 `global_save.json` 文件中的 `PreGeneratedMappings` 字段。

游戏启动时，会依据当前种子为所有检查点（包括拾取点、石碑互动点、地图购买点和车站激活点）预先分配奖励，并将对应关系写入 `PreGeneratedMappings` 文件的 `global_save.json` 字段中。

玩家可以在游戏开始前打开该文件，查看每个检查点对应的奖励内容。例如：
- `Tut_01_26.00_77.00_0.00` → `reward:virt:Silk_9`
- `Bonetown_224.00_80.00_0.00` → `reward:Materium`
- `loretrig:Abyss_02b:Inspect Region` → `reward:virt:BlueHealth_5`
- `check:UnlockedBoneforestEastStation` → `reward:virt:BlueHealth_5`

玩家可在游戏启动前打开该文件，查看每个检查点的具体奖励。例如：
- `Tut_01_26.00_77.00_0.00` → `reward:virt:Silk_9`
- `Bonetown_224.00_80.00_0.00` → `reward:Materium`
- `loretrig:Abyss_02b:Inspect Region` → `reward:virt:BlueHealth_5`
- `check:UnlockedBoneforestEastStation` → `reward:virt:BlueHealth_5`

文件路径为 `BepInEx/config/SilksongItemRandomizer/global_save.json`。使用文本编辑器打开后，查找 `"PreGeneratedMappings"` 即可查看全部映射。

该文件位于 `BepInEx/config/SilksongItemRandomizer/global_save.json`。用文本编辑器打开后，搜索 `"PreGeneratedMappings"` 即可查看所有映射内容。

该映射表在游戏启动时自动创建，无需手动操作。重置种子或重置种子世界后，映射表将被清空并重新生成，所有检查点的奖励将根据新种子重新分配。

该映射表会在游戏启动时自动生成，无需玩家手动干预。当重置种子或重置种子世界时，映射表会被清除并重新生成，所有检查点的奖励将依据新的种子进行重新分配。

此功能的主要用途是让玩家可以提前规划路线，优先获取高价值奖励，或通过对照映射表检查是否遗漏了某些检查点。同时也能确认在相同种子下奖励的稳定性，避免因反复读档或进出场景导致奖励发生变化。

该功能使玩家能够预先规划路线，优先获取高价值奖励，并可通过对比映射表检查是否遗漏了某些检查点。同时可验证相同种子下奖励的固定性，防止因重复读取存档或重新进入场景而导致奖励变化。

## v1.7.14
- 增加了基于区域的房间随机模式（实验性）。该模式下，各区域之间的出入口将被随机连接，而非全房间随机。目前该模式尚未完善，不建议正式游玩。

## v1.7.13
- 修复了爬墙能力获取后消失的问题
- 修复了地方志阅读不触发随机奖励的问题
- 调整了最近获得物品的界面显示

## v1.7.12
- 修复了商店购买卡死的问题
- 修复了商店购买卡死的问题
- 加入了地方志随机机制。现在，当你阅读游戏中的石碑或铭文时，有可能触发额外的随机奖励，比如获得物品或能力；同时，在普通物品随机池中，也加入了“随机日志”奖励
- 添加了地方志随机机制。现在，当你阅读游戏中的石碑或铭文时，存在触发额外随机奖励的可能性，例如获取物品或能力；同时，在常规物品随机池中，也加入了“随机日志”奖励

## v1.7.11
- 修复了开局物品和技能给予的部分
- 修复了技能给予异常的问题

## v1.7.10
- 恢复了椅子翻译部分
- 恢复了椅子翻译部分。
- 运行优化
- 运行优化。
- 关于贴图类的缓存
- 关于贴图类的缓存。

## v1.7.9
- 修复了拾取获得两次物品的情况
- 修复了物品被获取两次的问题。
- UI部分修改
- UI调整。
- 优秀的QoL！
- 我们需要QoL！

## v1.7.7

- 对模组库进行了整理，大部分开局选项可以正常应用，开局随机物品和随机技能目前不可用，若发现技能无法使用，请关闭技能分裂的具体选项。

- 大部分开局选项可以正常应用，但开局随机物品和随机技能目前不可用。如果发现技能无法使用，请关闭技能分裂的具体选项。

## v1.7.6

- 对面板进行了一些小修复  
- 关于面板字符串的少量修改

## v1.7.4

### - **修复开局设置未实际生效的严重问题，面板不再自动弹出**

- 修复了在配置面板中修改的攻击方向、技能模式、物品数量等开局选项在进入游戏后未真正生效的 bug。现在所有设置都会在游戏场景加载完成后正确应用，不再出现“改了等于没改”的情况。
- 配置面板不再在进入游戏时自动弹出。现在仅在主菜单按 **F8** 或通过“随机模式”按钮手动呼出，避免干扰正常游戏流程。

### - 修复了启动设置未真正生效的问题；面板不再自动弹出

- 修复了启动选项（攻击方向、技能模式、物品数量等）在配置面板中修改后，进入游戏后无法生效的问题。所有设置现在在游戏场景加载完成后正确应用。
- 配置面板不再在进入游戏时自动打开。现在只能通过主菜单中按 **F8** 或点击“随机模式”按钮手动打开，避免了意外中断。

---

### - **新增随机池类型配置页面（物品随机池类型）**

- 在主页面新增“物品随机池”按钮，点击进入独立配置页面。
- 提供三个独立开关：**纹章随机**、**技能类物品随机**、**遗物随机**。开关状态通过按钮背景颜色（绿/灰）直观显示，不再依赖文字。
- 这些开关控制对应类型的物品是否参与拾取随机替换。关闭后，捡到该类型物品时直接获得原物品，不进行随机化。
- 开关状态与种子绑定，会随种子世界重置或存档保存。

### - **新增随机池类型配置页面（物品池类型）**

- 在主页面新增了一个“物品随机池”按钮，可进入专属配置页面。
- 三个独立的切换选项：**纹章随机**、**技能物品随机**、**圣物随机**。状态通过按钮背景颜色（绿色/开启，灰色/关闭）直观显示，无需文字提示。
- 这些开关控制对应类型物品在拾取时是否被随机奖励替代。当关闭时，拾取此类物品将直接获得原物品。
- 切换状态与种子绑定，并在重置种子和存档文件时保持不变。

---

### - **新增重复物品次数配置页面（细粒度限制）**

- 在主页面新增“重复物品”按钮，进入后以 **12 列网格布局** 展示所有可重复获得的物品类别。
- 每个条目采用“上面标签、下面输入框”的竖直排列，清晰易读。
- 可配置项包括：
  - 普通物品（技能类物品、遗物、其他物品）
  - 方向权限（上劈、左劈、右劈、冲刺左/右、飞针左/右、漂浮左/右、壁跳左/右、回血）
  - 能力虚拟奖励（丝之矛、灵丝风暴、飞针、丝刃标、符文之怒、苍白之爪、丝忆弦针、格挡、深邃挽歌、幻兽歌、风铃摇、疾风步、流浪者披风、雪绒披风、灵丝升腾、蛛攀术、蓄力斩）
  - 珍贵虚拟奖励（面具碎片、丝轴碎片、丝线恢复上限+1、纹章槽位解锁器）
- 所有输入框实时修改对应的配置项（`ConfigEntry<int>`），无需确认立即生效。
- 该页面完全由代码动态生成，支持运行时调整，配置项数量可随需求扩展。

### - **新增重复次数限制配置页面（精细的限制设置）**

- 主页新增了一个“重复物品”按钮，点击后会打开一个包含所有可多次获得的物品分类的页面，采用 **12‑列网格布局**。
- 每项内容垂直排列（“标签在上，输入框在下”）以增强清晰度。
- 可配置的物品包括：
- 普通物品（技能物品、遗物、其他物品）
- 方向权限（向上斩击，向左斩击，向右斩击，向左/向右冲刺，鱼叉向左/向右，浮空向左/向右，墙壁跳跃向左/向右，治疗）
- 能力虚拟奖励（丝之矛，丝线球，鱼叉冲刺，缚丝充能，丝线炸弹，丝线首领针，需灵，格挡，深沉哀歌，兽之歌，EvaHeal，迅捷步法，游荡者斗篷， Faydown 斗篷，丝线翱翔，附着抓握，蓄力斩击）
- 珍贵的虚拟奖励（心之部件、卷轴部件、最大丝线再生提升、纹章槽位解锁器）
- 所有输入字段会实时修改其对应的 `ConfigEntry<int>`，并立即生效。
- 本页面在运行时完全生成，支持动态调整和轻松扩展。

---

### - **其它改进**

- 修复了部分情况下因反射类型不匹配导致的 `InvalidCastException` 和 `NullReferenceException`，现在所有配置项的读写都经过安全转换。
- 物品随机池页面（纹章/技能/遗物）的按钮颜色切换逻辑已修复，点击后背景色和文字色会正确变化。
- 所有配置项的暂存与延迟应用逻辑已整合到 `MenuChangerMod` 中，确保进入游戏场景后才统一应用，避免在菜单阶段过早初始化导致设置被重置。
- 优化了重复物品页面的动态生成性能，仅在首次打开页面时构建，后续刷新仅同步数值。

### - **其他改进**

- 修复了由反射中的类型不匹配引起的 `InvalidCastException` 和 `NullReferenceException` 问题；所有配置项的读取/写入操作现在均使用安全转换。
- 修复了物品池页面切换选项的颜色逻辑（纹章/技能/遗物）；点击时背景和文字颜色现在能正确变化。
- 所有待处理的设置现在在进入游戏场景后被正确保存并应用，避免了早期初始化及配置丢失的问题。
- 优化了重复限制页面的动态生成：布局仅在首次打开时构建，后续刷新仅同步值。

---

## v1.7.3

### - **初步实现外部配置面板，大幅提升模组交互体验**

- 基于新开发的 `NativeUIMaker` 原生 UI 库，重构了模式选择与详细设置界面，所有控件均使用丝之歌原生菜单风格（按钮、选项行、滑块、箭头、输入框等）。  
- 新增 **模式选择页面**：在存档选择后进入，提供“经典模式”“钢魂模式”“随机模式”三个入口。  
  - 点击“随机模式”会打开完整的详细设置面板；点击其他模式直接开始游戏。  
- **详细设置面板** 包含以下完整配置项（所有状态均保存并全局生效）：  
  - **游戏模式**：彻底疯狂、纹章诅咒、全随机模式（开启后禁用除下劈外的所有方向动作，改为拾取方向权限物品）。  
  - **开局相关**：  
    - 攻击方向权限：上劈、左劈、右劈（独立开关）。  
    - 技能随机模式：总随机（0–13 个技能）或分类随机（垂直/水平/特殊/攻击各 0–5/4/4/5）。  
    - 开局随机物品数量（0–10）。  
    - 种子输入与重置种子世界（重置所有拾取点、技能触发器）。  
  - **辅助设置**：  
    - 丝之心、疾跑和上冲（`RoomRandomMode` 开关）。  
    - 显示完成度百分比（全局）。  
    - 货币保底阈值（首次保底次数、二次保底次数、丝矛保底次数，可输入修改）。  
  - **房间随机**：  
    - 启用场景随机、显示场景名/种子、修改并应用房间种子、传送至指定场景。  
  - **陷阱随机**：  
    - 启用陷阱随机、陷阱难度（初猎/专注/满溢）、陷阱生命化（陷阱移动）、重置冰冻黑名单。  
  - **怪物随机**：  
    - 启用怪物随机、随机缩放开关及最小/最大缩放值。  
  - **物品随机**：  
    - 启用物品随机，显示当前物品种子。  
    - 重置商店记录、重置捡拾记录。  
  - **页面导航**：左右箭头切换页面，各页面均有“返回主页面/返回模式选择”按钮。  
- 所有界面文字已接入本地化系统，可根据游戏语言自动切换中英文（支持 CN/EN/Auto 手动切换）。  
- 配置面板默认按 F8 呼出（可在 `MenuChangerMod` 中修改），并与存档选择流程无缝集成。

### - **初步实现外部配置面板，大大提升了模组的交互体验**

- 基于新开发的 `NativeUIMaker` 原生 UI 库，重构了模式选择与详细设置界面，所有控件均使用丝之歌原生菜单风格（按钮、选项行、滑块、箭头、输入框等）。  
- **模式选择页面**：在存档选择后进入，提供“经典模式”“钢魂模式”“随机模式”三个入口。  
  - 点击“随机模式”会打开完整的详细设置面板；点击其他模式直接开始游戏。  
- **详细设置面板** 包含以下完整配置项（所有状态均保存并全局生效）：  
  - **游戏模式**：彻底疯狂、纹章诅咒、全随机模式（开启后禁用除下劈外的所有方向动作，改为拾取方向权限物品）。  
  - **开局相关**：  
    - 攻击方向权限：上劈、左劈、右劈（独立开关）。  
    - 技能随机模式：总随机（0–13 个技能）或分类随机（垂直/水平/特殊/攻击各 0–5/4/4/5）。  
    - 开局随机物品数量（0–10）。  
    - 种子输入与重置种子世界（重置所有拾取点、技能触发器）。  
  - **辅助设置**：  
    - 丝之心、疾跑和上冲（`RoomRandomMode` 开关）。  
    - 显示完成度百分比（全局）。  
    - 货币保底阈值（首次保底次数、二次保底次数、丝矛保底次数，可输入修改）。  
  - **房间随机**：  
    - 启用场景随机、显示场景名/种子、修改并应用房间种子、传送至指定场景。  
  - **陷阱随机**：  
    - 启用陷阱随机、陷阱难度（初猎/专注/满溢）、陷阱生命化（陷阱移动）、重置冰冻黑名单。  
  - **怪物随机**：  
    - 启用怪物随机、随机缩放开关及最小/最大缩放值。  
  - **物品随机**：  
    - 启用物品随机，显示当前物品种子。  
    - 重置商店记录、重置捡拾记录。  
  - **页面导航**：左右箭头切换页面，各页面均有“返回主页面/返回模式选择”按钮。  
- 所有界面文字已接入本地化系统，可根据游戏语言自动切换中英文（支持 CN/EN/Auto 手动切换）。  
- 配置面板默认按 F8 呼出（可在 `MenuChangerMod` 中修改），并与存档选择流程无缝集成。

---

### - **快速进入第一幕（跳过开场动画）**

- 点击“确认并开始游戏”或通过模式选择按钮开始新游戏时，会自动跳过开场动画序列。  
- 实现方式：在 `SkipToFirstAct` 中检测 `Opening_Sequence` 场景，进入后连续调用 `GameManager.SkipCutscene` 多次，直接进入第一幕（Act 1）。  
- 该功能仅在通过模组启动新游戏时生效，不影响正常游戏或手动跳过动画的行为。

### - **Fast transition to Act 1 (skip intro cutscenes)**

- 当通过“确认并开始游戏”或模式选择按钮启动新游戏时，会自动跳过开场动画序列。  
- 实现方式：在 `SkipToFirstAct` 中检测 `Opening_Sequence` 场景，进入后连续调用 `GameManager.SkipCutscene` 多次，直接进入第一幕（Act 1）。  
- 该功能仅在通过模组启动新游戏时生效，不影响正常游戏或手动跳过动画的行为。

---

### - **完整配置面板包含的所有可调项目（对应 `NativeUIMaker_Config.json`）**

以下是配置文件中定义的控件及其功能（已全部集成并测试通过）：

| 页面 | 控件名称 | 类型 | 功能说明 |
|------|----------|------|----------|
| ModeSelect | ClassicModeBtn | 按钮 | 经典模式（无永久死亡） |
| ModeSelect | SteelModeBtn | 按钮 | 钢魂模式（永久死亡） |
| ModeSelect | RandomModeBtn | 按钮 | 打开详细设置面板 |
| Main | RoomBtn | 开关按钮 | 启用/禁用场景随机 |
| Main | ItemBtn | 开关按钮 | 启用/禁用物品随机 |
| Main | TrapBtn | 开关按钮 | 启用/禁用陷阱随机 |
| Main | EnemyBtn | 开关按钮 | 启用/禁用怪物随机 |
| Main | DetailBtn | 按钮 | 切换到第一个详情页（开局相关） |
| Main | BackToModeSelectBtn | 按钮 | 返回模式选择页 |
| Main | ConfirmBtn | 按钮 | 应用所有设置并开始新游戏（跳过开场） |
| Main | ResetOption | 选项行 | 重置种子世界（开/关） |
| Main | SeedInput | 输入框 | 输入种子文本 |
| Detail0 | UpBtn / LeftBtn / RightBtn | 开关按钮 | 上劈/左劈/右劈权限 |
| Detail0 | ItemCountSlider | 滑块 | 开局随机物品数量 0–10 |
| Detail1 | SkillModeOption | 选项行 | 技能随机模式（总随机/分类随机） |
| Detail1 | TotalSkillSlider | 滑块 | 总随机技能数量 0–13 |
| Detail1 | VSlider / HSlider / SSlider / ASlider | 滑块 | 分类随机各类型数量 |
| Detail2 | CrazyBtn | 开关按钮 | 彻底疯狂模式（一键随机满配） |
| Detail2 | CrestCurseBtn | 开关按钮 | 纹章诅咒效果 |
| Detail2 | FullRandomBtn | 开关按钮 | 全随机模式（禁用方向动作，改为权限物品） |
| Detail2 | CategoryBtn1–6 | 开关按钮 | 全随机模式下的方向分裂细项（冲刺、飞针、漂浮、壁跳、回血、攻击方向） |
| Detail3 | SilkHeartBtn | 开关按钮 | 丝之心、疾跑和上冲（`RoomRandomMode`） |
| Detail3 | CompletionBtn | 开关按钮 | 显示完成度百分比 |
| Detail3 | CurrencyFirstThresholdInput / CurrencySecondThresholdInput / SilkSpearPityInput | 输入框 | 货币保底次数和丝矛保底次数 |
| Detail4 | ApplyRoomSeedBtn | 按钮 | 应用房间种子 |
| Detail4 | ShowRoomNameBtn / ShowRoomSeedBtn | 开关按钮 | 显示当前场景名/种子 |
| Detail4 | RoomSeedInput | 输入框 | 房间种子输入 |
| Detail5 | TrapDifficultyBtn | 三态按钮 | 陷阱难度（初猎/专注/满溢） |
| Detail5 | TrapMovementBtn | 开关按钮 | 陷阱生命化（移动） |
| Detail5 | ResetFrostBlacklistBtn | 按钮 | 重置冰冻黑名单 |
| Detail6 | EnemyScaleRandomBtn | 开关按钮 | 怪物随机缩放 |
| Detail6 | EnemyScaleMinSlider / EnemyScaleMaxSlider | 滑块 | 缩放最小/最大值 |
| Detail7 | ResetShopBtn / ResetPickupBtn | 按钮 | 重置商店记录 / 重置捡拾记录 |

此外，每个详情页底部都有左右箭头（`LeftArrow` / `RightArrow`）用于切换页面，以及 `BackToMainBtn` 返回主页面。  

所有控件的默认值、位置、尺寸均可在 `NativeUIMaker_Config.json` 中自由调整，无需重新编译模组。

### - **Full list of adjustable options in the configuration panel (as defined in `NativeUIMaker_Config.json`)**

配置文件中定义的所有控件及其功能均已完整集成并测试通过：

| 页面 | 控制名称 | 类型 | 描述 |
|------|--------------|------|-------------|
| ModeSelect | ClassicModeBtn | 按钮 | 经典模式（无永久死亡） |
| ModeSelect | SteelModeBtn | 按钮 | 钢铁模式（永久死亡） |
| ModeSelect | RandomModeBtn | 按钮 | 打开详细设置面板 |
| 主页 | RoomBtn | 开关 | 启用/禁用场景随机化 |
| 主页 | ItemBtn | 开关 | 启用/禁用物品随机化 |
| 主页 | TrapBtn | 开关 | 启用/禁用陷阱随机化 |
| 主页 | EnemyBtn | 开关 | 启用/禁用敌人随机化 |
| 主页 | DetailBtn | 按钮 | 切换到第一个详细页面（启动设置） |
| 主页 | BackToModeSelectBtn | 按钮 | 返回模式选择界面 |
| 主页 | ConfirmBtn | 按钮 | 应用所有设置并开始新游戏（跳过场景动画） |
| 主页 | ResetOption | 选项行 | 重置世界种子（开启/关闭） |
| 主页 | SeedInput | 输入框 | 输入种子文本 |
| 详细页面0 | UpBtn / LeftBtn / RightBtn | 开关 | 上斜线 / 左斜线 / 右斜线权限 |
| 详细页面0 | ItemCountSlider | 滑块 | 初始随机物品数量 0–10 |
| 详细页面1 | SkillModeOption | 选项行 | 技能随机模式（总数 / 分类） |
| 详细页面1 | TotalSkillSlider | 滑块 | 总共随机技能数量 0–13 |
| 详细页面1 | VSlider / HSlider / SSlider / ASlider | 滑块 | 按分类类型分配的随机数量 |
| 详细页面2 | CrazyBtn | 开关 | 完全疯狂模式（立即最大化随机物品） |
| 详细页面2 | CrestCurseBtn | 开关 | 纹章诅咒效果 |
| 详细页面2 | FullRandomBtn | 开关 | 全部随机模式（禁用方向操作，替换为权限类物品） |
| 详细页面2 | CategoryBtn1–6 | 开关 | 在全部随机模式下显示方向拆分细节（冲刺、鱼叉、浮空、墙跳、治疗、攻击方向） |
| 详细页面3 | SilkHeartBtn | 开关 | 丝之心、冲刺与超级跳跃（`RoomRandomMode`） |
| 详细页面3 | CompletionBtn | 开关 | 显示完成百分比 |
| 详细页面3 | CurrencyFirstThresholdInput / CurrencySecondThresholdInput / SilkSpearPityInput | 输入框 | 货币保底数量和丝矛保底数量 |
| 详细页面4 | ApplyRoomSeedBtn | 按钮 | 应用房间种子 |
| 详细页面4 | ShowRoomNameBtn / ShowRoomSeedBtn | 开关 | 显示当前场景名称 / 种子 |
| 详细页面4 | RoomSeedInput | 输入框 | 房间种子输入 |
| 详细页面5 | TrapDifficultyBtn | 三态按钮 | 陷阱难度（新手 / 聚焦 / 泄露） |
| 详细页面5 | TrapMovementBtn | 开关 | 陷阱生命值（移动） |
| 详细页面5 | ResetFrostBlacklistBtn | 按钮 | 重置冰霜黑名单 |
| 详细页面6 | EnemyScaleRandomBtn | 开关 | 随机敌人强度调整 |
| 详细页面6 | EnemyScaleMinSlider / EnemyScaleMaxSlider | 滑块 | 最小/最大强度值 |
| 详细页面7 | ResetShopBtn / ResetPickupBtn | 按钮 | 重置商店记录 / 重置拾取记录 |

此外，每个详细页面都配有左右箭头（`LeftArrow` / `RightArrow`）用于页面切换，以及一个 `BackToMainBtn` 返回主页。

所有控件的默认值、位置和大小均可在 `NativeUIMaker_Config.json` 中自由调整，无需重新编译模组。

## v1.7.2

### - **尝试整合空洞原生 UI 模块（实验性）**

- 为了更好地贴合丝之歌原生菜单的视觉与交互风格，我们开始拆解游戏内置的 UI 组件，并封装为独立可复用的模块。
- 目前已成功提取并封装了以下原生风格控件：
  - `NativeButton`（支持图标按钮、纯文字按钮、小型按钮）
  - `NativeLabel`（标准文本标签）
  - `NativeOption`（带描述浮层的开关/选项行）
  - `NativeSlider`（带数值显示的滑块）
  - `NativeArrow`（左右翻页光标，带动画效果）
- 这些控件目前仅作为独立库存在，**尚未接入游戏主流程**，因此在当前版本中“没有实际用处”。
- 拆解出的组件已通过 `PrefabMenuObjects` 从游戏运行时动态获取预制体（如 `NormalButton`、`OptionsMenuScreen` 中的左右箭头等），理论上可以零资源依赖地复刻原生 UI 样式。
- 后续版本将逐步用这些原生控件替换现有的开局选项面板和模式选择菜单，使模组的界面体验与丝之歌本体完全一致。
- 本次更新仅为底层技术探索，不影响任何现有功能，也不改变玩家的游戏体验。

### - **实验性整合空洞骑士原生 UI 模块**

- 为了更好地贴合丝之歌原生菜单的视觉与交互风格，我们开始拆解游戏内置的 UI 组件，并封装为独立可复用的模块。
- 已成功提取并封装了以下原生风格控件：
  - `NativeButton`（支持图标按钮、纯文字按钮、小型按钮）
  - `NativeLabel`（标准文本标签）
  - `NativeOption`（带描述浮层的开关/选项行）
  - `NativeSlider`（带数值显示的滑块）
  - `NativeArrow`（左右翻页光标，带动画效果）
- 这些控件目前仅作为独立库存在，**尚未接入游戏主流程**，因此在当前版本中“没有实际用处”。
- 拆解出的组件已通过 `PrefabMenuObjects` 从游戏运行时动态获取预制体（e.g、`NormalButton`、`OptionsMenuScreen` 中的左右箭头等），理论上可以零资源依赖地复刻原生 UI 样式。
- 后续版本将逐步用这些原生控件替换现有的开局选项面板和模式选择菜单，使模组的界面体验与丝之歌本体完全一致。
- 本次更新仅为底层技术探索，不影响任何现有功能，也不改变玩家的游戏体验。

## v1.7.1

### - **全面支持界面文字本地化（可自由翻译）**

- 现在模组的所有界面文字（按钮、标签、提示等）都可以通过外部 JSON 文件翻译为任意语言。
- 在模组目录下的 `language` 文件夹中放入任意名称的 `.json` 文件，格式为 `{"中文原文": "译文"}`，模组启动时会自动读取并应用。  
- 你甚至可以同时放多个语言文件（比如 `en.json`、`ja.json`），模组会合并所有条目。  
- 关于如何编写 JSON 文件，请阅读 `language` 文件夹中的 `README.md` 说明。
- 本次更新已经将模组内所有需要翻译的文字全部提取为标准键值，理论上不再有硬编码的中文。

### - 完全支持本地化（可添加任何语言的 JSON 文件）

- 所有用户界面文本（按钮、标签、提示信息）现在可以通过外部 JSON 文件翻译为任何语言。
- 将 `.json` 文件（e.g.、`en.json`、`ja.json`）放置在模组目录下的 `language` 文件夹中。每个文件遵循 `{"Chinese original": "translation"}` 的格式。模组将在启动时自动加载它们。
- 你可以同时拥有多个语言文件——所有条目都会被合并。
- 请参见 `README.md` 文件夹内的 `language` 获取详细说明。
- 本次更新已将模组中的所有文本提取至标准配置键中，因此不再硬编码中文。

---

### - **修复大量游戏逻辑问题，提升稳定性和体验**

- **怪物随机开关**：现在关闭“启用怪物随机”后，所有已经被替换的敌人会立即恢复原样，并且不会再出现新的随机敌人。之前关闭后敌人仍然会被替换的问题已彻底修复。  
- **商店改进**：每个商店现在固定有 **12 个商品**（原来是 8 个），并且商店里刷出的普通物品（面具碎片、丝轴碎片、纹章槽位解锁器等）在整个游戏中只会出现一次，不会在不同商店重复出现，也不会在同一商店里出现两个一样的东西。前三格特殊奖励（随机念珠、随机蓝血、随机上限提升）仍然每个商店都有，不受限制。  
- **纹章槽位解锁器**：这种特殊物品现在在商店里最多可以购买 20 次（原来是 3 次），并且它只会解锁非法术槽（攻击槽、工具槽），不会浪费在本来就解锁的法术槽上。  
- **全随机模式逻辑修正**：开启全随机模式后，原本会被错误保留的一些技能方向（比如冲刺、壁跳的左右权限）现在会被彻底清除，拾取方向权限物品后能正确解锁对应的单方向。关闭全随机模式后，所有方向恢复为默认允许，并且随机拾取池也恢复正常。  
- **随机池调整**：开启全随机模式时，原本会直接提供完整技能的原版物品（如“迅捷步”、“鱼叉突刺”等）将不再出现在拾取列表中，取而代之的是方向权限物品（冲刺左、冲刺右等）。关闭全随机模式时，物品池恢复为全部原版物品+虚拟奖励。
- **其他小修复**：修复了由于大小写问题导致壁跳技能无法正确赋予的漏洞；法术槽初始化现在仅会解锁一个，而非一次性全部解锁。

### - 修复了多个游戏逻辑问题，提升了稳定性和体验

- **敌人随机化开关**：当关闭“启用敌人随机化”时，所有已替换的敌人将立即恢复为原状，且不会再出现新的随机敌人。此前敌人在关闭后仍保持被替换的状态的问题现已修复。
- **商店优化**：每个商店现在都有 **12 个物品槽位**（之前为 8）。常见物品（面具碎片、卷轴部件、纹章槽位解锁器等）在整个游戏内仅出现一次——不同商店之间不会重复，同一商店内部也不会重复。前三个特殊槽位（随机硬币袋、随机蓝色生命值、随机帽子升级）仍会在每个商店中无限制地出现。
- **纹章插槽解锁器**：这个特殊物品现在可以在商店中购买最多 **20 次**（之前为 3）。它仅能解锁非法术插槽（攻击插槽、工具插槽），不会浪费在已经解锁的插槽上。
- **全随机模式逻辑修复**：在激活全随机模式时，任何之前解锁的方向权限（e.g.、左右冲刺、左右墙跳）现在会正确重置。拾取方向权限物品将仅解锁对应的具体方向。在退出全随机模式时，所有方向将恢复为默认允许状态，且随机获取池也将恢复正常。
- **随机池调整**：在开启完全随机模式时，原版会直接提供完整能力的物品（e.g、“Swift Step”、“Harpoon Dash”）将不再出现在拾取池中。取而代之的是加入方向权限类物品（e.g、“Dash Left”、“Dash Right”）。当该模式关闭时，池子中包含所有原始物品以及虚拟奖励。
- **其他小修复**：修复了一个大小写敏感的bug，该bug导致墙跳能力无法正确授予。法术槽的初始化现在每次仅解锁一个槽位，而不是一次性全部解锁。

## v1.7.0

### - **将所有能力纳入随机池（包含风铃摇）**

- 现在游戏中的所有能力（包括之前遗漏的“风铃摇”等）都已完整加入到开局技能随机池中。
- 无论是“总随机”还是“分类随机”模式，技能池均覆盖全部能力：攻击技能（丝之矛、灵丝风暴、符文之怒等）、移动能力（冲刺、二段跳、飞针、壁跳、超级跳等）以及特殊能力（丝忆弦针、深邃挽歌、幻兽歌、风铃摇等）。
- 技能获得时统一使用游戏原生弹窗（`SkillGetMsg` / `PowerUpGetMsg`），图标、名称、描述显示完整，与原版体验一致。

### - 所有技能（包括 EvaHeal）现已包含在随机池中

- 游戏中的每个技能（包括之前缺失的“EvaHeal”等）均已完全集成到初始技能随机池中。
- 无论处于“总模式”还是“分类模式”，技能池均涵盖所有能力：战斗技能（丝线之矛、丝线风暴、符文狂怒等）、移动能力（冲刺、双跳、鱼叉冲刺、墙跳、超级跳跃等）以及特殊能力（需洛林、深沉哀歌、兽之歌、EvaHeal等）。
- 技能获取现在始终使用游戏原生的弹窗（`SkillGetMsg` / `PowerUpGetMsg`），显示完整的图标、名称和描述，与原版体验一致。

---

### - **新增“全随机模式”**

- 在开局选项面板的技能随机模式下方（右侧边缘）添加了独立的“全随机模式”开关。
- **开启后**：
  - **所有额外方向动作被永久禁用**：上劈、左劈、右劈、回血、左右冲刺、左右飞针、左右漂浮、左右壁跳全部无法使用，仅保留默认下劈。
- 原版中直接赋予这些能力的物品（如“迅捷步法”“鱼叉突刺”“漂浮斗篷”“附着抓握”“EvaHeal”等）**不再出现在随机拾取池中**。
  - 取而代之，随机拾取池中会生成**方向权限物品**（例如“冲刺左”“冲刺右”“飞针左”“飞针右”“上劈权限”等）。拾取一个权限物品会**同时给予对应的基础技能**并**解锁该方向**（例如捡到“冲刺左”则获得冲刺技能并允许向左冲刺）。
  - 只有捡齐左右两个权限（或对应的攻击方向），该动作才能完全不受限制。
- **关闭后**：
  - 所有方向动作恢复为默认允许状态。
  - 随机拾取池恢复为正常物品池（包含全部原版物品和虚拟奖励）。
- 全随机模式的状态会保存到配置文件中，重启游戏后自动沿用。

### - **新增“完全随机模式”**

- 在启动选项面板中，已添加一个独立的“全随机模式”切换按钮（对齐至右侧），位于技能模式按钮下方。
- **启用时**：
- 所有额外的方向性动作从一开始就已禁用：向上斩击、向左斩击、向右斩击、治疗、左右冲刺、左右鱼叉冲刺、左右漂浮以及左右墙跳均被锁定。仅保留默认的向下斩击可使用。
- 原版物品中直接提供这些能力的项目（例如“迅捷步法”、“鱼叉突刺”、“漂浮斗篷”、“附着抓握”、“EvaHeal”等）**将从随机拾取池中移除**。
- 相反，池子会接收**方向权限物品**（e.g，如“冲刺左”、“冲刺右”、“鱼叉左”、“鱼叉右”、“向上斩许可”等）。拾取一个权限物品会获得相应的基础技能，并解锁该特定方向（e.g，例如拾取“冲刺左”将获得冲刺能力并允许向左冲刺）。
- 只有在获得左右权限（或相应的攻击方向）之后，该动作才会完全不受限制。
- **当禁用时**：
- 所有方向性操作会恢复为默认允许状态。
- 随机拾取池恢复为正常物品池（包含所有原版物品和虚拟奖励）。
- 全随机模式的状态会保存到配置文件中，并在游戏重启后保持不变。

## v1.6.11

### - **修正技能显示与原生弹窗，调整纹章排除池**

### - 修复了技能显示和原生弹窗，调整了纹章排除池

- 所有技能（攻击类与能力类）现在统一使用游戏原生弹窗（`SkillGetMsg` / `PowerUpGetMsg`）进行提示，不再依赖自定义贴图。

- 所有技能（包括战斗和移动技能）现在使用游戏原生的弹窗（`SkillGetMsg` / `PowerUpGetMsg`）进行提示，取消了对自定义纹理的依赖。

- 跑图能力（冲刺、爬墙、飞针、丝忆弦针、超级跳、风铃摇等）现在正确调用 `PowerUpGetMsg.Spawn`，显示与原版一致的能力获得界面，并配有完整图标与描述。

- 移动能力（冲刺、墙跳、鱼叉冲刺、需洛林、超级跳跃、EvaHeal 等）现在能正确调用 `PowerUpGetMsg.Spawn`，显示真实的能力获取界面，并带有正确的图标和描述。

- 修复了部分能力（如二段跳、流浪者披风、蓄力斩、格挡）因原生物品缺失而回退到自定义弹窗的问题；现通过动态创建临时 `ToolItemSkill` 并提取物品描述，确保所有技能都能呈现原生样式的弹窗。

- 修复了某些技能（e.g，双跳，漂浮斗篷，突进斩击，格挡）因缺少原版物品而回退到自定义弹窗的问题；现在通过动态创建临时 `ToolItemSkill` 对象并提取物品描述，所有技能均可显示原生风格的弹窗。

- 纹章随机排除池新增 `Hunter_v2`、`Hunter_v3`、`Witch_v2`、`Cloakless` 等纹章，避免它们参与随机映射，防止出现无效或破坏游戏流程的纹章组合。

- 纹章排除池现已包含 `Hunter_v2`、`Hunter_v3`、`Witch_v2`、`Cloakless` 及其他项，防止它们参与随机映射，并避免出现无效或导致游戏崩溃的纹章组合。

- 优化了排除池逻辑：现在通过 `HashSet` 统一管理，支持动态加载与保存，排除的纹章将完全不会出现在随机结果中，确保游戏平衡性。

- 优化了排除池的逻辑：现在通过一个 `HashSet` 进行管理，支持动态加载和保存；被排除的纹章将不会出现在随机结果中，确保游戏平衡。

## v1.6.8

### - **修复模组独立性问题，移除所有外部依赖**

- 模组现在**完全独立运行**，不再需要任何第三方模组（如 Architect、Benchwarp、EnemyRando 等）即可正常工作。
- 即使其他模组被禁用或缺失，以下功能依然完整可用：
  - 房间随机化（场景连接随机生成）
  - 物品随机化（拾取点替换、随机奖励池、次数限制）
  - 额外拾取点生成（69 个自定义位置的简易钥匙拾取点，坐标持久化）
  - 商店随机化（商品随机生成、购买刷新、本地化名称、特殊奖励槽位）
  - 纹章随机化（神龛纹章映射、重生装备、教堂禁用）
  - 传送至最后坐过的椅子（F6 快捷键，独立实现，不依赖 Benchwarp）
  - 陷阱随机化（陷阱生成、难度切换、移动系统、平台/弹跳物生成）
  - 货币保底（30/1000 次掉落简易钥匙）
  - 开局选项面板（完整设置界面，种子选择，难度调整）
- 所有功能在无任何其他模组的环境下均能正常运行，消除了之前因缺少依赖而导致的崩溃或功能缺失。

### - **Fixed mod independence, removed all external dependencies**

- The mod now runs **completely standalone**, requiring no third-party mods (e.g., Architect, Benchwarp, EnemyRando) to function correctly.
- Even when other mods are disabled or missing, the following features still work fully:
  - Room randomisation (scene connection generation)
  - Item randomisation (pickup replacement, reward pool, quantity limits)
  - Extra pickup generation (69 custom Simple Key pickups with coordinate persistence)
  - Shop randomisation (dynamic stock, purchase refresh, localised names, special reward slots)
  - Crest randomisation (shrine mapping, respawn equipment, chapel disabling)
  - Warp to last bench (F6 hotkey, implemented independently, does not require Benchwarp)
  - Trap randomisation (generation, difficulty toggles, movement system, platform/bounce object spawning)
  - Currency pity (Simple Key at 30/1000 drops)
  - Startup options panel (full settings interface, seed selection, difficulty adjustments)
- All features work reliably even with no other mods installed, eliminating crashes or missing functionality caused by missing dependencies.

---

### - **性能优化：CPU 占用降至 1% 以下**

- 彻底重写了物品池初始化、商店缓存刷新、陷阱生成循环等高频操作，消除了之前版本中因不当刷新导致的 CPU 峰值。
- 场景加载时的拾取点状态应用、陷阱缓存读取等操作改为增量更新，避免全量遍历。
- 经过实测，模组在正常游戏过程中 CPU 占用率稳定在 **1% 以下**，不再出现异常卡顿或高负载情况。

### - **Performance optimised: CPU usage below 1%**

- Rewrote high-frequency operations such as item pool initialisation, shop cache refreshes, and trap generation loops, removing CPU spikes caused by improper refreshes.
- Pickup state application and trap cache loading on scene changes are now incremental, avoiding full traversals.
- Benchmarking shows the mod’s CPU usage consistently stays **below 1%** during normal gameplay, with no abnormal stuttering or high load.

---

### - **商店随机化扩展：特殊奖励加入商店，槽位固定为 8 个**

- 商店随机池现在包含三种特殊奖励：
  - **槽位 0：随机金币**（价格 1，数量 50–300 念珠）
  - **槽位 1：随机蓝血**（价格 1，分 1–5 次给予，每次间隔 1 秒）
  - **槽位 2：随机上限提升物品**（价格 60–120，随机获得面具碎片/丝轴碎片/丝线恢复上限）
- 每个商店**固定生成 8 个商品槽位**，不受原商店预制体限制。
- 购买任何商品后，该槽位立即消失（`SetActive(false)`），其余槽位保持原位，不会出现错位或布局混乱。
- 所有商品的名称、描述均已接入本地化系统，中英文切换时正确显示。

### - **Shop randomisation extended: special rewards added, fixed 8 slots**

- The shop pool now includes three special reward types:
  - **Slot 0: Random coin pouch** (price 1, 50–300 coins)
  - **Slot 1: Random blue health** (price 1, 1–5 doses, one per second)
  - **Slot 2: Random cap upgrade** (price 60–120, random Heart Piece / Spool Part / Silk Heart)
- Every shop now **spawns exactly 8 item slots**, regardless of the original shop’s prefab.
- After purchase, that slot immediately disappears (`SetActive(false)`), leaving the remaining slots in place with no layout distortion.
- All item names and descriptions are localised, correctly switching between Chinese and English.

---

### - **随机物品概率调整**

- 重新平衡了随机奖励池的概率分布：
  - **普通虚拟奖励（灵丝、生命、蓝血、甲壳、灵丝碎片、完全恢复等）**：30%
  - **随机念珠堆（生成可打碎的钱堆）**：20%
  - **有限类奖励（原版物品、面具碎片、丝轴碎片、丝线恢复上限）**：50%
- 当有限类奖励全部达到次数上限（每种最多 2 次）时，自动回退到普通虚拟奖励，保证永远不会空奖励。
- 同时优化了货币保底和拾取点替换的混合池概率（物品 70%、技能 20%、纹章 10%），与全局随机奖励解耦。

### - **Random item probabilities adjusted**

- Rebalanced the random reward pool distribution:
  - **Common virtual rewards (silk, health, blue health, shards, silk parts, full restore, etc.)**: 30%
  - **Random coin pouch (spawns a breakable rosary shrine)**: 20%
  - **Limited rewards (vanilla items, Heart Piece, Spool Part, Silk Heart)**: 50%
- When all limited rewards have reached their quantity cap (2 each), the system falls back to common virtual rewards, ensuring no empty reward.
- Also tuned the mixed pool probabilities for currency pity and pickup replacement (70% item, 20% skill, 10% crest), decoupled from the global random reward logic.

---

### - **其他改进**

- 修复了开启“陷阱生命化”后某些陷阱移动时碰撞箱不同步的问题。
- 修复了重置种子世界后商店缓存未清空，导致新种子下仍显示旧商品的问题。
- 移除了控制台中大量冗余的调试日志，仅保留关键错误和警告信息。

### - **Other improvements**

- Fixed collision box desync when trap movement was enabled on certain traps.
- Fixed shop cache not being cleared when resetting the seed world, causing old items to appear under the new seed.
- Removed numerous redundant debug log entries, leaving only critical errors and warnings.

## v1.6.4

### - **修复商店界面显示与购买逻辑**

商店物品列表现在完全正常：已购买的物品槽位不会凭空消失，未购买的物品会紧凑排列，滚动区域高度自适应，背景无空洞。购买任意物品后，商店界面会立即刷新，剩余物品保持原位并可继续购买。所有商店物品的名称和描述均已接入本地化系统，中英文切换时正确显示对应文本。

### - **Shop display and purchase logic fixed**

Shop item lists now render correctly: purchased slots no longer disappear, unsold items are compactly arranged, scroll area height adapts to visible slots, and background gaps are gone. After any purchase, the shop UI instantly refreshes, leaving remaining items in place and fully purchasable. All shop item names and descriptions are now hooked into the localisation system, displaying correct Chinese/English text without placeholders.

### - **技能获得弹窗全面升级为原生 `SkillGetMsg`**

无论是从拾取点、商店购买还是保底获得的技能，现在都会调用游戏原生的技能获得弹窗（`SkillGetMsg`）。弹窗会正确显示技能的图标、中文名称（如“飞针”“疾风步”）以及对应的纹章槽位信息，与游戏内正常获取技能时的体验完全一致。同时，开局面板中选择的技能也使用原生弹窗。若原生预制体加载失败（极少数情况），会自动回退到自定义弹窗。

### - **Skill popups upgraded to native `SkillGetMsg`**

Skills obtained via pickups, shop purchases, or pity now use the game’s native skill popup (`SkillGetMsg`). Popups correctly show the skill’s icon, Chinese name (e.g., “飞针”, “疾风步”), and crest slot information, matching the vanilla skill acquisition experience. Skills chosen in the startup panel also use the native popup. A fallback custom GUI popup is kept in case the native prefab fails to load (rare).

### - **随机奖励系统彻底重构：统一映射池 + 次数限制 + 新奖励类型**

- **统一固定映射**：所有随机奖励（原版物品、珍贵虚拟物品、普通虚拟资源、念珠、丝线、生命、蓝血、上限提升等）现在共享一个基于种子的全局随机池。相同种子下，每次拾取的奖励序列完全固定且可复现。
- **新增奖励类型**：随机念珠（30%概率，数量10~60）、中灵丝（+3）、满灵丝（+9）、小生命（+2）、大生命（+3）、小蓝血（+2）、大蓝血（+5）、完全恢复（满血+满丝）、面具碎片（+1生命上限）、丝轴碎片（+1丝线容量）、丝之心（+1丝线恢复上限）。
- **次数限制**：原版物品和珍贵虚拟物品（面具碎片、丝轴碎片、丝之心）每个最多获得2次；普通虚拟资源（灵丝、生命、蓝血、念珠、甲壳、灵丝碎片、完全恢复）无次数限制。
- **概率分布**：普通虚拟奖励30%，随机念珠30%，有限类奖励40%。若有限类所有物品已满，则自动回退到普通虚拟奖励。

### - **Random reward system fully rewritten: unified pool + quantity limits + new reward types**

- **Unified deterministic mapping**: All random rewards (vanilla items, precious virtual items, common virtual resources, coin pouches, silk, health, blue health, upgrades) now share a seed‑based global pool. The same seed yields the exact same reward sequence every time.
- **New reward types**: Random coin pouch (30% chance, 10–60 coins), Medium Silk (+3), Full Silk (+9), Small Health (+2), Large Health (+3), Small Blue Health (+2), Large Blue Health (+5), Full Restore (full health + full silk), Heart Piece (+1 max health), Spool Part (+1 silk capacity), Silk Heart (+1 silk regen limit).
- **Quantity limits**: Vanilla items and precious virtual items (Heart Piece, Spool Part, Silk Heart) cap at **2** each; common virtual resources have no limit.
- **Probability distribution**: Common virtual 30%, random coin pouch 30%, limited rewards 40%. If all limited rewards are capped, fallback to common virtual rewards.
