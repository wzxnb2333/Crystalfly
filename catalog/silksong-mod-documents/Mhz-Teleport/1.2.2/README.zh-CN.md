# 传送模组

> **中文** | [中文](https://github.com/Masaicker/TeleportMod-Silksong/blob/main/README_zh.md)

一款专为《空洞骑士：丝之歌》设计的简易传送模组。

## 开源游戏模组文档

<details>  
<summary><h3>📋 已知问题与解决方案</h3></summary>  

#### 🔧 相机相关问题  
如果发现角色在传送后出现在屏幕外，或相机无法跟随角色移动（角色直接走出屏幕边界），这通常是由于传送过程中跳过了游戏的相机过渡机制所致。有关解决方法，请参考 [Troubleshooting Guide](#troubleshooting) 中的“安全重生”功能。

#### ⚠️ 传送到未解锁入口点  
传送和“安全重生”功能在某些情况下可能会将角色传送到未解锁的入口点。由于这些入口可能存在障碍物，角色可能被“推入”到意外封闭区域中。如果你发现自己被困在一个无法正常退出的场景中，请尝试：  
- 使用 **长椅传送** 功能返回至最近一次存档点  
- 或使用 **紧急传送** 返回安全位置  
- 暂时避免在该区域保存坐标  

📍 **特别说明**： “记忆之室”场景（需双跳才能到达）容易出现此类问题。若在解锁双跳前被传送到此场景，将被困其中，请务必格外小心。

#### 🗺️ 地图键持续按压导致传送不完整  
在跨场景传送时，如果传送组合键包含地图键（手柄默认为LB），持续按住地图键可能导致仅传送至场景入口而非目标坐标。当出现此情况时：  
- 松开地图键  
- 再次尝试传送至相同坐标保存点  
- 或使用“安全重生”功能  

💡 **提示**：虽然该操作方式较为罕见，但无论是键盘还是手柄都可能触发此问题。

</details>

### 使用说明

#### 键盘控制  
- `Ctrl + 1~5`：快速存档位置 🔊 音效  
- `Alt + 1~5`：快速加载位置（无存档数据时自动传送至长椅）  
- `Alt + 6`：**安全重生**（循环入口点）  
- `Alt + 7`：**长椅传送**（最近一次重生点）  
- `Alt + 0`：**重置所有坐标**  
- `Alt + - [minus key]`：**紧急传送**（至预设安全位置）  
- 🌟 ``Ctrl + ` [波浪键]``: **Open Teleport Interface** - Unlimited save slots with UI support for save, load, delete, overwrite, bench teleport, reset coordinates, safe respawn, and more
- **🆘 `Ctrl + F9`: Emergency return to main menu** (character out of control/stuck)

```
💡 Note: Default uses main keyboard number keys, NOT numpad keys. All key combinations are fully customizable in the config file, including modifier keys and function keys!
```

#### Gamepad Controls
- `LB + RB + Direction/A`: Teleport to slot
- `LB + Start + Direction/A`: Save to slot 🔊 Sound effect
- `LB + RB + Y`: **Safe respawn**
- `LB + RB + B`: **Teleport to bench** (last respawn point)
- `LB + Select + Start`: **Reset all coordinates**
- `LB + RB + X`: **Emergency teleport** (to preset safe location)

```
💡 Note: All gamepad controls are fully customizable to match your personal preferences.
```

#### 🎮 Gamepad Key Reference

**Direction Keys:**
- `DPadUp` = D-Pad Up
- `DPadDown` = D-Pad Down  
- `DPadLeft` = D-Pad Left
- `DPadRight` = D-Pad Right

**Face Buttons:**
- `JoystickButton0` = A Button
- `JoystickButton1` = B Button
- `JoystickButton2` = X Button
- `JoystickButton3` = Y Button

**Shoulders & Triggers:**
- `LeftBumper` = Left Bumper (LB)
- `RightBumper` = Right Bumper (RB)
- `LeftTrigger` = Left Trigger (LT)
- `RightTrigger` = Right Trigger (RT)

**System Buttons:**
- `JoystickButton6` = Select/Back Button
- `JoystickButton7` = Start Button
- `JoystickButton8` = Home/Guide Button

**Default Gamepad Configuration:**
- Slot 1: `DPadUp` (D-Pad Up)
- Slot 2: `DPadDown` (D-Pad Down)
- Slot 3: `DPadLeft` (D-Pad Left)
- Slot 4: `DPadRight` (D-Pad Right)
- Slot 5: `JoystickButton0` (A Button)
- Teleport Modifiers: `LeftBumper` + `RightBumper` (LB + RB)
- Save Modifiers: `LeftBumper` + `JoystickButton7` (LB + Start)
- Safe Respawn: `JoystickButton3` (Y Button) [in teleport mode]
- Hardcoded Teleport: `JoystickButton2` (X Button) [in teleport mode]
- Bench Teleport: `JoystickButton1` (B Button) [in teleport mode]
- Reset All: `LeftBumper` + `JoystickButton6` + `JoystickButton7` (LB + Select + Start)

```
⚙️ All gamepad controls can be customized in the game's config file.
💡 If older version config entries are too numerous and affect viewing, you can delete the .cfg config file and restart the game to automatically generate the latest configuration.
```

### Safety Guidelines

> **⚠️ Important: Please follow these safety guidelines to avoid game bugs and data corruption!**

#### When it's safe to use:
✅ Only save or teleport when your **character is fully controllable**  
✅ In **normal game scenes**, when **not in combat**

#### Dangerous situations - DO NOT use:
❌ **During boss battles**  
❌ **Inside closed combat areas**  
❌ **During cutscenes or animations**  
❌ **When character is controlled or immobilized**  
❌ **During any special states or triggered events**

#### Important Notes:
⚠️ **Avoid multiple teleportations in short time** (like teleporting multiple times within 1 second)  
⚠️ **Do not teleport immediately after death**

---

### Troubleshooting:

#### 🆘 If stuck/camera lost/character floating and unable to open menu:

1. **First try: Safe Respawn** (Keyboard: `Alt+6`, Gamepad: `LB+RB+Y`)  
   If teleported to entry point, then use load position as needed  
   > 💡 Example: If character is off-screen after loading save, or camera doesn't follow character movement, use `Alt+6` safe respawn, then reload the same save slot coordinates to fix camera/view issues

2. **Character completely out of control, or character floating and not landing: Emergency return to main menu** (Keyboard: `Ctrl+F9`, no gamepad shortcut)  
   > ⚠️ **Important**: **Do NOT use emergency return to main menu when character is dead**

3. **Still not working, completely unresponsive: Restart the game**

---

#### 🚨 All save data lost or stuck in a scene:

If all your **save slots are lost** or you're trapped in an unescapable scene, use **Emergency Teleport** (Keyboard: `Alt+-` [minus key], Gamepad: `LB+RB+X`) to instantly teleport to a **preset safe location** (starting town station).

> **This works independently of your save data**

---

### Emergency rescue config:

If all methods fail, you can **manually edit the save file** with this safe config:

> ⚠️ **Important:** **Close the game first**, modify and save, then restart the game, and load slot 1 after entering the game (Keyboard: `Alt+1`, Gamepad: `LB+RB+Up`)

```json
{
  "saveSlots": {
    "1": {
      "x": 71.42231,
      "y": 9.597684,
      "z": 0.004,
      "scene": "Bellway_01",
      "hasData": true
    }
  }
}
```

**Slot 1 defaults to the station in the starting town, which is absolutely safe.**

**Config file location:**  
`C:\Users\[Username]\AppData\LocalLow\Team Cherry\Hollow Knight Silksong\TeleportMod\savedata.json`

### Installation

1. Install BepInEx
2. Extract and put the `Teleport` related folder into `BepInEx/plugins/` folder
3. Ensure `Teleport.dll`, `manbo.wav`, and `Gamesave.wav` are in the same directory under the folder
4. Start game

### Config

- `Enable Detailed Logging` (default: `false`): Show detailed logs
- `Enable Gamepad Support` (default: `true`): Enable controller input
- `Enable Easter Egg Audio` (default: `false`): 🎵 Enable special sound effect for saving
- `Audio Volume` (default: `0.5`): 🔊 Volume level for save sound effect (0.0-1.0, 0=disable)
- `Save Modifier Key` (default: `LeftControl`): Modifier key for saving coordinates
- `Teleport Modifier Key` (default: `LeftAlt`): Modifier key for teleporting
- `Reset Modifier Key` (default: `LeftAlt`): Modifier key for reset functions
- `Slot 1-5 Keys` (default: `Alpha1-5`): Keys for save slots
- `Safe Respawn Key` (default: `Alpha6`): Key for safe respawn function
- `Bench Teleport Key` (default: `Alpha7`): Key for bench teleport function
- `Emergency Teleport Key` (default: `Minus`): Key for emergency teleport function
- `Reset All Key` (default: `Alpha0`): Key for reset all function

#### Data Storage
Coordinate data path:  
`C:\Users\[Username]\AppData\LocalLow\Team Cherry\Hollow Knight Silksong\TeleportMod\savedata.json`

Configuration file location:  
`{Game Install Directory}\BepInEx\config\Mhz.TeleportMod.cfg`

```
💡 Note: If the config file or folder doesn't exist, please run the game once to generate it first.
```
