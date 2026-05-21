param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$Output = "artifacts\publish\APACDigital",
  [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "apac_biblioteca.csproj"
$outputPath = Join-Path $root $Output

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

if (-not $NoRestore) {
  dotnet restore $project --configfile (Join-Path $root "NuGet.Config")
  if ($LASTEXITCODE -ne 0) {
    throw "Falha ao restaurar pacotes do projeto."
  }
}

dotnet publish $project `
  --configuration $Configuration `
  --runtime $Runtime `
  --self-contained true `
  --no-restore `
  --output $outputPath

if ($LASTEXITCODE -ne 0) {
  throw "Falha ao publicar o aplicativo."
}

Write-Host "Publicado em: $outputPath"
