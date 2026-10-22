# Changelog

## [1.0.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v1.0.1..v1.0.2) - 2025-10-24

### 🚜 Refactor

- *(CommandManager)* Convert to singleton and simplify initialization logic - ([6b0d4e1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/6b0d4e155a04693ea14fb73a9bbd9d2ab5c70cc5))

### Action

- Update `CHANGELOG.md` - ([ea932be](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/ea932beb00353092742bbf84ff0206b4932927f3))


## [1.0.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v1.0.0..v1.0.1) - 2025-10-21

### 🚜 Refactor

- *(Utils)* Change class visibility from public to internal - ([3db76c2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/3db76c27d4efc7c246e47ed0f8cc56c1239f2137))

### Action

- Update `CHANGELOG.md` - ([b3de7fa](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/b3de7faa7d32828bd005764cd696b4ae4b257318))


## [1.0.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.9.2..v1.0.0) - 2025-10-19

### ⛰️  Features

- *(CommandManager)* Make commands asynchronous by updating delegate and execution logic - ([a90bc48](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/a90bc4869fdfb74f5e2e4e76d0891378e1dd2ac1))

### 🚜 Refactor

- *(CommandManager)* Rename `TryExecuteCommand` to `ExecuteCommand` for clarity and consistency - ([9e8a06e](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/9e8a06e280954b539c0eaed18ce561024cb96343))
- *(CommandManager)* Remove unused `OnCommandManagerInitEvent` delegate - ([996d5f1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/996d5f16acbe1d068d43d81e88713c914f1c9e1b))
- *(CommandManager)* Simplify `CommandInfo` struct with record structure and streamline command initialization - ([f21a75a](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/f21a75a39105b62e6448ebdce149481f30125031))
- *(CommandManager)* Remove unused `OnCommandManagerInit` event and adjust initialization logic - ([2988d38](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/2988d38602d13afdf6338c6fc803ecfac5294fff))
- *(Extensions)* Remove outdated comments regarding nullable type comparison - ([f5783ac](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/f5783ac15a105051ebe1fae0b8c8d37b2500ee48))
- *(Logger)* Centralize log message formatting with reusable constant - ([f4dff2a](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/f4dff2a9169dd7063aa65bbdc6843459e197c96f))
- *(Logger)* Stop waiting log method - ([1d29dfd](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/1d29dfdf347b9f26c9bc8c9c70e66df9ba441e0d))
- *(MethodUtils)* Extract `GetCallerName` and `GetMethodName` methods for improved reusability and clarity - ([e6393dd](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/e6393dd6bc806859a8f3f84642c81f641522921c))

### ⚙️ Miscellaneous Tasks

- *(BepInExUtils)* Update Generator DLL - ([0bfec7c](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/0bfec7cb6191e17c4ad187069f15b4635e18736b))
- *(BepInExUtils)* Update Generator DLL - ([e91fffa](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/e91fffabdc68228e3c862bbea4359029877c03e4))
- *(BepInExUtils)* Update Generator DLL - ([0745c7a](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/0745c7ac151b24fb698923470a47185cc3cea080))

### Action

- Update `CHANGELOG.md` - ([62ef2f5](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/62ef2f5a878244bc72dc0cd3d9a6e0dbcdea3990))


## [0.9.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.9.1..v0.9.2) - 2025-10-15

### 🚜 Refactor

- *(Main)* Replace `PostAwake` with `Init` method for initialization - ([0be3aac](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/0be3aac06961f9e8465b7473078eb52694f6f7ea))

### ⚙️ Miscellaneous Tasks

- *(BepInExUtils)* Update Generator DLL - ([8dbfc28](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/8dbfc287ae6355156440123ad8964f5b3bc8cf22))

### Action

- Update `CHANGELOG.md` - ([ff3c79b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/ff3c79b77caee7e8225fdc01d31af2eca60d3bf1))


## [0.9.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.9.0..v0.9.1) - 2025-10-15

### 🐛 Bug Fixes

- *(Interfaces)* Mark `BepInUtilsUnityPlugin` as obsolete with guidance to use `IBepInUtils` instead - ([69f387c](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/69f387cd2936489a1bf35492e5000f9f7951f6f9))

### Action

- Update `CHANGELOG.md` - ([4b179c8](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/4b179c8f8080b4732c8d6013d0056cf2e350b3b6))


