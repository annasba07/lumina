; Lumina Installer Script for Inno Setup
; This script creates a professional Windows installer for the Lumina application

#define MyAppName "Lumina"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Lumina Team"
#define MyAppURL "https://github.com/annasba07/lumina"
#define MyAppExeName "Lumina.exe"
#define MyAppIcon "lumina-icon.ico"

[Setup]
; Basic application info
AppId={{A8B7C9D1-2E3F-4A5B-9C7D-1E2F3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation paths
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=..\..\Releases
OutputBaseFilename=LuminaSetup-{#MyAppVersion}
SetupIconFile=..\..\assets\icons\lumina-icon.ico

; Compression settings (good balance of size and speed)
Compression=lzma2
SolidCompression=yes
CompressionThreads=auto

; Windows version requirements
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; UI settings
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
DisableReadyPage=no
DisableFinishedPage=no

; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Version info
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} Speech-to-Text Application
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Optional features
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode
Name: "startup"; Description: "Start {#MyAppName} when Windows starts"; GroupDescription: "Startup Options:"; Flags: unchecked

[Files]
; Main application files from Release build
Source: "..\..\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Icon file
Source: "..\..\assets\icons\lumina-icon.ico"; DestDir: "{app}"; Flags: ignoreversion

; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
; Program group icons
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIcon}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Desktop icon (if selected)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIcon}"; Tasks: desktopicon

; Quick launch icon (if selected)
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIcon}"; Tasks: quicklaunchicon

; Startup folder icon (if selected)
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppIcon}"; Tasks: startup

[Run]
; Option to launch after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up any files created by the application
Type: filesandordirs; Name: "{localappdata}\Lumina"
Type: filesandordirs; Name: "{userappdata}\SuperWhisper"

[Code]
// Check if .NET 8 Desktop Runtime is installed
function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
begin
  // Check for .NET 8 Desktop Runtime using dotnet --info
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and
            (ResultCode = 0);

  // If we can't determine, assume it needs to be installed
  if not Result then
  begin
    Result := False;
  end;
end;

// Initialize setup
function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
  DownloadUrl: string;
begin
  Result := True;

  // Check for .NET 8 Desktop Runtime
  if not IsDotNet8DesktopRuntimeInstalled then
  begin
    if MsgBox('The .NET 8 Desktop Runtime is required to run Lumina.' + #13#10 + #13#10 +
              'Would you like to download and install it now?' + #13#10 + #13#10 +
              'This will open the Microsoft download page in your browser.',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Open the .NET 8 Desktop Runtime download page
      DownloadUrl := 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0';
      ShellExec('open', DownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);

      MsgBox('Please download and install the .NET 8 Desktop Runtime (x64) from the Microsoft website.' + #13#10 + #13#10 +
             'After installation is complete, run this installer again.',
             mbInformation, MB_OK);
      Result := False;
    end
    else
    begin
      MsgBox('Installation cancelled.' + #13#10 + #13#10 +
             'Lumina requires the .NET 8 Desktop Runtime to run.',
             mbInformation, MB_OK);
      Result := False;
    end;
  end;
end;

// Check if application is running before uninstall
function InitializeUninstall: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  // Try to close Lumina if it's running
  ShellExec('', 'taskkill', '/F /IM Lumina.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);

  // Small delay to ensure process is terminated
  Sleep(500);
end;

[Messages]
; Custom messages
BeveledLabel=Lumina - Elegant Speech-to-Text
SetupWindowTitle=Lumina Setup
WelcomeLabel1=Welcome to the Lumina Setup Wizard
WelcomeLabel2=This will install Lumina {#MyAppVersion} on your computer.%n%nLumina is an elegant speech-to-text application with ultra-minimal design, powered by OpenAI's Whisper model.%n%nPress Ctrl+Space to start recording your voice and watch as Lumina transcribes it in real-time.%n%nIt is recommended that you close all other applications before continuing.
FinishedLabel=Lumina has been installed on your computer.%n%nYou can launch the application by selecting the installed icons or pressing Ctrl+Space at any time to start recording.