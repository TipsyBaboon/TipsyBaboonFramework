# TipsyBaboon Dual-Remote Git Strategy

This repository uses a dual-remote strategy to separate public framework code from private test applications.

## Repository Structure

### Azure DevOps (origin) - EVERYTHING
Tracks all code including:
- Framework projects (Core, SqlServer, UI)
- Test sites with real configuration
- Private tools and scripts
- Development/staging configuration files

### GitHub (github) - PUBLIC FRAMEWORK ONLY
Tracks only:
- TipsyBaboon.Core
- TipsyBaboon.SqlServer
- TipsyBaboon.UI
- tools/ (excluding private scripts)
- Documentation

## Workflow

### Day-to-Day Development
Work normally - commit and push to Azure DevOps as usual:
```powershell
git add .
git commit -m "Your changes"
git push origin master
```

The `.gitignore` file defaults to `.gitignore.devops` which allows test sites locally.

### Publishing Framework Updates to GitHub
When ready to publish framework changes to GitHub:

```powershell
# Setup GitHub remote (first time only)
.\tools\sync-to-github.ps1 -SetupRemote -GitHubUrl https://github.com/youruser/TipsyBaboon.git

# Preview what will be pushed
.\tools\sync-to-github.ps1 -DryRun

# Actually push to GitHub
.\tools\sync-to-github.ps1
```

**What the sync script does:**
1. Swaps `.gitignore.github` → `.gitignore` (strict exclusions)
2. Commits and pushes to GitHub
3. Swaps `.gitignore.devops` → `.gitignore` (restores for local work)
4. Commits restoration and syncs to Azure DevOps

This ensures GitHub gets strict filtering while local development continues normally.

## Dual .gitignore Strategy

Three `.gitignore` files manage filtering:

- **`.gitignore`** - Active ignore file (automatically managed by sync script)
- **`.gitignore.devops`** - Azure DevOps version (relaxed, for local development)
- **`.gitignore.github`** - GitHub version (strict, excludes all private code)

**Manual editing:** Edit `.gitignore.devops` for local changes, or `.gitignore.github` for GitHub exclusions.

## Protected Files

The following are excluded from GitHub (via .gitignore):
- `client_secret_*.json` - OAuth credentials
- `**/appsettings.Development.json` - Local dev config
- `TipsyBaboonTestSite/` - Private test site with real config
- `TipsyBaboonTemplate/` - Template (distributed separately via .zip)
- `TipsyBaboonFishing/` - Private application (being moved to separate repo)
- `ai-test/`, `agents/`, `fishing-log-project/` - Private folders

## Security Notes

1. **Never commit real secrets to any branch** that might be pushed to GitHub
2. Template uses placeholder credentials (`YOUR-GOOGLE-CLIENT-ID`, etc.)
3. Test sites keep real credentials locally (ignored by git)
4. The sync script checks for staged private files before pushing

## Initial Setup Checklist

- [x] Clean template of real OAuth secrets
- [x] Update .gitignore to exclude sensitive files
- [x] Remove client_secret_*.json from git tracking
- [ ] Create GitHub repository
- [ ] Run sync-to-github.ps1 -SetupRemote
- [ ] Verify GitHub only shows framework code

## Maintenance

- Continue pushing ALL changes to Azure DevOps
- Periodically sync framework updates to GitHub
- Keep test sites and private config out of commits destined for GitHub
- If you accidentally commit secrets, rotate them immediately
