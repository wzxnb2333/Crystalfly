# StubbornHornet - Silksong Mod

## 🎮 Gameplay

### Core Mechanics

StubbornHornet is a **Stubborn Hornet Mod**. Hornet's mind is always fixated on a single direction, and she can only attack in the direction she wants.

### Arrow System

- The player displays **4 vertically arranged arrows** (up/down/left/right) above their head
- **The bottom arrow** is the direction Hornet's mind currently wants
- After successfully executing an action, the arrow queue scrolls down, generating a new direction

### Action Rules

| Action Type        | Input          | Required Direction                                                             |
| ------------------ | -------------- | ------------------------------------------------------------------------------ |
| **Basic Attack**   | Attack button  | Up/Down/Left/Right (based on attack direction)                                 |
| **Charged Attack** | Attack + Hold  | Left/Right (based on character facing)                                         |
| **Dash Attack**    | Dash + Attack  | Left/Right (based on character facing)                                         |
| Harpoon            | Harpoon button | Left/Right (based on character facing)                                         |
| **Spell/Tool**     | Spell button   | Left/Right (based on character facing), Up/Down (based on tool/spell position) |

### Failure Penalty

- When the action direction **does not match** the mind direction, the action will be **blocked** (no attack hitbox / no mana or tool consumption)
- The arrows won't scroll, you need to adjust your direction and try again

### Success

- When directions match, the action executes normally
- The arrow queue scrolls, moving to the next direction

## ⚙️ Settings

- You can **enable/disable** this Mod in the Mod options
- When disabled, all actions are unrestricted, restoring original game behavior

### Configuration Options

| Option        | Default | Description         |
| ------------- | ------- | ------------------- |
| Arrow Count   | 4       | Number of arrows    |
| Arrow Opacity | 1.0     | Arrow transparency  |
| Sound Volume  | 5       | Sound effect volume |

## 📋 Version

- Current Version: 1.0
- Supported Game Version: Silksong
