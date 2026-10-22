# RandomPingSilksong

Play Discord ping sound at random moment.

## Install

For manual installation, first [install BepinEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#installing-bepinex-1). Download the mod. Go to Silksong installation folder (where you should've installed BepinEx) and extract the mod zip file under `BepinEx/plugins`.

## Build

.NET 10 is required.

Create `SilksongPath.props` under `RandomPing` directory. Copy and paste the following text and edit as needed.

```xml
<Project>
  <PropertyGroup>
    <SilksongFolder>SilksongInstallPath</SilksongFolder>
    <!-- If you use a mod manager rather than manually installing BepInEx, this should be a profile directory for that mod manager. -->
    <SilksongPluginsFolder>$(SilksongFolder)/BepInEx/plugins</SilksongPluginsFolder>
  </PropertyGroup>
</Project>
```

```sh
dotnet build -c Release
```