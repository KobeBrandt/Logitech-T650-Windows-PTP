; ==============================================================================
; Inno Setup script: Logitech T650 Tray installer (setup .exe)
; Built by Installer\Build-Installer.ps1, which passes:
;   /DAppVersion=<x.y.z>  /DSourceDir=<publish folder>  /DOutputDir=<Dist folder>
; ==============================================================================

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "bin\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\Dist"
#endif

#define AppName "Logitech T650 Tray"
#define AppExe "T650Bridge.exe"
#define AppUrl "https://github.com/KobeBrandt/Logitech-T650-Windows-PTP"
; Same value name as the tray menu's Run at Login toggle, so both see one entry.
#define RunValueName "Logitech T650 Bridge"

[Setup]
AppId={{8107820F-F509-47B3-B521-15815EFF5615}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Logitech T650 Community Project
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=License.rtf
OutputDir={#OutputDir}
OutputBaseFilename=T650Tray-Setup-v{#AppVersion}
SetupIconFile=..\src\T650Bridge\T650.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start {#AppName} when I log in"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} (Console Diag)"; Filename: "{app}\{#AppExe}"; Parameters: "--console"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "{#RunValueName}"; ValueData: """{app}\{#AppExe}"" --silent"; \
  Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--silent"; Description: "{cm:LaunchProgram,{#AppName}}"; \
  Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "StopTray"
