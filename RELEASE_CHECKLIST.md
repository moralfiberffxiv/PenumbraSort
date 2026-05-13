# PenumbraSort - Release Checklist

Use this checklist before publishing a new version.

## Pre-Release

### Code Quality
- [ ] Run `dotnet build` successfully in Release mode
- [ ] No warnings or errors reported
- [ ] All source files compile without issues
- [ ] Code follows C# naming conventions
- [ ] No commented-out code or debug statements
- [ ] Solution compiles on fresh machine

### Testing
- [ ] Plugin loads in `/xlplugins`
- [ ] `/penumbrasort` command works
- [ ] UI renders without glitches
- [ ] Can search and filter mods
- [ ] Can tag and save mods
- [ ] Tags persist after game restart
- [ ] Auto-detection works for mod names
- [ ] Works with Penumbra installed (IPC)
- [ ] Gracefully handles Penumbra offline
- [ ] No exceptions in `/xllog`

### Documentation
- [ ] README.md is up-to-date
- [ ] DEVELOPMENT.md covers architecture
- [ ] SETUP.md has clear instructions
- [ ] BUILD.md explains the build process
- [ ] All code comments are accurate
- [ ] No placeholder text remains

### Configuration Files
- [ ] PenumbraSort.csproj has correct version
- [ ] PenumbraSort.json has correct version and metadata
- [ ] repo.json points to correct files
- [ ] GitHub Actions workflow is valid
- [ ] All references use `moralfiberffxiv` account

## Release Steps

### 1. Update Version Numbers

**File: PenumbraSort.csproj**
```xml
<Version>1.0.1</Version>  <!-- Update this -->
```

**File: PenumbraSort.json**
```json
"AssemblyVersion": "1.0.1.0",  <!-- Update this -->
```

**File: repo.json**
```json
"AssemblyVersion": "1.0.1.0",  <!-- Update this -->
"TestingAssemblyVersion": "1.0.1.0"  <!-- Update this -->
```

### 2. Update Changelog

**File: CHANGELOG.md**
```markdown
## [1.0.1] - 2024-05-13
- Fixed mod loading issue
- Improved tag detection
- Better error handling
```

### 3. Commit Changes

```bash
git add PenumbraSort.csproj PenumbraSort.json repo.json CHANGELOG.md
git commit -m "release: version 1.0.1"
git push origin main
```

### 4. Create Git Tag

```bash
git tag v1.0.1
git push origin v1.0.1
```

### 5. Monitor GitHub Actions

- Go to Actions tab
- Watch the build workflow run
- Verify it completes successfully
- Check that Release was created

### 6. Verify Release

- [ ] PenumbraSort.dll is attached to release
- [ ] Release notes are visible
- [ ] Release is NOT marked as "pre-release"
- [ ] Plugin appears in repo within 5 minutes

### 7. Test Installation

- [ ] Repo URL works: `curl https://raw.githubusercontent.com/moralfiberffxiv/PenumbraSort/main/repo.json | jq`
- [ ] Add custom repo in `/xlplugins`
- [ ] Search for and install plugin
- [ ] Plugin loads and works correctly
- [ ] Can uninstall and reinstall

## Post-Release

### Announce Update
- [ ] Update status on GitHub Discussions
- [ ] Announce in FFXIV modding communities if appropriate
- [ ] Reply to any issues that were fixed

### Monitor Feedback
- [ ] Check Issues for bug reports
- [ ] Monitor logs for any problems
- [ ] Address critical issues promptly

### Archive Old Releases
- [ ] Keep last 3 releases publicly available
- [ ] Old releases remain on GitHub (don't delete)

## Emergency Hotfix

If critical bug found after release:

```bash
# Fix the bug
# Test thoroughly

# Version bump: 1.0.1 → 1.0.2
git add .
git commit -m "fix: critical issue with X"
git tag v1.0.2
git push origin main v1.0.2

# Users auto-update within minutes
```

## Troubleshooting Release Issues

### Release creation fails
1. Verify GitHub Actions secrets are configured
2. Check workflow file syntax
3. Ensure `GITHUB_TOKEN` has write access

### Plugin doesn't appear in repo
1. Verify repo.json is valid JSON
2. Check file is at correct GitHub URL
3. Clear XIVLauncher cache: `%APPDATA%\XIVLauncher\`
4. Wait 5+ minutes for cache refresh

### Users can't update
1. Check version number is higher than previous
2. Verify DLL file is attached to release
3. Ensure release is not marked "pre-release"

## Maintenance Schedule

- **Weekly**: Monitor Issues and Discussions
- **Monthly**: Plan next features or improvements
- **As-needed**: Hotfixes for critical bugs
- **Quarterly**: Major version updates with features

---

For detailed information, see [BUILD.md](BUILD.md) and [DEVELOPMENT.md](DEVELOPMENT.md).
