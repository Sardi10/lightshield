; ------------------------------------------------------------
; LightShield Unified Installer
; SYSTEM Startup via Scheduled Task (XML)
; ------------------------------------------------------------

[Setup]
AppName=LightShield
AppVersion=1.1.2
DefaultDirName={commonpf}\LightShield
DefaultGroupName=LightShield
OutputDir=.
OutputBaseFilename=LightShield-Installer
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes
AlwaysRestart=yes

; ------------------------------------------------------------
; FILES
; ------------------------------------------------------------

[Files]
; Global configuration (persistent)
Source: "C:\LightShield\publish\lightshield_config.json"; \
    DestDir: "{commonappdata}\LightShield"; \
    Flags: onlyifdoesntexist

; API binaries
Source: "C:\LightShield\api\LightShield.Api.exe"; \
    DestDir: "{app}\api"; Flags: ignoreversion

Source: "C:\LightShield\api\appsettings.json"; \
    DestDir: "{app}\api"; Flags: ignoreversion

; Agent
Source: "C:\LightShield\agent\LightShield.Agent.exe"; \
    DestDir: "{app}\agent"; Flags: ignoreversion

; LogParser
Source: "C:\LightShield\logparser\LightShield.LogParser.exe"; \
    DestDir: "{app}\logparser"; Flags: ignoreversion
    
; Agent config
Source: "C:\LightShield\agent\lightshield_config.json"; \
    DestDir: "{app}\agent"; \
    Flags: ignoreversion

; LogParser config
Source: "C:\LightShield\logparser\lightshield_config.json"; \
    DestDir: "{app}\logparser"; \
    Flags: ignoreversion


; Launcher
Source: "C:\LightShield\launcher\LightShield.Launcher.exe"; \
    DestDir: "{app}\launcher"; Flags: ignoreversion

; Desktop UI
Source: "C:\LightShield\desktop\LightShield Desktop.exe"; \
    DestDir: "{app}\desktop"; Flags: ignoreversion

; Scheduled task XML
Source: "lightshield-task.xml"; \
    DestDir: "{app}"; Flags: ignoreversion

; ------------------------------------------------------------
; DIRECTORIES
; ------------------------------------------------------------

[Dirs]
Name: "{commonappdata}\LightShield"; Permissions: users-full

; ------------------------------------------------------------
; ENVIRONMENT VARIABLE
; ------------------------------------------------------------

[Registry]
Root: HKLM; \
Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
ValueType: string; ValueName: "LIGHTSHIELD_API_URL"; \
ValueData: "http://localhost:5213"; \
Flags: preservestringtype

; ------------------------------------------------------------
; SYSTEM STARTUP (Scheduled Task)
; ------------------------------------------------------------

[Run]
; Remove any existing task safely
Filename: "{sys}\schtasks.exe"; \
Parameters: "/delete /f /tn ""LightShield Launcher"""; \
Flags: runhidden waituntilterminated

; Register task using XML (SYSTEM + WorkingDirectory)
Filename: "{sys}\schtasks.exe"; \
Parameters: "/create /f /tn ""LightShield Launcher"" /xml ""{app}\lightshield-task.xml"""; \
Flags: runhidden waituntilterminated; \
StatusMsg: "Registering LightShield system startup service..."

; ------------------------------------------------------------
; SHORTCUTS
; ------------------------------------------------------------

[Icons]
Name: "{group}\LightShield Desktop"; \
Filename: "{app}\desktop\LightShield Desktop.exe"

Name: "{commondesktop}\LightShield Desktop"; \
Filename: "{app}\desktop\LightShield Desktop.exe"; \
Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create Desktop Shortcut"

; ------------------------------------------------------------
; UNINSTALL
; ------------------------------------------------------------

[UninstallRun]
Filename: "{sys}\schtasks.exe"; \
Parameters: "/delete /f /tn ""LightShield Launcher"""; \
Flags: runhidden; RunOnceId: "RemoveLightShieldTask"

; ------------------------------------------------------------
; MESSAGES
; ------------------------------------------------------------

[Messages]
FinishedRestartLabel=LightShield has been installed. A system restart is recommended to activate background protection.
FinishedRestartMessage=LightShield installs system-level background services that start at boot. Restarting now is recommended.
