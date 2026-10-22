
## v0.0.4

## NativeUIMaker 公共 API

### 1. 初始化
- **`Initialize(string configPath)`**  
  - 中文：加载指定路径的 JSON 配置文件，创建所有页面和控件。需在 UI 就绪后调用一次。  
  - English: Loads a JSON config and builds all pages/controls. Call once after UI is ready.

- **`IsUIReady`**（属性）  
  - 中文：返回是否已初始化完成。  
  - English: Returns `true` after initialization.

### 2. 页面控制
- **`ShowPage(string pageName)`**  
  - 中文：显示指定名称的页面，自动隐藏其他所有页面。  
  - English: Shows a page by name, hiding all others.

- **`HidePage(string pageName)`**  
  - 中文：隐藏指定页面。  
  - English: Hides a specific page.

- **`HideAllPages()`**  
  - 中文：隐藏所有页面。  
  - English: Hides all pages.

- **`GetCurrentPage()`**  
  - 中文：返回当前可见的 `NativePage` 对象（若存在）。  
  - English: Returns the currently visible `NativePage` (if any).

### 3. 控件获取（通过页面名和控件名）
- **`GetButton(pageName, buttonName)`** → `NativeButton`
- **`GetLabel(pageName, labelName)`** → `NativeLabel`
- **`GetSlider(pageName, sliderName)`** → `NativeSlider`
- **`GetOption(pageName, optionName)`** → `NativeOption`
- **`GetArrow(pageName, arrowName)`** → `NativeArrow`
- **`GetInput(pageName, inputName)`** → `NativeInput`

### 4. 控件属性读写（简化示例）
- **`SetButtonTitle(pageName, buttonName, title)`** / **`GetButtonTitle(...)`**
- **`SetSliderValue(pageName, sliderName, value)`** / **`GetSliderValue(...)`**
- **`SetOptionIndex(pageName, optionName, index)`** / **`GetOptionIndex(...)`** / **`GetOptionValue(...)`**
- **`SetInputText(pageName, inputName, text)`** / **`GetInputText(...)`**

### 5. 全局事件订阅
- **`OnUIEvent`**（事件）  
  - 中文：订阅该事件可接收所有控件（按钮点击、滑块值变化、选项切换、输入框文本变化）的全局通知。事件参数 `NativeUIEventArgs` 包含 `ControlName`、`ControlType`、`EventType`、`Value`、`Source`。  
  - English: Subscribe to receive global notifications for all controls (clicks, slider changes, option toggles, input text changes). EventArgs include `ControlName`, `ControlType`, `EventType`, `Value`, `Source`.

### 6. 辅助功能
- **`ShowDebugWindow()`** / **`HideDebugWindow()`**  
  - 中文：显示/隐藏内置的调试窗口（开发辅助）。  
  - English: Shows/hides the built-in debug window (developer aid).

- **`SaveCurrentConfig()`**  
  - 中文：将当前所有控件的状态保存到配置文件（覆盖原文件）。  
  - English: Saves current control states to the config file (overwrites).

- **`ReloadUI()`**  
  - 中文：重新加载配置文件并重建 UI（需已初始化）。  
  - English: Reloads the config and rebuilds UI (must be initialized).

---

**注意事项**：所有 `Get*` 方法在控件不存在时返回 `null`。所有 `Set*` 方法需确保页面和控件名称正确。初始化前调用任何方法会抛出异常，请先检查 `IsUIReady`。

**Notes**: All `Get*` methods return `null` if control not found. All `Set*` methods require correct page/control names. Calling any method before initialization throws an exception; check `IsUIReady` first.

## v0.0.3

-现在外部新加控件可以保存到配置，并且可以选择外部控件的所在页面固定

-New external controls can now be saved to the config, and you can choose which 
-page they belong to when pinning them.

-本地化增加

-Added localization support.

-ctrl快捷键可快速切换是否可拖动

-Ctrl hotkey can now quickly toggle draggable mode.

---
## v0.0.2

### - 新增本地化系统（UILocale）
- Added localization system (UILocale)

调试窗口顶部增加 CN / EN / Auto 按钮，手动切换语言。Auto 模式自动检测游戏语言（读取 `FontManager._currentLanguage` 或 `GameSettings.language`）。  
Debug window top added CN/EN/Auto buttons to manually switch language. Auto mode detects game language (`FontManager._currentLanguage` or `GameSettings.language`).

