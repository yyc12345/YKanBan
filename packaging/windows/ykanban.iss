; Inno Setup script for the Windows installer.
;
;   ISCC.exe /DMyAppVersion=<version> /DPublishDir=<publish-dir> /DOutputDir=<dist-dir> packaging/windows/ykanban.iss
;
; The setup UI is English only: the single [Languages] entry uses the stock
; Default.isl, so no language dialog is shown. The install scope is chosen at
; startup (all users vs current user); HKA routes the context-menu registry
; entries to HKLM or HKCU accordingly, and {autopf} picks the matching
; program folder.

#ifndef MyAppVersion
#define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
#define PublishDir "publish/win-x64"
#endif
#ifndef OutputDir
#define OutputDir "dist"
#endif

#define MyAppName "YKanBan"
#define MyAppExeName "YKanBan.exe"

[Setup]
AppId={{80A9F6AC-F6EB-48A8-A28C-EE7B0D6A6E81}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=YKanBan-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\..\YKanBan\Assets\ykanban.ico
LicenseFile=..\..\LICENSE
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ContextMenuTask=Add "Open in YKanBan" to the folder context menu
OpenInYKanBan=Open in YKanBan

[Tasks]
Name: "foldercontextmenu"; Description: "{cm:ContextMenuTask}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Registry]
; Folder context menu: right-clicking a folder opens it in YKanBan (%1).
Root: HKA; Subkey: "Software\Classes\Directory\shell\YKanBan"; ValueType: string; ValueName: ""; ValueData: "{cm:OpenInYKanBan}"; Flags: uninsdeletekey; Tasks: foldercontextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\YKanBan"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey; Tasks: foldercontextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\YKanBan\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: foldercontextmenu
; Folder background: right-clicking the empty area of a folder (%V).
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\YKanBan"; ValueType: string; ValueName: ""; ValueData: "{cm:OpenInYKanBan}"; Flags: uninsdeletekey; Tasks: foldercontextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\YKanBan"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey; Tasks: foldercontextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\YKanBan\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%V"""; Flags: uninsdeletekey; Tasks: foldercontextmenu

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
