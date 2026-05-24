# Sets up port discovery for two iOS simulators running NearbyChat with DevFlow.
#
# iOS simulators share the host network, so no port forwarding is needed.
# The DevFlow broker auto-assigns each simulator a unique port from the pool (10223-10899).
# This script retrieves the assigned ports from the broker and prints the run command.
#
# Usage: .\setup-ios.ps1
#
# Prerequisites:
#   - Two iOS simulators launched and running NearbyChat with DevFlow agent
#   - maui devflow CLI installed (dotnet tool install -g Microsoft.Maui.Cli --prerelease)

$ErrorActionPreference = 'Stop'

Write-Host "Querying DevFlow broker for connected agents..."
$listOutput = maui devflow list 2>&1

Write-Host $listOutput
Write-Host ""

# Extract iOS simulator agents from the list output
$iosAgents = $listOutput | Select-String 'iOS' | ForEach-Object {
    $cols = $_.Line -split '\s{2,}'
    [PSCustomObject]@{
        Id       = $cols[0].Trim()
        App      = $cols[1].Trim()
        Platform = $cols[2].Trim()
        Port     = $cols[4].Trim()
    }
}

if ($iosAgents.Count -lt 2) {
    Write-Warning "Found fewer than 2 iOS agents. Make sure both simulators are running NearbyChat."
    Write-Host "Run 'maui devflow list' manually to check what is connected."
    exit 1
}

$port1 = $iosAgents[0].Port
$port2 = $iosAgents[1].Port

Write-Host "Simulator 1 (Advertiser): port $port1"
Write-Host "Simulator 2 (Discoverer): port $port2"
Write-Host ""
Write-Host "Run scenarios with:"
Write-Host "  dotnet run -- run connection-lifecycle --device1-port $port1 --device2-port $port2"
