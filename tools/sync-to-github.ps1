# Sync TipsyBaboon Framework to GitHub
# This script helps maintain dual remotes: Azure DevOps (everything) + GitHub (framework only)

param(
    [switch]$DryRun,
    [switch]$SetupRemote,
    [string]$GitHubUrl = ""
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

Write-Host "TipsyBaboon GitHub Sync Tool" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""

# Setup GitHub remote if requested
if ($SetupRemote) {
    if ([string]::IsNullOrWhiteSpace($GitHubUrl)) {
        Write-Host "ERROR: -GitHubUrl parameter required when using -SetupRemote" -ForegroundColor Red
        Write-Host "Example: .\sync-to-github.ps1 -SetupRemote -GitHubUrl https://github.com/youruser/TipsyBaboon.git"
        exit 1
    }
    
    Write-Host "Setting up GitHub remote..." -ForegroundColor Yellow
    
    # Check if github remote already exists
    $existingRemote = git remote get-url github 2>$null
    if ($existingRemote) {
        Write-Host "GitHub remote already exists: $existingRemote" -ForegroundColor Yellow
        $replace = Read-Host "Replace with $GitHubUrl? (Y/N)"
        if ($replace -eq "Y" -or $replace -eq "y") {
            git remote remove github
            git remote add github $GitHubUrl
            Write-Host "GitHub remote updated!" -ForegroundColor Green
        }
    } else {
        git remote add github $GitHubUrl
        Write-Host "GitHub remote added!" -ForegroundColor Green
    }
    Write-Host ""
}

# Verify remotes
Write-Host "Current remotes:" -ForegroundColor Cyan
git remote -v
Write-Host ""

# Check for github remote
$githubRemote = git remote get-url github 2>$null
if (-not $githubRemote) {
    Write-Host "ERROR: No 'github' remote configured." -ForegroundColor Red
    Write-Host "Run with -SetupRemote to configure:" -ForegroundColor Yellow
    Write-Host "  .\sync-to-github.ps1 -SetupRemote -GitHubUrl https://github.com/youruser/TipsyBaboon.git"
    exit 1
}

# Verify we're on a clean working tree (or at least aware of changes)
$status = git status --porcelain
if ($status) {
    Write-Host "WARNING: You have uncommitted changes:" -ForegroundColor Yellow
    Write-Host ($status | Out-String)
    Write-Host ""
    $continue = Read-Host "Continue anyway? (Y/N)"
    if ($continue -ne "Y" -and $continue -ne "y") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "Preparing to push framework code to GitHub..." -ForegroundColor Cyan
Write-Host ""
Write-Host "What will be pushed:" -ForegroundColor Green
Write-Host "  ✓ TipsyBaboon.Core/" -ForegroundColor Gray
Write-Host "  ✓ TipsyBaboon.SqlServer/" -ForegroundColor Gray
Write-Host "  ✓ TipsyBaboon.UI/" -ForegroundColor Gray
Write-Host "  ✓ tools/ (public scripts only)" -ForegroundColor Gray
Write-Host "  ✓ .github/" -ForegroundColor Gray
Write-Host "  ✓ Root files (README, .gitignore, sln)" -ForegroundColor Gray
Write-Host ""
Write-Host "What will be excluded:" -ForegroundColor Red
Write-Host "  ✗ TipsyBaboonTestSite/" -ForegroundColor Gray
Write-Host "  ✗ TipsyBaboonTemplate/" -ForegroundColor Gray
Write-Host "  ✗ TipsyBaboonFishing/" -ForegroundColor Gray
Write-Host "  ✗ ai-test/, fishing-log-project/, agents/" -ForegroundColor Gray
Write-Host "  ✗ appsettings.Development.json files" -ForegroundColor Gray
Write-Host "  ✗ client_secret_*.json" -ForegroundColor Gray
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Files that would be pushed:" -ForegroundColor Cyan
    
    # Show what files would be included
    $files = git ls-files | Where-Object {
        $_ -match '^TipsyBaboon\.(Core|SqlServer|UI)/' -or
        $_ -match '^tools/' -or
        $_ -match '^\.github/' -or
        $_ -match '^[^/]+\.(sln|md|gitignore)$'
    } | Where-Object {
        $_ -notmatch 'appsettings\.Development\.json' -and
        $_ -notmatch 'client_secret_' -and
        $_ -notmatch '^tools/(reset-database|seed-)'
    }
    
    Write-Host "$($files.Count) files would be pushed" -ForegroundColor White
    $files | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    
    Write-Host ""
    Write-Host "To actually push, run without -DryRun flag" -ForegroundColor Yellow
    exit 0
}

# Confirm before pushing
Write-Host "Ready to push to GitHub." -ForegroundColor Yellow
$confirm = Read-Host "Continue? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan

# Strategy: Swap .gitignore to GitHub version, commit, push, then restore DevOps version
# This ensures GitHub gets strict exclusions while local work continues normally

# Verify .gitignore files exist
if (-not (Test-Path ".gitignore.github")) {
    Write-Host "ERROR: .gitignore.github not found" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path ".gitignore.devops")) {
    Write-Host "ERROR: .gitignore.devops not found" -ForegroundColor Red
    exit 1
}

