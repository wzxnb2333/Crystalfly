```
F1          :: global UI toggle off/on
LShift+F1   :: toggle only debug pane visibility (will reset stats)
LCtrl+F1    :: toggle crosshair visibility
F2          :: show scene selector
F3          :: toggle autoheal
LShift+F3   :: toggle invincibility
Ctrl+F3     :: toggle autosilk
F4          :: 500 rosaries
LShift+F4   :: 400 shards
F5          :: quickload last used savestate
Ctrl+F5     :: open savestate list
Shift+F5    :: make savestate from current game state
LAlt+F5     :: quick savestate
F6          :: PlayerData flag editor
LCtrl+F6    :: toggle player vignette
F7          :: teleport to scene checkpoint
LCtrl+F7    :: set scene checkpoint
F8          :: toggle between nail damage states: no damage or normal
LCtrl+F8    :: cycle between nail levels 0 > 1 > 2 ... 5 > 0 > 1, etc
LAlt+F8     :: toggle ability  to change equip. everywhere
F9          :: HP viewer
RShift+F9   :: Cocoon viewer
F10         :: toggle free cam mode
LShift+F10  :: toggle camera follow mode (always on hero)
LAlt+Mouse1 :: hold and drag mouse 1 to move camera around (only in freecam)
Mouse2      :: teleport to cursor position while in freecam
LCtrl+Mouse1 :: teleport to cursor position at any time
F11         :: toggle collider view on/off
LShift+F11  :: open collider config menu
RShift+F11  :: switch between lined|mitered|filled view (filled broken)
LCtrl+F11   :: toggle visibility of inactive colliders (noisy)
F12         :: reset player state after botched scene transition
LShift+Wheel :: zoom in/out
Backspace   :: reset zoom
MiddleMice  :: reset zoom
- and +     :: slowdown and speedup
Alpha 0     :: reset speed to default
RCtrl       :: fbf timestep, press continuously to advance frame, 0 to reset
RShift      :: speedup by 2x while holding. Can be configured in BepInEx\config\com.bepin.SSDebugkz.cfg
Home        :: reset boss and scene flags (re-enter the scene afterwards)


How to change configuration/rebind:
1. Launch the game with mod once
2. Exit and open file BepInEx\config\com.bepin.SSDebugkz.cfg
3. Edit values, save
4. Launch the game


=================================
 KNOWN ISSUES
=================================
1. No notifications for actions, this will be added
2. First iteration of scene transition uses default game methods,
   if you get stuck use F12 to restore control and camera
   and then mouse teleport to reposition yourself


=================================
 CHANGELOG
=================================
 - 0.1.66 - added ALT+F5 for quick temp savestate
 - 0.1.65 - added PlayerData flag Editor
 - 0.1.65 - savestate load now only can be used when state is PLAYING
 - 0.1.63 - fixes to debug info being cut off
 - 0.1.63 - fixes to mouse cursor behaviour
 - 0.1.62 - fixes to lost hotkeys
 - 0.1.62 - fixes to hardcoded path generation for sstates
 - 0.1.61 - fixes to default binds
 - 0.1.60 - added rebindable controls, theor current organization mimics default
 - 0.1.60 - fixed HPviewer not working for some targets
 - 0.1.60 - expanded scene names window
 - 0.1.60 - added F8 and modifiers to change some nail stats/equipment state
 - 0.1.60 - DebugPane now indicates if infinite silk/autoheal/invincibility is enabled
 - 0.1.60 - DebugPane now display velocity as X/Y
 - 0.1.50 - fixed Inventory Items, Quest and Currency display being broken after savestate load
 - 0.1.41 - some fixes to savestates
 - 0.1.39 - added base damage of the last damaging item + multipliers
 - 0.1.37 - fixes to scene menu, more familair names
 - 0.1.36 - fixed scroll for savestates
 - 0.1.34 - some fixes to debug info
 - 0.1.33 - some more small fxes to teleport/savestates
 - 0.1.31 - removed "CanJump" from debuginfo, it can be re-added via plugin config
 - 0.1.31 - added some more hero/game states
 - 0.1.30 - fixed ledge buffer data being cleared on by DebugInfo
 - 0.1.27 - more fixes to SStates: hp, silk, Crests updated properly
 - 0.1.23 - added hero flag info
 - 0.1.22 - fixes to Savestates
 - 0.1.22 - added game version info
 - 0.1.20 - hotkey changes
 - 0.1.20 - update colors and default layers for collider view
 - 0.1.17 - added quick boss flags reset
 - 0.1.16 - added scaling data
 - 0.1.15 - added Savestate menu
 - 0.1.13 - added Savestate functionality
 - 0.1.13 - fixes to SceneSelector's teleport mechanic
 - 0.1.13 - fixes to Freecam, not it behaves properly with Scene lockers in 90%
 - 0.1.8 - fixes to Dmg viewer
 - 0.1.7 - fixed cursor
 - 0.1.6 - added last damage (to enemy) info to DebugInfo module
 - 0.1.5 - added scene selection, temp version
 - 0.1.4 - fixes to cursor teleport
 - 0.1.3 - added FreeCam. See current issues
 - 0.1.3 - added mouse teleport
 - 0.1.1 - fixed bugged velocity view
 - 0.1.0 - added collission view
 - 0.0.8 - added completion percentage
 - 0.0.8 - fixed timescale not being restored on 0
 - 0.0.7 - added forced timescale
 - 0.0.6 - removed cursor flicker
 - 0.0.6 - Added speedup while holding Right Shift, can be configured via BepInEx\config\com.bepin.SSDebugkz.cfg
 - 0.0.6 - Changed Scene checkpoint save action to Ctrl+F7 because Shift is Dash
 - 0.0.5 - initial release


=================================
 CREDITS
=================================
KeinZantezuken
```