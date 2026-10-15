$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

# Version tracking: v1.3 (SemVer 1.3.0)
$appVersion = "1.3.0"
$appDisplayVersion = "v1.3"
$appTitle = "DupFinder-Neo $appDisplayVersion (by @heitorsecurity)"
$appPublisher = "@heitorsecurity"

# Dynamic ProductCode for clean reinstall/upgrade support, constant UpgradeCode
$productCode = "{" + [System.Guid]::NewGuid().ToString().ToUpper() + "}"
$upgradeCode = "{F89D4321-B765-4321-BEEF-9876543210FE}"

$msiFile = Join-Path $scriptDir "dupfinder-neo.msi"
if (Test-Path $msiFile) { Remove-Item $msiFile -Force }

$cabFile = Join-Path $scriptDir "data.cab"
if (Test-Path $cabFile) { Remove-Item $cabFile -Force }

$exeFile = Join-Path $scriptDir "dupfinder-neo.exe"
$watcherFile = Join-Path $scriptDir "dupfinder-watcher.exe"
$setupFile = Join-Path $scriptDir "dupfinder-neo-setup.exe"
$readmeFile = Join-Path $scriptDir "readme.txt"
$licenseFile = Join-Path $scriptDir "LICENSE.TXT"
$icoFile = Join-Path $scriptDir "Duplicate_Files_Finder.ico"

# Ensure all binaries and resources exist
if (-not (Test-Path $exeFile)) { throw "dupfinder-neo.exe nao encontrado!" }
if (-not (Test-Path $watcherFile)) { throw "dupfinder-watcher.exe nao encontrado!" }
if (-not (Test-Path $setupFile)) { throw "dupfinder-neo-setup.exe nao encontrado!" }
if (-not (Test-Path $icoFile)) { throw "Duplicate_Files_Finder.ico nao encontrado!" }

# Prepare CAB DDF file with internal names matching the File table primary keys
$ddfContent = @"
.Set CabinetNameTemplate=data.cab
.Set DiskDirectoryTemplate=$scriptDir
.Set CompressionType=MSZIP
.Set Cabinet=ON
.Set Compress=ON
"$exeFile" F_Exe
"$watcherFile" F_Watcher
"$setupFile" F_Setup
"$readmeFile" F_Readme
"$licenseFile" F_License
"$icoFile" F_Icon
"@

$ddfPath = Join-Path $scriptDir "cabinet.ddf"
[System.IO.File]::WriteAllText($ddfPath, $ddfContent)

Write-Host "Criando arquivo CAB com makecab..."
& makecab.exe /F "$ddfPath"
Remove-Item $ddfPath -Force

$exeSize = (Get-Item $exeFile).Length
$watcherSize = (Get-Item $watcherFile).Length
$setupSize = (Get-Item $setupFile).Length
$readmeSize = (Get-Item $readmeFile).Length
$licenseSize = (Get-Item $licenseFile).Length
$icoSize = (Get-Item $icoFile).Length

Write-Host "Criando banco de dados MSI com interface grafica completa..."
$wi = New-Object -ComObject WindowsInstaller.Installer

# Open Database in direct create mode (4 = msiOpenDatabaseModeCreateDirect)
$db = $wi.OpenDatabase($msiFile, 4)

function ExecSql($database, $sql) {
    try {
        $view = $database.OpenView($sql)
        $view.Execute()
        $view.Close()
    } catch {
        Write-Host "FAILED SQL: $sql" -ForegroundColor Red
        throw $_
    }
}

# 1. Properties
ExecSql $db "CREATE TABLE Property (Property CHAR(72) NOT NULL, Value CHAR(0) NOT NULL PRIMARY KEY Property)"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductCode', '$productCode')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductName', '$appTitle')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductVersion', '$appVersion')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('Manufacturer', '$appPublisher')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductLanguage', '1033')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('UpgradeCode', '$upgradeCode')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ALLUSERS', '1')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('INSTALLSCOPE', 'AllUsers')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('INSTALLWATCHER', '1')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('INSTALLDESKTOPSHORTCUT', '1')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('INSTALLMENUSHORTCUT', '1')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('INSTALLDIR', 'C:\Program Files\DupFinder-Neo')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('DiskPrompt', 'DupFinder-Neo $appDisplayVersion Installation [1]')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('REINSTALLMODE', 'amus')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('MSIRESTARTMANAGERCONTROL', 'Disable')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('DefaultUIFont', 'DlgFont8')"

