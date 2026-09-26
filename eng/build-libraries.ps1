<#
.SYNOPSIS
    Builds the toolkit libraries from source and packs them into a local NuGet feed.

.DESCRIPTION
    Viking Air is the integration test of AutoMappic, Rapp, Sannr and Skugga. Referencing their
    published packages would only test what was last released, and would fail outright whenever
    Viking Air depends on a fix that has not shipped yet. This script clones each library, packs
    it, and exports the resulting package versions so that Directory.Build.props picks them up.

    Prova is built first because AutoMappic's own test projects depend on it.

    Outputs, under -WorkDir:
      feed/          the packed .nupkg files
      nuget.config   the local feed plus the upstream source, for `dotnet restore --configfile`

    The versions are exported as AutoMappicVersion, RappVersion, SannrVersion and SkuggaVersion:
    to $env:GITHUB_ENV on GitHub Actions, and to the current process otherwise.

.PARAMETER Ref
    Branch to build in every library. Libraries that do not have that branch fall back to their
    default branch, so a cross-repository change can be tested by using one branch name in each.

.PARAMETER Source
    Upstream NuGet source. Defaults to nuget.org.

.PARAMETER LibraryRoot
    Use existing checkouts under this directory (one folder per library) instead of cloning.

.EXAMPLE
    ./eng/build-libraries.ps1 -Ref my-feature
    dotnet restore VikingAir.sln --configfile .toolkit/nuget.config
#>
[CmdletBinding()]
param(
    [string]$Ref = $env:TOOLKIT_REF,
    [string]$Source = 'https://api.nuget.org/v3/index.json',
    [string]$LibraryRoot,
    [string]$Owner = 'Digvijay',
    [string]$WorkDir = (Join-Path (Split-Path $PSScriptRoot -Parent) '.toolkit')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Order matters: each library is restored against the feed its predecessors were packed into.
$libraries = @(
    @{ Name = 'Prova';      Solution = 'Prova.sln';      Export = $false }
    @{ Name = 'AutoMappic'; Solution = 'AutoMappic.sln'; Export = $true }
    @{ Name = 'Rapp';       Solution = 'Rapp.sln';       Export = $true }
    @{ Name = 'Sannr';      Solution = 'Sannr.sln';      Export = $true }
    @{ Name = 'Skugga';     Solution = 'Skugga.slnx';    Export = $true }
)

function Invoke-Native {
    param([string]$File, [string[]]$Arguments)
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$File $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

$feed = Join-Path $WorkDir 'feed'
$sources = Join-Path $WorkDir 'src'
New-Item -ItemType Directory -Force -Path $feed, $sources | Out-Null
Get-ChildItem $feed -Filter *.nupkg | Remove-Item

$config = Join-Path $WorkDir 'nuget.config'
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="toolkit-local" value="$feed" />
    <add key="upstream" value="$Source" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $config -Encoding utf8

$pattern = { param($n) "^$([regex]::Escape($n))\.(\d+\.\d+\.\d+[^\\/]*)\.nupkg$" }

foreach ($library in $libraries) {
    $name = $library.Name

    if ($LibraryRoot) {
        $checkout = Join-Path $LibraryRoot $name
        if (-not (Test-Path $checkout)) { throw "No checkout of $name under $LibraryRoot" }
        Write-Host "==> $name (local checkout $checkout)"
    }
    else {
        $checkout = Join-Path $sources $name
        if (Test-Path $checkout) { Remove-Item $checkout -Recurse -Force }
        $url = "https://github.com/$Owner/$name.git"

        $branch = $null
        if ($Ref -and (git ls-remote --heads $url $Ref)) { $branch = $Ref }
        Write-Host "==> $name ($(if ($branch) { $branch } else { 'default branch' }))"

        # Full history: Nerdbank.GitVersioning computes the version from it and fails on a shallow clone.
        $clone = @('clone', '--quiet', $url, $checkout)
        if ($branch) { $clone = @('clone', '--quiet', '--branch', $branch, $url, $checkout) }
        Invoke-Native git $clone
    }

    $solution = Join-Path $checkout $library.Solution
    Invoke-Native dotnet @('restore', $solution, '--configfile', $config)
    Invoke-Native dotnet @('pack', $solution, '--no-restore', '--configuration', 'Release', '--output', $feed)

    if ($library.Export) {
        # Match the package itself, not its satellites: Sannr.1.7.0.nupkg, never Sannr.AspNetCore.1.7.0.nupkg.
        $regex = & $pattern $name
        $package = Get-ChildItem $feed -Filter "$name.*.nupkg" | Where-Object { $_.Name -match $regex } | Select-Object -First 1
        if (-not $package) { throw "Packing $name produced no $name package." }

        $version = [regex]::Match($package.Name, $regex).Groups[1].Value
        $variable = "${name}Version"
        Set-Item -Path "env:$variable" -Value $version
        if ($env:GITHUB_ENV) { Add-Content -Path $env:GITHUB_ENV -Value "$variable=$version" }
        Write-Host "    $variable=$version"
    }
}

Write-Host "Local feed: $feed"
Write-Host "Restore with: dotnet restore VikingAir.sln --configfile $config"