# For safety, verify no private files are staged
$staged = git diff --cached --name-only
$privateStaged = $staged | Where-Object {
    $_ -match 'TipsyBaboonTestSite/' -or
    $_ -match 'TipsyBaboonTemplate/' -or
    $_ -match 'TipsyBaboonFishing/' -or
    $_ -match 'appsettings\.Development\.json' -or
    $_ -match 'client_secret_'
}

if ($privateStaged) {
    Write-Host "ERROR: Private files are staged for commit:" -ForegroundColor Red
    $privateStaged | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Unstage these files before pushing to GitHub" -ForegroundColor Yellow
    exit 1
}

# Step 1: Swap to GitHub .gitignore
Write-Host "Swapping to GitHub .gitignore..." -ForegroundColor Yellow
Copy-Item ".gitignore.github" ".gitignore" -Force

# Step 2: Commit the change if .gitignore was modified
$gitignoreChanged = git diff --name-only .gitignore
if ($gitignoreChanged) {
    Write-Host "Committing GitHub .gitignore..." -ForegroundColor Yellow
    git add .gitignore
    git commit -m "Switch to GitHub .gitignore for public sync"
}

# Step 3: Push to GitHub
try {
    Write-Host "Pushing to GitHub main branch..." -ForegroundColor Yellow
    git push github HEAD:main --force
    Write-Host ""
    Write-Host "Successfully pushed to GitHub!" -ForegroundColor Green
    Write-Host "Framework code is now available at: $githubRemote" -ForegroundColor White
} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to push to GitHub" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    # Restore DevOps .gitignore even on failure
    Write-Host "Restoring DevOps .gitignore..." -ForegroundColor Yellow
    Copy-Item ".gitignore.devops" ".gitignore" -Force
    
    exit 1
}

# Step 4: Restore DevOps .gitignore for local work
Write-Host "Restoring DevOps .gitignore for local development..." -ForegroundColor Yellow
Copy-Item ".gitignore.devops" ".gitignore" -Force

# Step 5: Commit the restoration and push to DevOps
$gitignoreChanged = git diff --name-only .gitignore
if ($gitignoreChanged) {
    Write-Host "Committing DevOps .gitignore..." -ForegroundColor Yellow
    git add .gitignore
    git commit -m "Restore DevOps .gitignore for local development"
    
    Write-Host "Syncing to Azure DevOps..." -ForegroundColor Yellow
    git push origin master
}

Write-Host ""
Write-Host "Note: Azure DevOps repo still contains ALL code including test sites." -ForegroundColor Cyan
Write-Host "This is the recommended setup for your development workflow." -ForegroundColor Cyan
