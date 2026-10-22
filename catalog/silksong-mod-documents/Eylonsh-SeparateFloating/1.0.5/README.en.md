# SeparatedFloat

A mod that detaches the float action from the jump button, instead making the open-inventory-wishes button be the float.

The open-inventory-wishes' original functionality has been disabled! To open the inventory wishes panel, scroll sideways from another panel.

This means you need to rebind the "Tasks" button in order to rebind floating.



## Features:

Floating can now be held down, and will automatically activate when possible! If the button is still held and floating is cancelled with another move (dash, attack, binding, etc), floating will resume once 0.25 seconds have passed, and the move's animation has ended.



It is currently \*not possible\* to double jump while floating, might be added in the future. You can double jump after letting go of the float button.



## Bugs:

-If you use the Architect's Craft-Bind ability while floating, and keep holding the float button, floating will not reactivate, and Hornet's sprite will get stuck. This will fix itself once the floating button is let go, or by another animation playing.

-This is hopefully the only action that still causes this behaviour, but there is a chance I missed another one. The error message should be "\[Error  : Unity Log] Could not resolve animation clip: Float".
-This mod's "intentional" error messages start with "Separate-Float runtime/buildtime error:". Those should not appear, but if they do please contact me on Discord.



\## Compatibility with other mods

This mod only changes things related to character controls. As such, most mods should be work normally with it.
A mod that changes something about character controls (that aren't jumping) would probably still work, but there might be a sprite loader error, similiar to the ones mentioned above, these should not cause any serious problems.
A mod that changes something with Jump or Floating controls will obviously not be compatible.



## Manual Install

Like with most other mods, unzip the file, and copy the SeparateFloating.dll file into your Hollow Knight Silksong\\BepInEx\\plugins folder.

