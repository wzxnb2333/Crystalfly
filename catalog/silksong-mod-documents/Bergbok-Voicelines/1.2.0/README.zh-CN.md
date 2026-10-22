当大黄蜂执行特定动作时会播放音效。  
包含 [some sound effects from the first game](./Voicelines/SFX/)。

支持自定义音效，安装后只需将音效文件添加到 SFX 文件夹即可。  
任何 [file format supported by Unity](https://docs.unity3d.com/6000.2/Documentation/Manual/AudioFiles-compatibility.html) 都可以使用。

可以同时选择多个音效，程序将随机选取其中一个。  
通过编辑配置文件可以调整音效的播放概率。  
重复添加音效可提高其出现概率，若添加不存在的音效，则有可能完全不播放。

<details>  
<summary><strong>视频</strong></summary>

<br>

[default.mp4](https://github.com/user-attachments/assets/3e3813d6-4924-4982-8823-86eb9576f808)  
[custom-sound.mp4](https://github.com/user-attachments/assets/c6c63ed8-a299-40be-aace-94eba34b1a71)  
[random.mp4](https://github.com/user-attachments/assets/cd6d4045-beca-4860-a592-76896960c859)  
[shaw.mp4](https://github.com/user-attachments/assets/f8e58b28-b378-4999-8f3e-3baa8fb070e5)  

</details>

<details>  
<summary><strong>支持的事件</strong></summary>

- 攻击  
- 缚丝 / 治疗  
- 刺爪 / 钩射  
- 裁缝线 / 拦挡  
- 闪避 / 跑动攻击  
- 死亡  
- 飞行斗篷 / 浮空  
- 芳草斗篷 / 双跳  
- 受伤  
- 跳跃  
- 熔岩铃铛击中  
- 美甲 / 充能攻击  
- 需要林  
- 白色指甲  
- 环状挑衅 / 波什卡  
- 神秘怒气  
- 锐镖  
- 丝线翱翔 / 超级跳跃  
- 丝线矛  
- 快速步进  
- 挑衅  
- 线条风暴  
- 防御铃铛击中  

</details>

灵感来自 [something Skurry said](https://youtu.be/KoL2oD1TQuo?t=2930)。

## 手动安装

1. [Install BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#installing-bepinex-1)  
2. 如果在 Steam 上运行 [*nix](https://en.wikipedia.org/wiki/Unix-like) 系统，需 [set up launch options](https://docs.bepinex.dev/articles/advanced/steam_interop.html#2-set-up-permissions)  
3. 从 [GitHub releases](https://github.com/Bergbok/Silksong-Mods/releases?q=Voicelines) 或 [Thunderstore](https://thunderstore.io/c/hollow-knight-silksong/p/Bergbok/Voicelines) 下载模组  
4. 下载依赖项：[FsmUtil](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/FsmUtil/)  
5. 将下载的归档文件解压至 `silksong_path/BepInEx/plugins`  
6. 启动游戏  
7. 通过 BepInEx 的配置管理器（默认为 F1）进行 [Edit mod configs](https://docs.bepinex.dev/articles/user_guide/configuration.html#configuring-plugins)，或直接编辑 `silksong_path/BepInEx/plugins` 中的文件  

请考虑给它 [on GitHub](https://github.com/Bergbok/Silksong-Mods) 一个星标！
