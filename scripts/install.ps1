param(
  [string]$InstallDir = "$env:LOCALAPPDATA\APACDigital"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishScript = Join-Path $root "scripts\publish.ps1"
$publishDir = Join-Path $root "artifacts\publish\APACDigital"

& $publishScript

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $InstallDir -Recurse -Force

$exePath = Join-Path $InstallDir "APACDigital.exe"
$shell = New-Object -ComObject WScript.Shell

$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\APAC Digital"
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

$startShortcut = $shell.CreateShortcut((Join-Path $startMenuDir "APAC Digital.lnk"))
$startShortcut.TargetPath = $exePath
$startShortcut.WorkingDirectory = $InstallDir
$startShortcut.IconLocation = $exePath
$startShortcut.Save()

$desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "APAC Digital.lnk"))
$desktopShortcut.TargetPath = $exePath
$desktopShortcut.WorkingDirectory = $InstallDir
$desktopShortcut.IconLocation = $exePath
$desktopShortcut.Save()

Write-Host "APAC Digital instalado em: $InstallDir"
