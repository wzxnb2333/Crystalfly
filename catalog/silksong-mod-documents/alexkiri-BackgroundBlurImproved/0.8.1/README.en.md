# Background Blur Improved

A mod for Hollow Knight: Silksong that improves the quality of the blurred background, and allows some customizations.


## Features

The game natively renders the background at a fixed 360p resolution. While this isn't very obvious because of the blur effect applied on top, it is noticeable and distracting when in motion, it appear flickery, as can be seen below:

Before:

https://github.com/user-attachments/assets/83eef05f-16b2-497f-8402-fe771f915fc8

After:

https://github.com/user-attachments/assets/aebc2904-8367-448e-a53f-d6d412184327

Image comparison:

https://compare.promptingpixels.com/a/k6FwSSq

This mod allows adjusting the blur effect by:
- increasing the resolution
- increasing the intensity
- disabling the effect completely


## Installation

### The Simple Way

Use r2modman or Gale. This will properly take care of dependencies.

### Manual

1. Download [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/) and extract it to the game folder, next to the game executable
2. Download [BepinExConfigurationManager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepinExConfigurationManager/)
3. Download [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/) and it's dependencies
3. Download from [github](https://github.com/alexkiri/BackgroundBlurImprovedPlugin/releases), [thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/alexkiri/BackgroundBlurImproved/) or [nexusmods](https://www.nexusmods.com/hollowknightsilksong/mods/661) and extract it inside of `<game folder>/BepInEx/plugins`
4.
    - (Windows) Run the game normally
    - (Linux/MacOS) Run `run_bepinex.sh`


## Configuration

Any of these methods work:
- Use `ModMenu` and access in-game Options -> Mods
- Use `BepinExConfigurationManager` and open with F1 by default
- Manually edit the `io.github.alexkiri.backgroundblurimproved.cfg` file inside `<game folder>/BepInEx/config`

Using a combination of these parameters can make the game look the same, but completely remove the distracting flickering.

- `ApplyBlurPreset`
  - allows applying presets from the UI (`BepinExConfigurationManager` only)
- `BlurTextureHeight`
  - increases the resolution of the render target that displays the background blur effect
  - improves the quality of the effect
  - lowers the effect intensity
  - has low impact on performance
- `BlurPassCount`
  - increases the number of passes of the background blur effect
  - can increase intensity of the effect
  - has a medium / heavy impact on performance, values > 10 are not recommended
- `BlurEffectEnabled`
  - can disable the background blur effect completely
  - not recommended, some source textures are low res, and without the blur effect, the background will look inconsistent
- `BloomResolution`
  - resolution of the bloom effect
  - can choose between Low -> 1/4 main resolution, or High -> 1/2
- `BloomBlurIterations`
  - number of iterations for the bloom effect

The main menu "Blur Quality" setting is removed from the main menu, as it no longer has any effect with this mod enabled.


## Other important notes

Using a big `BlurPassCount` causes an issue where the shader slightly offsets the background, the more passes you use. The only fix is to use [renodx](https://github.com/clshortfuse/renodx/wiki/Mods) (follow the installation, and look for Silksong). This mod also greatly improves the look of the game by adding HDR, 16bit rendering, without compromising the artistic intent. Highly recommended.


## TODO
- [ ] Update the Sliders to use the [WiderSliderElement](https://github.com/kaycodes13/Silksong.EdgeDetection/blob/main/Menu/WiderSliderElement.cs), it looks much better with the ModMenu
- [ ] Add translations using [Silksong.I18N](https://github.com/silksong-modding/Silksong.I18N)
- [ ] Update the background drawing logic to allow multiple background layers blurred at different intensities
