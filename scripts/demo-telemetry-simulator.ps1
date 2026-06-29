#requires -Version 5.1
<#
.SYNOPSIS
Run from another Windows PC to simulate live Battery Pass telemetry.

.DESCRIPTION
The script writes realistic demo telemetry through the public external API.
It can discover reset-created batteries through cluster endpoints when the
token has read access, then it seeds recent history and keeps posting live
points so the passport telemetry charts have moving data.

For all batteries created by the battery-family reset, use a global read-write
API token from Admin > API Token Management. Cluster-scoped tokens only see and
write batteries in their own cluster.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\demo-telemetry-simulator.ps1 `
  -BaseUrl "https://your-app.example.com" `
  -Token "YOUR_READ_WRITE_TOKEN"

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\demo-telemetry-simulator.ps1 `
  -BaseUrl "https://your-app.example.com" `
  -Token "YOUR_READ_WRITE_TOKEN" `
  -BatteryId "BATTERY_ID_1","BATTERY_ID_2" `
  -NoSeedHistory
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Token,

    [string[]]$BatteryId = @(),

    [string[]]$ClusterId = @(),

    [switch]$DiscoverBatteries,

    [int]$SeedHistoryMinutes = 60,

    [int]$SeedIntervalSeconds = 60,

    [int]$LiveIntervalSeconds = 25,

    [int]$MaxPointsPerRequest = 200,

    [int]$WritesPerMinuteLimit = 28,

    [switch]$NoSeedHistory,

    [switch]$Once
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

if ($SeedIntervalSeconds -lt 10) {
    throw "SeedIntervalSeconds must be at least 10 seconds."
}

if ($LiveIntervalSeconds -lt 5) {
    throw "LiveIntervalSeconds must be at least 5 seconds."
}

if ($MaxPointsPerRequest -lt 1 -or $MaxPointsPerRequest -gt 250) {
    throw "MaxPointsPerRequest must be between 1 and 250."
}

if ($WritesPerMinuteLimit -lt 1) {
    throw "WritesPerMinuteLimit must be at least 1."
}

$BaseUrl = $BaseUrl.TrimEnd("/")
$encodedToken = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${Token}:"))
$script:Headers = @{
    Authorization = "Basic $encodedToken"
}
$script:WriteTimestamps = New-Object System.Collections.Generic.Queue[datetime]

function Join-ApiUrl {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    return "$BaseUrl$Path"
}

function Get-HttpStatusCode {
    param([Parameter(Mandatory = $true)]$ErrorRecord)

    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) {
        return 0
    }

    try {
        return [int]$response.StatusCode
    }
    catch {
        return 0
    }
}

function Wait-ForWriteBudget {
    while ($true) {
        $now = [DateTime]::UtcNow
        while ($script:WriteTimestamps.Count -gt 0 -and ($now - $script:WriteTimestamps.Peek()).TotalSeconds -ge 60) {
            [void]$script:WriteTimestamps.Dequeue()
        }

        if ($script:WriteTimestamps.Count -lt $WritesPerMinuteLimit) {
            return
        }

        $oldest = $script:WriteTimestamps.Peek()
        $sleepSeconds = [Math]::Max(1, [Math]::Ceiling(61 - ($now - $oldest).TotalSeconds))
        Write-Host "Write rate budget reached; waiting $sleepSeconds second(s)." -ForegroundColor Yellow
        Start-Sleep -Seconds $sleepSeconds
    }
}

function Invoke-BatteryPassApi {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST")]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body = $null
    )

    $uri = Join-ApiUrl -Path $Path
    $attempt = 1
    $maxAttempts = 5

    while ($attempt -le $maxAttempts) {
        try {
            $parameters = @{
                Method = $Method
                Uri = $uri
                Headers = $script:Headers
                ErrorAction = "Stop"
            }

            if ($null -ne $Body) {
                $parameters.ContentType = "application/json"
                $parameters.Body = $Body | ConvertTo-Json -Depth 8
            }

            if ($Method -eq "POST") {
                Wait-ForWriteBudget
            }

            $result = Invoke-RestMethod @parameters

            if ($Method -eq "POST") {
                $script:WriteTimestamps.Enqueue([DateTime]::UtcNow)
            }

            return $result
        }
        catch {
            $statusCode = Get-HttpStatusCode -ErrorRecord $_
            if (($statusCode -eq 429 -or $statusCode -ge 500) -and $attempt -lt $maxAttempts) {
                $delay = [Math]::Min(60, 5 * $attempt)
                Write-Host "Request returned HTTP $statusCode. Retrying in $delay second(s): $Method $uri" -ForegroundColor Yellow
                Start-Sleep -Seconds $delay
                $attempt++
                continue
            }

            throw
        }
    }
}

