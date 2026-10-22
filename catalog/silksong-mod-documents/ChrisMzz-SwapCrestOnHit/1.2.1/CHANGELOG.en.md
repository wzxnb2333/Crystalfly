# Changelog

## v1.2.1

### Bugfixes

Fixed crests sometimes changing when Hornet was not taking damage due to invulnerability. Situations included using Sharpdart, or parrying an enemy and temporarily entering their hitbox for example.
(I fixed this by postfixing `HeroController.HeroDamaged` instead of `HeroController.TakeDamage`)

## v1.2.0

### New features

Implemented a cooldown to ensure the swapped crest would be different from previous one (because of invicibility frames, it was possible for the crest to swap multiple times a second, and therefore would sometimes loop back to the previous crest, which was unintended).

### Bugfixes

Fixed an index out-of-bounds error in the crest list which under quite rare circumstances, could make Hornet take damage until death.

## v1.1.2

### Bugfixes

Compatibility fix. Works the same but will probably work better with other mods.

## v1.1.1

### Bugfixes

Fixed getting base version of Hunter Crest regardless of upgrades.

## v1.1.0

### New features

Crest Sanity option in config menu. Whether to use only unlocked crests (Crest Sanity = true) or all crests in the game (Crest Sanity = false).

### Bugfixes

Fixed bug where silk would sometimes be removed on taking damage. (at least it should be fixed)