# 2. Upgrade Table for Seamless Reinstallation and Clean Removal of Previous Versions
ExecSql $db "CREATE TABLE Upgrade (UpgradeCode CHAR(38) NOT NULL, VersionMin CHAR(20), VersionMax CHAR(20), Language CHAR(30), Attributes LONG NOT NULL, Remove CHAR(255), ActionProperty CHAR(72) NOT NULL PRIMARY KEY UpgradeCode, VersionMin, VersionMax, Language, Attributes)"
ExecSql $db "INSERT INTO Upgrade (UpgradeCode, VersionMin, VersionMax, Language, Attributes, Remove, ActionProperty) VALUES ('$upgradeCode', NULL, '99.0.0', NULL, 256, NULL, 'PREVIOUSVERSIONSINSTALLED')"

# 3. Directory
ExecSql $db "CREATE TABLE Directory (Directory CHAR(72) NOT NULL, Directory_Parent CHAR(72), DefaultDir CHAR(255) NOT NULL PRIMARY KEY Directory)"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('TARGETDIR', NULL, 'SourceDir')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramFilesFolder', 'TARGETDIR', '.')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('INSTALLDIR', 'ProgramFilesFolder', 'DUPFIN~1|DupFinder-Neo')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramMenuFolder', 'TARGETDIR', '.')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('MenuFolder', 'ProgramMenuFolder', 'DUPFIN~1|DupFinder-Neo')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('DesktopFolder', 'TARGETDIR', '.')"

# 4. Component
ExecSql $db "CREATE TABLE Component (Component CHAR(72) NOT NULL, ComponentId CHAR(38), Directory_ CHAR(72) NOT NULL, Attributes SHORT NOT NULL, Condition CHAR(255), KeyPath CHAR(72) PRIMARY KEY Component)"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Exe', '{A1111111-2222-3333-4444-555555555551}', 'INSTALLDIR', 0, NULL, 'F_Exe')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Setup', '{A1111111-2222-3333-4444-555555555553}', 'INSTALLDIR', 0, NULL, 'F_Setup')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Docs', '{A1111111-2222-3333-4444-555555555554}', 'INSTALLDIR', 0, NULL, 'F_Readme')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Watcher', '{A1111111-2222-3333-4444-555555555552}', 'INSTALLDIR', 0, 'INSTALLWATCHER = 1', 'F_Watcher')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_DeskShort', '{A1111111-2222-3333-4444-555555555555}', 'DesktopFolder', 4, 'INSTALLDESKTOPSHORTCUT = 1', 'R_Desk')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_MenuShort', '{A1111111-2222-3333-4444-555555555556}', 'MenuFolder', 4, 'INSTALLMENUSHORTCUT = 1', 'R_Menu')"

# 5. File
ExecSql $db "CREATE TABLE File (File CHAR(72) NOT NULL, Component_ CHAR(72) NOT NULL, FileName CHAR(255) NOT NULL, FileSize LONG NOT NULL, Version CHAR(72), Language CHAR(20), Attributes SHORT, Sequence SHORT NOT NULL PRIMARY KEY File)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Exe', 'C_Exe', 'dupfneo.exe|dupfinder-neo.exe', $exeSize, '$appVersion', '1033', 16384, 1)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Watcher', 'C_Watcher', 'dupfwat.exe|dupfinder-watcher.exe', $watcherSize, '$appVersion', '1033', 16384, 2)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Setup', 'C_Setup', 'dupfset.exe|dupfinder-neo-setup.exe', $setupSize, '$appVersion', '1033', 16384, 3)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Readme', 'C_Docs', 'readme.txt', $readmeSize, NULL, NULL, 16384, 4)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_License', 'C_Docs', 'license.txt|LICENSE.TXT', $licenseSize, NULL, NULL, 16384, 5)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Icon', 'C_Docs', 'dupfind.ico|Duplicate_Files_Finder.ico', $icoSize, NULL, NULL, 16384, 6)"

# 6. Media
ExecSql $db "CREATE TABLE Media (DiskId SHORT NOT NULL, LastSequence SHORT NOT NULL, DiskPrompt CHAR(64), Cabinet CHAR(64), VolumeLabel CHAR(32), Source CHAR(72) PRIMARY KEY DiskId)"
ExecSql $db "INSERT INTO Media (DiskId, LastSequence, DiskPrompt, Cabinet, VolumeLabel, Source) VALUES (1, 6, 'Disk 1', '#data.cab', NULL, NULL)"

