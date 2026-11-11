$ErrorActionPreference = 'Stop'
$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $appDir 'InkDragonCodex.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Launcher not found: $exe" }
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Codex - Ink Dragon.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $appDir
$shortcut.Description = 'Play the ink dragon video and launch Codex'
$shortcut.IconLocation = "$exe,0"
$shortcut.Save()
Write-Host "Created: $shortcutPath"
