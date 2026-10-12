$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

# Version tracking: v1.0 (SemVer 1.0.0, easily increments to 1.1.0, 1.2.0, etc.)
$appVersion = "1.0.0"
$appDisplayVersion = "v1.0"
$appTitle = "DupFinder-Neo $appDisplayVersion (by @heitorsecurity)"
$appPublisher = "@heitorsecurity"

$msiFile = Join-Path $scriptDir "dupfinder-neo.msi"
if (Test-Path $msiFile) { Remove-Item $msiFile -Force }

$cabFile = Join-Path $scriptDir "data.cab"
if (Test-Path $cabFile) { Remove-Item $cabFile -Force }

$exeFile = Join-Path $scriptDir "dupfinder-neo.exe"
$watcherFile = Join-Path $scriptDir "dupfinder-watcher.exe"
$setupFile = Join-Path $scriptDir "dupfinder-neo-setup.exe"
$readmeFile = Join-Path $scriptDir "readme.txt"
$licenseFile = Join-Path $scriptDir "LICENSE.TXT"

# Ensure all binaries exist
if (-not (Test-Path $exeFile)) { throw "dupfinder-neo.exe nao encontrado!" }
if (-not (Test-Path $watcherFile)) { throw "dupfinder-watcher.exe nao encontrado!" }
if (-not (Test-Path $setupFile)) { throw "dupfinder-neo-setup.exe nao encontrado!" }

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

Write-Host "Criando banco de dados MSI com WindowsInstaller COM..."
$wi = New-Object -ComObject WindowsInstaller.Installer

# Open Database in direct create mode (4 = msiOpenDatabaseModeCreateDirect)
$db = $wi.OpenDatabase($msiFile, 4)

function ExecSql($database, $sql) {
    $view = $database.OpenView($sql)
    $view.Execute()
    $view.Close()
}

# 1. Properties
ExecSql $db "CREATE TABLE Property (Property CHAR(72) NOT NULL, Value CHAR(0) NOT NULL PRIMARY KEY Property)"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductCode', '{B76C1234-A890-4DEF-9102-1234567890AB}')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductName', '$appTitle')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductVersion', '$appVersion')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('Manufacturer', '$appPublisher')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductLanguage', '1033')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('UpgradeCode', '{F89D4321-B765-4321-BEEF-9876543210FE}')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ALLUSERS', '1')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('DiskPrompt', 'DupFinder-Neo $appDisplayVersion Installation [1]')"

# 2. Directory
ExecSql $db "CREATE TABLE Directory (Directory CHAR(72) NOT NULL, Directory_Parent CHAR(72), DefaultDir CHAR(255) NOT NULL PRIMARY KEY Directory)"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('TARGETDIR', NULL, 'SourceDir')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramFilesFolder', 'TARGETDIR', '.')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('INSTALLDIR', 'ProgramFilesFolder', 'DUPFIN~1|DupFinder-Neo')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramMenuFolder', 'TARGETDIR', '.')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('MenuFolder', 'ProgramMenuFolder', 'DUPFIN~1|DupFinder-Neo')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('DesktopFolder', 'TARGETDIR', '.')"

# 3. Component
ExecSql $db "CREATE TABLE Component (Component CHAR(72) NOT NULL, ComponentId CHAR(38), Directory_ CHAR(72) NOT NULL, Attributes SHORT NOT NULL, Condition CHAR(255), KeyPath CHAR(72) PRIMARY KEY Component)"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Exe', '{A1111111-2222-3333-4444-555555555551}', 'INSTALLDIR', 0, NULL, 'F_Exe')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Watcher', '{A1111111-2222-3333-4444-555555555552}', 'INSTALLDIR', 0, NULL, 'F_Watcher')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Setup', '{A1111111-2222-3333-4444-555555555553}', 'INSTALLDIR', 0, NULL, 'F_Setup')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Docs', '{A1111111-2222-3333-4444-555555555554}', 'INSTALLDIR', 0, NULL, 'F_Readme')"

# 4. File
ExecSql $db "CREATE TABLE File (File CHAR(72) NOT NULL, Component_ CHAR(72) NOT NULL, FileName CHAR(255) NOT NULL, FileSize LONG NOT NULL, Version CHAR(72), Language CHAR(20), Attributes SHORT, Sequence SHORT NOT NULL PRIMARY KEY File)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Exe', 'C_Exe', 'dupfneo.exe|dupfinder-neo.exe', $exeSize, '$appVersion', '1033', 16384, 1)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Watcher', 'C_Watcher', 'dupfwat.exe|dupfinder-watcher.exe', $watcherSize, '$appVersion', '1033', 16384, 2)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Setup', 'C_Setup', 'dupfset.exe|dupfinder-neo-setup.exe', $setupSize, '$appVersion', '1033', 16384, 3)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Readme', 'C_Docs', 'readme.txt', $readmeSize, NULL, NULL, 16384, 4)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_License', 'C_Docs', 'license.txt|LICENSE.TXT', $licenseSize, NULL, NULL, 16384, 5)"

