---
applyTo: "**/*"
---

# NuGet Package Publishing Workflow

Agent-managed versioning and publishing for TipsyBaboon framework packages.

## Quick Reference

**Current Version:** Check [Directory.Build.props](../../Directory.Build.props) `<Version>` element

**Packages:**
- TipsyBaboon.Core
- TipsyBaboon.SqlServer  
- TipsyBaboon.UI

**Published to:** nuget.org

## Publishing Workflow

### 1. Determine Version Bump

Ask user or infer from changes:
- **Patch** (1.0.x): Bug fixes, non-breaking changes
- **Minor** (1.x.0): New features, backwards-compatible
- **Major** (x.0.0): Breaking changes

### 2. Bump Version

```powershell
# From repo root
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bump-version.ps1 -Part Patch
# Or: -Part Minor, -Part Major
```

### 3. Commit Version Bump

```powershell
git add Directory.Build.props
git commit -m "Bump version to 1.0.x"
```

### 4. Build, Pack, and Publish

```powershell
# Requires $env:NUGET_API_KEY set
powershell -NoProfile -ExecutionPolicy Bypass -File tools/publish-packages.ps1
```

**Options:**
- `-PackOnly` - Create .nupkg files without publishing (for testing)
- `-SkipBuild` - Skip build step if already built
- `-ApiKey "key"` - Override environment variable

### 5. Tag and Push

```powershell
$version = "1.0.x"  # From Directory.Build.props
git tag -a "v$version" -m "Release v$version"
git push origin master
git push origin "v$version"
```

## Agent Instructions

When user requests package publishing:

1. **Confirm version bump type** (if not specified by user)
2. **Run bump-version.ps1** with appropriate -Part
3. **Commit version change** with descriptive message
4. **Run publish-packages.ps1** (requires NUGET_API_KEY env var)
5. **Create git tag** for release
6. **Push to remote** (both commit and tag)

## Environment Setup

User must set before publishing:
```powershell
$env:NUGET_API_KEY = "oy2_your_api_key_here"
```

Alternatively, store in [.secrets.json](../../.secrets.json) (gitignored) and load:
```powershell
$secrets = Get-Content .secrets.json | ConvertFrom-Json
$env:NUGET_API_KEY = $secrets.NuGet.NuGetOrg.ApiKey
```

## Troubleshooting

**"Version already exists"**: NuGet doesn't allow re-publishing same version. Bump version and retry.

**"Build failed"**: Fix errors before packing. Can use `-SkipBuild` if already built successfully.

**"API key invalid"**: Verify `$env:NUGET_API_KEY` is set and valid (check nuget.org account).

## Manual Alternative

```powershell
# Build all
dotnet build -c Release

# Pack to artifacts/
dotnet pack TipsyBaboon.Core/TipsyBaboon.Core.csproj -c Release -o artifacts
dotnet pack TipsyBaboon.SqlServer/TipsyBaboon.SqlServer.csproj -c Release -o artifacts
dotnet pack TipsyBaboon.UI/TipsyBaboon.UI.csproj -c Release -o artifacts

# Push each package
dotnet nuget push artifacts/TipsyBaboon.Core.1.0.0.nupkg --source nuget.org --api-key $env:NUGET_API_KEY
dotnet nuget push artifacts/TipsyBaboon.SqlServer.1.0.0.nupkg --source nuget.org --api-key $env:NUGET_API_KEY
dotnet nuget push artifacts/TipsyBaboon.UI.1.0.0.nupkg --source nuget.org --api-key $env:NUGET_API_KEY
```
