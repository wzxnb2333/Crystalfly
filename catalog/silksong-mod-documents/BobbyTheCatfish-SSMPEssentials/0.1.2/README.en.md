# SSMP Essentials

An addon for Silksong Multiplayer (SSMP) that adds several useful utilities for multiplayer games.
Each module can be disabled with a command, so you can use it even if you only want one feature.

## Teleports
Ever wanted to teleport to your friend quickly, or gather everyone in one spot? Well you're in luck!

- `/tp <username>` Sends a teleport request to another player
- `/tpa` (`/tpaccept`) Accepts a teleport request
- `/tpd` (`/tpdeny`) Rejects a teleport request
- `/back` Teleports you back to where you were before you teleported or died.
- `/huddle` Teleports everyone to you. Must be authorized to use.
- `/huddle <username>` Teleports everyone to a specific player. Must be authorized to use.


## Spectating/Freecam
View the world of Pharloom from the perspective of other players! You can view anyone's perspective as long as you're in the room with them.
In addition, freecam is available to freely spectate your current room.

### Keybinds
I'd recommend changing these. You can use BepInEx Config Manager or ModMenu.
- `2` - Spectate previous player
- `3` - Spectate next player
- `4` - Exit spectate/freecam mode
- `5` - Freecam

Pausing the game also exits spectate/freecam mode.


## Healthbars
Healthbars can be displayed above players usernames. They reflect the player's actual healthbar,
showing current health, missing masks, and lifeblood masks.


## Death Messages
When you inevitably die, the game will send out a death message, informing other players of your demise.
These messages are similar in style to the ones found in Minecraft.


# Configuration
`/essentials set <setting> <true/false>` (`/e set`) will toggle a module on or off for all players.

Valid options are:
- `huddle` Toggles ability to do `/huddle`
- `tp` Toggles ability to do `/tp`
- `back` Toggles ability to use `/back`
- `tprequests` If true, `/tp` requires a `/tpa` response. If false, players are teleported immediately.
- `deathmessages` Toggles death messages
- `healthbars` Toggles all healthbars
- `spectate` Toggles ability to spectate other players
- `spectateteam` If enabled, players can only spectate other players that are on their team.
- `freecam` Toggles ability to use freecam

By default, all options (except for spectateteam) are enabled.