# 7. Feature
ExecSql $db "CREATE TABLE Feature (Feature CHAR(38) NOT NULL, Feature_Parent CHAR(38), Title CHAR(64), Description CHAR(255), Display SHORT, Level SHORT NOT NULL, Directory_ CHAR(72), Attributes SHORT NOT NULL PRIMARY KEY Feature)"
ExecSql $db "INSERT INTO Feature (Feature, Feature_Parent, Title, Description, Display, Level, Directory_, Attributes) VALUES ('Complete', NULL, '$appTitle', 'Instalacao Completa do DupFinder-Neo $appDisplayVersion', 1, 1, 'INSTALLDIR', 0)"

# 8. FeatureComponents
ExecSql $db "CREATE TABLE FeatureComponents (Feature_ CHAR(38) NOT NULL, Component_ CHAR(72) NOT NULL PRIMARY KEY Feature_, Component_)"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Exe')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Setup')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Docs')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Watcher')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_DeskShort')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_MenuShort')"

# 8b. Icon Table and ARPPRODUCTICON
ExecSql $db "CREATE TABLE Icon (Name CHAR(72) NOT NULL, Data OBJECT NOT NULL PRIMARY KEY Name)"
$view = $db.OpenView("INSERT INTO Icon (Name, Data) VALUES ('AppIcon.ico', ?)")
$record = $wi.CreateRecord(1)
$record.SetStream(1, $icoFile)
$view.Execute($record)
$view.Close()
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ARPPRODUCTICON', 'AppIcon.ico')"