## [0.9.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.8.2..v0.9.0) - 2025-10-15

### ⛰️  Features

- *(Interfaces)* Add `IBepInUtils` interface with initialization method - ([fc9912d](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/fc9912d1bb443666ffa93b76d14a45953414ea4e))

### ⚙️ Miscellaneous Tasks

- *(BepInExUtils)* Update Generator DLL - ([76a2d8b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/76a2d8bcd3f6efd3c3a618cb6bc86c9de5f4ad66))

### Action

- Update `CHANGELOG.md` - ([d4e2f05](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/d4e2f053411ae41959d4b3841b0039fcf74b582a))


## [0.8.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.8.1..v0.8.2) - 2025-10-15

### 🐛 Bug Fixes

- *(Extensions)* Remove the return type check - ([7a9bf7c](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/7a9bf7c3afb46d7e77e8e797f1231532ff7d997e))

### Action

- Update `CHANGELOG.md` - ([b739012](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/b739012b458ac1352ebe4db312e4a34fced99c2e))


## [0.8.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.8.0..v0.8.1) - 2025-10-15

### ⚙️ Miscellaneous Tasks

- *(Main)* Fix version - ([61e809f](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/61e809fb41016421f3a12fe28f2f15604c633006))

### Action

- Update `CHANGELOG.md` - ([b4ba93e](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/b4ba93ee1a645762cb45fc0a5009d6e9f3918334))


## [0.8.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.10..v0.8.0) - 2025-10-15

### ⛰️  Features

- *(CommandManager)* Introduce command management system with default commands - ([cd3a13c](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/cd3a13c4bae2441bd944d0cbe58ece8f5d3e380d))

### 🐛 Bug Fixes

- *(Main)* Change name, load `CommandManager` when awake - ([67568e0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/67568e0ecd7371d3207187f137669d4c1ce0f359))

### ⚙️ Miscellaneous Tasks

- *(ci)* Update versioning script in workflows - ([655f446](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/655f446581fe801f33c471d37505049d551cd2c0))

### Action

- Update `CHANGELOG.md` - ([50b07e2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/50b07e2c12fa27ccfc0c4416d487e550554d418b))


## [0.7.10](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.9..v0.7.10) - 2025-10-15

### ⚙️ Miscellaneous Tasks

- *(ci)* Remove Nexus Mods publishing workflow - ([b84bf9f](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/b84bf9f618953f85c2fdafa11086654e98c0dcf7))

### Action

- Update `CHANGELOG.md` - ([b87024b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/b87024b0067ef884bfaacf567257b3ecb74f30b7))


## [0.7.9](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.8..v0.7.9) - 2025-10-15

### ⚙️ Miscellaneous Tasks

- *(ci)* Update Nexus Mods upload step to use cookie-based authentication - ([53baf57](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/53baf57ff06b71bd348bd1f1eed806e169408b3d))

### Action

- Update `CHANGELOG.md` - ([374bc64](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/374bc64bd95f1bcd4a24d580c7b43963f06c1587))


## [0.7.8](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.7..v0.7.8) - 2025-10-15

### ⚙️ Miscellaneous Tasks

- *(ci)* Fix incorrect package path in Nexus Mods upload step - ([8ad5c66](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/8ad5c660387f5bc1315d523b24182e68b3cbc5aa))

### Action

- Update `CHANGELOG.md` - ([abb4380](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/abb4380066559f40222519fe3f53e6c659da04f6))


## [0.7.7](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.6..v0.7.7) - 2025-10-15

### ⚙️ Miscellaneous Tasks

- *(ci)* Add Nexus Mods publishing workflow and update packaging rules - ([edda042](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/edda0421eae5a0fbbe3ad5c62cfd2ab3ed5a8379))

### Action

- Update `CHANGELOG.md` - ([cad83f7](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/cad83f78fc7e00993958e015eaa788bcdef9dfe8))


## [0.7.6](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.5..v0.7.6) - 2025-10-15

### 🚜 Refactor

- *(ClassProxy)* Remove unused `ClassName` field for cleaner and consistent implementation - ([afd7305](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/afd73057c6c496d72ae1fd9f43cdd123f05b8d92))

### Action

- Update `CHANGELOG.md` - ([2d30398](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/2d30398204b8c08ed39930ce5197c269629f629a))


## [0.7.5](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.4..v0.7.5) - 2025-10-15

