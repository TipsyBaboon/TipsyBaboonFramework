#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Increments version in Directory.Build.props following semantic versioning.

.DESCRIPTION
    Reads current version from Directory.Build.props and increments according to -Part parameter.
    Supports: Major (x.0.0), Minor (1.x.0), Patch (1.0.x)

.PARAMETER Part
    Which version part to increment: Major, Minor, or Patch (default: Patch)

.PARAMETER DryRun
    Show what would be changed without modifying files

.EXAMPLE
    .\bump-version.ps1 -Part Patch
    # 1.0.0 -> 1.0.1

.EXAMPLE
    .\bump-version.ps1 -Part Minor
    # 1.0.5 -> 1.1.0

.EXAMPLE
    .\bump-version.ps1 -Part Major
    # 1.5.3 -> 2.0.0
#>

param(
    [Parameter()]
    [ValidateSet('Major', 'Minor', 'Patch')]
    [string]$Part = 'Patch',
    
    [Parameter()]
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# Find Directory.Build.props
$repoRoot = Split-Path $PSScriptRoot -Parent
$propsFile = Join-Path $repoRoot 'Directory.Build.props'

if (-not (Test-Path $propsFile)) {
    throw "Directory.Build.props not found at: $propsFile"
}

# Read current version
$content = Get-Content $propsFile -Raw
if ($content -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') {
    throw "Could not find <Version>x.y.z</Version> in Directory.Build.props"
}

$major = [int]$matches[1]
$minor = [int]$matches[2]
$patch = [int]$matches[3]
$oldVersion = "$major.$minor.$patch"

# Increment version
switch ($Part) {
    'Major' {
        $major++
        $minor = 0
        $patch = 0
    }
    'Minor' {
        $minor++
        $patch = 0
    }
    'Patch' {
        $patch++
    }
}

$newVersion = "$major.$minor.$patch"

Write-Host "Version bump: $oldVersion -> $newVersion ($Part)" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "DryRun mode - no changes made" -ForegroundColor Yellow
    exit 0
}

# Update file
$newContent = $content -replace "<Version>$oldVersion</Version>", "<Version>$newVersion</Version>"
Set-Content -Path $propsFile -Value $newContent -NoNewline

Write-Host "Updated: $propsFile" -ForegroundColor Green
Write-Host "New version: $newVersion" -ForegroundColor Green
