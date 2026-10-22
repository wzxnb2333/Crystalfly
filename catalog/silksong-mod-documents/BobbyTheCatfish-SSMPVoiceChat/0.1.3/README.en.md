# SSMP Voice Chat
A mod and SSMP addon that implements voice chat between players.

### Features:
- Room, team, and global communication
- Positional audio
- Push to talk / Push to toggle (see [Usage](#usage))

## Install
### Client
Make sure that you have OpenAL installed.
It's a library program that facilitates recording and playing audio (positionally).
To install it:
- **Windows**: Go to [OpenAL.org](https://www.openal.org/downloads/) and download and run the installer.
- **Linux**: Most likely already installed with your distribution. Otherwise, use the package manager to install it.


SSMP.VoiceChat needs to be installed on both the server and the client.
If you host from in-game, simply install the mod and start hosting.
To install it on your client, you should put the contents of the `SSMP.VoiceChat.zip` in your `Plugins/` folder,
such that you have the folder `Plugins/SSMP.VoiceChat/` with the file `SSMPVoiceChat.dll` in it (and a bunch of others).

The mods folder can be found in your steam installation (Beware that these are the default locations.
Your install may be in a different location):
- **Windows**: `C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong\BepInEx\plugins\`
- **Mac**: `~/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/`,
- **Linux**: `~/.local/share/Steam/steamapps/common/Hollow Knight Silksong/BepInEx/plugins/`

### Standalone Server
If you are hosting a standalone server, make sure to add the following files/directories to the server directory
(these are included in the `.zip` file):
- `SSMPVoiceChat.dll`
- `SSMPVoiceChat.pdb`
- `Natives/`
- `OpenTK.dll`
- `OpenTK.dll.config`

## Usage
### Client
Configuration for most things, such as selecting a microphone or a speaker, are located in the BepInEx config.
You can use [BepInEx Config Manager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/) or [ModMenu](https://thunderstore.io/c/hollow-knight-silksong/p/silksong_modding/ModMenu/) to access these settings.
> Note that the list of devices can't be reloaded in-game. If you change your system's default mic/speaker, or plug one in, you'll need to relaunch the game to use it.

#### Input Modes
- Normal: You don't have to press any buttons to talk!
- Push to talk: Press and hold the selected key/button to enable your microphone. If the key isn't pressed, you're essentially muted.
- Push to toggle: Press the selected key/button to enable or disable your microphone. It will toggle the current mute state.

#### Commands
- `/vcc mute` : Toggle mute your voice chat, such that other can(not) hear you.
- `/vcc devices <mics|speakers>` : Lists the available microphones or speakers that can be used with voice chat. The IDs given in the output can be used in the mod configuration.

#### Input Activity Icon
There is an indicator in the upper left of the HUD that shows your current microphone status.
- Green border: Sound detected.
- Gray border: No sound detected.
- Red border, mic with circle: You're muted with push to talk or push to toggle.
- Red border, crossed out mic: You're muted with `/vcc mute`.
- Purple border: Microphone error

### Server
For the server configuration you can use the commands below. Please note that these commands require the sender to be authorized in order to execute them.
- `/vcs set <setting name> [value]` : Get or set the value of a voice chat server setting. The following settings can be used (along with their function):
    - `proximity_based_volume` (aliases: `proximity`, `prox`): Whether the volume and position of voice chat should be based on the proximity of the source and listener.
    - `team_voices_globally` (aliases: `teamglobal`, `teamglobally`): Whether to hear your team's voices globally independent of proximity or scenes.
    - `team_voices_only` (aliases: `teamonly`): Whether to hear only your team's voices and not other teams, even if they are in the same scene or in close proximity.
- `/vcs broadcast` : Toggle broadcasting your voice chat to the entire server.

## How it works
SSMP Voice Chat works using a few libraries to facilitate voice chat:
- [OpenAL](https://www.openal.org/): To record and play audio positionally
- [Opus](https://opus-codec.org/): Audio codec to efficiently encode audio for networking
- [RNNoise](https://jmvalin.ca/demo/rnnoise/): Lightweight neural network that filters out noise from audio

SSMP Voice Chat uses the SSMP API to network audio data and the server addon takes care of delivering the audio to the
correct clients based on the server configuration.