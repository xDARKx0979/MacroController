#define AppName "MacroController"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppExeName "MacroController.App.exe"
#define PublishDir "publish"

[Setup]
AppId={{1F2C9C7E-6D2C-4C2E-9B6F-3F0E2C7E9A11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename={#AppName}Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\MacroController.App\Assets\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; ViGEmBus (virtual controller driver - see Core/Input/VirtualGamepadSender.cs) is a
; kernel driver and always needs admin, unlike this installer (PrivilegesRequired=lowest).
; Its own installer has an embedded manifest that requests elevation itself, so this
; triggers its own UAC prompt without changing our installer's privilege level. Skipped
; entirely if a ViGEmBus install is already registered - see IsViGEmBusInstalled below.
Filename: "{app}\ViGEmBusSetup.exe"; Parameters: "/quiet /norestart"; StatusMsg: "Installing virtual controller driver..."; Flags: waituntilterminated skipifsilent; Check: not IsViGEmBusInstalled
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function IsViGEmBusInstalled(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  DisplayName: String;
  Views: array[0..1] of Integer;
  V: Integer;
begin
  Result := False;
  Views[0] := HKLM64;
  Views[1] := HKLM32;

  for V := 0 to 1 do
  begin
    if RegGetSubkeyNames(Views[V], 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', Names) then
    begin
      for I := 0 to GetArrayLength(Names) - 1 do
      begin
        if RegQueryStringValue(Views[V], 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + Names[I], 'DisplayName', DisplayName) then
        begin
          if Pos('ViGEmBus', DisplayName) > 0 then
          begin
            Result := True;
            Exit;
          end;
        end;
      end;
    end;
  end;
end;
