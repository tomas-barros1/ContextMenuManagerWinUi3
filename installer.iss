; Inno Setup script for Context Menu Manager (WinUI 3)
; Usage: iscc installer.iss /DAppVersion=1.2.3
; Expects the app published to .\publish (dotnet publish -r win-x64 -p:SelfContained=true)

#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

[Setup]
AppId={{6F5B3C2A-9D4E-4A8F-B7C1-3E5A2D9B8F04}
AppName=Context Menu Manager
AppVersion={#AppVersion}
AppPublisher=tomas-barros1
AppPublisherURL=https://github.com/tomas-barros1/ContextMenuManagerWinUi3
AppSupportURL=https://github.com/tomas-barros1/ContextMenuManagerWinUi3/issues
DefaultDirName={autopf}\ContextMenuManager
DefaultGroupName=Context Menu Manager
UninstallDisplayIcon={app}\ContextMenuManager.exe
OutputDir=dist
OutputBaseFilename=ContextMenuManager-Setup-{#AppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=LICENSE

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Context Menu Manager"; Filename: "{app}\ContextMenuManager.exe"
Name: "{autodesktop}\Context Menu Manager"; Filename: "{app}\ContextMenuManager.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ContextMenuManager.exe"; Description: "{cm:LaunchProgram,Context Menu Manager}"; Flags: postinstall nowait skipifsilent
