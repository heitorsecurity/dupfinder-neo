$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }

$msiFile = Join-Path $scriptDir "dupfinder-neo.msi"
if (Test-Path $msiFile) { Remove-Item $msiFile -Force }

$cabFile = Join-Path $scriptDir "data.cab"
if (Test-Path $cabFile) { Remove-Item $cabFile -Force }

$exeFile = Join-Path $scriptDir "dupfinder-neo.exe"
$readmeFile = Join-Path $scriptDir "readme.txt"
$licenseFile = Join-Path $scriptDir "LICENSE.TXT"

# Prepare CAB DDF file with internal names matching the File table primary keys
$ddfContent = @"
.Set CabinetNameTemplate=data.cab
.Set DiskDirectoryTemplate=$scriptDir
.Set CompressionType=MSZIP
.Set Cabinet=ON
.Set Compress=ON
"$exeFile" F_Exe
"$readmeFile" F_Readme
"$licenseFile" F_License
"@


$ddfPath = Join-Path $scriptDir "cabinet.ddf"
[System.IO.File]::WriteAllText($ddfPath, $ddfContent)

Write-Host "Criando arquivo CAB com makecab..."
& makecab.exe /F "$ddfPath"
Remove-Item $ddfPath -Force

$exeSize = (Get-Item $exeFile).Length
$readmeSize = (Get-Item $readmeFile).Length
$licenseSize = (Get-Item $licenseFile).Length

Write-Host "Criando banco de dados MSI com WindowsInstaller COM..."
$wi = New-Object -ComObject WindowsInstaller.Installer

# Open Database in direct create mode (4 = msiOpenDatabaseModeCreateDirect, 3 = msiOpenDatabaseModeCreate)
$db = $wi.OpenDatabase($msiFile, 4)

function ExecSql($database, $sql) {
    $view = $database.OpenView($sql)
    $view.Execute()
    $view.Close()
}

# 1. Properties
ExecSql $db "CREATE TABLE Property (Property CHAR(72) NOT NULL, Value CHAR(0) NOT NULL PRIMARY KEY Property)"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductCode', '{B76C1234-A890-4DEF-9102-1234567890AB}')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductName', 'DupFinder-Neo')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductVersion', '0.9.0')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('Manufacturer', 'DupFinder Neo Project')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ProductLanguage', '1033')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('UpgradeCode', '{F89D4321-B765-4321-BEEF-9876543210FE}')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('ALLUSERS', '1')"
ExecSql $db "INSERT INTO Property (Property, Value) VALUES ('DiskPrompt', 'DupFinder-Neo Installation [1]')"

# 2. Directory
ExecSql $db "CREATE TABLE Directory (Directory CHAR(72) NOT NULL, Directory_Parent CHAR(72), DefaultDir CHAR(255) NOT NULL PRIMARY KEY Directory)"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('TARGETDIR', NULL, 'SourceDir')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramFilesFolder', 'TARGETDIR', '.')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('INSTALLDIR', 'ProgramFilesFolder', 'DUPFIN~1|DupFinder-Neo')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramMenuFolder', 'TARGETDIR', '.')"
ExecSql $db "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('DesktopFolder', 'TARGETDIR', '.')"

# 3. Component
ExecSql $db "CREATE TABLE Component (Component CHAR(72) NOT NULL, ComponentId CHAR(38), Directory_ CHAR(72) NOT NULL, Attributes SHORT NOT NULL, Condition CHAR(255), KeyPath CHAR(72) PRIMARY KEY Component)"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Exe', '{A1111111-2222-3333-4444-555555555551}', 'INSTALLDIR', 0, NULL, 'F_Exe')"
ExecSql $db "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) VALUES ('C_Docs', '{A1111111-2222-3333-4444-555555555552}', 'INSTALLDIR', 0, NULL, 'F_Readme')"

# 4. File
ExecSql $db "CREATE TABLE File (File CHAR(72) NOT NULL, Component_ CHAR(72) NOT NULL, FileName CHAR(255) NOT NULL, FileSize LONG NOT NULL, Version CHAR(72), Language CHAR(20), Attributes SHORT, Sequence SHORT NOT NULL PRIMARY KEY File)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Exe', 'C_Exe', 'dupfneo.exe|dupfinder-neo.exe', $exeSize, '0.9.0', '1033', 16384, 1)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_Readme', 'C_Docs', 'readme.txt', $readmeSize, NULL, NULL, 16384, 2)"
ExecSql $db "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) VALUES ('F_License', 'C_Docs', 'license.txt', $licenseSize, NULL, NULL, 16384, 3)"

