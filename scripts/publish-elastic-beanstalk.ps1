param(
    [string]$ProjectPath = "web/BatteryPassWeb.csproj",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectFullPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "publish\elastic-beanstalk"
$releaseDir = Join-Path $artifactsRoot "releases"

function Assert-InArtifacts([string]$PathToCheck) {
    $fullPath = [System.IO.Path]::GetFullPath($PathToCheck)
    $fullArtifactsRoot = [System.IO.Path]::GetFullPath($artifactsRoot)

    if (-not $fullPath.StartsWith($fullArtifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean path outside artifacts: $fullPath"
    }
}

if (Test-Path $publishDir) {
    Assert-InArtifacts $publishDir
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (-not (Test-Path $releaseDir)) {
    New-Item -Path $releaseDir -ItemType Directory | Out-Null
}

Write-Host "Publishing Elastic Beanstalk bundle..."
dotnet publish $projectFullPath -c $Configuration -o $publishDir --self-contained false

$procfilePath = Join-Path $publishDir "Procfile"
"web: dotnet BatteryPassWeb.dll --urls http://localhost:5000" | Set-Content -Path $procfilePath -Encoding ascii

Get-ChildItem -Path $publishDir -Force -Filter ".env*" | Remove-Item -Force

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipPath = Join-Path $releaseDir "BatteryPassWeb-elastic-beanstalk-$timestamp.zip"

if (Test-Path $zipPath) {
    Assert-InArtifacts $zipPath
    Remove-Item -LiteralPath $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression

$publishRoot = (Resolve-Path $publishDir).Path.TrimEnd('\', '/')
$zipStream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew)
try {
    $archive = [System.IO.Compression.ZipArchive]::new($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -Path $publishDir -Recurse -File -Force | ForEach-Object {
            $relativePath = $_.FullName.Substring($publishRoot.Length).TrimStart('\', '/').Replace('\', '/')
            $entry = $archive.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]$_.LastWriteTimeUtc

            $entryStream = $entry.Open()
            try {
                $fileStream = [System.IO.File]::OpenRead($_.FullName)
                try {
                    $fileStream.CopyTo($entryStream)
                }
                finally {
                    $fileStream.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $zipStream.Dispose()
}

Write-Host "Created Elastic Beanstalk source bundle:"
Write-Host $zipPath
