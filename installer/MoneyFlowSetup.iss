; =========================================================================
; MoneyFlow Desktop ERP — Inno Setup Script
; Master Prompt Section 58 ("INSTALLATION")
;
; Target Installation Directory: C:\Program Files\MoneyFlow\
; Output Installer Executable: MoneyFlowSetup.exe
; Architecture: Windows 64-bit (x64)
; =========================================================================

#define MyAppName "MoneyFlow Desktop ERP"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MoneyFlow Desktop Accounting"
#define MyAppExeName "MoneyFlow.Desktop.exe"
#define MyAppAssocName MyAppName + " File"
#define MyAppAssocExt ".mflow"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
; Unique application GUID
AppId={{D37F28B1-4A59-4E68-9B9D-1C387B62A481}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MoneyFlow
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=MoneyFlowSetup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
DisableWelcomePage=no
DisableProgramGroupPage=yes
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=MoneyFlow Desktop Accounting Standalone Windows Application
VersionInfoCopyright=Copyright (C) 2026 MoneyFlow

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Published .NET 8 application binaries and runtime libraries
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Exclude scratch / temporary files
; NOTE: Do not use "Flags: ignoreversion" on any shared system files

[Icons]
; Start Menu shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional task)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Option to launch the application immediately after setup completes
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\appsettings.json.bak"
Type: dirifempty; Name: "{app}"
