## 0.4.0
- Add config option `capTargetToObtainedUpgradeLevel`
    - *default: true; changes default behaviour by capping the internal tracker to the current save's max upgrade level to avoid requiring multiple "downgrades" to see a change in applied Needle level*
- Avoid NullReferenceException in InventoryItemButtonPromptBase.OnEnable()
    - *Extending the component allows for sidestepping OnEnable before component is fully initialised*

### 0.3.3
- Try fix missing audio when installed with certain other mods
    - *Try improve asset loading robustness*
    - *Tested with **`cometcake575-Architect`** (and dependencies)*
    > *I'm not really sure if this is a proper solution. Let me know if there is a better way of handling game assets!*
- Update button prompts to match control scheme without needing to quit to menu

### 0.3.2
- Fix incompatibility with **`silksong_modding-SilksongPrepatcher`**

### 0.3.1
- More verbose debug logging
- Update README
    - document additional notes related to debugging and known issues

## 0.3.0
- Add button prompts for "upgrading"/"downgrading"

## 0.2.0
- Fix audio feedback
- Update README
    - minor wording adjustments
    - document controller inputs

## 0.1.0
- Initial release
