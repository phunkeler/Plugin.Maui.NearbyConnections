#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Renders a TRX file as Markdown for the GitHub Actions run summary.

.DESCRIPTION
    A TRX is XML, so this needs no tooling beyond PowerShell's [xml] cast. It prints the run
    counters, then every test grouped by its class, then a detail block per failing test.

    Each failure block carries the error message plus the structured log lines that test emitted --
    TestOutputLogger writes one JSON object per line into StdOut, so the level/eventName/fields are
    queryable here rather than being a wall of prose.

    There is no coverage section, and there cannot be one: neither dotnet-coverage nor coverlet can
    instrument the Android/iOS app runtimes, so the device runs produce a TRX and nothing else.
    ReportGenerator does not take TRX as input. Coverage stays the unit suite's job.

.PARAMETER Path
    The .trx file to render.

.PARAMETER Title
    Heading for the summary section, e.g. "iOS".
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Title
)

if (-not (Test-Path $Path)) {
    "## $Title`n`nNo TRX produced -- the run failed before tests reported."
    exit 0
}

[xml]$trx = Get-Content $Path
$counters = $trx.TestRun.ResultSummary.Counters
$total = [int]$counters.total
$passed = [int]$counters.passed
$failed = [int]$counters.failed
$icon = if ($failed -gt 0) { ':x:' } else { ':white_check_mark:' }

"## $icon $Title"
""
"| Total | Passed | Failed | Duration |"
"|---|---|---|---|"

# TRX stores per-test times, not a run total; sum them so the summary reports real test time.
$duration = [TimeSpan]::Zero
foreach ($r in $trx.TestRun.Results.UnitTestResult) {
    if ($r.duration) { $duration += [TimeSpan]::Parse($r.duration) }
}
"| $total | $passed | $failed | $([math]::Round($duration.TotalSeconds, 1))s |"
""

# testName is Namespace.Class.Method. The suite is [Fact]-only, so no theory display name
# brings parentheses or arguments into the string -- splitting on the last two dots is safe.
$byClass = $trx.TestRun.Results.UnitTestResult | Group-Object {
    $parts = $_.testName -split '\.'
    if ($parts.Count -ge 2) { $parts[-2] } else { $_.testName }
}

foreach ($group in $byClass | Sort-Object Name) {
    "### $($group.Name)"
    ""
    "| Result | Test | Duration |"
    "|---|---|---|"

    foreach ($r in $group.Group | Sort-Object testName) {
        # Literal emoji, not a :shortcode: -- device-tests.ps1 prints this same output to a local
        # console, which has no shortcode renderer. GitHub renders both forms identically.
        $icon = switch ($r.outcome) {
            'Passed' { "$([char]0x2705)" }
            'Failed' { "$([char]0x274C)" }
            default  { "$([char]0x23ED)" }
        }

        $method = ($r.testName -split '\.')[-1]

        $time = '--'
        if ($r.duration) {
            $span = [TimeSpan]::Parse($r.duration)
            $time = if ($span.TotalSeconds -lt 1) {
                "$([math]::Round($span.TotalMilliseconds))ms"
            }
            else {
                "$([math]::Round($span.TotalSeconds, 1))s"
            }
        }

        "| $icon | $method | $time |"
    }
    ""
}

foreach ($r in $trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' }) {
    "<details><summary><b>$($r.testName)</b></summary>"
    ""
    '```'
    $r.Output.ErrorInfo.Message
    $r.Output.ErrorInfo.StackTrace
    '```'
    ""

    # Only warnings and worse -- a passing-level dump would bury the failure.
    $noisy = $r.Output.StdOut -split "`n" | Where-Object { $_.Trim() } | ForEach-Object {
        try { $_ | ConvertFrom-Json } catch { $null }
    } | Where-Object { $_ -and $_.level -in @('warn', 'fail', 'crit') }

    if ($noisy) {
        "Log (warning and above):"
        ""
        '```'
        $noisy | ForEach-Object { "$($_.level) $($_.eventName) $($_.message)" }
        '```'
    }
    "</details>"
    ""
}
