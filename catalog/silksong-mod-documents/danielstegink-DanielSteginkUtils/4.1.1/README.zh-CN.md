# DanielSteginkUtils  
一个包含我在模组开发过程中积累的各种辅助类和逻辑的代码库。

## 通用结构
- ExternalFiles - 用于与外部文件交互的工具，例如 AssetBundles
	- GetAssetBundle - 加载资源包
	- GetAudioClip - 将嵌入式资源中的音频文件转换为 AudioClips
	- GetSprite - 将嵌入式资源中的图像文件加载为精灵图
- 工具类 - 用于实现常见游戏机制的工具
	- DamageEnemy - 对敌人造成伤害
	- GetEnemy - 获取附近的敌人
	- GetTools - 获取已装备的工具列表
	- Needolin - 处理 Needolin 的音效变化
- 日志记录器 - 专门用于日志记录的工具
	- EnemyDamagerLogger - 记录敌人受到伤害的时刻。需通过 EnemyDamageLogger.Toggle 手动启用
- 工具库 - 各种逻辑和计算用的库
	- 计算 - 执行数值计算与转换
	- ClassIntegrations - 从其他类中访问属性、字段和方法
	- 组件 - 处理 Unity 组件
	- NotchCosts - 将不同增益的值按空洞骑士中的魅力刻度进行计算

## 特别致谢  
logo 由 cristina233_ 设计