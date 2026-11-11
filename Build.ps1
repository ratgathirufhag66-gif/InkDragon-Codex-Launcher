$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $projectRoot 'dist\InkDragonCodex'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Windows .NET Framework C# compiler was not found.'
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $outputDir 'media') | Out-Null

$assemblyRoot = 'C:\Windows\Microsoft.NET\assembly'
function Find-FrameworkAssembly([string] $name) {
    $assembly = Get-ChildItem -Path $assemblyRoot -Filter ($name + '.dll') -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'v4\.0_' } |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $assembly) { throw "Framework assembly not found: $name" }
    return $assembly
}

$presentationCore = Find-FrameworkAssembly 'PresentationCore'
$presentationFramework = Find-FrameworkAssembly 'PresentationFramework'
$windowsBase = Find-FrameworkAssembly 'WindowsBase'
$systemXaml = Find-FrameworkAssembly 'System.Xaml'

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /out:"$outputDir\InkDragonCodex.exe" `
    /reference:"$presentationCore" `
    /reference:"$presentationFramework" `
    /reference:"$windowsBase" `
    /reference:"$systemXaml" `
    "$projectRoot\Program.cs"

if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

Copy-Item -LiteralPath "$projectRoot\media\startup.mp4" -Destination "$outputDir\media\startup.mp4" -Force
Copy-Item -LiteralPath "$projectRoot\launcher.ini" -Destination "$outputDir\launcher.ini" -Force
Copy-Item -LiteralPath "$projectRoot\Install-Shortcut.ps1" -Destination "$outputDir\Install-Shortcut.ps1" -Force
Copy-Item -LiteralPath "$projectRoot\Uninstall-Shortcut.ps1" -Destination "$outputDir\Uninstall-Shortcut.ps1" -Force
Copy-Item -LiteralPath "$projectRoot\Preview.cmd" -Destination "$outputDir\Preview.cmd" -Force
Copy-Item -LiteralPath "$projectRoot\README.md" -Destination "$outputDir\README.md" -Force
Copy-Item -LiteralPath "$projectRoot\THIRD_PARTY_NOTICES.md" -Destination "$outputDir\THIRD_PARTY_NOTICES.md" -Force
Copy-Item -LiteralPath "$projectRoot\ACKNOWLEDGEMENTS.md" -Destination "$outputDir\ACKNOWLEDGEMENTS.md" -Force
Copy-Item -LiteralPath "$projectRoot\LICENSE" -Destination "$outputDir\LICENSE" -Force

Write-Host "Build complete: $outputDir"
