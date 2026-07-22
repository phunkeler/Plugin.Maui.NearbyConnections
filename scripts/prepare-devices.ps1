# Prepares connected Android devices for automated testing.
# Reads DEVICE1_SERIAL and DEVICE2_SERIAL from the environment.
# Optionally reads DEVICE_PIN if Smart Lock is unavailable.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

foreach ($var in 'DEVICE1_SERIAL', 'DEVICE2_SERIAL') {
    if (-not [string]::IsNullOrEmpty([System.Environment]::GetEnvironmentVariable($var))) { continue }
    throw "$var is not set"
}

$serials = $env:DEVICE1_SERIAL, $env:DEVICE2_SERIAL

foreach ($serial in $serials) {
    Write-Host "Preparing $serial..."

    adb -s $serial shell settings put global stay_on_while_plugged_in 7
    adb -s $serial shell input keyevent 26
    adb -s $serial shell wm dismiss-keyguard

    # Uncomment if Smart Lock is unavailable and a PIN is required:
    # if ([string]::IsNullOrEmpty($env:DEVICE_PIN)) { throw 'DEVICE_PIN is not set' }
    # adb -s $serial shell input swipe 540 1600 540 800
    # adb -s $serial shell input text $env:DEVICE_PIN
    # adb -s $serial shell input keyevent 66

    Write-Host "$serial ready."
}
