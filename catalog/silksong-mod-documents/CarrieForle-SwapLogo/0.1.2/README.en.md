# SwapLogo

Swap the intro Team Cherry logo with another image.

![Demonstration](https://github.com/user-attachments/assets/aabb6f8d-7bbd-4d17-8992-d3eaac57b6b0)

## Usage

Install the mod (either manual or via a mod manager) and place a image file named `logo.png` or `logo.jpg` in the same folder as the DLL. Your folder structure should look like this:

```
.
└── BepinEx/
    └── plugins/
        └── CarrieForle-SwapLogo/
            ├── icon.png
            ├── logo.png <- your image
            ├── manifest.json
            ├── README.md
            ├── SwapLogo.dll
            └── SwapLogo.pdb
```

If you use a mod manager (e.g., r2modman), the above structure should be in a profile folder. If you installed BepinEx manually, it should be in the Silksong installation folder.

png and jpg format are supported.

## Build

.NET 10 is required.

Create `SilksongPath.props`. Copy and paste the following text and edit as needed.

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
