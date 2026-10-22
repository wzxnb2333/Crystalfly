## What does this mod do

​	﻿This mod allows player to use shard to build a bench anywhere they want by holding Z(configurable) for 2 second. By default, it will cost 150 shards to create a bench. Each scene (not area) can have up to 2(configurable) modded benches (destroyed bench also counts). Player can also destroy a modded bench by holding the same button as creating for a second when near a modded bench. If the destroyed bench is your current save point, it will not disappear and linger in the current scene, which serves as a temporary respawn point. Upon sitting on another bench, the destroyed bench will be removed. Modded benches will also get pinned on the map with a custom pin (a blue color version of vanilla bench pin)
﻿The modded benches are shared between saves, for they use the same data file.
​	﻿This mod has an in-game UI (F2 to open it by default) that shows the current scene name and allows player to manage the modded benches they created (delete and teleport). The blue ID means this modded bench is currently your respawn/save point (A modded bench served as current save point cannot be removed directly and will turn into a destroyed one for safety reason when deleted). It is not recommended to spam teleportation as it might crash your game. You can rename the bench name by click on it in the UI for better management.
​	﻿**If you are to uninstall this mod, please sit on a vanilla bench first, or else after you load the save, you will respawn in the first scene (the scene with Hornet's cage)**.



​	All modded benches data will be saved in **BepInEx\config\BenchesBuilt.json**, don't delete it or modify it unless you know what you are doing. you should use the UI to delete the modded bench you want.

​	As for bug or issue it might cause, so far, from my test, it hasn't caused any fatal issue that breaks the game or anything like that. The only issue it has is the thumbnail of your save might be not correctly set for unknown reason, for example, if you sit on a modded bench created in The Marrow, the thumbnail shows your save scene is somewhere else.



if you are using a controller, be sure to edit the config file to change the BuildKey to use the create and destroy function. Here is the joystick mapping

```
For PlayerStation Controller:
Cross = JoystickButton0
Circle = JoystickButton1
Square = JoystickButton2
Triangle = JoystickButton3
L1 = JoystickButton4
R1 = JoystickButton5
L2 = JoystickButton6
R2 = JoystickButton7
Share/Create = JoystickButton8
Options = JoystickButton9
LS(Left Stick Click) = JoystickButton10
RS = JoystickButton11
PS-Button = JoystickButton12
Touchpad = JoystickButton13

For Xbox Controller:
A = JoystickButton0
B = JoystickButton1
X = JoystickButton2
Y = JoystickButton3
LB = JoystickButton4
RB = JoystickButton5
Back/View = JoystickButton6
Start/Menu = JoystickButton7
LS(Left Stick Click) = JoystickButton8
RS = JoystickButton9
LT and RT are not recognized
```