function Add-TargetBattery {
    param(
        [System.Collections.Generic.List[object]]$Targets,
        [hashtable]$Seen,
        [Parameter(Mandatory = $true)][string]$Id,
        [string]$Family = "",
        [string]$Model = "",
        [string]$SerialNumber = "",
        [string]$Cluster = "",
        [string]$ClusterName = ""
    )

    if ([string]::IsNullOrWhiteSpace($Id) -or $Seen.ContainsKey($Id)) {
        return
    }

    $Seen[$Id] = $true
    $Targets.Add([pscustomobject]@{
        BatteryId = $Id
        BatteryFamily = $Family
        BatteryModel = $Model
        SerialNumber = $SerialNumber
        ClusterId = $Cluster
        ClusterName = $ClusterName
    })
}

function Get-TargetBatteries {
    $targets = New-Object System.Collections.Generic.List[object]
    $seen = @{}

    foreach ($id in $BatteryId) {
        Add-TargetBattery -Targets $targets -Seen $seen -Id $id
    }

    $shouldDiscover = $DiscoverBatteries.IsPresent -or $BatteryId.Count -eq 0
    if (-not $shouldDiscover) {
        return $targets
    }

    $clusters = @()
    if ($ClusterId.Count -gt 0) {
        foreach ($id in $ClusterId) {
            if (-not [string]::IsNullOrWhiteSpace($id)) {
                $clusters += [pscustomobject]@{
                    clusterId = $id
                    name = $id
                }
            }
        }
    }
    else {
        $clusterResponse = Invoke-BatteryPassApi -Method GET -Path "/api/external/v1/clusters"
        $clusters = @($clusterResponse.data.clusters)
    }

    foreach ($cluster in $clusters) {
        if ($null -eq $cluster) {
            continue
        }

        $currentClusterId = [string]$cluster.clusterId
        if ([string]::IsNullOrWhiteSpace($currentClusterId)) {
            continue
        }

        $escapedClusterId = [Uri]::EscapeDataString($currentClusterId)
        $path = "/api/external/v1/clusters/{0}/batteries" -f $escapedClusterId
        $batteryResponse = Invoke-BatteryPassApi -Method GET -Path $path
        foreach ($battery in @($batteryResponse.data.batteries)) {
            if ($null -eq $battery) {
                continue
            }

            Add-TargetBattery `
                -Targets $targets `
                -Seen $seen `
                -Id ([string]$battery.batteryId) `
                -Family ([string]$battery.batteryFamily) `
                -Model ([string]$battery.batteryModel) `
                -SerialNumber ([string]$battery.serialNumber) `
                -Cluster ([string]$batteryResponse.data.clusterId) `
                -ClusterName ([string]$batteryResponse.data.clusterName)
        }
    }

    return $targets
}

function Get-StableUnit {
    param([Parameter(Mandatory = $true)][string]$Value)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
        $hash = $sha.ComputeHash($bytes)
        $raw = [BitConverter]::ToUInt32($hash, 0)
        return [double]$raw / 4294967295.0
    }
    finally {
        $sha.Dispose()
    }
}

function Get-FamilyFactor {
    param([string]$BatteryFamily)

    if ($BatteryFamily -match "13") {
        return 1.25
    }

    if ($BatteryFamily -match "Core") {
        return 0.80
    }

    return 1.00
}

function Limit-Range {
    param(
        [double]$Value,
        [double]$Minimum,
        [double]$Maximum
    )

    return [Math]::Min($Maximum, [Math]::Max($Minimum, $Value))
}

function New-TelemetryPoint {
    param(
        [Parameter(Mandatory = $true)]$Battery,
        [Parameter(Mandatory = $true)][datetime]$MeasuredAtUtc
    )

    $utc = $MeasuredAtUtc.ToUniversalTime()
    $unixSeconds = [int64](($utc - [DateTime]"1970-01-01T00:00:00Z").TotalSeconds)
    $phase = (Get-StableUnit -Value "$($Battery.BatteryId)|phase") * 6.283185307179586
    $shape = Get-StableUnit -Value "$($Battery.BatteryId)|shape"
    $familyFactor = Get-FamilyFactor -BatteryFamily $Battery.BatteryFamily
    $slowWave = [Math]::Sin(($unixSeconds / 600.0) + $phase)
    $fastWave = [Math]::Sin(($unixSeconds / 75.0) + ($phase * 0.73))
    $fourHourCycle = (($unixSeconds + [int]($phase * 1000)) % 14400) / 14400.0

    $charge = Limit-Range -Value (82 - ($fourHourCycle * 9) + (($shape * 6) - 3) + ($slowWave * 1.8)) -Minimum 18 -Maximum 96
    $current = Limit-Range -Value (28 + ((0.5 + ($fastWave * 0.5)) * 38 * $familyFactor) + ($shape * 7)) -Minimum 5 -Maximum 120
    $voltage = Limit-Range -Value (355 + ($charge * 0.62) + ($slowWave * 2.0) + ($familyFactor * 3)) -Minimum 330 -Maximum 430
    $consumption = Limit-Range -Value (95 + ($familyFactor * 35) + ((($unixSeconds % 86400) / 3600.0) * (1.1 * $familyFactor)) + ($shape * 12) + ($fastWave * 0.4)) -Minimum 0 -Maximum 260

    return [pscustomobject]@{
        currentConsumptionKwh = [Math]::Round($consumption, 2)
        currentChargeLevelPct = [Math]::Round($charge, 1)
        currentVoltageV = [Math]::Round($voltage, 1)
        currentCurrentA = [Math]::Round($current, 1)
        measuredAt = $utc.ToString("O")
    }
}

