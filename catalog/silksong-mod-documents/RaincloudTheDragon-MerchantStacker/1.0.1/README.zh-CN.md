# MerchantStacker

空洞骑士：丝之歌 的便捷性模组 —— 无需反复点击确认，可批量购买可堆叠的商人补给品（如念珠、贝壳碎片等）。  

[Demonstration](https://github.com/user-attachments/assets/e88ae520-edc6-4bd1-a329-e501b698577c)

<img width="720" height="810" alt="comparison" src="https://github.com/user-attachments/assets/9258997b-eeae-4cfc-8770-6a8d8a76e890" />

## 特性

- 对符合条件的无限库存商店物品（具有堆叠上限），普通购买确认变为数量确认  
- 与 Silksong 中相同的是/否对话 — **d-pad** ±1 / **右摇杆** ±5（按住可加速）；成本和物品数量实时更新  
- 确认购买相应数量；取消则不进行任何购买  
- 适用于 **商人商店** 和 **念珠串线机** （e.g. Greymoor）

## 安装

1. 安装 [BepInExPack Silksong](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)  
2. 将 `MerchantStacker.dll` 放入 `BepInEx/plugins`（或通过 Thunderstore / r2modman 安装）  
3. 在 `BepInEx/config/io.github.raincloudthedragon.merchantstacker.cfg` 中进行配置（或使用 F1 配置管理器）

## 编译

示例：  
```bash
dotnet new silksongpath --silksong-install-path "[Path to your Steam Library]/steamapps/common/Hollow Knight Silksong"
dotnet build -c Release
```
  
需要能够编译 netstandard2.1 项目的 .NET SDK。本地 `SilksongPath.props` 被 Git 忽略。
