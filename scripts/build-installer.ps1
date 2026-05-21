param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$InstallerName = "APACDigitalSetup.exe",
  [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $root "artifacts\publish\APACDigital"
$installerDir = Join-Path $root "artifacts\installer"
$stagingDir = Join-Path $root "artifacts\installer\staging"
$packagePath = Join-Path $stagingDir "APACDigital.zip"
$outputPath = Join-Path $installerDir $InstallerName
$setupProject = Join-Path $root "installer\Setup\APACDigitalInstaller.csproj"
$setupPublishDir = Join-Path $stagingDir "setup-publish"

New-Item -ItemType Directory -Force -Path $installerDir, $stagingDir | Out-Null

$publishArgs = @{
  Configuration = $Configuration
  Runtime = $Runtime
}

if ($NoRestore) {
  $publishArgs.NoRestore = $true
}

& (Join-Path $PSScriptRoot "publish.ps1") @publishArgs

Remove-Item -LiteralPath $packagePath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $setupPublishDir -Recurse -Force -ErrorAction SilentlyContinue
$filesToPackage = Get-ChildItem -LiteralPath $publishDir -Force | Where-Object { $_.Name -ne "APACDigital.pdb" }
Compress-Archive -Path $filesToPackage.FullName -DestinationPath $packagePath -Force

dotnet publish $setupProject `
  --configuration $Configuration `
  --runtime $Runtime `
  --self-contained true `
  --output $setupPublishDir

if ($LASTEXITCODE -ne 0) {
  throw "Falha ao publicar o instalador."
}

Copy-Item -LiteralPath (Join-Path $setupPublishDir "APACDigitalSetup.exe") -Destination $outputPath -Force

if (-not (Test-Path -LiteralPath $outputPath)) {
  throw "Nao foi possivel gerar o instalador em: $outputPath"
}

Write-Host "Instalador criado em: $outputPath"
