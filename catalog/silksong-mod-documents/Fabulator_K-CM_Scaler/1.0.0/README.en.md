# CM_Scaler (ConfigurationManagerScaler)

A simple plugin to scale Configuration Manager UI for higher resolution displays.

[BepInEx ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)'s
F1 menu is drawn at fixed pixel sizes — small at 1440p, tiny at 4K. CM_Scaler wraps
CM's rendering in a uniform scale so the whole window (text, sliders, checkboxes)
grows to match your resolution. No ConfigurationManager files are modified.

## Settings (edit them in the scaled window itself)

- **Auto scale** (default on) — fits to your resolution: `screen height / 1080`, never below 1.0, capped at 2.5.
- **Manual scale** (0.5–3.0) — used when Auto scale is off.

## Notes

- Requires ConfigurationManager (mono/BepInEx 5). Without it the plugin quietly does nothing.
- Nothing here is Silksong-specific — the same DLL should work in any BepInEx 5 game that runs ConfigurationManager.

## Legal & attribution

The package icon is based on the [BepInEx](https://github.com/BepInEx/BepInEx) logo and
is used for identification only. CM_Scaler is an independent community plugin: it is not
affiliated with, endorsed by, or supported by the BepInEx team or the ConfigurationManager
authors, and any issues with it should be reported to me rather than to them.

BepInEx is licensed under LGPL-2.1 and ConfigurationManager under LGPL-3.0. This package
contains no code or files from either project — it links against BepInEx's public API and
patches ConfigurationManager in memory at runtime only.

If the BepInEx maintainers would prefer their logo not be used here, contact me and it
will be replaced.
