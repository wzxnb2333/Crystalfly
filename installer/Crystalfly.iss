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
Type: files; Name: "{app}\portable.flag"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\Crystalfly.App.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Crystalfly.App.exe"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: "crystalfly"; ValueType: string; ValueName: ""; ValueData: "URL:Crystalfly Protocol"; Flags: uninsdeletekey
Root: HKCR; Subkey: "crystalfly"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCR; Subkey: "crystalfly\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Crystalfly.App.exe,0"
Root: HKCR; Subkey: "crystalfly\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Crystalfly.App.exe"" ""%1"""

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\Crystalfly.App.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
