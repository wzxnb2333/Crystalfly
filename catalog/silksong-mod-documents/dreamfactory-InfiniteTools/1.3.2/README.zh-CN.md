

# 无限工具模组

一个专为《空洞骑士：丝之歌》设计的 BepInEx 模组，可为选定的工具提供无限资源。  
你还可以选择工具何时补给：每次使用工具时、每次坐在长椅上时，或每次受到治疗时。

## 配置

首次启动后，请编辑您 Silksong 文件夹中的 `BepInEx/config/com.dream.InfiniteTools.cfg` 来选择哪些工具应拥有无限资源。  
如果您希望启用某个工具，只需将该工具前的 false 改为 true 即可。  
如果想设定工具何时补给，请修改这行内容：```Replenishment Method = EveryUse```。您可以在上方找到相关说明注释。  
请注意，如果您希望部分工具作弊功能被禁用，则必须将“所有工具无限使用”设置为 false。

### 配置示例

```ini
[Infinite Tools]
Replenishment Method = EveryUse
Enable Infinite Use for All Tools = false
Enable Infinite Use for Cogfly = true
Enable Infinite Use for Cogwork Wheel = false
```
  
仅在使用时自动补给齿轮飞镖，无需每次消耗资源。

## 安装  

```ini
1.Install BepInEx
2.Download the zip and extract the .dll into your BepInEx/plugins folder.
3.Setup you config file as you want.
4.Enjoy!
```