### 🚜 Refactor

- *(Extensions)* Replace nullable returns with non-nullable and throw exceptions on type mismatch - ([29d158c](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/29d158c2e9c7b38fe4ef72706528b978dfc17aee))

### Action

- Update `CHANGELOG.md` - ([eab6279](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/eab62799680ce75e2e0f874c96e2bea53356195e))


## [0.7.4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.3..v0.7.4) - 2025-10-15

### 🚜 Refactor

- *(ClassProxy)* Standardize exception argument naming and enhance class consistency - ([e8bc789](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/e8bc789ecd0f599a0b781efe5208a44be9e297f3))
- *(ClassProxy)* Remove `_internalNativeInstance` and make `Native` property readonly - ([79d90ad](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/79d90adbe947b768114364db65e18a18db5ef91f))
- *(ClassProxy)* Replace obsolete `Instance` with `Native` property - ([33b8ce6](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/33b8ce65a4c0dceda77c6c5e52fe534abd5be549))
- *(ClassProxy)* Remove obsolete `MethodAccess` methods and unused import - ([0c13937](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/0c139372fdabbacb31c5c2958da063e94085f575))
- Replace `[UsedImplicitly]` attributes with `[PublicAPI]` across the project - ([4d60851](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/4d6085179ff10d36e3057eae645540f508d6f4c7))

### Action

- Update `CHANGELOG.md` - ([997d5d4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/997d5d45eacdf2e0284e60a03f64a32452bf60c5))


## [0.7.3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.2..v0.7.3) - 2025-10-14

### 🐛 Bug Fixes

- *(Extensions)* Improve `GetEmbeddedResourceBytes` to handle large streams efficiently - ([d359b54](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/d359b5456c7e0d3a097ce8b0852ba58f0e992a64))

### 🚜 Refactor

- *(ClassProxy)* Rename `_internalInstance` to `_internalNativeInstance` and add `Native` property - ([6b31ff5](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/6b31ff52100ba0dae11e148a07e3bd5277db5c3d))

### Action

- Update `CHANGELOG.md` - ([1b7698c](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/1b7698ccb808a49d367302bab6d2b8058a7d8ea1))


## [0.7.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.1..v0.7.2) - 2025-10-12

### ⚙️ Miscellaneous Tasks

- *(packaging)* Add missing `dependencies` section to `thunderstore.toml` - ([bb12a4a](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/bb12a4a8dddbe510700ae2930878eeaf92bedb41))

### Action

- Update `CHANGELOG.md` - ([db34573](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/db3457386cd1d69ec56d393560724999ee20e692))


## [0.7.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.7.0..v0.7.1) - 2025-10-12

### ⚙️ Miscellaneous Tasks

- *(UnityModules)* Change UnityEngine module libraries to older versions - ([1688a8d](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/1688a8dffc5c334eeae80ad36bcaf8dd2b6a9db8))

### Action

- Update `CHANGELOG.md` - ([5a73b81](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/5a73b810caf0f2da219574bfa8587da5f92f8ce8))


## [0.7.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.6.4..v0.7.0) - 2025-10-12

### ⛰️  Features

- *(Extensions)* Add `AssemblyExtensions` for embedded resource handling - ([3b79fa0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/3b79fa0fb3242ad7670d5bd5e15538431754f2cd))

### 🐛 Bug Fixes

- *(Extensions)* Remove redundant null check in `FullName` method of `TransformExtensions` - ([e331b44](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/e331b44b5ade06fe32654372f2761402cf516a59))

### ⚙️ Miscellaneous Tasks

- *(packaging)* Remove unused dependency from `thunderstore.toml` - ([ad39025](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/ad39025a654afd7cc8fcc7978bec1bb859ca0933))
- *(workflows)* Add `git pull` step in release workflow - ([e9bfb1b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/e9bfb1bf7d14cc929ad044fd47ba0ce560c249bf))

### Action

- Update `CHANGELOG.md` - ([629ab98](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/629ab988377a126338d4e74c97edc5183be7c61d))


## [0.6.4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.6.3..v0.6.4) - 2025-10-10

### 🐛 Bug Fixes

- *(ClassProxy)* Add detailed type validation and improved exception handling - ([307c575](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/307c575baf09e6fc68e757e7c7629061f7c8958d))

### Action