加载配置文件时自动翻译控件文本（标题、描述、标签、选项、占位符等），无需重新编译。  
Control texts (title, description, label, options, placeholder, etc.) are auto‑translated when loading a config file, no recompile needed.

---

### - 新增页面管理（NativePage）
- Added page management (NativePage)

`NativePage` 类：可创建全屏页面容器，支持显示/隐藏、返回按钮、标题。  
`NativePage` class: full‑screen page container with show/hide, back button, and title.

`UIManagerEx.ShowPage(string pageName)` 切换页面，自动隐藏其他页面。  
`UIManagerEx.ShowPage(string pageName)` switches pages, auto‑hides others.

---

### - 新增配置加载器（UIManagerEx）
- Added config loader (UIManagerEx)

`InitializeUI(string configPath)` 从 JSON 文件批量创建页面和控件。  
`InitializeUI(string configPath)` batch‑creates pages and controls from a JSON file.

配置文件格式：`ConfigData`，包含 `controls` 数组，每个元素定义控件的类型、名称、页面、位置、大小、文本等。  
Config format: `ConfigData` with a `controls` array; each element defines control type, name, page, position, size, text, etc.

初始化后所有页面默认隐藏。  
All pages are hidden by default after init.

---

### - 新增输入框控件（NativeInput）
- Added input field control (NativeInput)

手动创建的输入框，包含背景、文本组件、占位符。提供 `GetText/SetText` 和 `OnTextChanged` 事件。  
Manually created input field with background, text component, placeholder. Provides `GetText/SetText` and `OnTextChanged` event.

自动注册到 `NativeUIEventDispatcher`，值变化时触发统一事件。  
Auto‑registers with `NativeUIEventDispatcher`, triggers unified event on value change.

---

### - 滑块控件增强（NativeSlider）
- Slider control enhanced (NativeSlider)

新增偏移量调节：滑块 X 偏移、滑块宽度覆盖、标签 X 偏移、数值 X 偏移。  
Added offsets: slider X offset, slider width override, label X offset, value X offset.

修复预制体干扰组件清除逻辑，确保数值显示正确。  
Fixed cleanup of interfering components on prefab, ensuring correct value display.

---

### - 选项行控件增强（NativeOption）
- Option row enhanced (NativeOption)

自动显示描述浮层（游戏原生行为）。通过 `SetDescription` 设置描述文本。  
Auto‑shows description tooltip (vanilla behaviour). Use `SetDescription` to set description text.

增加 `OptionValueWatcher` 实时监听选项索引变化，不依赖 `MenuOptionHorizontal` 事件。  
Added `OptionValueWatcher` to monitor option index changes in real time, without relying on `MenuOptionHorizontal` events.

---

### - 拖拽功能完善（DragHandler）
- Drag functionality improved (DragHandler)

拖拽仅对 DebugWindow 中选中的控件生效。  
Dragging only affects the control selected in DebugWindow.

拖拽期间临时禁用按钮交互和射线检测，结束后恢复。  
Temporarily disables button interactivity and raycast during drag, restores after.

拖拽结束自动刷新 DebugWindow 编辑区域的数值。  
Auto‑refreshes DebugWindow edit area values after drag ends.

---

### - 调试窗口（DebugWindow）大幅扩展
- DebugWindow greatly expanded

#### 本地控件标签页
#### Local Controls tab

- 创建按钮（图标/纯文字/小型），支持图标路径、切换文字序列。
  Create buttons (Icon/Text/Small), supports icon path and toggle text sequences.

- 创建文本标签，可调字体大小、颜色、对齐方式。
  Create text labels with font size, colour, alignment.

- 创建滑块，可调标签、最小值/最大值、整数步进及偏移。
  Create sliders with label, min/max, integer step, offsets.

- 创建选项行，可设选项列表、默认索引、标签、描述。
  Create option rows with option list, default index, label, description.

- 创建箭头（左/右/上/下），可设可点击和拖拽。
  Create arrows (left/right/up/down), with clickable and draggable options.

- 创建输入框，可设初始文本和占位符。
  Create input fields with initial text and placeholder.

- 所有已创建控件列出，点击即选中编辑。
  Listed created controls, click to select and edit.

#### 外部控件标签页
#### External Controls tab

