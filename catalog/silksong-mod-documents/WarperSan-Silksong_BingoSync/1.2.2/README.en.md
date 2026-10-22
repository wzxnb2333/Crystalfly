<p align="center">
    <img src="https://raw.githubusercontent.com/WarperSan/Silksong.BingoSync/refs/heads/master/icon.png" alt="Logo" height="128"/>
</p>

# Silksong.BingoSync

[![Thunderstore Version](https://img.shields.io/thunderstore/v/WarperSan/Silksong_BingoSync)](https://thunderstore.io/c/hollow-knight-silksong/p/WarperSan/Silksong_BingoSync/)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/WarperSan/Silksong_BingoSync?color=purple)](https://thunderstore.io/c/hollow-knight-silksong/p/WarperSan/Silksong_BingoSync/versions/)
[![License](https://img.shields.io/github/license/WarperSan/Silksong.BingoSync?color=orange)](https://raw.githubusercontent.com/WarperSan/Silksong.BingoSync/master/LICENSE)


This mod allows you to connect to [BingoSync](https://bingosync.com/) directly in-game. This allows you to play a bingo game without leaving the game.

> [!WARNING]
> This mod is still WIP. Bugs can occur, and not every feature is yet available.

### How to use

If you are a player, you will simply have to fill out the information, then press `Play`. It will try to connect to the room.

If you are the host, you will have to go on [the website](https://bingosync.com/), and create a room using this JSON:

```json
[{"name":"Kill all Conchfly bosses"},{"name":"Kill either Lace 1 or Phantom"},{"name":"Kill Widow before Fourth Chorus"},{"name":"Kill 4 bosses in Act 1"},{"name":"Kill Bell Beast"},{"name":"Kill Fourth Chorus"},{"name":"Kill Great Conchflies"},{"name":"Kill Lace 1"},{"name":"Kill Last Judge"},{"name":"Kill Moorwing"},{"name":"Kill Moss Mother"},{"name":"Kill Moss Mothers"},{"name":"Kill Phantom"},{"name":"Kill Savage Beastfly 1"},{"name":"Kill Sister Splinter"},{"name":"Kill Skull Tyrant 1"},{"name":"Kill Skull Tyrant 2"},{"name":"Kill Widow"},{"name":"Kill Broodmother"},{"name":"Kill Cogwork Dancers"},{"name":"Kill Disgraced Chef Lugoli"},{"name":"Kill Father of the Flame"},{"name":"Kill First Sinner"},{"name":"Kill Forebrothers"},{"name":"Kill Garmond"},{"name":"Kill Grand Mother Silk"},{"name":"Kill Groal the Great"},{"name":"Kill Lace 2"},{"name":"Kill Raging Conchfly"},{"name":"Kill Savage Beastfly 2"},{"name":"Kill Second Sentinel"},{"name":"Kill Shakra"},{"name":"Kill The Unravelled"},{"name":"Kill Trobbio"},{"name":"Kill Voltvyrm"},{"name":"Kill Bell Eater"},{"name":"Kill Clover Dancers"},{"name":"Kill Crawfather"},{"name":"Kill Crust King Khann"},{"name":"Kill Gurr the Outcast"},{"name":"Kill Lost Garmond"},{"name":"Kill Lost Lace"},{"name":"Kill Nyleth"},{"name":"Kill Palestag"},{"name":"Kill Pinstress"},{"name":"Kill Plasmified Zango"},{"name":"Kill Shrine Guardian Seth"},{"name":"Kill Skarrsinger Karmelita"},{"name":"Kill Tormented Trobbio"},{"name":"Kill Watcher"},{"name":"Obtain Hunter Crest"},{"name":"Obtain Hunter Crest Evolved"},{"name":"Obtain Hunter Crest Fully Evolved"},{"name":"Obtain Reaper Crest"},{"name":"Obtain Wanderer Crest"},{"name":"Obtain Beast Crest"},{"name":"Obtain Cursed Crest"},{"name":"Obtain Witch Crest"},{"name":"Obtain Architect Crest"},{"name":"Obtain Shaman Crest"},{"name":"Obtain Cloakless Crest"},{"name":"Complete Twisted Child as the first ending"},{"name":"Complete Weaver Queen"},{"name":"Complete Snared Silk"},{"name":"Complete Twisted Child"},{"name":"Complete Sister of the Void"},{"name":"Complete Passing of the Age"},{"name":"Obtain Silkspear"},{"name":"Obtain Thread Storm"},{"name":"Obtain Cross Stitch"},{"name":"Obtain Sharpdart"},{"name":"Obtain Rune Rage"},{"name":"Obtain Pale Nails"},{"name":"Obtain Straight Pin"},{"name":"Obtain Threefold Pin"},{"name":"Obtain Sting Shard"},{"name":"Obtain Tacks"},{"name":"Obtain Longpin"},{"name":"Obtain Curveclaw"},{"name":"Obtain Curvesickle"},{"name":"Obtain Throwing Ring"},{"name":"Obtain Pimpillo"},{"name":"Obtain Conchcutter"},{"name":"Obtain Weaver Silkshot"},{"name":"Obtain Architect Silkshot"},{"name":"Obtain Forge Silkshot"},{"name":"Obtain Delver's Drill"},{"name":"Obtain Cogwork Wheel"},{"name":"Obtain Cogfly"},{"name":"Obtain Rosary Cannon"},{"name":"Obtain Voltvessels"},{"name":"Obtain Flintslate"},{"name":"Obtain Snare Setter"},{"name":"Obtain Flea Brew"},{"name":"Obtain Plasmium Phial"},{"name":"Obtain Needle Phial"},{"name":"Obtain Druid's Eye"},{"name":"Obtain Druid's Eyes"},{"name":"Obtain Magma Bell"},{"name":"Obtain Warding Bell"},{"name":"Obtain Pollip Pouch"},{"name":"Obtain Fractured Mask"},{"name":"Obtain Multibinder"},{"name":"Obtain Weavelight"},{"name":"Obtain Sawtooth Circlet"},{"name":"Obtain Injector Band"},{"name":"Obtain Spool Extender"},{"name":"Obtain Reserve Bind"},{"name":"Obtain Claw Mirror"},{"name":"Obtain Claw Mirrors"},{"name":"Obtain Memory Crystal"},{"name":"Obtain Snitch Pick"},{"name":"Obtain Volt Filament"},{"name":"Obtain Quick Sling"},{"name":"Obtain Wreath of Purity"},{"name":"Obtain Longclaw"},{"name":"Obtain Wispfire Lantern"},{"name":"Obtain Egg of Flealia"},{"name":"Obtain Pin Badge"},{"name":"Obtain Compass"},{"name":"Obtain Shard Pendant"},{"name":"Obtain Magnetite Brooch"},{"name":"Obtain Weighted Belt"},{"name":"Obtain Barbed Bracelet"},{"name":"Obtain Dead Bug's Purse"},{"name":"Obtain Shell Satchel"},{"name":"Obtain Magnetite Dice"},{"name":"Obtain Scuttlebrace"},{"name":"Obtain Ascendant's Grip"},{"name":"Obtain Spider Strings"},{"name":"Obtain Silkspeed Anklets"},{"name":"Obtain Thief's Mark"},{"name":"Obtain Basic Vesticrest"},{"name":"Obtain Upgraded Vesticrest"}]
```

> [!IMPORTANT]
> Make sure everyone has the same goal files. Players can have more than the host, but they need to have every goal used on the board.

> [!NOTE]
> This version of the mod doesn't allow you to create rooms in-game. However, it is a planned feature for the future!

### Goals

This mod comes bundled with a lot of goals. They are mostly simple, but they allow some variety.

Users can also create their own goal using the available actions. Every goal under `Goals/` will be loaded on launch.

> [!WARNING]
> There is currently no condensed source of information that teaches how to make goals. It is suggested to see how bundled goals are made, and try to copy them.

## Contributing

Contributions are welcome! If you encounter a bug or have a feature request, please [open an issue](https://github.com/WarperSan/Silksong.BingoSync/issues/new).

<br/>
<hr/>
<p align="center">
    <sub>
        Published using <a href="https://github.com/WarperSan/ThunderPipe">ThunderPipe</a>
    </sub>
</p>