- Update `CHANGELOG.md` - ([78a5896](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/78a5896296321e8d133eea65937b28c1d2d1e6d5))


## [0.6.3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.6.2..v0.6.3) - 2025-10-10

### 🚜 Refactor

- *(ClassProxy)* Simplify `MethodAccess` implementation by using expression-bodied member - ([41969e0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/41969e042d3bf4fe6e61efb908853883674ee2ee))

### Action

- Update `CHANGELOG.md` - ([e7b08f4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/e7b08f4144e178d08357dcf7593d791767724643))


## [0.6.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.6.1..v0.6.2) - 2025-10-10

### 🚜 Refactor

- *(ClassProxy)* Replace `Instance` with `_internalInstance` for clarity and encapsulation - ([6b634a8](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/6b634a8d02b47e7d5b6393323f8cdb61ef944003))

### Action

- Update `CHANGELOG.md` - ([ae8c67d](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/ae8c67ddb8242fa656a0e3f62c83e4ea235f713d))


## [0.6.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.6.0..v0.6.1) - 2025-10-10

### 🐛 Bug Fixes

- *(Extensions)* Replace `NullReferenceException` with `ArgumentNullException` for null `obj` checks in reflection utilities - ([7b0dfe6](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/7b0dfe62266d9a77933545eaf2e161b234aa911b))

### Action

- Update `CHANGELOG.md` - ([1752fc6](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/1752fc6d445a5bbc2558fb8e67739d20d4bbabcd))


## [0.6.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.5.2..v0.6.0) - 2025-10-10

### ⛰️  Features

- *(Extensions)* Add generic method invocation utilities in `ObjectExtensions` and `TypeExtensions` - ([68c4a2f](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/68c4a2f2536e2b10f67ba6ecc9969184768eee1c))
- *(Extensions)* Extend `ObjectExtensions` and introduce `TypeExtensions` for enhanced reflection utilities - ([dbd653b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/dbd653b2061bd26f1580564cc46d748bb0f4bea3))

### 🐛 Bug Fixes

- *(Extensions)* Remove unnecessary null-coalescing for parameters in method invocation utilities - ([2be87eb](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/2be87eba9bb2d51bc2b9a8dcd24967f69a79213c))
- *(Extensions)* Mark `MethodAccess` methods as obsolete and recommend `MethodInvoke` as replacement - ([66f56a3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/66f56a370eacd809503fbb36b54a225b428ed42d))
- *(Extensions)* Add null/empty checks for method, field, and property names in reflection utilities - ([2549ecc](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/2549ecc145435add78a53467e0dced423d81381f))

### Action

- Update `CHANGELOG.md` - ([a138fab](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/a138fabf9ee10d748e812e65933a84f4e5f32b7b))


## [0.5.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.5.1..v0.5.2) - 2025-10-09

### 🐛 Bug Fixes

- *(Extensions)* Replace inline indexing logic in `GetValueOrDefault` with `TryGetValue` for readability and consistency - ([45a21e9](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/45a21e95bc938cd67b861c12490df7871ff6eb71))

### 🚜 Refactor

- *(Extensions)* Replace `TryGetValue` with `GetValueOrDefault` for consistency and add `DictionaryExtensions` with utility method - ([f4e4124](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/f4e412463fe3946a6fa2200f6a1851198dd4ebf8))

### Action

- Update `CHANGELOG.md` - ([21c17a5](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/21c17a51b57ffb7734e2f251852b15cca20891f3))


## [0.5.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.5.0..v0.5.1) - 2025-10-09

### ⚙️ Miscellaneous Tasks

- Bump `BepInEx-BepInExPack_Silksong` dependency to version 5.4.2304 - ([3803751](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/3803751b4f765c0d7727731f2db25e094607d5c8))

### Action

- Update `CHANGELOG.md` - ([2a2f323](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/2a2f32378c39b11cbaacef3e3d4c595045b06b88))


## [0.5.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.4.4..v0.5.0) - 2025-10-09

### ⛰️  Features

- Introduce new utility attributes and extend `Extensions` functionality - ([65c6e5d](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/65c6e5db49c6b29807eeae65786a60de310b9b51))

### Action

- Update `CHANGELOG.md` - ([a04436b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/a04436b5150f852a715762a4909332216cbc2c50))


## [0.4.4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.4.3..v0.4.4) - 2025-10-08

