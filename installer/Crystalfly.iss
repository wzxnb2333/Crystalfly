#ifndef PublishDir
  #error PublishDir must be defined by the release build script
#endif

#define AppName "Crystalfly"
#ifndef AppVersion
  #error AppVersion must be defined by the release build script
#endif

[Setup]
AppId={{5B0B81C8-D29A-46E5-A731-B889F5C2BE34}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName=D:\Program Files\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=Crystalfly-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\Crystalfly.App.exe

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\Avalonia.Themes.Fluent.dll"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\Crystalfly.App.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Crystalfly.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\Crystalfly.App.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
