# 快照-SilkSong

 **- [English](#English)**
 - [Introduction](#introduction)
 - [Tutorial](#tutorial)
 - [Develop Libraries](#develop-libraries)
 - [Feedback](#feedback)
 - [Another](#another)

 **- [简体中文](#简体中文)**
 - [介绍](#介绍)
 - [使用教程](#使用教程)
 - [开发依赖项](#开发依赖项)
 - [反馈](#反馈)
 - [其他](#其他)

# English

## 引言

这是一个专为《空洞骑士：丝之歌》设计的存档/即时存档模组。

它允许玩家通过按键保存当前游戏状态（包括玩家、敌人和地图），并在之后恢复该状态。

此模组通常用于练习目的，例如从特定点位反复挑战首领，或作为临时存档以降低游戏难度。

**本模组中的存档仅在当前游戏会话内有效，关闭游戏后将丢失。**

**! ! ! 使用本模组存在极小概率可能导致你的存档文件不可逆损坏，请使用前务必备份存档！ ! ! !**

## 教程

将下载的 DLL 文件放入 BepInEx/plugins/ 文件夹中并启动游戏。

在游戏中，按住 <code>SaveKey</code> 保存当前状态，然后使用 <code>LoadKey</code> 返回到该已保存的状态。

具体按键可在配置文件中进行修改。

本模组提供四个默认存档槽位，用户可通过编辑配置文件添加新的存档槽位：
|<code>SaveKey</code> | <code>LoadKey</code>|
| --- | --- |
|<code>f1</code> | <code>ctrl + f1</code>|
|<code>f2</code> | <code>ctrl + f2</code>|
|<code>1</code> | <code>ctrl + 1</code>|
|<code>2</code> | <code>ctrl + 2</code>|

## 开发库  
- 0Harmony.dll  
- Assembly-CSharp.dll  
- BepInEx.dll  
- BepInEx.Harmony.dll  
- PlayMaker.dll  
- UnityEngine.CoreModule.dll  
- UnityEngine.dll  
- UnityEngine.InputLegacyModule.dll

## 反馈

请在问题部分分享您的想法、建议或报告错误。如果遇到bug，请务必提供详细描述。

您也可以通过电子邮件联系 <code>LingkongSky@gmail.com</code>，或加入 [QQ Group](https://qm.qq.com/q/ryHSL84Guk) 进行讨论。

## 另一个

此模组需要 [BepInEx5.4.32 or Newer](https://github.com/BepInEx/BepInEx) 或更高版本以及 [.Net Framework Runtime 4.8](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48)。

You are free to modify and distribute this mod, but do not use it for commercial purposes. The project follows the [MIT License](LICENSE).

# 简体中文

## 介绍

这是一个用于空洞骑士：丝之歌的还原点Mod。  
可以通过按键保存当前游戏中角色、敌人、地图的状态并恢复。  
一般用于游戏练习，在指定节点重复挑战首领，也可用作临时存档以降低游戏难度等。

**本Mod的还原点仅在当前游戏中生效，关闭游戏后还原点失效。**

**! ! ! 使用该Mod存在极小的可能性使得存档出现不可逆的错误，请在使用前注意备份存档 ! ! !**

## 使用教程

将下载好的Dll文件放入BepInEx/plugins/文件夹中，启动游戏即可。

进入游戏后，可按下<code>SaveKey</code>可对当前状态进行保存

再通过按下<code>LoadKey</code>来回到之前保存的状态。

具体按键可通过配置文件进行修改。
插件默认提供四个槽位，用户可通过配置文件来新增任意槽位：

|<code>SaveKey</code> | <code>LoadKey</code>|
| --- | --- |
|<code>f1</code> | <code>ctrl + f1</code>|
|<code>f2</code> | <code>ctrl + f2</code>|
|<code>1</code> | <code>ctrl + 1</code>|
|<code>2</code> | <code>ctrl + 2</code>|

## 开发依赖项
- 0Harmony.dll
- Assembly-CSharp.dll
- BepInEx.dll
- BepInEx.Harmony.dll
- PlayMaker.dll
- UnityEngine.CoreModule.dll
- UnityEngine.dll
- UnityEngine.InputLegacyModule.dll

## 反馈

请在Issue中提出您的想法与意见，如果发现Bug，请务必带上详细的情况描述。

发送邮件至邮箱<code>LingkongSky@gmail.com</code>

或加入[QQ群](https://qm.qq.com/q/ryHSL84Guk)进行讨论。

## 其他

本模组依赖于[BepInEx5.4.32 or Newer](https://github.com/BepInEx/BepInEx) 及 [.Net Framework Runtime4.8](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48)

您可以随意修改及分发该Mod，但请勿用于商业内容，该项目遵守[MIT](LICENSE)协议
