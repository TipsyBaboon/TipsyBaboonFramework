#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packs and publishes all TipsyBaboon NuGet packages.

.DESCRIPTION
    Builds, packs, and publishes Core, SqlServer, and UI packages to nuget.org.
    Requires NUGET_API_KEY environment variable or -ApiKey parameter.

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER ApiKey
    NuGet API key (overrides $env:NUGET_API_KEY)

.PARAMETER Source
    NuGet source (default: https://api.nuget.org/v3/index.json)

.PARAMETER SkipBuild
    Skip dotnet build step

.PARAMETER PackOnly
    Pack but don't push (for testing)

.EXAMPLE
    $env:NUGET_API_KEY = "oy2_..."
    .\publish-packages.ps1

.EXAMPLE
    .\publish-packages.ps1 -PackOnly
    # Just create .nupkg files without publishing
#>

param(
    [Parameter()]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [string]$ApiKey = $env:NUGET_API_KEY,
    
    [Parameter()]
    [string]$Source = 'https://api.nuget.org/v3/index.json',
    
    [Parameter()]
    [switch]$SkipBuild,
    
    [Parameter()]
    [switch]$PackOnly
)

$ErrorActionPreference = 'Stop'

# Validate API key
if (-not $PackOnly -and [string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "NuGet API key required. Set `$env:NUGET_API_KEY or use -ApiKey parameter"
}

# Find repo root and projects
$repoRoot = Split-Path $PSScriptRoot -Parent
$projects = @(
    'TipsyBaboon.Core\TipsyBaboon.Core.csproj'
    'TipsyBaboon.SqlServer\TipsyBaboon.SqlServer.csproj'
    'TipsyBaboon.UI\TipsyBaboon.UI.csproj'
)

# Read version from Directory.Build.props
$propsFile = Join-Path $repoRoot 'Directory.Build.props'
if (-not (Test-Path $propsFile)) {
    throw "Directory.Build.props not found"
}

$propsContent = Get-Content $propsFile -Raw
if ($propsContent -notmatch '<Version>([^<]+)</Version>') {
    throw "Could not find <Version> in Directory.Build.props"
}
$version = $matches[1]

Write-Host "`n=== Publishing TipsyBaboon Packages v$version ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray

# Build projects
if (-not $SkipBuild) {
    Write-Host "`n[1/3] Building projects..." -ForegroundColor Yellow
    foreach ($proj in $projects) {
        $projPath = Join-Path $repoRoot $proj
        Write-Host "  Building: $proj" -ForegroundColor Gray
        dotnet build $projPath -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $proj" }
    }
}

# Pack projects
Write-Host "`n[2/3] Packing projects..." -ForegroundColor Yellow
$packages = @()
foreach ($proj in $projects) {
    $projPath = Join-Path $repoRoot $proj
    Write-Host "  Packing: $proj" -ForegroundColor Gray
    dotnet pack $projPath -c $Configuration --no-build --nologo -o "$repoRoot\artifacts"
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $proj" }
    
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
    $packages += "$repoRoot\artifacts\$projName.$version.nupkg"
}

if ($PackOnly) {
    Write-Host "`n✓ Packages created (PackOnly mode):" -ForegroundColor Green
    foreach ($pkg in $packages) {
        Write-Host "  $pkg" -ForegroundColor Gray
    }
    exit 0
}

# Push packages
Write-Host "`n[3/3] Publishing packages..." -ForegroundColor Yellow
foreach ($pkg in $packages) {
    Write-Host "  Publishing: $(Split-Path $pkg -Leaf)" -ForegroundColor Gray
    dotnet nuget push $pkg --source $Source --api-key $ApiKey --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "Push failed: $pkg" }
}

Write-Host "`n✓ Successfully published v$version to nuget.org" -ForegroundColor Green
Write-Host "  View at: https://www.nuget.org/packages?q=TipsyBaboon" -ForegroundColor Cyan
