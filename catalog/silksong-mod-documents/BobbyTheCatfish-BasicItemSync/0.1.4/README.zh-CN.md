# BasicItemSync  
在 SSMP 中同步玩家之间的物品及其他关键进度。  
详见 [commands section](#commands) 以了解本模组同步的所有内容。

> 该模组仍处于 beta 版本。请将 bug 提交至：https://github.com/BobbyTheCatfish/Silksong-BasicItemSync/issues

## 重要使用说明  
BasicItemSync 目前仅支持完全同步的游玩模式。  
这意味着 **所有玩家都需要从全新的存档文件开始**，且 **新玩家不应在游戏进行中加入**。  
获得的物品只会发送给当前连接服务器的玩家。**对于新加入的玩家，这些物品不会被重新发送。**

## 安装
BasicItemSync 可由任何 Thunderstore 兼容的模组管理器自动安装。

### 手动安装
1) 安装 [SSMP](https://thunderstore.io/c/hollow-knight-silksong/p/SSMP/SSMP) 和 [AssetHelper](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/AssetHelper/)。  
2) 下载并解压 BasicItemSync  
3) 进入你的 BepInEx 插件文件夹  
4) 将解压后的模组文件夹复制到插件文件夹中（示例：`plugins/BasicItemSync/BasicItemSync.dll`）

## 命令  
```
/sync-ui
```
 显示一个图形界面，相当于 `/sync`，可一次性设置多个选项。  

```
/sync [module] [true/false]
```
 启用或禁用以下模块之一的同步功能。未指定时，默认启用模块。  
- 能力  
	- 移动  
	- Needolin  
	- 针刺攻击  
- 地图  
- 标记  
	- Shakra 标记  
	- 跳蚤标记  
- 货币  
	- 贝壳碎片  
	- 念珠  
	- 箱子  
	- 不从茧中获得收益  
- SpendingCurrency（默认值：false）  
	- 商店和捐赠中的所有金钱支出  
	- Snitchflies  
	- 死亡时不损失金钱  
- 任务  
	- 任务完成  
	- 任务奖励  
- QuestItems（默认值：false）  
- 首领  
	- + 空洞质量  
- 比赛场  
- 进展  
	- 关键物品（钥匙、旋律等）  
	- 铃铛圣殿  
	- 通行长椅  
	- 重要 NPCs  
- 跳蚤  
- 收集品  
	- 普通物品  
	- 宝物  
	- 记忆之物  
	- Bellhome 布置装饰  
- 升级  
	- 面具  
	- 丝线卷轴  
	- 丝心  
	- 制作套件  
	- 工具包  
	- 针具升级  
- 工具  
	- 颜色工具  
	- 丝线技能  
- 纹章  
	- Vesticrest  
- 交通  
	- 铃铛之路  
	- Ventricas  
- 快捷方式  
	- 拉杆  
	- 压力板  
	- 可破坏墙壁