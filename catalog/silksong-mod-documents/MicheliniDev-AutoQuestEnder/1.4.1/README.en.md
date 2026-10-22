# Auto Quest Finisher
A quality of life mod that automatically ends the boring quests (wishes) upon starting them.


## Important Information
- This mod does not work with Main Quests (Reach the Citadel, Getting the Three Melodies, etc);
- **BepInEx Configuration Manager** can be used to disable and enable the mod mid-game, removing the need to re-open in order to disable it;
- Will skip almost all quests in the game, automatically giving the reward onto the player;
- Rescue quests require re-entering the room in order for the new NPC to appear.
- Speaking to NPCs will automatically finish all quests that were not complete yet, this behavior can be disabled in the settings
- This mod can make Hornet go in beast shard debt, since it will always allow completion of donate quests, even if you don't have the necessary amount for them.

## Exceptions
This mod will not skip the **Rite of Rebirth**, **Pinmaster's Oil**, **Infestation Operation**, **Silk and Soul** and Boss Quests that are enabled on the settings.
<img width="622" height="335" alt="Settings" src="https://raw.githubusercontent.com/MicheliniDev/AutoQuestEnder/refs/heads/main/Settings.png"/> 

BepInEx Configuration Manager is required to be able to choose which boss quests you don't want to be skipped. <br>
This is made for people who like to fight certain bosses and don't want to miss out on them due to quest skipping.

## Changelog
If you find any bugs or weird interactions with quests, make sure to report them at the github repository's <a href="https://github.com/MicheliniDev/AutoQuestEnder/issues">Issue Page</a>. <br>
If you experienced any of the "not received x item" bugs, try re-opening your save file or completing another quest, you should receive the item automatically.
### Version 1.4.1
- Fixed a small bug that would instantly give the **Drifter's Cloak** regardless whether the quest was complete or not.
### Version 1.4.0
- Fixed a bug where completing **Balm for the Wounded** wouldn't give a Spool Fragment.
- Added **Silk and Soul** to exceptions, now the player should immediately receive the necessary items to progress the quest instead.
### Version 1.3.0
- Added **Infestation Operation** to exceptions, instead of completing automatically, now it instantly gives the necessary item onto the player once the quest starts. 
### Version 1.2.0
- Fixed a bug that would automatically finish quests even if you had the mod disabled.
- Fixed the **Flexible Spines** quest not giving the **Drifter's Cloak**.
- Added the **Pinmaster's Oil** quest to exceptions, because it would automatically upgrade the needle without not needing the oil, which lead to having an extra unneeded oil.  
### Version 1.1.0
- Fixed the **Berry Picking** quest not giving the **Druid's Eye** tool.