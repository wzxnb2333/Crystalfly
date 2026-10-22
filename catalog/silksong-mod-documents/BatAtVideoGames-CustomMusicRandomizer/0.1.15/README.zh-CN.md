# 自定义音乐随机器

使用自定义曲目、原版曲目或两者结合，随机化区域、战斗、首领、菜单及片尾音乐。
该模组是为 Silksong AP 随机器设计的，因为它会关联种子，确保整个游戏过程中音乐保持一致，但也可在该框架之外使用。

该模组默认处于禁用状态。首次启动后，请在 `BepInEx/config/moriko.silksong.custommusicrandomizer.cfg` 中启用它，然后重启 Silksong 以创建自定义曲目文件夹。

将 `.ogg`、`.wav`、`.mp3`、`.m4a` 或 `.flac` 文件添加到该模组安装包内的生成文件夹 `CustomMusic/Area`、`Battle`、`Boss`、`Menu` 或 `Fanfare` 中。长曲目会循环播放，片尾音乐仅播放一次。类别、池、显示和种子选项在配置文件中设置。
