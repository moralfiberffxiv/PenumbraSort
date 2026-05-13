# PenumbraSort Build & Deploy Guide

This document explains the complete project structure and how everything works together.

## Project Files Overview

### Source Code (`src/`)
- **Plugin.cs** - Main Dalamud plugin entry point implementing `IDalamudPlugin`
- **PluginUI.cs** - ImGui user interface using `WindowSystem`
- **Configuration.cs** - Settings persistence using `IPluginConfiguration`
- **TagManager.cs** - Core tagging logic with auto-detection
- **PenumbraIpc.cs** - Communication with Penumbra via IPC
- **ModData.cs** - Data classes (`ModEntry`, `SortGroup`, `TagCategory`)

### Configuration Files
- **PenumbraSort.csproj** - C# project configuration with Dalamud references
- **PenumbraSort.json** - Individual plugin manifest (used by repo.json)
- **repo.json** - Plugin repository definition (published to GitHub raw)

### Build & Development
- **build.bat** - Windows build script
- **build.sh** - Linux/macOS build script
- **.vscode/settings.json** - VS Code configuration
- **.vscode/launch.json** - VS Code debugging setup
- **.github/workflows/build.yml** - GitHub Actions CI/CD
- **.gitignore** - Git ignore patterns

### Documentation
- **README.md** - User-facing documentation
- **DEVELOPMENT.md** - Developer guide (architecture, build, troubleshooting)
- **SETUP.md** - Complete environment setup (for first-time developers)
- **BUILD.md** - This file

### Metadata
- **LICENSE** - MIT License
- **CHANGELOG.md** - Version history

---

## Build Process

### Local Build (Windows)

```powershell
# Option 1: Use build script
./build.bat

# Option 2: Manual build
dotnet build -c Release

# Option 3: Clean rebuild
dotnet clean
dotnet restore
dotnet build -c Release
```

**Output**: `bin\x64\Release\net8.0-windows\PenumbraSort.dll`

### Local Build (macOS/Linux)

```bash
# Use build script
./build.sh

# Or manual build
dotnet build -c Release
```

### CI/CD Build (GitHub Actions)

The `.github/workflows/build.yml` workflow:
1. Runs on every push and tag
2. Sets up .NET 8 SDK
3. Restores NuGet packages
4. Builds in Release configuration
5. Creates GitHub Release with DLL artifact on tags

**To trigger automated release**:
```bash
git tag v1.0.1
git push origin v1.0.1
```

---

## Deployment

### For End Users

1. Add custom repository: `https://raw.githubusercontent.com/moralfiberffxiv/PenumbraSort/main/repo.json`
2. Install via `/xlplugins` search
3. Updates automatically via XIVLauncher

### For Developers

1. Build locally: `./build.bat`
2. Place DLL: `%APPDATA%\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\`
3. Reload: `/xlreload` in-game
4. Open: `/penumbrasort`

---

## Version Management

### Incrementing Version

Update version in multiple places:

**PenumbraSort.csproj**:
```xml
<Version>1.0.1</Version>
```

**PenumbraSort.json**:
```json
"AssemblyVersion": "1.0.1.0"
```

**repo.json**:
```json
"AssemblyVersion": "1.0.1.0",
"TestingAssemblyVersion": "1.0.1.0"
```

### Creating a Release

```bash
# Update version in files above
# Commit changes
git add .
git commit -m "bump: version to 1.0.1"

# Create and push tag
git tag v1.0.1
git push origin main
git push origin v1.0.1

# GitHub Actions will:
# 1. Build the plugin
# 2. Create GitHub Release
# 3. Attach DLL
# 4. Notify users (they auto-update)
```

---

## Dependency Management

### Dalamud References

All Dalamud libraries are loaded from XIVLauncher directory:
- `$(AppData)\XIVLauncher\addon\Hooks\dev\`

These are NOT included in the repo (they're user-installed). The `.csproj` file references them with `<Private>false</Private>` so they're not copied to output.

**Required files in Dalamud folder**:
- `Dalamud.dll` - Core plugin framework
- `ImGui.NET.dll` - ImGui bindings
- `ImGuiScene.dll` - ImGui rendering
- `Newtonsoft.Json.dll` - JSON serialization

### NuGet Packages

Currently, NO NuGet packages are used (all via direct DLL references). To add NuGet packages:

```bash
# Example: adding a package
dotnet add package SomePackage --version 1.0.0
```

---

## Troubleshooting Build Issues

### Error: "Dalamud.dll not found at path"

**Cause**: Dalamud not installed
**Fix**: Launch FFXIV through XIVLauncher once (installs Dalamud)

```powershell
# Verify path exists
dir "$env:APPDATA\XIVLauncher\addon\Hooks\dev\"
```

### Error: ".NET 8 SDK not found"

**Cause**: .NET 8 not installed
**Fix**: Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet --version  # Should show 8.0.x or higher
```

### Build fails but no obvious error

**Fix**: Clean and rebuild
```bash
dotnet clean
dotnet restore
dotnet build -c Release -v diag
```

The `-v diag` flag shows detailed diagnostic output.

---

## Code Architecture

### Plugin Lifecycle

1. **Load** (`Plugin()` constructor)
   - Loads configuration from `Configuration.cs`
   - Creates UI (`PluginUI`)
   - Registers command `/penumbrasort`
   - Hooks into ImGui draw pipeline

2. **Draw** (`PluginUI.Draw()` every frame)
   - Renders ImGui windows
   - Handles user input
   - Updates UI state

3. **Unload** (`Plugin.Dispose()`)
   - Saves configuration
   - Unregisters command
   - Cleans up UI

### Data Flow

```
Penumbra (IPC)
    ↓
PenumbraIpc.GetMods()
    ↓
List<ModEntry>
    ↓
TagManager.ApplyTags()  ← Auto-detection
    ↓
TagManager.GroupMods()  ← Sort by mode
    ↓
List<SortGroup>
    ↓
PluginUI.DrawModList()
    ↓
ImGui Rendering
```

### Tagging System

1. **Auto-Detection**: Keywords in mod names → tags
   - Example: "Autumn Harvest Skirt" → [Bottoms, Autumn, Casual]
   - Keywords defined in `TagManager.cs`

2. **Manual Override**: User can edit tags in tag editor
   - Stored in `Configuration.ModTags` dictionary

3. **Persistence**: Tags saved to config file
   - `/loot/saves/config.json` (XIVLauncher folder)

---

## Future Improvements

Potential enhancements:
- [ ] Support for grouping mods in Penumbra folders
- [ ] Mod preview images
- [ ] Bulk tag operations
- [ ] Custom tag creation UI
- [ ] Import/export tag profiles
- [ ] Conflict detection between mods

---

## Contributing

1. Fork the repository
2. Create feature branch: `git checkout -b feature/awesome`
3. Make changes
4. Test locally
5. Push and create Pull Request
6. Maintainer reviews and merges

### Code Style

- Use `// ──` comments for major sections
- Use meaningful variable names
- Keep methods under 50 lines
- Use string interpolation over concatenation
- Document public APIs

---

## Support

- **Issues**: GitHub Issues tab
- **Discussions**: GitHub Discussions tab
- **Discord**: FFXIV Modding Discord community
- **Documentation**: See [DEVELOPMENT.md](DEVELOPMENT.md) and [SETUP.md](SETUP.md)

---

## License

MIT License - Free for personal use, modification, and redistribution.
See [LICENSE](LICENSE) file for full text.
