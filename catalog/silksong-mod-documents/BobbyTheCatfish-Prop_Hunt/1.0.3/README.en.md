# Prop Hunt

## What is Prop Hunt?
Prop Hunt is very similar to hide and seek.
You hide as objects (props) around the map while seekers try to find and you.
Once you've been found, you become a seeker. The last person standing wins! Look at the [Gameplay](#gameplay) section for more details.

## Installation
This mod requires [SSMP](https://thunderstore.io/c/hollow-knight-silksong/p/SSMP/SSMP/) for multiplayer.

### Thunderstore
You should be able to install this with any compatible installer/mod manager.

### Manual Installation
1) Download and extract the mod
2) Navigate to your BepInEx plugins folder
3) Copy the extracted mod folder to the plugins folder (Example: `Plugins/PropHunt/PropHuntMod.dll`)


> For mod support, you can find me (BobbyTheCatfish) in the [HK Modding](https://discord.gg/hollow-knight-modding-879125729936298015) and [HKMP](https://discord.gg/6nzXWhPpEf) Discord servers, as well as my [personal one](https://discord.gg/drv64CKZh4).

## Gameplay
Before and between rounds (see [commands](#commands) section), players are free to switch between props and destroy other players' props. Each room has a different set of props you can hide as.
> A list of each room's props can be found [here](https://docs.google.com/spreadsheets/d/1pxumrYVEBxFdBz4NKolcoCLyxjkwwWMmx7_CYVWZ0Qc).
You can use a mod like [QuickWarp](https://thunderstore.io/c/hollow-knight-silksong/p/hk_speedrunning/QuickWarp/) to go to specific rooms easily.

> It's worth noting that some rooms (especially memories) may not have any props, or may have very few.
If you encounter any, please create an issue on the mod's [GitHub](https://github.com/BobbyTheCatfish/PropHuntMod/issues).

Take this time before starting to establish a few rules and configure some settings (see [commands](#commands) section):
- How long should hiders have before the seekers start? (30 seconds by default)
- Can hiders hide in other rooms? (Only reccomended for larger games)
- How many seekers should there be? (1 by default)
- Should seekers have an attack cooldown, and how many seconds should it be? (none by default)

Once a round is started with [/ph start](#commands), at least 1 player will be selected to be the seeker, and everyone else will be a hider. 

#### Hiders
As soon as you're chosen as a hider, you'll be hidden as a random prop. You can change that prop and move it around if desired. (See [keybinds](#keybinds))

Hide somewhere in the room (or in nearby rooms if allowed), blending in with the environment around you.
**If a seeker comes along and attacks your prop, you'll be revealed and will become a seeker.**
If you're the last player found, you win!

> If you're playing with a lot of people, it may be a good idea to allow hiding in nearby rooms and sub-areas.

#### Seekers
As soon as you're chosen as a seeker, your screen will turn black for 30 seconds (by default), and you'll be unable to move. The hiders will use this time to hide.
Once the waiting period is over, you can go searching for the hiders. **Attacking a prop will reveal the player underneath.**
Once a hider is found, they become a seeker.

## Usage
> See the [SSMP usage documentation](https://thunderstore.io/c/hollow-knight-silksong/p/SSMP/SSMP/) for details on setting up or joining a server

### FAQ
- Does it matter which save file I use?
	- Nope! You can be on any act, have whatever progress you want, and other people *should* be able to see your props just fine
	- That being said, it'd probably be a good idea to be in a mostly completed act 2 file.

### Keybinds
There are a few keybinds for this mod, which can be changed within BepInEx Configuration Manager.

`P` -> `Prop` (Change your prop)

`R` -> `Reset` (Reveal yourself and remove your prop)

`Numpad 5` -> `Reset Prop Position`

While hiding, you can move your prop in a few ways, within a few limits. There are a few methods to move your prop, and you can change the one you use in the configuration.
#### Numpad
```
7 8 9      forward         up          back
4 5 6       left      center/reset     right  
1 2 3     rot. left       down       rot. right
-   +     scale down                  scale up 
```

#### Controller Left/Right Stick
Press in the selected stick to change modes (Move in 2d space, move forward/back, rotate, scale, stop movement), and move the stick to
move the prop. An indicator will appear near your healthbar to indicate which movement mode you're in.

#### Keyboard Movement
Similar to the controller stick method, this uses your movement keys to move the prop.
You can switch between modes by pressing the `taunt` button.


### Commands
These commands can be used by pressing `Y` to open the SSMP chat
- `/ph start` (starts a round of prop hunt)
- `/ph stop` (stops the current round of prop hunt)
- `/ph config <seekerwait|seekercount|attackcooldown>` (set seeker settings)
- `/sync` (syncs the states of other players props)



## Roadmap
These are just some things that I'd like to add in the future
- Add game configuration
	- limit the number of prop swaps per player per round

## Reporting Issues
This is still a little beta at the moment. I haven't had many chances to test the multiplayer features, so please check your logs often.
Please report any errors you see (either in the logs or just things you noticed) to the mod's [GitHub repository](https://github.com/BobbyTheCatfish/PropHuntMod/issues).
I'd really appreciate it! I want this mod to be fun for everyone to play.