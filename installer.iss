[Setup]
AppName=Prospect Server Overlay
AppVersion=1.2
AppId={{12345678-1234-1234-1234-123456789ABC}}
DefaultDirName={autopf}\Prospect Server Overlay
DefaultGroupName=Prospect Server Overlay
OutputDir=.
OutputBaseFilename=ProspectServerOverlay_Installer
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
SetupIconFile=
UninstallDisplayIcon={app}\ProspectServerOverlay.exe
; Enable version checking and upgrading
AppVerName=Prospect Server Overlay 1.2
AppPublisher=Ondario
AppPublisherURL=https://github.com/ondario
AppSupportURL=https://github.com/ondario/prospect-server-overlay
AppUpdatesURL=https://github.com/ondario/prospect-server-overlay/releases
; Upgrade/uninstall previous version automatically
Uninstallable=yes
CreateUninstallRegKey=yes
UpdateUninstallLogAppName=yes

[Files]
; Main application executable (single-file deployment)
Source: "Release\ProspectServerOverlay.exe"; DestDir: "{app}"; Flags: ignoreversion

; Configuration and documentation
Source: "Release\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "Release\README.txt"; DestDir: "{app}"; Flags: ignoreversion

; Native runtime dependencies (not embedded in single-file exe)
Source: "Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Prospect Server Overlay"; Filename: "{app}\ProspectServerOverlay.exe"
Name: "{group}\Uninstall Prospect Server Overlay"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\ProspectServerOverlay.exe"; Description: "Launch Prospect Server Overlay"; Flags: postinstall nowait skipifsilent unchecked

[InstallDelete]
; Clean up any leftover files from previous installations that might conflict
; This runs BEFORE the new files are installed, ensuring a clean upgrade
Type: files; Name: "{app}\debug.log"
Type: files; Name: "{app}\*.tmp"
Type: files; Name: "{app}\*.bak"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
