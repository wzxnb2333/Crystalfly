Every professional mod uploaded to Thunderstore requires a properly formatted markdown file
( README.md ) in its root folder. This file serves as the main description landing page where
players learn what your custom DLC boss expansion adds, how to activate it, and configuration
details.

1. Copy the Markdown Text Below
Create a file named exactly README.md inside your modding project folder using a raw text
application like Notepad or VS Code, and paste this entire template into it:

# Custom DLC Boss Expansion
An extensive combat mod for *Hollow Knight: Silksong* that injects a brand new,
highly intense custom boss encounter into the game engine via BepInEx and Harmony
hooks.
## Key Features
* **Custom DLC Boss Encounter:** Face off against an entirely original, multi-phase
boss fight complete with unique AI behavior patterns.
* **Intelligent Scaling:** The boss scales dynamically depending on your active
shell variants and health thresholds.
* **Mod Manager Native Support:** Completely optimized to run instantly alongside
other custom add-ons inside Thunderstore.
## Installation Instructions
For a seamless installation, we highly recommend using the Thunderstore Mod Manager
or r2modman.
### Option A: Automatic Setup (Recommended)
1. Open your **Thunderstore Mod Manager** app.
2. Search for `Custom DLC Boss Expansion` in the online directory.
3. Click **Install with Dependencies**.
### Option B: Manual File Setup
1. Download the mod `.zip` archive from Thunderstore.
2. Locate your *Hollow Knight: Silksong* install path.
3. Extract the contents into your local directory inside the following path:
`Silksong/BepInEx/plugins/`
4. Confirm that the `SilksongBossMod.dll` file rests directly inside that folder.
## How to Trigger the Battle
* Locate the newly generated ancient seal object placed within the core trial hubs

1

of Pharloom.
* **Developer Quick-Test Key:** For mod testers and developers, you can forcefully
trigger the test sequence by pressing the `B` key on your keyboard while inside any
active combat or staging room.
## Dependency Requirements
* **BepInEx Pack** (v5.4.21 or higher)
* Compatible with standard PC game configurations.
## Version History
* **1.0.0** - Initial release containing core boss behaviors, room generation
triggers, and basic movement phases.

Pro-Tip for Package Verification: Make sure you do not save this file as README.md.txt . In
Windows Explorer, check that "File name extensions" is enabled to ensure the extension reads
precisely as .md before compressing it into your final upload archive.