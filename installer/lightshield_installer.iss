; ------------------------------------------------------------
; LightShield Installer (Corrected, Warning-Free)
; ------------------------------------------------------------

[Setup]
AppName=LightShield
AppVersion=1.0.0
DefaultDirName={commonpf}\LightShield
DefaultGroupName=LightShield
OutputDir=.
OutputBaseFilename=LightShield-Installer
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin

[Files]
    ; Install the LightShield global configuration file
Source: "C:\Users\sardi\source\repos\lightshield\lightshield_config.json"; \
    DestDir: "{app}"; DestName: "lightshield_config.json"; Flags: onlyifdoesntexist



; API
Source: "C:\Users\sardi\source\repos\lightshield\src\LightShield.Api\bin\Release\net9.0\win-x64\publish\LightShield.Api.exe"; \
    DestDir: "{app}"; Flags: ignoreversion

; Agent
Source: "C:\Users\sardi\source\repos\lightshield\src\LightShield.Agent\bin\Release\net9.0\win-x64\publish\LightShield.Agent.exe"; \
    DestDir: "{app}"; Flags: ignoreversion

; LogParser
Source: "C:\Users\sardi\source\repos\lightshield\src\LightShield.LogParser\bin\Release\net9.0\win-x64\publish\LightShield.LogParser.exe"; \
    DestDir: "{app}"; Flags: ignoreversion

; Launcher
Source: "C:\Users\sardi\source\repos\lightshield\src\LightShield.Launcher\bin\Release\net9.0\win-x64\publish\LightShield.Launcher.exe"; \
    DestDir: "{app}"; Flags: ignoreversion

; Desktop App (Tauri build)
Source: "C:\Users\sardi\Source\Repos\lightshield\lightshield-desktop\src-tauri\target\release\bundle\nsis\*"; \
    DestDir: "{app}\Desktop"; Flags: ignoreversion recursesubdirs createallsubdirs

; Config file
Source: "C:\Users\sardi\source\repos\lightshield\lightshield_config.json"; \
    DestDir: "{commonappdata}\LightShield"; Flags: ignoreversion


[Dirs]
Name: "{commonappdata}\LightShield"; Permissions: users-full


[Dirs]
Name: "{commonappdata}\LightShield"; Flags: uninsalwaysuninstall

[Icons]
Name: "{group}\LightShield Desktop"; Filename: "{app}\Desktop\lightshield-desktop.exe"
Name: "{commondesktop}\LightShield Desktop"; Filename: "{app}\Desktop\lightshield-desktop.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create Desktop Shortcut"

[Run]
Filename: "{app}\LightShield.Launcher.exe"; Description: "Start LightShield"; Flags: nowait postinstall skipifsilent

[Registry]
; System-wide startup (avoids HKCU warning)
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
ValueType: string; ValueName: "LightShield Launcher"; \
ValueData: """{app}\LightShield.Launcher.exe"""

; Environment variable
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
ValueType: string; ValueName: "LIGHTSHIELD_API_URL"; \
ValueData: "http://localhost:5213"; Flags: preservestringtype
