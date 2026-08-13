#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the device-test suite on an Android emulator and/or iOS simulator.

.DESCRIPTION
    Wraps `dotnet test` on the DeviceRunners-hosted runner app. The same script runs locally
    (macOS/Windows/Linux) and in CI — boot state is ensured, the target device is passed
    explicitly (DeviceRunners' booted-simulator auto-detection is unreliable; see the plan notes),
    and TRX results land in artifacts/.

.PARAMETER Platform
    android, ios, or all. 'all' runs what the host OS supports and reports what it skipped
    (iOS requires macOS + Xcode).

.EXAMPLE
    ./eng/device-tests.ps1 -Platform all
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('android', 'ios', 'all')]
    [string]$Platform
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runnerProject = Join-Path $repoRoot 'test/Plugin.Maui.NearbyConnections.DeviceTests.Runner/Plugin.Maui.NearbyConnections.DeviceTests.Runner.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$failed = @()

function Invoke-AndroidTests {
    # Resolve the SDK the same way the IDE tooling does.
    $sdkRoot = $env:ANDROID_HOME
    if (-not $sdkRoot) { $sdkRoot = $env:ANDROID_SDK_ROOT }
    if (-not $sdkRoot) {
        $sdkRoot = if ($IsMacOS) { "$HOME/Library/Android/sdk" }
                   elseif ($IsWindows) { "$env:LOCALAPPDATA\Android\Sdk" }
                   else { "$HOME/Android/Sdk" }
    }
    $adb = Join-Path $sdkRoot 'platform-tools/adb'
    $emulator = Join-Path $sdkRoot 'emulator/emulator'

    # Reuse a running emulator/device; boot the first configured AVD otherwise. (In CI the
    # emulator-runner action has already booted one, so this branch is local-only.)
    $devices = & $adb devices | Select-String -Pattern '\tdevice$'
    if (-not $devices) {
        $avd = (& $emulator -list-avds | Select-Object -First 1)
        if (-not $avd) { throw 'No Android emulator AVD configured and no device connected.' }
        Write-Host "Booting emulator '$avd'..."
        Start-Process -FilePath $emulator -ArgumentList @('-avd', $avd, '-no-snapshot', '-no-audio', '-no-boot-anim') -RedirectStandardOutput (Join-Path $artifacts 'emulator.log')
        & $adb wait-for-device

        # Bounded: an emulator that never finishes booting fails the run instead of hanging it.
        $deadline = (Get-Date).AddMinutes(5)
        while ((& $adb shell getprop sys.boot_completed 2>$null) -ne '1') {
            if ((Get-Date) -gt $deadline) { throw "Emulator '$avd' did not finish booting within 5 minutes." }
            Start-Sleep -Seconds 2
        }

        $devices = & $adb devices | Select-String -Pattern '\tdevice$'
    }

    $serial = (($devices | Select-Object -First 1) -split '\t')[0]
    Write-Host "Running Android device tests on '$serial'..."

    dotnet test $runnerProject -f net10.0-android -p:DeviceRunnersDevice=$serial
    if ($LASTEXITCODE -ne 0) { throw "Android device tests failed (exit $LASTEXITCODE)." }
}

function Invoke-IosTests {
    if (-not $IsMacOS) { throw 'iOS device tests require macOS with Xcode.' }

    # Reuse a booted simulator; boot the newest available iPhone otherwise.
    $booted = (xcrun simctl list devices booted -j | ConvertFrom-Json).devices.PSObject.Properties.Value |
        Where-Object { $_.isAvailable } | Select-Object -First 1
    if (-not $booted) {
        # simctl returns runtimes in arbitrary order, so sort explicitly. Sort on the numeric
        # version parsed out of the runtime key ("...SimRuntime.iOS-26-4"); a plain string sort
        # would rank iOS-9-0 above iOS-18-0.
        $booted = (xcrun simctl list devices available -j | ConvertFrom-Json).devices.PSObject.Properties |
            Where-Object { $_.Name -match 'SimRuntime\.iOS-\d+-\d+$' } |
            Sort-Object { [version](($_.Name -split 'iOS-')[-1] -replace '-', '.') } -Descending |
            ForEach-Object { $_.Value } |
            Where-Object { $_.name -match 'iPhone' } | Select-Object -First 1
        if (-not $booted) { throw 'No available iPhone simulator found.' }
        Write-Host "Booting simulator '$($booted.name)' ($($booted.udid))..."
        xcrun simctl boot $booted.udid
    }

    # Match the RID to the host: Apple Silicon simulators are arm64, Intel ones x64.
    $rid = if ((uname -m) -eq 'arm64') { 'iossimulator-arm64' } else { 'iossimulator-x64' }
    Write-Host "Running iOS device tests on '$($booted.name)' ($($booted.udid), $rid)..."

    dotnet test $runnerProject -f net10.0-ios -p:RuntimeIdentifier=$rid -p:DeviceRunnersDevice=$($booted.udid)
    if ($LASTEXITCODE -ne 0) { throw "iOS device tests failed (exit $LASTEXITCODE)." }
}

function Copy-Results([string]$suffix) {
    $resultsDir = Join-Path (Split-Path $runnerProject) 'test-results'
    if (Test-Path $resultsDir) {
        Get-ChildItem $resultsDir -Filter '*.trx' | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $artifacts "device-$suffix.trx") -Force
        }
        Get-ChildItem $resultsDir -Filter '*.txt' | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $artifacts "device-$suffix-$($_.Name)") -Force
        }
    }
}

if ($Platform -in @('android', 'all')) {
    try { Invoke-AndroidTests } catch { $failed += "android: $_" } finally { Copy-Results 'android' }
}

if ($Platform -in @('ios', 'all')) {
    if ($IsMacOS) {
        try { Invoke-IosTests } catch { $failed += "ios: $_" } finally { Copy-Results 'ios' }
    }
    elseif ($Platform -eq 'all') {
        Write-Warning 'Skipping iOS: requires macOS with Xcode.'
    }
    else {
        throw 'iOS device tests require macOS with Xcode.'
    }
}

if ($failed) {
    $failed | ForEach-Object { Write-Error -ErrorAction Continue $_ }
    exit 1
}

Write-Host "Done. TRX results in $artifacts"
