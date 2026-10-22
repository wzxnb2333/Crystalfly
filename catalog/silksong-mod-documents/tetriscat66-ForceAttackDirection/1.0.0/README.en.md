# Force Attack Direction 

Allows you to force your attack direction to up, down, or horizontal. Also includes an option to disable grounded sprint attacks. 

Some use cases of this mod include challenge runs, platforming sections where you don't want to have to hold down to pogo, and flea bounce. 

## Config Settings 
#### "Attack Direction" 
- Can be set to "NotForced, "Horizontal", "Up", or "Down" to change the forced attack direction. 
- If using BepInExConfigurationManager this setting can be changed at any time and will update its effects in game immediately. 
#### "Sprint Attack" 
- Can be set to "NoAttack", or "SprintAttack" to optionally block sprint attacks. 
- If using BepInExConfigurationManager this setting can be changed at any time, but will only update its effects in game when near a bench (does not require you to sit at it). This is because this behaviour involves editing the Sprint FSM, which for performance reasons probably shouldn't be done constantly. 