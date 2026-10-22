# Snapshot-SilkSong

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

## Introduction

This is a save state mod for Hollow Knight: Silksong.

It allows you to save the current state of the player, enemies, and map in the game with a key press, and later restore it.

It is generally used for practice purposes, such as repeatedly challenging a boss from a specific point, or as a temporary save to reduce game difficulty.

**The save states in this mod only take effect within the current game session and will be lost after closing the game.**

**! ! ! There is a very small chance that using this mod may cause irreversible damage to your save file. Please back up your saves before using it ! ! !**

## Tutorial

Place the downloaded DLL file into the BepInEx/plugins/ folder and launch the game.

In-game, press the <code>SaveKey</code> to save the current state, then use the <code>LoadKey</code> to return to that saved state.

The specific keys can be modified in the configuration file.

The mod provides four default save slots,user can add new slots through edit the config file:
|<code>SaveKey</code> | <code>LoadKey</code>|
| --- | --- |
|<code>f1</code> | <code>ctrl + f1</code>|
|<code>f2</code> | <code>ctrl + f2</code>|
|<code>1</code> | <code>ctrl + 1</code>|
|<code>2</code> | <code>ctrl + 2</code>|

## Develop Libraries
- 0Harmony.dll
- Assembly-CSharp.dll
- BepInEx.dll
- BepInEx.Harmony.dll
- PlayMaker.dll
- UnityEngine.CoreModule.dll
- UnityEngine.dll
- UnityEngine.InputLegacyModule.dll

## Feedback

Please share your ideas, suggestions, or report bugs in the Issue section. If you encounter a bug, be sure to include a detailed description.

You can also email <code>LingkongSky@gmail.com</code> or join the [QQ Group](https://qm.qq.com/q/ryHSL84Guk) for discussion.

## Another

This mod requires [BepInEx5.4.32 or Newer](https://github.com/BepInEx/BepInEx) or newer and [.Net Framework Runtime 4.8](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48).

You are free to modify and distribute this mod, but do not use it for commercial purposes. The project follows the [MIT License](LICENSE).

# 简体中文

## 介绍

这是一个用于空洞骑士：丝之歌的还原点Mod。
可以通过按键保存当前游戏中角色，敌人，地图的状态并恢复。
一般用于游戏练习，在指定节点重复挑战Boss，也可用做临时存档以降低游戏难度等。

**本Mod的还原点只在当前游戏中生效，关闭游戏后还原点失效。**

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

发送邮件到邮箱<code>LingkongSky@gmail.com</code>

或是加入[QQ群](https://qm.qq.com/q/ryHSL84Guk)进行讨论。

## 其他

本Mod依赖于[BepInEx5.4.32 or Newer](https://github.com/BepInEx/BepInEx) 及 [.Net Framework Runtime4.8](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48)

您可以随意修改及分发该Mod，但请勿用于商业内容，该项目遵守[MIT](LICENSE)协议



