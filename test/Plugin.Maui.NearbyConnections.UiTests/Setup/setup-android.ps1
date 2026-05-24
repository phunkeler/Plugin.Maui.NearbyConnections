# setup-android.ps1
# Verifies connected Android devices and prints the serials to paste into
# NearbyConnections.runsettings (or export as environment variables).
#
# Usage: .\Setup\setup-android.ps1

$rawDevices = adb devices 2>&1 | Select-String '^\S+\s+device$'
$serials = $rawDevices | ForEach-Object { ($_.Line -split '\s+')[0] }

if ($serials.Count -lt 2) {
    Write-Error "Need at least 2 connected Android devices. Found: $($serials.Count). Run 'adb devices' to check."
    exit 1
}

$d1 = $serials[0]
$d2 = $serials[1]

Write-Host ""
Write-Host "Found devices:"
Write-Host "  Device 1 (Advertiser): $d1"
Write-Host "  Device 2 (Discoverer): $d2"
Write-Host ""
Write-Host "Paste into NearbyConnections.runsettings:"
Write-Host "  <DEVICE1_SERIAL>$d1</DEVICE1_SERIAL>"
Write-Host "  <DEVICE2_SERIAL>$d2</DEVICE2_SERIAL>"
Write-Host ""
Write-Host "Or set for this PowerShell session:"
Write-Host "  `$env:DEVICE1_SERIAL = '$d1'"
Write-Host "  `$env:DEVICE2_SERIAL = '$d2'"
Write-Host ""
Write-Host "Then run tests (Appium server must be running on localhost:4723):"
Write-Host "  dotnet test --settings NearbyConnections.runsettings"
