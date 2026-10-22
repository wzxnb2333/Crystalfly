# NativeUIMaker - 丝之歌原生 UI 组件库

NativeUIMaker 是一个为丝之歌模组开发者提供的原生 UI 组件库，通过克隆游戏内置 UI 预制体，实现与游戏本体完全一致的视觉和交互风格。

## 功能特性

- **原生按钮** (`NativeButton`)：支持图标按钮、纯文字按钮、小型按钮，保留游戏内闪光、音效、导航等原生行为。
- **原生标签** (`NativeLabel`)：标准文本标签，自动适配游戏字体和本地化。
- **原生选项行** (`NativeOption`)：带描述浮层的开关/选项行，左右切换值，自动显示描述文字。
- **原生滑块** (`NativeSlider`)：带数值显示的滑块，支持整数/浮点步进，可自由调整偏移。
- **原生箭头** (`NativeArrow`)：左右翻页光标装饰，带旋转动画，支持常驻显示和方向切换。

所有控件均从游戏运行时动态获取预制体（如 `NormalButton`、`MenuOptionHorizontal` 等），无需额外资源文件，完美复刻丝之歌菜单风格。

## 安装方法

1. 确保已安装 [BepInEx 5.4.23](https://github.com/BepInEx/BepInEx/releases) 或更高版本。  
2. 将 `NativeUIMaker.dll` 放入 `BepInEx/plugins` 文件夹。  
3. 启动游戏，按 \*\*F9\*\* 打开调试窗口即可测试所有控件。

## 使用示例（供模组开发者）

```csharp

using NativeUIMaker;

// 创建一个图标按钮
NativeButton btn = NativeButton.Create(parentRect, ButtonStyle.IconButton);
btn.SetTitle("开始游戏");
btn.SetDescription("点击开始新旅程");
btn.AddClickListener(() => Debug.Log("按钮被点击"));

// 创建一个选项行（开关）
NativeOption opt = NativeOption.Create(parentRect);
opt.SetLabel("启用随机种子");
opt.SetOptions(new[] { "关", "开" }, 0);
opt.OnValueChanged(index => Debug.Log($"当前值: {index}"));

// 创建一个箭头
NativeArrow arrow = NativeArrow.Create(parentRect, NativeArrow.Direction.Right);
arrow.SetPosition(new Vector2(100, 0));
arrow.SetClickable(true, () => Debug.Log("箭头被点击"));
