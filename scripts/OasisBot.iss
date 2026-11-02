[Setup]
#define AppName "OasisBot"
#define AppVersion "2.10.1"
#define AppPublisher "Silkroad Developer Community"
#define AppURL "https://github.com/Silkroad-Developer-Community/OasisBot"
#define AppExeName "OasisBot.exe"

AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes

; "PrivilegesRequired=admin" is necessary for OasisBot to attach to the game process
PrivilegesRequired=admin
OutputDir=..
OutputBaseFilename=OasisBot-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern dark includetitlebar
AppMutex=OasisBotMutex
CloseApplications=yes

[Dirs]
Name: "{app}"; Permissions: users-modify

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; TODO: move python files to config
[Files]
; Main application and standard data (ignores version to always overwrite core files)
Source: "..\Build\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "..\Build\User\*, ..\Build\Data\Python\Plugins\*"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up runtime-generated logs but preserve User settings
Type: filesandordirs; Name: "{app}\Build\User\Logs"

