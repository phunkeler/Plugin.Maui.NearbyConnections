#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs the device-test suite on an Android emulator and/or iOS simulator.

.DESCRIPTION
    Wraps `dotnet test` on the DeviceRunners-hosted runner app. The same script runs locally
    (macOS/Windows/Linux) and in CI. Device setup (creating/booting the emulator or simulator)
    goes through the pinned `Microsoft.Maui.Cli` local tool (`dotnet tool restore` first) --
    the same commands CI runs, so local and CI share one setup algorithm instead of two. The
    target device is passed explicitly to `dotnet test` (DeviceRunners' booted-device
    auto-detection is unreliable), and TRX results land in artifacts/.

    On-device code coverage is not possible: dotnet-coverage/coverlet cannot instrument the
    Android/iOS app runtimes. See AGENTS.md -> Commands. This script produces TRX results and a
    console summary, not a coverage report -- that stays a unit-suite concern.

.PARAMETER Platform
    android, ios, or all (default). 'all' runs what the host OS supports and reports what it
    skipped (iOS requires macOS + Xcode).

.PARAMETER AndroidApiLevel
    Which Android API level to test: latest, common, minimum (resolved from
    .config/android-api-levels.json), or a literal API level number. Defaults to 'latest'.
    CI runs all three levels as a matrix; a local run tests one at a time.

.EXAMPLE
    ./scripts/device-tests.ps1

.EXAMPLE
    ./scripts/device-tests.ps1 -Platform android -AndroidApiLevel minimum
