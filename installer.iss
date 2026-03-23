; CC Pad - Inno Setup Script
; 使用方法:
;   1. dotnet publish CCPad/CCPad.csproj -c Release -p:PublishProfile=win-x64
;   2. 安装 Inno Setup: https://jrsoftware.org/isinfo.php
;   3. 用 Inno Setup 编译此脚本，或命令行: iscc installer.iss

#define MyAppName "CC Pad"
#define MyAppExeName "CCPad.exe"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "CCPad.dev"
#define PublishDir "CCPad\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{B7E3F2A1-5C4D-4E6F-8A9B-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\Assets\claude.ico
OutputDir=installer_output
OutputBaseFilename=CCPad-Setup-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
SetupIconFile=CCPad\Assets\claude.ico

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 CC Pad"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterApp"
