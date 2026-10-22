# Changelog
## Version 1.2.1
- Use the correct binaries in the releases now

## Version 1.2.0
- Migrate to [Silksong.I18N](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/I18N/) for translatable text
- Eliminate `DontDestroyOnLoad` warning caused by this mod (other mods may still produce them)

## Version 1.1.2
- Include localization files in thunderstore package

## Version 1.1.1
- Update README.md
- Add CHANGELOG.md

## Version 1.1.0
- Switched button swap toggle for button swap options
- Button swap options are saved across sessions
- Fix `NullReferenceException` on controller menu when gamepad skin is changed while no controllers are connected
- Migrate to using translatable text (mod now depends on SimpleSilksongLocalizer)