$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Codex - Ink Dragon.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
    Write-Host "Removed: $shortcutPath"
} else {
    Write-Host 'The Ink Dragon shortcut was not found.'
}