- “扫描外部控件”按钮：发现其他模组创建的 Native 控件实例。
  “Scan External Controls”: discovers Native control instances created by other mods.

- “保存配置”按钮：将所有控件属性导出为 `NativeUIMaker_Config.json`（位置、大小、文本、选项列表等）。
  “Save Config”: exports all control properties to `NativeUIMaker_Config.json` (position, size, text, option lists, etc.).

- “加载配置”按钮：从 JSON 恢复控件状态，并自动翻译文本。
  “Load Config”: restores control states from JSON, auto‑translates texts.

- 外部控件按类型分组显示，点击即可编辑。
  External controls grouped by type, click to edit.

#### 页面管理标签页
#### Page Management tab

- 列出所有 `NativePage` 实例。
  Lists all `NativePage` instances.

- 选中页面后可保存/加载该页面内所有控件的配置（`PageConfigs` 子文件夹）。
  After selecting a page, save/load config for all controls inside that page (`PageConfigs` subfolder).

- “刷新”按钮重新扫描当前页面内的控件。
  “Refresh” rescans controls inside the current page.

#### 统一编辑区域
#### Unified Editor

- 选中控件后显示编辑面板，可修改标题、描述、位置、尺寸、偏移量、拖拽许可、切换文字序列等。
  Shows editor panel for selected control: modify title, description, position, size, offsets, draggable flag, toggle texts, etc.

- 支持删除控件，自动销毁 GameObject 并从列表移除。
  Supports deleting control, auto‑destroys GameObject and removes from list.

---

### - 预制体管理增强（PrefabMenuObjects）
- Prefab management enhanced (PrefabMenuObjects)

优化从 `UIManager` 各界面获取预制体的逻辑，增加后备创建路径，确保原生预制体缺失时自动生成功能完整的后备控件。  
Improved fetching of prefabs from various `UIManager` screens, added fallback creation paths to generate fully functional fallback controls when native prefabs are missing.

---

### - 父物体管理优化（UIParent）
- Parent object management optimised (UIParent)

`GetDefaultParent` 等待最多 3 秒获取 `UICanvas`，避免启动初期创建失败。  
`GetDefaultParent` waits up to 3 seconds for a valid `UICanvas`, preventing early creation failures.

增加协程延迟搜索，保底使用场景中任意 Canvas。  
Added coroutine‑based delayed search, fallback to any Canvas in scene.

---

### - 全局事件分发器（NativeUIEventDispatcher）
- Global event dispatcher (NativeUIEventDispatcher)

所有控件创建时自动注册到 `OnNativeUIEvent`。外部模组可订阅该事件统一响应按钮点击、滑块变化、选项切换、输入框变化等。  
All controls auto‑register to `OnNativeUIEvent`. External mods can subscribe to uniformly respond to button clicks, slider changes, option toggles, input field changes, etc.

事件参数包含控件名称、类型、事件类型、变化值、源引用。  
Event args contain control name, type, event type, changed value, source reference.

---

### - 本地化字典大幅扩充（UILocale）
- Localisation dictionary greatly expanded (UILocale)

`EnglishTranslations` 增至 200+ 条，覆盖调试窗口所有文字。  
`EnglishTranslations` increased to 200+ entries, covering all debug window texts.

`ControlTextTranslations` 新增近百条常用映射，用于配置加载时翻译。  
`ControlTextTranslations` added nearly 100 common mappings for config loading translation.

`TranslateControlConfig` 递归翻译 `UIControlConfig` 中所有文本字段。  
`TranslateControlConfig` recursively translates all text fields in `UIControlConfig`.

---

### - UI 布局改进
- UI layout improvements

调试窗口可拖拽标题栏，窗口位置保持。  
Debug window title bar draggable, position persists.

窗口尺寸动态适应屏幕高度，初始位置固定在底部。  
Window size adapts to screen height, initial position fixed at bottom.

控件列表使用滚动视图，防止过多控件时超出高度。  
Control lists use scroll views, preventing height overflow.

---

### - 代码稳定性提升
- Code stability improvements

移除原生 UI 组件中的干扰组件（`AutoLocalizeTextUI`、`EventTrigger`、`StartGameEventTrigger`、`AudioSource` 等）。  
Removed interfering components from native UI objects (`AutoLocalizeTextUI`, `EventTrigger`, `StartGameEventTrigger`, `AudioSource`, etc.).