### 🐛 Bug Fixes

- *(workflow)* Fix upload issue - ([7382b93](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/7382b93c2771f6d8613605ff8cb73dc90765da18))

### Action

- Update `CHANGELOG.md` - ([181f89b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/181f89bcc4d717e8fad0b626b2bcbe47df065698))


## [0.4.3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.4.2..v0.4.3) - 2025-10-08

### 🐛 Bug Fixes

- *(workflows)* Add Thunderstore packaging configuration and update actions dependencies - ([36f20e4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/36f20e40a71dcf488eadb4dc7b15d446339f8e26))

### Action

- Update `CHANGELOG.md` - ([5353147](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/5353147f9eb0b94bbae45c0ab0be69aabb4d82d6))


## [0.4.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.4.1..v0.4.2) - 2025-10-08

### 🐛 Bug Fixes

- *(workflows)* Restore Thunderstore package upload step and include LICENSE in packaging - ([0e74ba4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/0e74ba4d0c515ff58794a9757a59f1af75d2f222))

### Action

- Update `CHANGELOG.md` - ([67c341f](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/67c341fbe8f0f1b2d995a10a6f2c2f826aca1192))


## [0.4.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.4.0..v0.4.1) - 2025-10-08

### 🐛 Bug Fixes

- *(workflows)* Add new version output - ([5f54307](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/5f54307133ab4341adb0c687b630277a573a5efe))

### Action

- Update `CHANGELOG.md` - ([73fd8c7](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/73fd8c77744b7a6625b1f0172e1d7427cc525187))


## [0.4.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.3.0..v0.4.0) - 2025-10-07

### ⛰️  Features

- Add `StringExtensions` class with utilities for extracting parts of a string. - ([468585b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/468585b9d5944327b141527d557cb87187522186))

### Action

- Update `CHANGELOG.md` - ([a483a0a](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/a483a0a9ebb2cb478731c14f7b3ccadc368441b1))


## [0.3.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.2.5..v0.3.0) - 2025-10-07

### ⛰️  Features

- Migrate `EX` namespace contents to `Extensions`, replace `EX` suffix with `Extensions`, and add new utility attributes - ([6e05aa3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/6e05aa3177ba08431ceddd529325a224a758be9a))

### Action

- Update `CHANGELOG.md` - ([4f1d35a](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/4f1d35af4c0556e1ab868e6294a928e4fcef270b))


## [0.2.5](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.2.4..v0.2.5) - 2025-10-06

### 🚜 Refactor

- *(EX)* Replace extension methods with `extension` keyword and add `[UsedImplicitly]` annotations - ([7ab23fa](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/7ab23fabdd0886e3df4654eb7d5c37a42d345914))

### Action

- Update `CHANGELOG.md` - ([292b41b](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/292b41bdd9020dfaf4543596ad7840c5626edac0))


## [0.2.4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.2.3..v0.2.4) - 2025-10-06

### 🐛 Bug Fixes

- *(workflows)* Update branch references from `master` to `fast` in pre-release workflow - ([1510fdf](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/1510fdf0ed64112073ad1f9f2637c808c655987b))

### Action

- Update `CHANGELOG.md` - ([463d32f](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/463d32fce0746916406a7ea5832679dfa6443b1f))


## [0.2.3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.2.2..v0.2.3) - 2025-10-06

### 🚜 Refactor

- *(workflows)* Replace changelog generation with `git-cliff` and update actions dependencies - ([65f3a42](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/65f3a425f15f68fb82be942518e3f0b1b2e2455a))

### Action

- Update `CHANGELOG.md` - ([db686fe](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/db686fe6292b44f4d0a2c0922aafbbfccc795e32))


## [0.2.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.2.1..v0.2.2) - 2025-10-05

### 🐛 Bug Fixes

- *(CHANGELOG)* Change version title to h2 format - ([d47b662](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/d47b6623c466254e220fa61d05dfa442f2c37ce7))

### Action

- Update `CHANGELOG.md` - ([58d6155](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/58d615556cde2ddf35312f11aa194624ec0bd1f6))


## [0.2.1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.2.0..v0.2.1) - 2025-10-05

### 🐛 Bug Fixes

- *(CHANGELOG)* Update version titles to h2 - ([1e3f787](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/1e3f787c84419ae35f8fe7d33f5c97d0184738d0))

### Action