# 5. Media
ExecSql $db "CREATE TABLE Media (DiskId SHORT NOT NULL, LastSequence SHORT NOT NULL, DiskPrompt CHAR(64), Cabinet CHAR(64), VolumeLabel CHAR(32), Source CHAR(72) PRIMARY KEY DiskId)"
ExecSql $db "INSERT INTO Media (DiskId, LastSequence, DiskPrompt, Cabinet, VolumeLabel, Source) VALUES (1, 3, 'Disk 1', '#data.cab', NULL, NULL)"

# 6. Feature
ExecSql $db "CREATE TABLE Feature (Feature CHAR(38) NOT NULL, Feature_Parent CHAR(38), Title CHAR(64), Description CHAR(255), Display SHORT, Level SHORT NOT NULL, Directory_ CHAR(72), Attributes SHORT NOT NULL PRIMARY KEY Feature)"
ExecSql $db "INSERT INTO Feature (Feature, Feature_Parent, Title, Description, Display, Level, Directory_, Attributes) VALUES ('Complete', NULL, 'DupFinder-Neo', 'Instalacao Completa do DupFinder-Neo', 1, 1, 'INSTALLDIR', 0)"

# 7. FeatureComponents
ExecSql $db "CREATE TABLE FeatureComponents (Feature_ CHAR(38) NOT NULL, Component_ CHAR(72) NOT NULL PRIMARY KEY Feature_, Component_)"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Exe')"
ExecSql $db "INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('Complete', 'C_Docs')"

# 8. Shortcut
ExecSql $db "CREATE TABLE Shortcut (Shortcut CHAR(72) NOT NULL, Directory_ CHAR(72) NOT NULL, Name CHAR(128) NOT NULL, Component_ CHAR(72) NOT NULL, Target CHAR(72) NOT NULL, Arguments CHAR(255), Description CHAR(255), Hotkey SHORT, Icon_ CHAR(72), IconIndex SHORT, ShowCmd SHORT, WkDir CHAR(72) PRIMARY KEY Shortcut)"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_Menu', 'ProgramMenuFolder', 'DUPFIN~1|DupFinder-Neo', 'C_Exe', '[#F_Exe]', NULL, 'Localizador de Arquivos Duplicados e Pastas Vazias', NULL, NULL, NULL, 1, 'INSTALLDIR')"
ExecSql $db "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('S_Desk', 'DesktopFolder', 'DUPFIN~1|DupFinder-Neo', 'C_Exe', '[#F_Exe]', NULL, 'Localizador de Arquivos Duplicados e Pastas Vazias', NULL, NULL, NULL, 1, 'INSTALLDIR')"

# 9. Sequences
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
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('CostInitialize', NULL, 800)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('FileCost', NULL, 900)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('CostFinalize', NULL, 1000)"
ExecSql $db "INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('ExecuteAction', NULL, 1300)"

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
$summary.Property(2) = "DupFinder-Neo Installation" # Title
$summary.Property(3) = "DupFinder-Neo"              # Subject
$summary.Property(4) = "DupFinder Neo Project"      # Author
$summary.Property(7) = "Intel;1033"                 # Platform;Language
$summary.Property(9) = "{B76C1234-A890-4DEF-9102-1234567890AB}" # Package Code
$summary.Property(14) = 200                         # Schema (2.0)
$summary.Property(15) = 2                           # WordCount: Bit 1 set = source compressed in cabinet
$summary.Property(19) = 0                           # Security
$summary.Persist()

$db.Commit()

# Cleanup temporary CAB and makecab reports
Remove-Item $cabFile -Force -ErrorAction SilentlyContinue
$infFile = Join-Path $scriptDir "setup.inf"
$rptFile = Join-Path $scriptDir "setup.rpt"
if (Test-Path $infFile) { Remove-Item $infFile -Force }
if (Test-Path $rptFile) { Remove-Item $rptFile -Force }

Write-Host "MSI gerado com sucesso: $msiFile"

