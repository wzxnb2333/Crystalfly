<h1>Description:</h1>
I use mouse-and-keyboard to play pretty much all games.

Hollow Knight (and Silksong) is made for controller, with mouse-and-keyboard as an afterthough, which is fine I guess.
It controls pretty well for most of the game, and I've completed it without any crazy issue.

The only hang-up I have really, is the inventory system. 
It's kinda awkward to bump stuff up down left and right, instead of just... clicking on things.

In Hollow Knight, there's one button that opens the inventory, 
But in Silksong, they give you additional buttons for map/tasks/... etc for the other "tabs", and give you a little view at the top to see which ones you'll navigate to if you get to the left/right arrows at the edge of the screen

Except, now all the additional buttons open their own tabs -- but the original 'inventory' one just opens and closes the whole view.
So, to get to the 'inventory' tab (with the game currency), it's extra awkward.

This mod breaks the function for controller, but fixes it for keyboard.
Whereby now, the inventory button will always navigate to the inventory.
From closed, or from open.

<h1>Changelog:</h1>

- 0.1.0 = base release
- 0.1.1 = bugfix for interacting with bone bottom quest board before opening inventory

<h1>Links:</h1>
GitHub: <a href = "https://github.com/Bigfootmech/Silksong_Keyfix">https://github.com/Bigfootmech/Silksong_Keyfix</a> <br />
NexusMods: <a href = "https://www.nexusmods.com/hollowknightsilksong/mods/359">https://www.nexusmods.com/hollowknightsilksong/mods/359</a> <br />
Thunderstore: <a href = "https://thunderstore.io/c/hollow-knight-silksong/p/Bigfootmech/Silksong_Keyboard_Inventory_Fix/">https://thunderstore.io/c/hollow-knight-silksong/p/Bigfootmech/Silksong_Keyboard_Inventory_Fix/</a>

<h1>To install:</h1>

<h3>Thunderstore:</h3>
It should all be handled for you auto-magically.

ie: I set this package's dependency as BepInEx.

<h3>Manual:</h3>
First install BepInEx to your Silksong folder,
(note: this will break how thunderstore does things)

You can find it at
https://github.com/BepInEx/BepInEx/releases
latest stable is currently 5.4.23.3

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