- Update `CHANGELOG.md` - ([4057332](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/40573320569695b7840ff52978abc41e632e37f0))


## [0.2.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.1.0..v0.2.0) - 2025-10-05

### ⛰️  Features

- Add `BepInUtilsUnityPlugin` class and improve `ConfigBindAttribute` constructor - ([ed45206](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/ed45206c76ec7ff43842d01500bbe4edc9d3770f))

### Action

- Update `CHANGELOG.md` - ([c3ce2f8](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/c3ce2f842f47123a80835d0eeac8b24832ede4ec))


## [0.1.0](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.11..v0.1.0) - 2025-10-05

### ⛰️  Features

- Add attributes for generator - ([d6d05e3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/d6d05e3c76a761fe28be76c96d218ce6c0b14d11))

### Action

- Update `CHANGELOG.md` - ([2451168](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/2451168d8a71a6dff934bda6aea7e4574907e2ed))


## [0.0.11](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.10..v0.0.11) - 2025-10-04

### 🐛 Bug Fixes

- *(README)* Upgrade `README.md` - ([ec41794](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/ec417946a258a98110ef85ba7cb1df38d5afb145))
- *(README)* Upgrade `README.md` - ([78274ff](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/78274ffbf7ffc5aecb3890e37b1fdbbbdf46582f))
- Clean code - ([6c32e82](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/6c32e8217b7fa5aaed90f8be725872f460dda699))

### Action

- Update `CHANGELOG.md` - ([795de91](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/795de913faec6fbb9922f627573d45f3881e981a))


## [0.0.10](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.9..v0.0.10) - 2025-09-25

### 🐛 Bug Fixes

- *(Workflow)* Upgrade change log generator - ([ae4a1f2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/ae4a1f27d6f8066e6fe9d73357a4f02b3d5bfd84))

### Action

- Update `CHANGELOG.md` - ([97328d4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/97328d4046d3692d8192b4eafabf26c5c6f4a764))


## [0.0.9](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.8..v0.0.9) - 2025-09-24

### 🐛 Bug Fixes

- *(Workflow)* Add `paths-ignore` to workflow - ([a9aaae4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/a9aaae45d15b117040937560b737e28342e59937))

### Action

- Update `CHANGELOG.md` - ([575af99](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/575af99ec6fa7f7ee2ad1ce3159d43eeb4667ed8))

## New Contributors ❤️

* @github-actions[bot] made their first contribution

## [0.0.8](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.7..v0.0.8) - 2025-09-24

### 🐛 Bug Fixes

- *(Workflow)* Fix multlines string - ([7d99059](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/7d990590d729db87f6feceb1422d7505fe69b2b2))


## [0.0.7](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.6..v0.0.7) - 2025-09-24

### 🐛 Bug Fixes

- *(Workflows)* Try fix change log string - ([9a03d9e](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/9a03d9e6970de78e220d270a47477cdf1b51c8e9))


## [0.0.6](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.5..v0.0.6) - 2025-09-24

### 🐛 Bug Fixes

- *(Workflow)* Create `CHANGELOG.md` file with workflow - ([6760b79](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/6760b79dcdf0afc43bea8eaf0b5a45e25f2360f1))


## [0.0.5](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.4..v0.0.5) - 2025-09-19

### 🐛 Bug Fixes

- *(ClassProxy)* Add `className` check - ([7786af8](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/7786af808667bc368a352c650171eee863a2d779))


## [0.0.4](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.3..v0.0.4) - 2025-09-19

### 🐛 Bug Fixes

- *(ClassProxy)* `IsAbstract` check - ([6e3a7f3](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/6e3a7f3f621687bc2f8cf93d46440907596e275a))
- *(ClassProxy)* Add new constructor - ([89d84f8](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/89d84f8bc86b40db22fd72eec018c106472be447))


## [0.0.2](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/compare/v0.0.1..v0.0.2) - 2025-06-06

### 🐛 Bug Fixes

- *(TraverseEX)* Add `Value` to `TraverseEX` - ([1a549b1](https://github.com/T2PeNBiX99wcoxKv3A4g/BepInEx-Utils/commit/1a549b12ad05511736621c956b4c979a61b57e58))


## [0.0.1] - 2025-05-31

## New Contributors ❤️

* @T2PeNBiX99wcoxKv3A4g made their first contribution

<!-- generated by git-cliff -->
