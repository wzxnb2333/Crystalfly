# Shaman Dive Bind 

Makes the Shaman Crest bind function more similarly to Desolate Dive. Also removes the bind's maximum fall distance/time limit, and makes Injector Band double the bind's fall speed. 

This mod has a soft dependency on I18N in order to update the crest description. It is listed as a required mod on Thunderstore but if you don't want to use it this mod will work without it. 

## Behaviour 
The dive bind consists of two damaging hitboxes. The first hitbox (dive hitbox) is smaller and does 0.75x needle damage. The second hitbox (shockwave hitbox) is larger and does 2.5x needle damage. Both of these are affected by Volt Filament's and Shaman Crest's Silk Skill damage multipliers, and with Volt Filament it applies 1 zap damage tick. 

Upon hitting the ground, you are granted 0.4 seconds of invulnerability frames, the same amount as Desolate Dive. 

## Damage Calculation 
Assuming Volt Filament is equipped, you're using a max upgraded needle, and the enemy has a 1x damage modifier, then it would do (21)(0.75)(1+0.4+0.25)(1)=26 damage on the first hit, and (21)(2.5)(1+0.4+0.25)(1)=87 on the second hit, for a total of 118 damage (because of the +5 from Volt Filament's zap damage tick). 

For reference, this is how it compares to the damage of the other Silk Skills:

| Silk Skill                  | Damage |
| --------------------------- | ------ |
| Rune Rage                   | 78-215 |
| Pale Nails                  | 109    |
| Cross Stitch                | 110    |
| Silk Spear                  | 114    |
| Dive Bind                   | 118    |
| Thread Storm (Not Extended) | 118    |
| Sharpdart                   | 127    |
| Thread Storm (Extended)     | 142    |

*Some of the damage numbers may be off by a couple points due to how the game rounds values ending in ".5", and Rune Rage's numbers may be off because its damage calculation is more complicated.

## Hitbox Screenshots 

Dive Hitbox 

![](https://github.com/Tetriscat66/SylasThunderstoreImages/blob/main/Silksong.ShamanBindBuff/DiveHitbox1.png?raw=true)

##

Shockwave Hitbox (lingers slightly after the animation ends) 

![](https://github.com/Tetriscat66/SylasThunderstoreImages/blob/main/Silksong.ShamanBindBuff/DiveHitbox2.png?raw=true)

## Mod Dependencies 
- FsmUtil (Required) 
- I18N (Optional, used for the crest description) 