; ------------------------------------------------------------
; LightShield Unified Installer
; Files + SYSTEM Startup Task
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
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
DisableProgramGroupPage=yes

; ------------------------------------------------------------
; FILES
; ------------------------------------------------------------

[Files]
; Global configuration (installed once)
Source: "C:\LightShield\publish\lightshield_config.json"; \
    DestDir: "{commonappdata}\LightShield"; \
    Flags: onlyifdoesntexist

; API
Source: "C:\LightShield\publish\api\LightShield.Api.exe"; \
    DestDir: "{app}\api"; Flags: ignoreversion

; Agent
Source: "C:\LightShield\publish\agent\LightShield.Agent.exe"; \
    DestDir: "{app}\agent"; Flags: ignoreversion

; LogParser
Source: "C:\LightShield\publish\logparser\LightShield.LogParser.exe"; \
    DestDir: "{app}\logparser"; Flags: ignoreversion

; Launcher
Source: "C:\LightShield\publish\launcher\LightShield.Launcher.exe"; \
    DestDir: "{app}\launcher"; Flags: ignoreversion

; Desktop UI
Source: "C:\LightShield\publish\desktop\LightShield Desktop.exe"; \
    DestDir: "{app}\desktop"; Flags: ignoreversion

; ------------------------------------------------------------
; DIRECTORIES
; ------------------------------------------------------------

[Dirs]
Name: "{commonappdata}\LightShield"; Permissions: users-full
Name: "{commonappdata}\LightShield"; Flags: uninsalwaysuninstall

; ------------------------------------------------------------
; SYSTEM STARTUP (Scheduled Task)
; ------------------------------------------------------------

[Run]
; Create SYSTEM startup task for LightShield Launcher
Filename: "schtasks.exe"; \
    Parameters: "/create /f /sc onstart /ru SYSTEM /rl HIGHEST /tn ""LightShield Launcher"" /tr ""\""{app}\launcher\LightShield.Launcher.exe\"""" ; \
    Flags: runhidden

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
