# EndingIndicators

A `Hollow Knight: Silksong` mod that shows which endings were completed
for every save slot.

**Github Release**:
https://github.com/UserIsntAvailable/Silksong.EndingIndicators/releases/latest

**Thunderstore**:
https://thunderstore.io/c/hollow-knight-silksong/p/Unavailable/EndingIndicators

## Installation (manual)

1. [Download and install BepInEx].
2. Ensure that the game has run at least once so that a `plugins`
   directory is generated in the `BepInEx` directory besides the game
   executable.
3. Download the .zip file from one of the sources listed above.
4. Extract the .zip file to the generated `plugins` directory.
5. Launch the game and enjoy!

## Configuration

A config file would be created, on first plugin load, under
`<SAVEFILES>/plugins/<PLUGINGUID>/config.dat`. You can use
[ConfigurationManager] to update and reload the config in-game; if you
prefer modifying config files by hand, you are gonna need to close and
reopen the game to see the changes.

(below there are the current available configurable options)

<!-- TODO(Unavailable): Automatically generate this section and make
it prettier (add some screenshots?). -->

```ini
## Plugin GUID: unavailable.ending-indicators

[Options]

## How the indicators should be displayed
## Endings that are not yet completed, will be shown in a more transparent color
## if `ShowAllIfAnyCompleted` or `ShowAll` are used.
# Setting type: EndingDisplayOpts
# Default value: OnlyCompleted
# Acceptable values: OnlyCompleted, ShowAllIfAnyCompleted, ShowAll
EndingDisplay = OnlyCompleted

## Reverse the order of the endings to match their natural order of unlocking
# Setting type: Boolean
# Default value: false
NaturalOrder = false
```

[Download and install BepInEx]: https://docs.bepinex.dev/articles/user_guide/installation/index.html
[ConfigurationManager]: https://github.com/BepInEx/BepInEx.ConfigurationManager
