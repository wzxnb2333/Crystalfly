<h1>Description:</h1>
I have difficulty with game elements that are static, and SHOULD be static (no interaction). <br />
Yet they do something to warrant immediate attention (flashing/pulsing/...)

Currently disables: <br />
- Main menu silksong theme particles
- Inventory selection "cursor" pulsation
- Map pin flashing

<h1>Links:</h1>
GitHub: <a href = "https://github.com/Bigfootmech/Silksong_Reduce_Motion">https://github.com/Bigfootmech/Silksong_Reduce_Motion</a> <br />
NexusMods: <a href = "https://www.nexusmods.com/hollowknightsilksong/mods/556">https://www.nexusmods.com/hollowknightsilksong/mods/556</a> <br />
Thunderstore: <a href = ""></a>


<h1>To install:</h1>

<h3>Thunderstore:</h3>
It should all be handled for you auto-magically.

ie: I set this package's dependency as BepInEx.

<h3>Manual:</h3>
First install BepInEx to your Silksong folder,
(note: this will break how thunderstore does things)

You can find it at
https://github.com/BepInEx/BepInEx/releases
latest stable is currently 5.4.23.4

After unzipping, run the game once, so that the BepInEx folder structure generates
(ie: there's folders in there apart from just 'core')

Then pull this DLL, or folder including the dll in to 
Hollow Knight <code>Silksong\BepInEx\plugins</code>

<h3>Hybrid:</h3>
If you somehow have Thunderstore.
And have BepInEx installed through it.
But this mod's dependencies glitched out or something,

You should be able to find the Thunderstore's BepInEx plugins folder at 

<code>C:\\Users\\{username}\\AppData\\Roaming\\Thunderstore Mod Manager\\DataFolder\\{game}\\profiles\\{profile_name}\\BepInEx</code>

If you're somehow using thunderstore NOT on windows, AND I screwed up the packagin, AND you aren't a techie... god help you.

