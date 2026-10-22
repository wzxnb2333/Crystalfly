# MapUnlocker

A BepInEx mod for Hollow Knight: Silksong that allows you to unlock maps and control map visibility throughout your playthrough.

## Features

- **Individual Map Control**: Unlock specific maps individually or all at once
- **Individual Pin Control**: Unlock specific pins individually or all at once
- **Individual Marker Control**: Unlock specific pins individually or all at once
- **Quill Tool**: Enable/disable the quill tool for map drawing
- **Auto-Unlock**: Option to unlock all maps and pins automatically when starting a new game
- **Map Reset**: Option to restore original map and pin states when leaving the game
- **Debug Mode**: Detailed logging for troubleshooting
- **Configurable**: All options available through BepInEx configuration menu

## Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx) for Hollow Knight: Silksong
2. Download the latest release from the releases page
3. Extract the `MapUnlocker.dll` file to your `BepInEx/plugins/` folder
4. Launch the game

## Configuration

The mod adds several configuration options accessible through the BepInEx configuration menu:

### General Settings

- **Debug Mode**: Enable detailed logging for troubleshooting
- **Reset Maps After Leaving**: Restore original map states when exiting the game
- **Has Quill**: Enable/disable the quill tool for map drawing

### Individual Map Settings

Each map, pin and marker has their own toggle to unlock/lock:
- **Unlock All Start**: Each field has a master toggle for every item when you load into a save.
- **Unlock All Now**: Each field has a Master toggle for every item under its category.
- Individual toggles for each map, pin and marker.

## How It Works

The mod uses Harmony patches to:
1. Intercept map data when the game starts
2. Apply your configuration settings to unlock desired maps
3. Optionally restore original map states when saving/leaving
4. Provide real-time map unlocking through the configuration menu

## Compatibility

- **Game Version**: Compatible with Hollow Knight: Silksong
- **BepInEx Version**: Requires BepInEx 5.4.21 or later
- **Dependencies**: Uses Silksong.GameLibs 1.0.2-silksong1.0.28561

## Troubleshooting

If you encounter issues:

1. Enable **Debug Mode** in the configuration to see detailed logs
2. Check the BepInEx console for error messages
3. Ensure you're using a compatible version of BepInEx
4. Verify the mod is properly installed in the `BepInEx/plugins/` folder

## Credits

- Based on Skydorm's AbilitiesUnlocked mod framework
- Uses Harmony for runtime patching
- Built with BepInEx plugin system

## License

This mod is provided as-is for educational and entertainment purposes. Please respect the game's terms of service and use responsibly.

## Version History

- **v1.2.0**: Map marker additions.
- **v1.1.2**: Optimizations.
- **v1.1.1**: Map pin bug fixes.
- **v1.1.0**: Map pin additions and config menu improvements.
- **v1.0.0**: Initial release with full map unlocking functionality
