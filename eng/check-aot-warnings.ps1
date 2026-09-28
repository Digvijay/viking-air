#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Compares the trim/AOT warnings from a Native AOT publish against a reviewed baseline.

.DESCRIPTION
    The AOT gate cannot simply treat every ILC warning as an error, because two third-party
    dependencies emit warnings that this repository has no way to fix. It must not simply
    ignore them either, or a genuine regression in first-party code would go unnoticed.

    So the gate is exact: any warning NOT in eng/aot-baseline.txt fails the build.

    Two rules keep the baseline from decaying into a suppression list:

      1. A baseline entry whose origin is a first-party namespace is REJECTED. It is not
         possible to quiet a warning in code this repository owns by adding a line here.
      2. A baseline entry that no longer occurs is reported as stale and fails, so the
         baseline shrinks as upstream dependencies improve instead of accumulating.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $LogPath,
    [Parameter(Mandatory)] [string] $BaselinePath,
    # Warnings originating in these namespaces may never be baselined.
    [string[]] $FirstPartyPrefixes = @('VikingAir', 'AutoMappic', 'Rapp', 'Sannr', 'Skugga', 'Prova')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-Signature {
    <#
        Produces a stable, machine-comparable form of an ILC warning line.

        Raw lines carry an absolute project path that differs between the Linux and Windows
        runners, so it is stripped. Everything else is kept: dropping the message text would
        collapse distinct warnings that share an origin into one signature, and a regression
        could then hide behind an existing entry.
    #>
    param([string[]] $Lines)

    $Lines |
        ForEach-Object {
            if ($_ -match 'IL(\d{4}): (.+)$') {
                $rest = $Matches[2] -replace '\s*\[[A-Za-z]?:?[\\/].*$', ''
                "IL$($Matches[1]): $($rest.TrimEnd())"
            }
        } |
        Sort-Object -Unique
}

function Get-Origin {
    # The member an ILC warning is attributed to, which is everything up to the first colon.
    param([string] $Signature)
    if ($Signature -match '^IL\d{4}: ([^:]+):') { return $Matches[1] }
    return $Signature
}

if (-not (Test-Path -LiteralPath $LogPath)) {
    Write-Error "Publish log not found: $LogPath"
}
if (-not (Test-Path -LiteralPath $BaselinePath)) {
    Write-Error "Baseline not found: $BaselinePath"
}

$actual = @(ConvertTo-Signature (Get-Content -LiteralPath $LogPath))
$baseline = @(Get-Content -LiteralPath $BaselinePath |
    Where-Object { $_ -match '^IL\d{4}:' } |
    ForEach-Object { $_.TrimEnd() } |
    Sort-Object -Unique)

Write-Host "Warnings reported : $($actual.Count)"
Write-Host "Baseline entries  : $($baseline.Count)"

$failed = $false

# Rule 1: nothing first-party may be baselined.
$illegal = @($baseline | Where-Object {
    $origin = Get-Origin $_
    @($FirstPartyPrefixes | Where-Object { $origin.StartsWith("$_.") -or $origin -eq $_ }).Count -gt 0
})
if ($illegal.Count -gt 0) {
    $failed = $true
    Write-Host "::error::eng/aot-baseline.txt contains $($illegal.Count) entry/entries originating in first-party code. A warning in code this repository owns must be fixed, not baselined."
    $illegal | ForEach-Object { Write-Host "::error::illegal baseline entry: $_" }
}

# The gate itself: anything not reviewed fails.
$new = @($actual | Where-Object { $baseline -notcontains $_ })
if ($new.Count -gt 0) {
    $failed = $true
    Write-Host "::error::$($new.Count) trim/AOT warning(s) are not in the reviewed baseline."
    $new | ForEach-Object { Write-Host "::error::$_" }
}

# Rule 2: the baseline must not outlive the problem.
$stale = @($baseline | Where-Object { $actual -notcontains $_ })
if ($stale.Count -gt 0) {
    $failed = $true
    Write-Host "::error::$($stale.Count) baseline entry/entries no longer occur. Delete them so the baseline keeps reflecting reality."
    $stale | ForEach-Object { Write-Host "::error::stale: $_" }
}

if ($env:GITHUB_STEP_SUMMARY) {
    $status = if ($failed) { 'FAILED' } else { 'matched the reviewed baseline' }
    @(
        "### Native AOT warning check - $status",
        '',
        "| | Count |",
        "|---|---|",
        "| Reported | $($actual.Count) |",
        "| Baselined (third-party, reviewed) | $($baseline.Count) |",
        "| **New, not reviewed** | **$($new.Count)** |",
        "| Stale baseline entries | $($stale.Count) |"
    ) | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
}

if ($failed) { exit 1 }

Write-Host "No unreviewed trim/AOT warnings. Every reported warning is a known third-party issue."