# 9. Shortcut
ExecSql $db "CREATE TABLE Shortcut (Shortcut CHAR(72) NOT NULL, Directory_ CHAR(72) NOT NULL, Name CHAR(128) NOT NULL, Component_ CHAR(72) NOT NULL, Target CHAR(72) NOT NULL, Arguments CHAR(255), Description CHAR(255), Hotkey SHORT, Icon_ CHAR(72), IconIndex SHORT, ShowCmd SHORT, WkDir CHAR(72) PRIMARY KEY Shortcut)"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_MenuExe', 'MenuFolder', 'DUPFIN~1|DupFinder-Neo', 'C_MenuShort', '[#F_Exe]', NULL, 'DupFinder-Neo $appDisplayVersion (by @heitorsecurity)', NULL, 'AppIcon.ico', 0, 1, 'INSTALLDIR')"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_MenuReadme', 'MenuFolder', 'README~1|Readme.txt', 'C_MenuShort', '[#F_Readme]', NULL, 'Informacoes e Ajuda do DupFinder-Neo', NULL, NULL, NULL, 1, 'INSTALLDIR')"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_MenuUninstall', 'MenuFolder', 'DESINS~1|Desinstalar DupFinder-Neo', 'C_MenuShort', '[#F_Setup]', '/uninstall', 'Desinstalar DupFinder-Neo $appDisplayVersion', NULL, 'AppIcon.ico', 0, 1, 'INSTALLDIR')"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_DeskExe', 'DesktopFolder', 'DUPFIN~1|DupFinder-Neo', 'C_DeskShort', '[#F_Exe]', NULL, 'DupFinder-Neo $appDisplayVersion (by @heitorsecurity)', NULL, 'AppIcon.ico', 0, 1, 'INSTALLDIR')"

# 9b. Registry Table for conditional shortcut key paths
ExecSql $db "CREATE TABLE Registry (Registry CHAR(72) NOT NULL, Root SHORT NOT NULL, ``Key`` CHAR(255) NOT NULL, Name CHAR(255), Value CHAR(0), Component_ CHAR(72) NOT NULL PRIMARY KEY Registry)"
ExecSql $db "INSERT INTO Registry (Registry, Root, ``Key``, Name, Value, Component_) VALUES ('R_Desk', -1, 'Software\DupFinder-Neo', 'DesktopShortcut', '1', 'C_DeskShort')"
ExecSql $db "INSERT INTO Registry (Registry, Root, ``Key``, Name, Value, Component_) VALUES ('R_Menu', -1, 'Software\DupFinder-Neo', 'MenuShortcuts', '1', 'C_MenuShort')"

# 10. Native MSI UI Tables: TextStyle, Dialog, Control, ControlEvent, RadioButton, CheckBox, EventMapping
Write-Host "Configurando telas interativas do assistente MSI (Next/Back/Install)..."

ExecSql $db "CREATE TABLE TextStyle (TextStyle CHAR(72) NOT NULL, FaceName CHAR(32) NOT NULL, Size SHORT NOT NULL, Color LONG, StyleBits SHORT PRIMARY KEY TextStyle)"
ExecSql $db "INSERT INTO TextStyle (TextStyle, FaceName, Size, Color, StyleBits) VALUES ('DlgFont8', 'Tahoma', 8, 0, 0)"
ExecSql $db "INSERT INTO TextStyle (TextStyle, FaceName, Size, Color, StyleBits) VALUES ('DlgTitleBold', 'Tahoma', 10, 0, 1)"

ExecSql $db "CREATE TABLE RadioButton (Property CHAR(72) NOT NULL, ``Order`` SHORT NOT NULL, Value CHAR(64) NOT NULL, X SHORT NOT NULL, Y SHORT NOT NULL, Width SHORT NOT NULL, Height SHORT NOT NULL, Text CHAR(0), Help CHAR(255) PRIMARY KEY Property, ``Order``)"
ExecSql $db "INSERT INTO RadioButton (Property, ``Order``, Value, X, Y, Width, Height, Text, Help) VALUES ('INSTALLSCOPE', 1, 'AllUsers', 0, 0, 320, 15, 'Todos os usuarios deste computador (Recomendado)', NULL)"
ExecSql $db "INSERT INTO RadioButton (Property, ``Order``, Value, X, Y, Width, Height, Text, Help) VALUES ('INSTALLSCOPE', 2, 'CurrentUser', 0, 18, 320, 15, 'Apenas para o usuario atual', NULL)"

ExecSql $db "CREATE TABLE CheckBox (Property CHAR(72) NOT NULL, Value CHAR(64) PRIMARY KEY Property)"
ExecSql $db "INSERT INTO CheckBox (Property, Value) VALUES ('INSTALLWATCHER', '1')"
ExecSql $db "INSERT INTO CheckBox (Property, Value) VALUES ('INSTALLDESKTOPSHORTCUT', '1')"
ExecSql $db "INSERT INTO CheckBox (Property, Value) VALUES ('INSTALLMENUSHORTCUT', '1')"

ExecSql $db "CREATE TABLE Dialog (Dialog CHAR(72) NOT NULL, HCentering SHORT NOT NULL, VCentering SHORT NOT NULL, Width SHORT NOT NULL, Height SHORT NOT NULL, Attributes LONG NOT NULL, Title CHAR(128) NOT NULL, Control_First CHAR(72) NOT NULL, Control_Default CHAR(72) NOT NULL, Control_Cancel CHAR(72) NOT NULL PRIMARY KEY Dialog)"
ExecSql $db "INSERT INTO Dialog (Dialog, HCentering, VCentering, Width, Height, Attributes, Title, Control_First, Control_Default, Control_Cancel) VALUES ('WelcomeDlg', 50, 50, 370, 270, 3, '$appTitle', 'Next', 'Next', 'Cancel')"
ExecSql $db "INSERT INTO Dialog (Dialog, HCentering, VCentering, Width, Height, Attributes, Title, Control_First, Control_Default, Control_Cancel) VALUES ('LicenseAgreementDlg', 50, 50, 370, 270, 3, '$appTitle', 'Next', 'Next', 'Cancel')"
ExecSql $db "INSERT INTO Dialog (Dialog, HCentering, VCentering, Width, Height, Attributes, Title, Control_First, Control_Default, Control_Cancel) VALUES ('InstallDirDlg', 50, 50, 370, 270, 3, '$appTitle', 'Next', 'Next', 'Cancel')"
ExecSql $db "INSERT INTO Dialog (Dialog, HCentering, VCentering, Width, Height, Attributes, Title, Control_First, Control_Default, Control_Cancel) VALUES ('CustomizeDlg', 50, 50, 370, 270, 3, '$appTitle', 'Next', 'Next', 'Cancel')"
ExecSql $db "INSERT INTO Dialog (Dialog, HCentering, VCentering, Width, Height, Attributes, Title, Control_First, Control_Default, Control_Cancel) VALUES ('VerifyReadyDlg', 50, 50, 370, 270, 3, '$appTitle', 'Install', 'Install', 'Cancel')"
ExecSql $db "INSERT INTO Dialog (Dialog, HCentering, VCentering, Width, Height, Attributes, Title, Control_First, Control_Default, Control_Cancel) VALUES ('ProgressDlg', 50, 50, 370, 270, 1, '$appTitle', 'Cancel', 'Cancel', 'Cancel')"
ExecSql $db "INSERT INTO Dialog (Dialog, HCentering, VCentering, Width, Height, Attributes, Title, Control_First, Control_Default, Control_Cancel) VALUES ('ExitDialog', 50, 50, 370, 270, 3, '$appTitle', 'Finish', 'Finish', 'Finish')"

ExecSql $db "CREATE TABLE Control (Dialog_ CHAR(72) NOT NULL, Control CHAR(72) NOT NULL, Type CHAR(32) NOT NULL, X SHORT NOT NULL, Y SHORT NOT NULL, Width SHORT NOT NULL, Height SHORT NOT NULL, Attributes LONG NOT NULL, Property CHAR(72), Text CHAR(0), Control_Next CHAR(72), Help CHAR(255) PRIMARY KEY Dialog_, Control)"

# Controls: WelcomeDlg (Cycle: Title -> Desc -> BottomLine -> Next -> Cancel -> Title)
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('WelcomeDlg', 'Title', 'Text', 20, 20, 330, 35, 3, NULL, '{\DlgTitleBold}Bem-vindo ao Assistente de Instalacao do DupFinder-Neo v1.1', 'Desc', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('WelcomeDlg', 'Desc', 'Text', 20, 65, 330, 110, 3, NULL, 'O Assistente de Instalacao vai instalar o DupFinder-Neo v1.1 (by @heitorsecurity) no seu computador.[~][~]Desenvolvido originalmente por Patrick De Smet e modernizado com novas tecnologias por @heitorsecurity.[~][~]Clique em Avancar para continuar ou Cancelar para sair.', 'BottomLine', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('WelcomeDlg', 'BottomLine', 'Line', 0, 234, 370, 0, 1, NULL, NULL, 'Next', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('WelcomeDlg', 'Next', 'PushButton', 236, 243, 56, 17, 3, NULL, '&Avancar >', 'Cancel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('WelcomeDlg', 'Cancel', 'PushButton', 304, 243, 56, 17, 3, NULL, 'Cancelar', 'Title', NULL)"

# Controls: LicenseAgreementDlg (Cycle: Title -> Subtitle -> LicenseText -> BottomLine -> Back -> Next -> Cancel -> Title)
$licenseSummary = "DupFinder-Neo v1.1 (by @heitorsecurity)[~][~]Original Work: Copyright (C) Patrick De Smet (GNU GPL v2)[~]Modernizacao e Recursos Novos: Copyright (C) 2026 @heitorsecurity[~][~]Este programa e software livre; voce pode redistribui-lo e/ou modifica-lo sob os termos da licenca GNU General Public License versao 2.[~][~]Para ver os termos integrais, consulte LICENSE.TXT instalado na pasta do programa.[~][~]Clique em Avancar para concordar e continuar com a instalacao."
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('LicenseAgreementDlg', 'Title', 'Text', 20, 12, 330, 25, 3, NULL, '{\DlgTitleBold}Contrato de Licenca e Modificacoes', 'Subtitle', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('LicenseAgreementDlg', 'Subtitle', 'Text', 20, 35, 330, 20, 3, NULL, 'Leia os termos da licenca GNU GPL v2 e adendos de @heitorsecurity:', 'LicenseText', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('LicenseAgreementDlg', 'LicenseText', 'Text', 20, 60, 330, 130, 3, NULL, '$licenseSummary', 'BottomLine', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('LicenseAgreementDlg', 'BottomLine', 'Line', 0, 234, 370, 0, 1, NULL, NULL, 'Back', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('LicenseAgreementDlg', 'Back', 'PushButton', 180, 243, 56, 17, 3, NULL, '< &Voltar', 'Next', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('LicenseAgreementDlg', 'Next', 'PushButton', 236, 243, 56, 17, 3, NULL, '&Avancar >', 'Cancel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('LicenseAgreementDlg', 'Cancel', 'PushButton', 304, 243, 56, 17, 3, NULL, 'Cancelar', 'Title', NULL)"

# Controls: InstallDirDlg (Cycle: Title -> Subtitle -> PathEdit -> BottomLine -> Back -> Next -> Cancel -> Title)
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('InstallDirDlg', 'Title', 'Text', 20, 15, 330, 25, 3, NULL, '{\DlgTitleBold}Pasta de Destino da Instalacao', 'Subtitle', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('InstallDirDlg', 'Subtitle', 'Text', 20, 45, 330, 40, 3, NULL, 'O assistente vai instalar o DupFinder-Neo v1.1 na pasta abaixo.[~][~]Para instalar nesta pasta, clique em Avancar. Para alterar, digite o caminho:', 'PathEdit', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('InstallDirDlg', 'PathEdit', 'PathEdit', 20, 95, 330, 18, 3, 'INSTALLDIR', NULL, 'BottomLine', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('InstallDirDlg', 'BottomLine', 'Line', 0, 234, 370, 0, 1, NULL, NULL, 'Back', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('InstallDirDlg', 'Back', 'PushButton', 180, 243, 56, 17, 3, NULL, '< &Voltar', 'Next', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('InstallDirDlg', 'Next', 'PushButton', 236, 243, 56, 17, 3, NULL, '&Avancar >', 'Cancel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('InstallDirDlg', 'Cancel', 'PushButton', 304, 243, 56, 17, 3, NULL, 'Cancelar', 'Title', NULL)"

# Controls: CustomizeDlg (Cycle: Title -> Subtitle -> ScopeLabel -> ScopeRadio -> WatcherLabel -> WatcherCheck -> WatcherDesc -> ShortcutLabel -> DesktopCheck -> MenuCheck -> BottomLine -> Back -> Next -> Cancel -> Title)
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'Title', 'Text', 20, 10, 330, 18, 3, NULL, '{\DlgTitleBold}Opcoes e Componentes da Instalacao', 'Subtitle', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'Subtitle', 'Text', 20, 28, 330, 14, 3, NULL, 'Selecione o escopo de usuarios, recursos e atalhos desejados:', 'ScopeLabel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'ScopeLabel', 'Text', 20, 44, 330, 14, 3, NULL, '{\DlgTitleBold}Escopo da Instalacao:', 'ScopeRadio', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'ScopeRadio', 'RadioButtonGroup', 20, 59, 330, 34, 3, 'INSTALLSCOPE', NULL, 'WatcherLabel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'WatcherLabel', 'Text', 20, 96, 330, 14, 3, NULL, '{\DlgTitleBold}Monitor em Segundo Plano (Opcional):', 'WatcherCheck', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'WatcherCheck', 'CheckBox', 20, 111, 330, 15, 3, 'INSTALLWATCHER', '&Instalar Monitor em Tempo Real (dupfinder-watcher.exe)', 'WatcherDesc', NULL)"
$watcherExp = "Monitora o sistema em tempo real estilo Everything (voidtools). Ao detectar que voce esta duplicando um arquivo no PC, exibe notificacao para abrir o local ou fechar."
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'WatcherDesc', 'Text', 35, 127, 315, 32, 3, NULL, '$watcherExp', 'ShortcutLabel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'ShortcutLabel', 'Text', 20, 162, 330, 14, 3, NULL, '{\DlgTitleBold}Atalhos do Sistema:', 'DesktopCheck', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'DesktopCheck', 'CheckBox', 20, 177, 330, 15, 3, 'INSTALLDESKTOPSHORTCUT', 'Criar atalho na &Area de Trabalho (Desktop)', 'MenuCheck', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'MenuCheck', 'CheckBox', 20, 194, 330, 15, 3, 'INSTALLMENUSHORTCUT', 'Criar atalhos no &Menu Iniciar (DupFinder-Neo, Readme e Desinstalar)', 'BottomLine', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'BottomLine', 'Line', 0, 234, 370, 0, 1, NULL, NULL, 'Back', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'Back', 'PushButton', 180, 243, 56, 17, 3, NULL, '< &Voltar', 'Next', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'Next', 'PushButton', 236, 243, 56, 17, 3, NULL, '&Avancar >', 'Cancel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('CustomizeDlg', 'Cancel', 'PushButton', 304, 243, 56, 17, 3, NULL, 'Cancelar', 'Title', NULL)"

# Controls: VerifyReadyDlg (Cycle: Title -> Subtitle -> BottomLine -> Back -> Install -> Cancel -> Title)
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('VerifyReadyDlg', 'Title', 'Text', 20, 25, 330, 25, 3, NULL, '{\DlgTitleBold}Pronto para Instalar', 'Subtitle', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('VerifyReadyDlg', 'Subtitle', 'Text', 20, 65, 330, 80, 3, NULL, 'O Assistente de Instalacao esta pronto para iniciar a instalacao do DupFinder-Neo v1.1 (by @heitorsecurity).[~][~]Clique em Instalar para aplicar as configuracoes e iniciar a instalacao.', 'BottomLine', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('VerifyReadyDlg', 'BottomLine', 'Line', 0, 234, 370, 0, 1, NULL, NULL, 'Back', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('VerifyReadyDlg', 'Back', 'PushButton', 180, 243, 56, 17, 3, NULL, '< &Voltar', 'Install', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('VerifyReadyDlg', 'Install', 'PushButton', 236, 243, 56, 17, 3, NULL, '&Instalar', 'Cancel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('VerifyReadyDlg', 'Cancel', 'PushButton', 304, 243, 56, 17, 3, NULL, 'Cancelar', 'Title', NULL)"

# Controls: ProgressDlg (Cycle: Title -> Subtitle -> ProgressBar -> BottomLine -> Cancel -> Title)
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ProgressDlg', 'Title', 'Text', 20, 25, 330, 25, 3, NULL, '{\DlgTitleBold}Instalando o DupFinder-Neo v1.1...', 'Subtitle', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ProgressDlg', 'Subtitle', 'Text', 20, 65, 330, 35, 3, NULL, 'Aguarde enquanto os arquivos e componentes sao instalados no computador.', 'ProgressBar', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ProgressDlg', 'ProgressBar', 'ProgressBar', 20, 115, 330, 15, 65539, NULL, NULL, 'BottomLine', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ProgressDlg', 'BottomLine', 'Line', 0, 234, 370, 0, 1, NULL, NULL, 'Cancel', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ProgressDlg', 'Cancel', 'PushButton', 304, 243, 56, 17, 3, NULL, 'Cancelar', 'Title', NULL)"

# Controls: ExitDialog (Cycle: Title -> Subtitle -> BottomLine -> Finish -> Title)
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ExitDialog', 'Title', 'Text', 20, 25, 330, 25, 3, NULL, '{\DlgTitleBold}Instalacao Concluida com Sucesso!', 'Subtitle', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ExitDialog', 'Subtitle', 'Text', 20, 65, 330, 95, 3, NULL, 'O DupFinder-Neo v1.1 (by @heitorsecurity) foi instalado com sucesso no seu computador![~][~]Foram criados os componentes e atalhos selecionados.[~][~]Clique em Concluir para finalizar.', 'BottomLine', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ExitDialog', 'BottomLine', 'Line', 0, 234, 370, 0, 1, NULL, NULL, 'Finish', NULL)"
ExecSql $db "INSERT INTO Control (Dialog_, Control, Type, X, Y, Width, Height, Attributes, Property, Text, Control_Next, Help) VALUES ('ExitDialog', 'Finish', 'PushButton', 236, 243, 56, 17, 3, NULL, '&Concluir', 'Title', NULL)"

# EventMapping table: Links ProgressBar to installer engine progress events
ExecSql $db "CREATE TABLE EventMapping (Dialog_ CHAR(72) NOT NULL, Control_ CHAR(72) NOT NULL, Event CHAR(72) NOT NULL, Attribute CHAR(72) NOT NULL PRIMARY KEY Dialog_, Control_, Event)"
ExecSql $db "INSERT INTO EventMapping (Dialog_, Control_, Event, Attribute) VALUES ('ProgressDlg', 'ProgressBar', 'SetProgress', 'Progress')"

# ControlEvent table: Wiring buttons and dialog transitions
ExecSql $db "CREATE TABLE ControlEvent (Dialog_ CHAR(72) NOT NULL, Control_ CHAR(72) NOT NULL, Event CHAR(72) NOT NULL, Argument CHAR(255) NOT NULL, Condition CHAR(255), Ordering SHORT NOT NULL PRIMARY KEY Dialog_, Control_, Event, Argument, Condition)"

# WelcomeDlg Events
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('WelcomeDlg', 'Next', 'NewDialog', 'LicenseAgreementDlg', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('WelcomeDlg', 'Cancel', 'EndDialog', 'Exit', '1', 1)"

# LicenseAgreementDlg Events
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('LicenseAgreementDlg', 'Back', 'NewDialog', 'WelcomeDlg', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('LicenseAgreementDlg', 'Next', 'NewDialog', 'InstallDirDlg', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('LicenseAgreementDlg', 'Cancel', 'EndDialog', 'Exit', '1', 1)"

# InstallDirDlg Events
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('InstallDirDlg', 'Back', 'NewDialog', 'LicenseAgreementDlg', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('InstallDirDlg', 'Next', 'SetTargetPath', 'INSTALLDIR', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('InstallDirDlg', 'Next', 'NewDialog', 'CustomizeDlg', '1', 2)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('InstallDirDlg', 'Cancel', 'EndDialog', 'Exit', '1', 1)"

# CustomizeDlg Events
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('CustomizeDlg', 'Back', 'NewDialog', 'InstallDirDlg', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('CustomizeDlg', 'Next', '[ALLUSERS]', '1', 'INSTALLSCOPE = ""AllUsers""', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('CustomizeDlg', 'Next', '[ALLUSERS]', '{}', 'INSTALLSCOPE = ""CurrentUser""', 2)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('CustomizeDlg', 'Next', 'NewDialog', 'VerifyReadyDlg', '1', 3)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('CustomizeDlg', 'Cancel', 'EndDialog', 'Exit', '1', 1)"

# VerifyReadyDlg Events
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('VerifyReadyDlg', 'Back', 'NewDialog', 'CustomizeDlg', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('VerifyReadyDlg', 'Install', 'EndDialog', 'Return', '1', 1)"
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('VerifyReadyDlg', 'Cancel', 'EndDialog', 'Exit', '1', 1)"

# ProgressDlg Events
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('ProgressDlg', 'Cancel', 'EndDialog', 'Exit', '1', 1)"

# ExitDialog Events
ExecSql $db "INSERT INTO ControlEvent (Dialog_, Control_, Event, Argument, Condition, Ordering) VALUES ('ExitDialog', 'Finish', 'EndDialog', 'Exit', '1', 1)"

# 11. Sequences: Execution & UI Flow
ExecSql $db "CREATE TABLE InstallExecuteSequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence SHORT PRIMARY KEY Action)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('FindRelatedProducts', NULL, 200)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('CostInitialize', NULL, 800)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('FileCost', NULL, 900)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('CostFinalize', NULL, 1000)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallValidate', NULL, 1400)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RemoveExistingProducts', 'PREVIOUSVERSIONSINSTALLED', 1450)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallInitialize', NULL, 1500)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('ProcessComponents', NULL, 1600)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('UnpublishComponents', NULL, 1700)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('UnpublishFeatures', NULL, 1800)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RemoveShortcuts', NULL, 3200)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RemoveRegistryValues', NULL, 2600)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RemoveFiles', NULL, 3500)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallFiles', NULL, 4000)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('CreateShortcuts', NULL, 4500)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('WriteRegistryValues', NULL, 5000)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RegisterProduct', NULL, 6100)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('PublishFeatures', NULL, 6300)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('PublishProduct', NULL, 6400)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallFinalize', NULL, 6600)"

ExecSql $db "CREATE TABLE InstallUISequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence SHORT PRIMARY KEY Action)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('FindRelatedProducts', NULL, 200)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('CostInitialize', NULL, 800)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('FileCost', NULL, 900)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('CostFinalize', NULL, 1000)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('WelcomeDlg', 'NOT Installed', 1100)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('ProgressDlg', NULL, 1200)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('ExecuteAction', NULL, 1300)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('ExitDialog', NULL, 1400)"

ExecSql $db "CREATE TABLE AdminExecuteSequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence SHORT PRIMARY KEY Action)"
ExecSql $db "INSERT INTO AdminExecuteSequence (Action, Condition, Sequence) VALUES ('CostInitialize', NULL, 800)"
ExecSql $db "INSERT INTO AdminExecuteSequence (Action, Condition, Sequence) VALUES ('FileCost', NULL, 900)"
ExecSql $db "INSERT INTO AdminExecuteSequence (Action, Condition, Sequence) VALUES ('CostFinalize', NULL, 1000)"
ExecSql $db "INSERT INTO AdminExecuteSequence (Action, Condition, Sequence) VALUES ('InstallInitialize', NULL, 1500)"
ExecSql $db "INSERT INTO AdminExecuteSequence (Action, Condition, Sequence) VALUES ('InstallFiles', NULL, 4000)"
ExecSql $db "INSERT INTO AdminExecuteSequence (Action, Condition, Sequence) VALUES ('InstallFinalize', NULL, 6600)"

# Embed CAB into MSI _Streams
Write-Host "Embutindo data.cab no MSI..."
$view = $db.OpenView("INSERT INTO _Streams (Name, Data) VALUES ('data.cab', ?)")
$record = $wi.CreateRecord(1)
$record.SetStream(1, $cabFile)
$view.Execute($record)
$view.Close()

# Summary Information
$summary = $db.SummaryInformation(20)
$summary.Property(2) = "$appTitle Installation"  # Title
$summary.Property(3) = "$appTitle"              # Subject
$summary.Property(4) = "$appPublisher"          # Author
$summary.Property(7) = "Intel;1033"             # Platform;Language
$summary.Property(9) = "$productCode"           # Package Code matches new ProductCode
$summary.Property(14) = 200                     # Schema (2.0)
$summary.Property(15) = 2                       # WordCount: Bit 1 set = source compressed in cabinet
$summary.Property(19) = 0                       # Security
$summary.Persist()

$db.Commit()

# Cleanup temporary CAB and makecab reports
Remove-Item $cabFile -Force -ErrorAction SilentlyContinue
$infFile = Join-Path $scriptDir "setup.inf"
$rptFile = Join-Path $scriptDir "setup.rpt"
if (Test-Path $infFile) { Remove-Item $infFile -Force }
if (Test-Path $rptFile) { Remove-Item $rptFile -Force }

Write-Host "MSI $appDisplayVersion com janelas nativas (Next/Back/Install) gerado com sucesso: $msiFile"
