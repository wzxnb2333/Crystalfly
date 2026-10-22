<div align="center">

# ✦ SSMP Enemy Syncing Addon ✦

### Comprehensive Enemy, Boss & Revive Syncing for Hollow Knight: Silksong Multiplayer (SSMP)

[![Version](https://img.shields.io/badge/version-1.0.1-blueviolet?style=for-the-badge)](https://github.com/Brothergaming52/EnemySyncing/releases)
[![Game](https://img.shields.io/badge/game-Hollow%20Knight:%20Silksong-red?style=for-the-badge)](https://www.teamcherry.com.au/)
[![SSMP](https://img.shields.io/badge/SSMP-Silksong%20Multiplayer-orange?style=for-the-badge&logo=github)](https://github.com/Extremelyd1/SSMP)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Me-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/brothergaming52)

**Real-Time Enemy Sync · Boss Phase Sync · Downed Cocoon System · Teammate Revives**

---

</div>

## 🧩 What is SSMP Enemy Syncing Addon?

**SSMP Enemy Syncing Addon** is an expansion mod for **[Hollow Knight: Silksong Multiplayer (SSMP)](https://github.com/Extremelyd1/SSMP)**. It enables complete, real-time multiplayer synchronization for all standard enemies, mini-bosses, and complex multi-phase bosses throughout Pharloom. 

In addition to enemy synchronization, it introduces a **Co-op Downed & Cocoon Revive System**, allowing teammates to rescue each other in battle!

> [!WARNING]
> ### ⚠️ Work in Progress & Development Disclaimer
> **This mod is still actively in development and is not perfect.** You may encounter desyncs, glitches, or **game-breaking bugs** during multiplayer sessions.
> 
> Please back up your save files, report bugs on the [GitHub Issues](https://github.com/Brothergaming52/EnemySyncing/issues) page, and share your feedback!


---

## ✨ What's New in v1.0.1

- 🔌 **Native BepInEx Plugin Integration:** Added `EnemySyncingPlugin` entry point (`bg52.enemysync.ssmp`) allowing the mod to be loaded directly by BepInEx and managed via Thunderstore / r2modman seamlessly.
- 🎯 **Boss Aggro & Target HUD Indicators:** When a boss targets a player in combat, that player's nametag dynamically turns red, giving immediate tactical feedback to teammates.
- 💀 **Corpse Snapshot & Death Sync:** Enemy deaths now synchronize exact knockback velocity, position, rotation, and attack angles across all clients.
- 🛡️ **Scene Owner Death Verification:** Implemented authoritative death validation (`VerifyEnemyDieRequest`) to prevent client-side desync and duplicate phantom enemies.
- 🏹 **Enhanced Projectile Synchronization:** Added support for projectile animation clip states, custom scaling (`ScaleX`), and improved trajectory tracking.
- 🧠 **PlayMaker FSM Target Patches:** Overhauled FSM distance/position calculations (`GetDistance`, `GetXDistance`, `GetYDistance`, `GetPosition`) so enemies consistently navigate toward their active network target.
- 🔄 **Anti-Duplication & Desync Recovery:** Background watchdog monitors and restores missing scene enemies while cleaning up stale or dead entities.

---

## ⚡ Core Features

### ⚔️ Complete Network Synchronization

| Feature | Description |
|---|---|
| **Transform & Animation** | Smooth interpolation for enemy position, rotation, scale, and active animation states |
| **Damage & Health** | Synchronized health bars, damage tracking, Scene Owner death verification, and remote death triggers |
| **Projectile Sync** | Synchronized enemy projectiles, spell attacks, custom scales, and spawned sub-objects |
| **FSM & Boss Phases** | Syncs PlayMaker Finite State Machines (FSMs) & child state changes across multi-phase boss fights |

### 👑 Dynamic Authority & Claiming System

- **Proximity & FOV Claiming:** The nearest player looking at an enemy automatically claims authority to control its AI and movement.
- **Boss Target Highlighting:** Players currently targeted by active bosses are highlighted with a red username tag.
- **Host Fallback:** Unclaimed or distant enemies cleanly fall back to host-side simulation to eliminate desync.
- **Auto-Spawner for Missing Enemies:** Automatically instantiates missing scene prefabs for late-joining players.

### 🕸️ Downed & Cocoon Revive System

- **Multiplayer Downed State:** Upon taking fatal damage, players enter a protective **Cocoon state** instead of instantly dying.
- **Teammate Revives:** Nearby teammates can strike a fallen player's cocoon with their weapon to break them free and revive them back into action.
- **Hazard Auto-Settling:** Cocoons falling into spikes or environmental hazards automatically settle onto the nearest safe ground.
- **Hold-to-Suicide Action:** Stuck or isolated players can hold their **Quick Map key** (default: `TAB` / Controller equivalent) to surrender and respawn.
- **Team-Wipe Logic:** If all players in the active scene are cocooned simultaneously, the mod triggers a synchronized native team defeat.

---

## 🎮 How It Works

```
1. Take Fatal Damage  →  Player enters Cocoon State (Invulnerable to hazards)
2. Teammate Hits      →  Striking the cocoon reduces HP & breaks player free!
3. Team Wipe Check    →  If everyone is downed → Sync'd team defeat & respawn
```

---

## 📥 Installation Guide

### Via Thunderstore / r2modman (Recommended)
1. Install **SSMP Enemy Syncing Addon** directly through the **Thunderstore Mod Manager** or **r2modman**.
2. Launch the game via the mod manager.

### Manual Installation
1. **Prerequisites:**
   - **Hollow Knight: Silksong**
   - **[BepInExPack for Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)**
   - **[SSMP (Silksong Multiplayer)](https://github.com/Extremelyd1/SSMP)**
2. **Install Addon:**
   - Extract `EnemySyncing.dll` into your BepInEx plugins directory:
     `path\to\Hollow Knight Silksong\BepInEx\plugins\EnemySyncing\` (or inside `BepInEx\plugins\SSMP\`).
3. **Run the Game:**
   - Launch Silksong. BepInEx and SSMP will automatically load `EnemySyncing.dll`.
   - **Note:** Both Host and joining clients must have `EnemySyncing` installed.

---

## ⚙️ Keybinds & Controls

| Action | Control |
|---|---|
| **Revive Teammate** | Melee Attack (`Nail / Needle`) on a remote player's Cocoon |
| **Cocoon Suicide / Give Up** | Hold **Quick Map Key** (Default: `TAB` or Controller Button) while cocooned |

---

## 🛠️ Building from Source

### Requirements
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- Visual Studio 2022 / JetBrains Rider / VS Code

### Steps

```bash
# 1. Clone the repository
git clone https://github.com/Brothergaming52/EnemySyncing.git
cd EnemySyncing

# 2. Build the solution
dotnet build EnemySyncing.csproj -c Release
```

The compiled binary will be placed in `bin/Release/netstandard2.1/EnemySyncing.dll`.

---

## 🙌 Credits & Acknowledgements

- **Brothergaming52** – Creator & Lead Developer
- **FieryCGamingz** – Addon Tester & Quality Assurance

---

## ☕ Support the Project

<div align="center">

If this addon improved your Silksong co-op experience, consider supporting the development & testing team!

[![Brothergaming52 Ko-fi](https://img.shields.io/badge/Brothergaming52-Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/brothergaming52)
[![FieryCGamingz Ko-fi](https://img.shields.io/badge/FieryCGamingz%20(Tester)-Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/fierycgamingz)

**[ko-fi.com/brothergaming52](https://ko-fi.com/brothergaming52)** · **[ko-fi.com/fierycgamingz](https://ko-fi.com/fierycgamingz)**

</div>

---

## 📜 License

This project is licensed under the [MIT License](LICENSE).

---

<div align="center">

**Made with ❤️ by Brothergaming52**

[SSMP Project](https://github.com/Extremelyd1/SSMP) · [Ko-fi](https://ko-fi.com/brothergaming52) · [GitHub Repo](https://github.com/Brothergaming52/EnemySyncing)

</div>
