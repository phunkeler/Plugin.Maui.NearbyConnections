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
    ./scripts/device-tests.ps1 -Platform all
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

    # --logger trx is required, not cosmetic: DeviceRunners omits the CLI's --logger flag when
    # none is given, and writes no TRX at all. The name matches _DeviceRunnersTrxFile, which the
    # report phase reads back.
    # -p:TargetFrameworks pins the restore graph to this TFM. `-f` scopes the *build*, but restore
    # still walks every framework in <TargetFrameworks>, which fails with NETSDK1147 on a machine
    # that only has the one platform's workload installed (as each CI job does).
    dotnet test $runnerProject -f net10.0-android -p:TargetFrameworks=net10.0-android -p:DeviceRunnersDevice=$serial --logger 'trx;LogFileName=test-results.trx'
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

    # `simctl boot` starts the runtime headless -- Simulator.app is a separate GUI client that
    # attaches to an already-booted device. Without this the tests run with no visible window,
    # unlike the Android leg, where `emulator` *is* the GUI app and shows itself. Skipped in CI,
    # which has no display server. `open` is a no-op if the app is already running.
    if (-not $env:CI) { open -a Simulator --args -CurrentDeviceUDID $booted.udid }

    # Match the RID to the host: Apple Silicon simulators are arm64, Intel ones x64.
    $rid = if ((uname -m) -eq 'arm64') { 'iossimulator-arm64' } else { 'iossimulator-x64' }
    Write-Host "Running iOS device tests on '$($booted.name)' ($($booted.udid), $rid)..."

    # See the Android note: restore ignores -f, so the TFM is pinned for the restore graph too.
    dotnet test $runnerProject -f net10.0-ios -p:TargetFrameworks=net10.0-ios -p:RuntimeIdentifier=$rid -p:DeviceRunnersDevice=$($booted.udid) --logger 'trx;LogFileName=test-results.trx'
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
    # Write-Host, not just $failed: the collected message is printed at the end, but CI needs the
    # failure visible in the job log at the point it happened.
    try { Invoke-AndroidTests } catch { Write-Host "android failed: $_"; $failed += "android: $_" } finally { Copy-Results 'android' }
}

if ($Platform -in @('ios', 'all')) {
    if ($IsMacOS) {
        try { Invoke-IosTests } catch { Write-Host "ios failed: $_"; $failed += "ios: $_" } finally { Copy-Results 'ios' }
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
