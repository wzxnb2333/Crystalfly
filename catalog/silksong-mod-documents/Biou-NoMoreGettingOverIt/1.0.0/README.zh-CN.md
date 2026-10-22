# NoMoreGettingOverIt

一款空洞骑士：丝之歌模组，可让你回溯时间。再也不会丢失进度！

## 特性

- 每 10 秒自动保存一次游戏状态  
- 按下 **F2** 可回退到上一个存档  
- 在内存中保留最多 12 个存档（相当于 2 分钟的游戏时间）  
- 多次按下 F2 可回退更久之前的时间  
- 支持场景切换时的使用

## 什么会被保存

- 位置和速度
- 生命值、最大生命值、地形、丝线
- 面向方向
- 当前场景

## 安装

1. 安装空洞骑士：丝之歌的 [BepInEx 5.x](https://github.com/BepInEx/BepInEx)  
2. 从 [Releases](https://github.com/iburel/SilksongMod-NoMoreGettingOverIt/releases) 下载 `NoMoreGettingOverIt.dll`  
3. 将 DLL 文件放入 `BepInEx/plugins/`  
4. 启动游戏

## 配置

首次启动后，会在 `BepInEx/config/com.iwan.nomoregettingoverit.cfg` 创建配置文件：

| 设置项 | 默认值 | 描述 |
|--------|--------|------|
| RewindKey | F2 | 用于回退到上一个存档的按键 |
| SaveInterval | 10 | 自动存档之间的间隔（秒） |
| MaxSavestates | 12 | 内存中保留的最大存档数量 |

## 从源码构建

```bash
dotnet build -c Release
```

DLL 文件将在 `bin/Release/netstandard2.1/` 中。

## License

MIT
