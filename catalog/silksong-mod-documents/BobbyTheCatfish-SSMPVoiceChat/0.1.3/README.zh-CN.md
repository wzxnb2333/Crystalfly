# SSMP 语音聊天  
一个模组和 SSMP 插件，实现玩家之间的语音聊天。

### 功能：  
- 房间、团队及全局通信  
- 位置音频  
- 按下即发言 / 切换模式（详见 [Usage](#usage)）

## 安装  
### 客户端  
请确保已安装 OpenAL。  
它是一个用于音频录制与播放（支持空间定位）的库程序。  
安装方法如下：  
- **Windows**：前往 [OpenAL.org](https://www.openal.org/downloads/) 下载并运行安装程序。  
- **Linux**：通常已随发行版安装。若未安装，请使用包管理器进行安装。  

SSMP.VoiceChat 需要在服务器端和客户端均安装。  
如果你在游戏内搭建服务器，只需安装该模组并开始托管即可。  
要在客户端安装，请将 `SSMP.VoiceChat.zip` 的内容放入你的 `Plugins/` 文件夹中，确保文件夹结构为：包含 `Plugins/SSMP.VoiceChat/` 文件夹，其中包含文件 `SSMPVoiceChat.dll`（以及其他若干文件）。  

模组文件夹的位置通常位于 Steam 安装目录中（请注意，这些是默认路径，你的安装位置可能不同）：  
- **Windows**：`C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\`  
- **Mac**：`~/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/`  
- **Linux**：`~/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/`  

### 独立服务器  
如果你在搭建独立服务器，请确保将以下文件/目录添加到服务器目录中（这些文件包含在 `.zip` 中）：  
- `SSMPVoiceChat.dll`  
- `SSMPVoiceChat.pdb`  
- `Natives/`  
- `OpenTK.dll`  
- `OpenTK.dll.config`

## 使用方法  
### 客户端  
大多数设置，例如选择麦克风或扬声器，均位于 BepInEx 配置文件中。  
你可以使用 [BepInEx Config Manager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/) 或 [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/) 来访问这些设置。  
> 注意：设备列表无法在游戏内重新加载。如果你更改了系统默认的麦克风或扬声器，或者插入了新的设备，你需要重新启动游戏才能使用它们。

#### 输入模式  
- 正常模式：无需按任何按键即可说话！  
- 按下说话：按下并持续按住选定的键/按钮以启用麦克风。如果未按下该键，则相当于静音。  
- 按下切换：按下选定的键/按钮来开启或关闭麦克风，会切换当前的静音状态。

#### 命令  
- `/vcc mute`：切换语音聊天的静音状态，使其他玩家（不能）听到你。  
- `/vcc devices <mics|speakers>`：列出可用于语音聊天的可用麦克风或扬声器。输出中的 IDs 可用于模组配置中。

#### 输入活动图标  
HUD 左上角有一个指示器，显示当前麦克风的状态。  
- 绿色边框：检测到声音。  
- 浅灰色边框：未检测到声音。  
- 红色边框，带圆形的麦克风：使用“按下说话”或“按下切换”模式时处于静音状态。  
- 红色边框，麦克风被划掉：使用 `/vcc mute` 时处于静音状态。  
- 紫色边框：麦克风出现错误。

### 服务器  
对于服务器配置，可以使用以下命令。请注意，这些命令需要发送者具有授权权限才能执行。  
- `/vcs set <setting name> [value]`：获取或设置语音聊天服务器的参数值。可使用的设置及其功能如下：  
    - `proximity_based_volume`（别名：`proximity`，`prox`）：语音聊天的音量和位置是否基于声源与监听者之间的距离。  
    - `team_voices_globally`（别名：`teamglobal`，`teamglobally`）：是否在全局范围内听到队友的声音，不受距离或场景限制。  
    - `team_voices_only`（别名：`teamonly`）：是否仅能听到自己团队的声音，而无法听到其他团队的声音，即使他们处于同一场景或近距离。  
- `/vcs broadcast`：切换将语音聊天广播至整个服务器的状态。

## 它是如何工作的  
SSMP语音聊天通过几个库来实现语音聊天功能：  
- [OpenAL](https://www.openal.org/)：用于录制和播放空间音频  
- [Opus](https://opus-codec.org/)：音频编码器，可高效地对音频进行网络传输编码  
- [RNNoise](https://jmvalin.ca/demo/rnnoise/)：轻量级神经网络，用于从音频中过滤噪声  

SSMP语音聊天使用SSMP API来传输音频数据，服务器插件则根据服务器配置将音频发送到正确的客户端。