增加空值检查和异常处理，防止预制体缺失或组件查找失败时崩溃。  
Added null checks and exception handling to prevent crashes due to missing prefabs or component lookup failures.

控件名称唯一性生成（`EnsureUniqueNames`），避免保存配置时重名冲突。  
Control names uniqueness generation (`EnsureUniqueNames`) to avoid name conflicts when saving configs.

---

### - 已知问题
- Known issues

上下箭头使用旋转后的左箭头纹理，形状可能不完美，但功能正常。  
Up/down arrows use rotated left arrow texture, shape may not be perfect but functionality works.

某些游戏版本中 `CursorLeft`/`CursorRight` 子物体名称可能不同，若找不到箭头将显示后备空白纹理。  
In some game versions, `CursorLeft`/`CursorRight` child names may differ; if arrows not found, fallback blank texture is shown.

---

### - 计划
- Roadmap

增加更多原生控件（`MenuToggle`、`MenuDropdown`）。  
Add more native controls (`MenuToggle`, `MenuDropdown`).

优化上下箭头纹理（独立生成）。  
Improve up/down arrow textures (generate independently).

支持从外部 JSON 动态加载翻译。  
Support dynamic translation loading from external JSON.

v0.0.1



\### 初始发布



\- \*\*原生按钮组件\*\* (`NativeButton`)

&#x20; - 支持 `IconButton`（带图标、标题、描述）、`TextButton`（纯文字）、`SmallButton`（小型按钮）三种样式。

&#x20; - 自动清除预制体上的干扰组件（`AutoLocalizeTextUI`、`EventTrigger`、`StartGameEventTrigger`）。

&#x20; - 提供位置、大小、标题、描述、拖拽、点击等 API。



\- \*\*原生标签组件\*\* (`NativeLabel`)

&#x20; - 克隆游戏内标准文本标签，支持字体大小、颜色、对齐方式调整。

&#x20; - 自动移除本地化组件。



\- \*\*原生选项行组件\*\* (`NativeOption`)

&#x20; - 封装游戏内的 `MenuOptionHorizontal`，支持选项列表切换。

&#x20; - 自动显示描述浮层（当焦点在控件上时）。

&#x20; - 提供值变化回调。



\- \*\*原生滑块组件\*\* (`NativeSlider`)

&#x20; - 基于游戏内 `Slider` 预制体，支持数值显示、偏移调整。

&#x20; - 可自定义最小值、最大值、整数/浮点步进。



\- \*\*原生箭头组件\*\* (`NativeArrow`)

&#x20; - 支持上、下、左、右四个方向（上下方向通过旋转左箭头实现）。

&#x20; - 常驻显示，可点击、可拖拽。

&#x20; - 方向在创建后锁定，拖拽不会改变方向。



\- \*\*调试窗口\*\* (`DebugWindow`)

&#x20; - 按 F9 打开，可实时创建、编辑、拖拽所有控件。

&#x20; - 支持中英文手动切换（CN/EN/Auto 按钮）。



\- \*\*本地化支持\*\* (`UILocale`)

&#x20; - 内置中英文翻译字典，可通过 CN/EN/Auto 按钮手动切换语言。

&#x20; - Auto 模式自动检测游戏语言（反射 `FontManager.\_currentLanguage` 或 `GameSettings.language`）。



\- \*\*预制体管理\*\* (`PrefabMenuObjects`)

&#x20; - 动态从游戏运行时获取按钮、标签、滑块、选项行等预制体，并缓存为 DontDestroyOnLoad。

&#x20; - 无需打包任何资源文件。



\- \*\*父物体管理\*\* (`UIParent`)

&#x20; - 自动获取 `UIManager.instance.UICanvas` 作为默认父物体，确保控件正确显示。



\- \*\*拖拽支持\*\* (`DragHandler`)

&#x20; - 所有控件均可启用拖拽功能（鼠标左键拖动改变位置）。



\## 已知问题



\- 上下箭头使用旋转后的左箭头，形状可能不对称，但功能正常。

\- 某些游戏版本中 `CursorLeft`/`CursorRight` 子物体名称可能不同，若找不到箭头将显示后备纹理。



\## 计划



\- 增加更多原生控件（如 `MenuToggle`、`MenuDropdown`）。

\- 优化上下箭头的纹理（独立生成）。

\- 支持从外部 JSON 加载翻译。

