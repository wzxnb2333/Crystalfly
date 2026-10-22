# 空洞骑士的荣耀

空洞骑士的荣耀会修改特定区域的游戏资产，使其外观和感受符合游戏背景设定中的过去状态。

该模组**不会**添加任何新的游戏场景，仅对现有场景进行修改，通过解析包含自定义场景调整的文本文件来替换纹理并添加游戏资产。然而，由于房间布局保持不变，整体玩法并未发生显著变化，这些改动通常仅为视觉上的。

## 主要特性

* 将“爆裂步道”修改为“珊瑚步道”：
	* 移除风效
	* 将纹理中的大部分沙粒和骨质替换为珊瑚材质
	* 重新设计背景，增加更多精灵与鱼类粒子效果
	* 实现自定义地图区域颜色及全图区域纹理
	* 添加原创背景音乐

* 将“卡拉之沙”修改为“红珊瑚峡谷”：
	* 移除风效
	* 将所有沙粒和骨质纹理替换为珊瑚材质
	* 重新设计背景，增加更多精灵与鱼类粒子效果
	* 将“沙雕师”替换为贯穿整个区域的河流
	* 实现自定义全图区域纹理
	* 重新添加 [cut Coral Gorge soundtrack](https://hollowknight.wiki/w/Cut_Content_%28Silksong%29#Audio "Silksong wiki cut content page, audio section")

* 对对话和用户界面文本进行相应修改

### 计划中的特性

将现代版“维尔丹尼亚”替换为失落的维尔丹尼亚，并使其在第 2 关卡中可访问。

## 手动安装

### 不使用模组管理器
在 `your-silksong-install-location/Hollow Knight Silksong/BepInEx/plugins/` 中创建一个新文件夹，并命名为 `PharloomsGlory`。从 Thunderstore 下载模组 zip 文件，将 `PharloomsGlory.zip/plugins/` 的内容放入你刚刚创建的文件夹中。最终结构应为：

```
Hollow Knight Silksong/
> BepInEx/
  > plugins/
    > PharloomsGlory/
      > Audio/
      > Scenes/
      > Text/
      > Textures/
      > PharloomsGlory.dll
```


### 使用 r2modman
在 r2modman 中导航至 `Settings > All > Browse profile folder` 打开你的配置文件夹。在 `folder-you-just-opened/BepInEx/plugins/` 中创建一个新文件夹，并命名为 `Norrie_Boy-Pharlooms_Glory`。从 Thunderstore 下载模组 zip 文件，将 `PharloomsGlory.zip/plugins/` 的内容放入你刚刚创建的文件夹中。最终结构应为：

```
your-profile-folder/
> BepInEx/
  > plugins/
    > PharloomsGlory/
      > Audio/
      > Scenes/
      > Text/
      > Textures/
      > PharloomsGlory.dll
```

## Credits

The audio system in this plugin was adapted from Ashiepaws's [Patchwork](https://github.com/Ashiepaws/silksong-patchwork) mod, please check it out.

All textures come from Team Cherry and are unused Silksong assets in the game files.

## 截图

珊瑚阶梯

![](https://raw.githubusercontent.com/norrie-boy/pharlooms-glory/refs/heads/main/Images/CoralSteps1.jpg)

![](https://raw.githubusercontent.com/norrie-boy/pharlooms-glory/refs/heads/main/Images/CoralSteps2.jpg)

红珊瑚峡谷

![](https://raw.githubusercontent.com/norrie-boy/pharlooms-glory/refs/heads/main/Images/RedCoralGorge1.jpg)

![](https://raw.githubusercontent.com/norrie-boy/pharlooms-glory/refs/heads/main/Images/RedCoralGorge2.jpg)