#>
[CmdletBinding()]
param(
    [ValidateSet('android', 'ios', 'all')]
    [string]$Platform = 'all',

    [string]$AndroidApiLevel = 'latest'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runnerProject = Join-Path $repoRoot 'test/Plugin.Maui.NearbyConnections.DeviceTests.Runner/Plugin.Maui.NearbyConnections.DeviceTests.Runner.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

Write-Host 'Restoring local dotnet tools (maui CLI)...'
dotnet tool restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

$failed = @()

# The one place .config/android-api-levels.json is parsed, so CI's matrix and a local run
# resolve 'latest'/'common'/'minimum' to the same number by construction.
function Get-AndroidApiLevel([string]$Name) {
    if ($Name -match '^\d+$') { return [int]$Name }
    $levelsPath = Join-Path $repoRoot '.config/android-api-levels.json'
    $levels = Get-Content $levelsPath -Raw | ConvertFrom-Json
    $value = $levels.$Name
    if (-not $value) { throw "Unknown Android API level key '$Name'. Expected latest, common, minimum, or a number." }
    return [int]$value
}

# dotnet maui device list --json returns snake_case fields: identifier, is_running, is_emulator,
# platform, details.avd -- verified against the pinned CLI build; do not assume PascalCase.
function Get-MauiDevices([string]$PlatformFilter) {
    $json = dotnet maui device list --json --platform $PlatformFilter 2>$null
    if ($LASTEXITCODE -ne 0) { throw "dotnet maui device list failed (exit $LASTEXITCODE)." }
    return $json | ConvertFrom-Json
}

function Invoke-AndroidTests {
    $level = Get-AndroidApiLevel $AndroidApiLevel
    $avdName = "device-tests-$level"

    $running = Get-MauiDevices 'android' | Where-Object { $_.is_running }
    if (-not $running) {
        $emulators = (dotnet maui android emulator list --json | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0) { throw "dotnet maui android emulator list failed (exit $LASTEXITCODE)." }
        $exists = $emulators | Where-Object { $_.name -eq $avdName }

        if (-not $exists) {
            Write-Host "Creating Android emulator '$avdName' (API $level)..."
            # Self-bootstrap: a fresh clone has no Android SDK images installed yet. This is a
            # one-time, large download -- surfaced explicitly so it doesn't look like a hang.
            Write-Host "  (installing Android SDK packages if missing -- one-time, large download)"
            # Global --ci (before the subcommand) makes `emulator create` default its device
            # profile non-interactively instead of prompting -- verified: without it, create
            # fails with "current terminal isn't interactive" whenever no shell is attached.
            dotnet maui --ci android install --packages "platform-tools,emulator,system-images;android-$level;google_apis;x86_64" --accept-licenses
            dotnet maui --ci android emulator create $avdName --package "system-images;android-$level;google_apis;x86_64" --force
            if ($LASTEXITCODE -ne 0) { throw "dotnet maui android emulator create failed (exit $LASTEXITCODE). Run 'dotnet maui doctor' to diagnose." }
        }

        Write-Host "Booting Android emulator '$avdName'..."
        dotnet maui --ci android emulator start $avdName --wait
        if ($LASTEXITCODE -ne 0) { throw "dotnet maui android emulator start failed (exit $LASTEXITCODE). Run 'dotnet maui doctor' to diagnose." }

        $running = Get-MauiDevices 'android' | Where-Object { $_.is_running }
        if (-not $running) { throw "No running Android device found after starting '$avdName'." }
    }

    $serial = ($running | Select-Object -First 1).identifier
    Write-Host "Running Android device tests on '$serial' (API $level)..."

    # Nearby Connections needs BLUETOOTH_SCAN/CONNECT/ADVERTISE and NEARBY_WIFI_DEVICES granted
    # at runtime (Android 12+) before GMS will start advertising -- ungranted, GMS's
    # StartAdvertisingAsync blocks forever rather than failing fast, hanging the whole run.
    # `adb install -g` auto-grants every manifest-declared runtime permission at install time;
    # verified empirically that a same-signature incremental reinstall (what DeviceRunners' own
    # Install MSBuild target performs as part of `dotnet test`, below) preserves those grants
    # rather than resetting them, so installing here first is sufficient.
    Write-Host 'Building and pre-installing the runner app (adb install -g)...'
    dotnet build $runnerProject -f net10.0-android -p:TargetFrameworks=net10.0-android | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet build (pre-install) failed (exit $LASTEXITCODE)." }
    $apk = Get-ChildItem -Path (Join-Path (Split-Path $runnerProject) 'bin') -Recurse -Filter '*-Signed.apk' |
        Where-Object { $_.FullName -match 'net10\.0-android' } |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $apk) { throw 'Could not find the built runner APK to pre-install.' }
    & adb -s $serial install -r -g $apk
    if ($LASTEXITCODE -ne 0) { throw "adb install -g failed (exit $LASTEXITCODE)." }

    # Live-tail structured plugin logs (see AGENTS.md -> Conventions -> Logging) to the console
    # while the run is in progress -- in CI this interleaves directly into the Actions step log,
    # not just the post-run artifact, so a hang is visible as it happens instead of only after
    # the job times out.
    $logcatJob = Start-Job -ScriptBlock {
        param($AdbSerial)
        & adb -s $AdbSerial logcat -v raw -s 'DOTNET:I'
    } -ArgumentList $serial
    $logcatReceiver = Start-Job -ScriptBlock {
        param($JobId)
        while ($true) {
            Receive-Job -Id $JobId | ForEach-Object { Write-Host "[logcat] $_" }
            Start-Sleep -Milliseconds 250
        }
    } -ArgumentList $logcatJob.Id

    try {
        # --logger trx is required, not cosmetic: DeviceRunners omits the CLI's --logger flag when
        # none is given, and writes no TRX at all. The name matches _DeviceRunnersTrxFile, which the
        # report phase reads back.
        # -p:TargetFrameworks pins the restore graph to this TFM. `-f` scopes the *build*, but restore
        # still walks every framework in <TargetFrameworks>, which fails with NETSDK1147 on a machine
        # that only has the one platform's workload installed (as each CI job does).
        # --filter excludes tests traited RequiresRealPeer (see AcceptTimeoutTests.android.cs): they
        # call the real GMS AcceptConnectionAsync, which needs a genuine second device's
        # requestConnection to reach the connection state that call validates against -- a
        # single, radio-isolated emulator always fails them with STATUS_OUT_OF_ORDER_API_CALL.
        dotnet test $runnerProject -f net10.0-android -p:TargetFrameworks=net10.0-android -p:DeviceRunnersDevice=$serial --filter 'Category!=RequiresRealPeer' --logger 'trx;LogFileName=test-results.trx'
        $testExitCode = $LASTEXITCODE
    }
    finally {
        Stop-Job $logcatReceiver, $logcatJob -ErrorAction SilentlyContinue
        Receive-Job -Id $logcatJob.Id | ForEach-Object { Write-Host "[logcat] $_" }
        Remove-Job $logcatReceiver, $logcatJob -Force -ErrorAction SilentlyContinue
    }
    if ($testExitCode -ne 0) { throw "Android device tests failed (exit $testExitCode)." }

    # Only CI captured logcat until now; local flake triage gets the same evidence.
    & adb logcat -d > (Join-Path $artifacts "device-android-$level-logcat.txt") 2>$null

    $trxPath = Join-Path $artifacts "device-android-$level.trx"
    if (Test-Path $trxPath) {
        pwsh (Join-Path $repoRoot 'scripts/trx-summary.ps1') -Path $trxPath -Title "Android API $level"
    }
}