function Send-TelemetryPoints {
    param(
        [Parameter(Mandatory = $true)]$Battery,
        [Parameter(Mandatory = $true)][object[]]$points
    )

    if ($points.Count -eq 0) {
        return
    }

    $escapedBatteryId = [Uri]::EscapeDataString($Battery.BatteryId)
    $path = "/api/external/v1/batteries/{0}/telemetry" -f $escapedBatteryId
    $bodyObject = @{ points = $points }
    [void](Invoke-BatteryPassApi -Method POST -Path $path -Body $bodyObject)
}

function Send-HistorySeed {
    param([Parameter(Mandatory = $true)]$Battery)

    if ($NoSeedHistory.IsPresent -or $SeedHistoryMinutes -le 0) {
        return
    }

    $endUtc = [DateTime]::UtcNow.AddSeconds(-2)
    $startUtc = $endUtc.AddMinutes(-1 * $SeedHistoryMinutes)
    $points = New-Object System.Collections.Generic.List[object]
    $cursor = $startUtc

    while ($cursor -le $endUtc) {
        $points.Add((New-TelemetryPoint -Battery $Battery -MeasuredAtUtc $cursor))
        $cursor = $cursor.AddSeconds($SeedIntervalSeconds)
    }

    for ($index = 0; $index -lt $points.Count; $index += $MaxPointsPerRequest) {
        $remaining = $points.Count - $index
        $take = [Math]::Min($MaxPointsPerRequest, $remaining)
        $chunk = @()
        for ($offset = 0; $offset -lt $take; $offset++) {
            $chunk += $points[$index + $offset]
        }

        Send-TelemetryPoints -Battery $Battery -points $chunk
    }

    Write-Host ("Seeded {0} history point(s) for {1}" -f $points.Count, $Battery.BatteryId)
}

$targets = @(Get-TargetBatteries)
if ($targets.Count -eq 0) {
    throw "No target batteries were found. Use a global read-write token, pass -BatteryId, or pass -ClusterId for clusters the token can access."
}

$estimatedLiveWritesPerMinute = [Math]::Ceiling(($targets.Count * 60.0) / $LiveIntervalSeconds)
if ($estimatedLiveWritesPerMinute -gt $WritesPerMinuteLimit) {
    Write-Host ("Live cadence may hit the write limit: {0} target batteries at {1}s is about {2} writes/min. The script will pace writes automatically." -f $targets.Count, $LiveIntervalSeconds, $estimatedLiveWritesPerMinute) -ForegroundColor Yellow
}

Write-Host ("Targeting {0} battery/batteries:" -f $targets.Count)
foreach ($battery in $targets) {
    $label = $battery.BatteryId
    if (-not [string]::IsNullOrWhiteSpace($battery.SerialNumber)) {
        $label = "$label ($($battery.BatteryFamily), $($battery.BatteryModel), $($battery.SerialNumber))"
    }

    Write-Host " - $label"
}

foreach ($battery in $targets) {
    Send-HistorySeed -Battery $battery
}

Write-Host "Starting live telemetry loop. Press Ctrl+C to stop."
do {
    $now = [DateTime]::UtcNow
    foreach ($battery in $targets) {
        $point = New-TelemetryPoint -Battery $battery -MeasuredAtUtc $now
        Send-TelemetryPoints -Battery $battery -points @($point)
        Write-Host ("{0:u} {1} SOC={2:n1}% V={3:n1} A={4:n1} kWh={5:n2}" -f $now, $battery.BatteryId, $point.currentChargeLevelPct, $point.currentVoltageV, $point.currentCurrentA, $point.currentConsumptionKwh)
    }

    if ($Once.IsPresent) {
        break
    }

    Start-Sleep -Seconds $LiveIntervalSeconds
} while ($true)
