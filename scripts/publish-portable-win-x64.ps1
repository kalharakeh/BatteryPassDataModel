param(
    [string]$ProjectPath = "web/BatteryPassWeb.csproj",
    [string]$PublishProfile = "PortableWinX64",
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectFullPath = Resolve-Path (Join-Path $repoRoot $ProjectPath)
$projectDir = Split-Path -Parent $projectFullPath
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectFullPath)

Write-Host "Publishing $projectName with profile '$PublishProfile'..."
dotnet publish $projectFullPath -c Release -p:PublishProfile=$PublishProfile

$publishDir = Join-Path $projectDir "..\artifacts\publish\portable-win-x64"
$publishDir = (Resolve-Path $publishDir).Path

$runScriptPath = Join-Path $publishDir "run-batterypass.cmd"
@"
@echo off
setlocal
if not exist ".env.local" (
  echo Missing .env.local next to this file.
  echo Copy .env.local.example to .env.local and fill values before first run.
  pause
  exit /b 1
)
set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://0.0.0.0:5186
start "" "http://localhost:5186"
$projectName.exe
"@ | Set-Content -Path $runScriptPath -Encoding ascii

$envExampleSource = Join-Path $projectDir ".env.example"
$envExampleTarget = Join-Path $publishDir ".env.local.example"
if (Test-Path $envExampleSource) {
    Copy-Item -Path $envExampleSource -Destination $envExampleTarget -Force
}

$readmePath = Join-Path $publishDir "README-PORTABLE.txt"
@"
BatteryPass portable package (Windows x64)

1. Copy .env.local.example to .env.local
2. Fill required values in .env.local:
   - MONGODB_URI
   - MONGODB_DB
   - SESSION_SECRET
   - EXTERNAL_API_ENCRYPTION_KEY
   - (optional) DEMO_ADMIN_EMAIL, DEMO_ADMIN_PASSWORD
   - Note: app startup requires a reachable MongoDB database
3. Run: run-batterypass.cmd
4. Open: http://localhost:5186
"@ | Set-Content -Path $readmePath -Encoding ascii

if (-not $SkipZip) {
    $releaseDir = Join-Path $projectDir "..\artifacts\releases"
    if (-not (Test-Path $releaseDir)) {
        New-Item -Path $releaseDir -ItemType Directory | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $zipPath = Join-Path $releaseDir "BatteryPassWeb-portable-win-x64-$timestamp.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "Created zip: $zipPath"
}

Write-Host "Publish directory: $publishDir"