# 5. Media
ExecSql $db "CREATE TABLE Media (DiskId SHORT NOT NULL, LastSequence SHORT NOT NULL, DiskPrompt CHAR(64), Cabinet CHAR(64), VolumeLabel CHAR(32), Source CHAR(72) PRIMARY KEY DiskId)"
ExecSql $db "INSERT INTO Media (DiskId, LastSequence, DiskPrompt, Cabinet, VolumeLabel, Source) VALUES (1, 5, 'Disk 1', '#data.cab', NULL, NULL)"

# 6. Feature
ExecSql $db "CREATE TABLE Feature (Feature CHAR(38) NOT NULL, Feature_Parent CHAR(38), Title CHAR(64), Description CHAR(255), Display SHORT, Level SHORT NOT NULL, Directory_ CHAR(72), Attributes SHORT NOT NULL PRIMARY KEY Feature)"
ExecSql $db "INSERT INTO Feature (Feature, Feature_Parent, Title, Description, Display, Level, Directory_, Attributes) VALUES ('Complete', NULL, '$appTitle', 'Instalacao Completa do DupFinder-Neo $appDisplayVersion', 1, 1, 'INSTALLDIR', 0)"

# 7. FeatureComponents
ExecSql $db "CREATE TABLE FeatureComponents (Feature_ CHAR(38) NOT NULL, Component_ CHAR(72) NOT NULL PRIMARY KEY Feature_, Component_)"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Exe')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Watcher')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Setup')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Docs')"

# 8. Shortcut
ExecSql $db "CREATE TABLE Shortcut (Shortcut CHAR(72) NOT NULL, Directory_ CHAR(72) NOT NULL, Name CHAR(128) NOT NULL, Component_ CHAR(72) NOT NULL, Target CHAR(72) NOT NULL, Arguments CHAR(255), Description CHAR(255), Hotkey SHORT, Icon_ CHAR(72), IconIndex SHORT, ShowCmd SHORT, WkDir CHAR(72) PRIMARY KEY Shortcut)"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_MenuExe', 'MenuFolder', 'DUPFIN~1|DupFinder-Neo', 'C_Exe', '[#F_Exe]', NULL, 'DupFinder-Neo $appDisplayVersion (by @heitorsecurity)', NULL, NULL, NULL, 1, 'INSTALLDIR')"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_MenuReadme', 'MenuFolder', 'README~1|Readme.txt', 'C_Docs', '[#F_Readme]', NULL, 'Informacoes e Ajuda do DupFinder-Neo', NULL, NULL, NULL, 1, 'INSTALLDIR')"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_MenuUninstall', 'MenuFolder', 'DESINS~1|Desinstalar DupFinder-Neo', 'C_Setup', '[#F_Setup]', '/uninstall', 'Desinstalar DupFinder-Neo $appDisplayVersion', NULL, NULL, NULL, 1, 'INSTALLDIR')"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_DeskExe', 'DesktopFolder', 'DUPFIN~1|DupFinder-Neo', 'C_Exe', '[#F_Exe]', NULL, 'DupFinder-Neo $appDisplayVersion (by @heitorsecurity)', NULL, NULL, NULL, 1, 'INSTALLDIR')"

# 9. Binary table for interactive wizard setup
ExecSql $db "CREATE TABLE Binary (Name CHAR(72) NOT NULL, Data OBJECT NOT NULL PRIMARY KEY Name)"
$binView = $db.OpenView("INSERT INTO Binary (Name, Data) VALUES ('SetupWizardBinary', ?)")
$binRec = $wi.CreateRecord(1)
$binRec.SetStream(1, $setupFile)
$binView.Execute($binRec)
$binView.Close()

# 10. CustomAction table
ExecSql $db "CREATE TABLE CustomAction (Action CHAR(72) NOT NULL, Type SHORT NOT NULL, Source CHAR(72), Target CHAR(255) PRIMARY KEY Action)"
# Type 2 = EXE stored in Binary table
ExecSql $db "INSERT INTO CustomAction (Action, Type, Source, Target) VALUES ('LaunchWizardUI', 2, 'SetupWizardBinary', '')"

# 11. Sequences
ExecSql $db "CREATE TABLE InstallExecuteSequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence SHORT PRIMARY KEY Action)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('CostInitialize', NULL, 800)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('FileCost', NULL, 900)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('CostFinalize', NULL, 1000)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallValidate', NULL, 1400)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallInitialize', NULL, 1500)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('ProcessComponents', NULL, 1600)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('UnpublishComponents', NULL, 1700)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('UnpublishFeatures', NULL, 1800)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RemoveShortcuts', NULL, 3200)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RemoveFiles', NULL, 3500)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallFiles', NULL, 4000)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('CreateShortcuts', NULL, 4500)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('RegisterProduct', NULL, 6100)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('PublishFeatures', NULL, 6300)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('PublishProduct', NULL, 6400)"
ExecSql $db "INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('InstallFinalize', NULL, 6600)"

ExecSql $db "CREATE TABLE InstallUISequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence SHORT PRIMARY KEY Action)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('LaunchWizardUI', NULL, 1)"

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
$summary.Property(9) = "{B76C1234-A890-4DEF-9102-1234567890AB}" # Package Code
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

Write-Host "MSI v1.0 gerado com sucesso: $msiFile"
