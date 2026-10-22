# DanielSteginkUtils
A code library containing various helper classes and logic I've accumulated in my modding journey.

## General structure
- ExternalFiles - Utilities for interacting with external files such as AssetBundles
	- GetAssetBundle - Loads asset bundles
	- GetAudioClip - Converts audio files in embedded resources to AudioClips
	- GetSprite - Loads image files in embedded resources to Sprites
- Helpers - Utilities for implementing common gameplay mechanics
	- DamageEnemy - Deals damage to an enemy
	- GetEnemy - Gets nearby enemies
	- GetTools - Gets a list of equipped tools
	- Needolin - Handles changes to the Needolin's sound clip
- Loggers - Utilities specifically for logging information
	- EnemyDamagerLogger - Logs when an enemy takes damage. 
	  Has to be manually enabled via EnemyDamageLogger.Toggle
- Utilities - Various libraries for logic and calculations
	- Calculations - Performs numeric calculations and conversions
	- ClassIntegrations - Accesses properties, fields and methods from other classes
	- Components - Deals with Unity components
	- NotchCosts - Calculates the value of different bonuses in terms of charm notches from Hollow Knight

## Special Thanks
Logo by cristina233_