# Runs the reset tool against the Atlas database battery_pass_v3 (design 14, 3d-2).
#   inventory  only reads: it writes a review folder with decisions.csv in your user folder.
#   reset      a dry run that changes nothing, unless you add -Run; then it asks you to type the database name,
#              saves a safety copy of everything it removes in your user folder, and only then makes the changes.
# The live app (AWS, database battery_pass_demo) is never touched; the tool refuses any other database.
# The password is asked each time and kept only in this process.
#
# Examples (close the Battery Pass app first):
#   .\tools\V3Reset\v3-reset.ps1 inventory -User <Atlas user> -ClusterHost <cluster host>
#   .\tools\V3Reset\v3-reset.ps1 reset -User <Atlas user> -ClusterHost <cluster host> -Decisions <path to decisions.csv>
#   .\tools\V3Reset\v3-reset.ps1 reset -User <Atlas user> -ClusterHost <cluster host> -Decisions <path to decisions.csv> -Run
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("inventory", "reset")]
    [string]$Command,
    [Parameter(Mandatory = $true)]
    [string]$User,
    [Parameter(Mandatory = $true)]
    [string]$ClusterHost,
    [string]$Decisions = "",
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$Database = "battery_pass_v3"
$project = Join-Path $PSScriptRoot "V3Reset.csproj"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

if (Get-Process -Name "BatteryPassWeb" -ErrorAction SilentlyContinue) {
    throw "Close the Battery Pass app first: the tool builds the app's code, and the reset shouldn't run while the app writes."
}

if ($Command -eq "reset" -and -not (Test-Path -LiteralPath $Decisions)) {
    throw "Give the decisions file from the inventory: -Decisions <path to decisions.csv>."
}

$secure = Read-Host "Atlas password for $User" -AsSecureString
$plain = [System.Net.NetworkCredential]::new("", $secure).Password

# Process-only environment variables: they disappear when this window closes.
$env:MONGODB_URI = "mongodb+srv://$([Uri]::EscapeDataString($User)):$([Uri]::EscapeDataString($plain))@${ClusterHost}/${Database}?retryWrites=true&w=majority"
$env:MONGODB_DB = $Database

try {
    if ($Command -eq "inventory") {
        $folder = Join-Path $HOME "battery-pass-v3-review-$stamp"
        dotnet run --project $project -- inventory --out $folder
    }
    elseif (-not $Run) {
        dotnet run --project $project -- reset --decisions $Decisions
    }
    else {
        $copy = Join-Path $HOME "battery-pass-v3-safety-copy-$stamp"
        dotnet run --project $project -- reset --decisions $Decisions --run --copy $copy
        if (Test-Path -LiteralPath $copy) {
            Write-Host ""
            Write-Host "The safety copy is in $copy" -ForegroundColor Cyan
            Write-Host "It holds removed accounts' password hashes: keep it on this PC, and delete the folder once you're satisfied with the reset." -ForegroundColor Cyan
        }
    }
}
finally {
    Remove-Item Env:MONGODB_URI, Env:MONGODB_DB -ErrorAction SilentlyContinue
    $plain = $null
}