function Invoke-IosTests {
    if (-not $IsMacOS) { throw 'iOS device tests require macOS with Xcode.' }

    $simName = 'device-tests'
    $running = Get-MauiDevices 'ios' | Where-Object { $_.is_running -and $_.name -eq $simName }

    if (-not $running) {
        # `xcrun simctl list devicetypes` is already ordered newest-first (verified: iPhone 17
        # Pro before iPhone 6s Plus), so the first iPhone entry is the newest device type -- omit
        # --runtime to let the CLI pick the newest installed runtime that supports it.
        $deviceType = (xcrun simctl list devicetypes -j | ConvertFrom-Json).devicetypes |
            Where-Object { $_.name -match '^iPhone' } |
            Select-Object -First 1 -ExpandProperty identifier
        if (-not $deviceType) { throw 'No iPhone simulator device type found via xcrun simctl.' }

        $createJson = dotnet maui apple simulator create $deviceType --name $simName --if-not-exists --json
        if ($LASTEXITCODE -ne 0) { throw "dotnet maui apple simulator create failed (exit $LASTEXITCODE). Run 'dotnet maui doctor' to diagnose." }
        # `simulator create --json` returns its own shape (udid/name/device_type), distinct from
        # the unified `device list --json` shape (identifier/is_running/...) used everywhere else
        # in this function -- verified separately against the pinned CLI build.
        $udid = ($createJson | ConvertFrom-Json).udid

        # `--if-not-exists` can hand back the UDID of a sim that's already booted (e.g. created
        # by a previous run this script didn't itself detect as running yet) -- `simulator start`
        # errors on an already-booted device (verified: exit 1, "Unable to boot device in
        # current state: Booted"), so check state first rather than treating that as a failure.
        $alreadyBooted = (Get-MauiDevices 'ios' | Where-Object { $_.identifier -eq $udid -and $_.is_running })
        if (-not $alreadyBooted) {
            Write-Host "Booting simulator '$simName' ($udid)..."
            # `--no-open` skips launching Simulator.app in CI, which has no display server; locally
            # the app is opened so the run is visible, matching the Android leg's own emulator window.
            if ($env:CI) {
                dotnet maui apple simulator start $udid --no-open
            }
            else {
                dotnet maui apple simulator start $udid
            }
            if ($LASTEXITCODE -ne 0) { throw "dotnet maui apple simulator start failed (exit $LASTEXITCODE). Run 'dotnet maui doctor' to diagnose." }
        }

        $running = Get-MauiDevices 'ios' | Where-Object { $_.identifier -eq $udid }
    }

    $device = $running | Select-Object -First 1
    $udid = $device.identifier

    # Match the RID to the host: Apple Silicon simulators are arm64, Intel ones x64.
    $rid = if ((uname -m) -eq 'arm64') { 'iossimulator-arm64' } else { 'iossimulator-x64' }
    Write-Host "Running iOS device tests on '$($device.name)' ($udid, $rid)..."

    # See the Android note: restore ignores -f, so the TFM is pinned for the restore graph too.
    dotnet test $runnerProject -f net10.0-ios -p:TargetFrameworks=net10.0-ios -p:RuntimeIdentifier=$rid -p:DeviceRunnersDevice=$udid --logger 'trx;LogFileName=test-results.trx'
    if ($LASTEXITCODE -ne 0) { throw "iOS device tests failed (exit $LASTEXITCODE)." }

    $trxPath = Join-Path $artifacts 'device-ios.trx'
    if (Test-Path $trxPath) {
        pwsh (Join-Path $repoRoot 'scripts/trx-summary.ps1') -Path $trxPath -Title 'iOS'
    }
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
        Get-ChildItem $resultsDir -Filter '*.jsonl' | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $artifacts "device-$suffix-$($_.Name)") -Force
        }
    }
}

if ($Platform -in @('android', 'all')) {
    $androidSuffix = "android-$(Get-AndroidApiLevel $AndroidApiLevel)"
    # Write-Host, not just $failed: the collected message is printed at the end, but CI needs the
    # failure visible in the job log at the point it happened.
    try { Invoke-AndroidTests } catch { Write-Host "android failed: $_"; $failed += "android: $_" } finally { Copy-Results $androidSuffix }
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
