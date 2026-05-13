# ✦ PenumbraSort - Setup Complete! 

Your Dalamud plugin for FFXIV is now ready for development, building, and deployment.

## 🎯 What Was Set Up

Your PenumbraSort project now includes:

### ✅ Build System
- **GitHub Actions CI/CD** - Automated builds on every push and tag
- **Build Scripts** - `build.bat` (Windows) and `build.sh` (Unix)
- **Project Configuration** - Proper .csproj with metadata

### ✅ Developer Tools
- **VS Code Integration** - Ready for C# development with debugging
- **git Configuration** - .gitignore for build artifacts
- **Launch Configuration** - Debug directly into running FFXIV

### ✅ Documentation
- **SETUP.md** - Complete guide to set up your Dalamud dev environment
- **DEVELOPMENT.md** - Usage guide and architecture overview
- **BUILD.md** - Build process and release management
- **RELEASE_CHECKLIST.md** - Pre-release checklist for deployments

### ✅ Code Quality
- All 6 source files verified ✓
- No compilation errors ✓
- Proper Dalamud plugin structure ✓
- IPC communication ready ✓

---

## 🚀 Quick Start

### For Playing the Plugin

**As an End User:**
1. Open XIVLauncher → `/xlplugins`
2. Settings → Custom Plugin Repositories
3. Add: `https://raw.githubusercontent.com/moralfiberffxiv/PenumbraSort/main/repo.json`
4. Search "PenumbraSort" → Install
5. Type `/penumbrasort` in-game

**Updates Install Automatically!**

### For Developing the Plugin

**First Time Setup (30 minutes):**
1. Read [SETUP.md](SETUP.md) for complete Dalamud environment setup
2. Install .NET 8 SDK and XIVLauncher with Dalamud
3. Clone this repository
4. Run `./build.bat` (Windows) or `./build.sh` (Unix)

**Local Testing:**
```powershell
# Build
./build.bat

# Install to Dalamud
copy bin\x64\Release\net8.0-windows\PenumbraSort.dll `
     $env:APPDATA\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\

# In-game: /xlreload
```

---

## 📁 Project Structure At a Glance

```
PenumbraSort/
├── src/                          # Source code
│   ├── Plugin.cs                # Main Dalamud plugin
│   ├── PluginUI.cs              # ImGui interface
│   ├── Configuration.cs         # Settings persistence
│   ├── TagManager.cs            # Tag logic
│   ├── PenumbraIpc.cs           # Penumbra communication
│   └── ModData.cs               # Data models
│
├── .vscode/                      # VS Code configuration
├── .github/workflows/            # GitHub Actions (auto-build)
├── build.bat / build.sh         # Build scripts
│
├── SETUP.md                     # Environment setup guide
├── DEVELOPMENT.md               # Developer documentation
├── BUILD.md                     # Build system details
├── RELEASE_CHECKLIST.md         # Release procedures
│
├── PenumbraSort.csproj          # C# project file
├── PenumbraSort.json            # Plugin manifest
└── repo.json                    # Plugin repository
```

---

## 📋 Key Features of This Setup

### ✅ Continuous Integration
GitHub Actions automatically:
- Builds on every push
- Creates releases on version tags
- Attaches built DLL to releases
- Users auto-update via XIVLauncher

**To release a new version:**
```bash
# Update version numbers in 3 files (see BUILD.md)
git tag v1.0.1
git push origin v1.0.1
# → GitHub Actions handles the rest!
```

### ✅ Developer Friendly
- ImGui UI already implemented
- Penumbra IPC integration ready
- Auto-detection of tags from mod names
- Graceful fallback if Penumbra offline
- Demo mods for testing without Penumbra

### ✅ Production Ready
- DalamudApiLevel 10 - compatible with current Dalamud
- Proper error handling and IPC fallbacks
- Persistent configuration storage
- Clean shutdown and resource cleanup

---

## 🔍 File Overview

### Documentation
| File | Purpose |
|------|---------|
| **SETUP.md** | Complete Dalamud environment setup (🎯 Start here!) |
| **DEVELOPMENT.md** | Architecture, building, troubleshooting |
| **BUILD.md** | Build pipeline, CI/CD, versioning |
| **RELEASE_CHECKLIST.md** | Deployment checklist |

### Build & Config
| File | Purpose |
|------|---------|
| **.github/workflows/build.yml** | Automated GitHub Actions build |
| **build.bat / build.sh** | Manual build scripts |
| **PenumbraSort.csproj** | C# project configuration |
| **PenumbraSort.json** | Plugin metadata |
| **repo.json** | Plugin repository definition |

### Source Code
| File | Purpose |
|------|---------|
| **Plugin.cs** | Dalamud plugin lifecycle |
| **PluginUI.cs** | ImGui window rendering |
| **Configuration.cs** | Settings persistence |
| **TagManager.cs** | Tag detection and grouping |
| **PenumbraIpc.cs** | Penumbra mod communication |
| **ModData.cs** | Data structures |

---

## ⚡ Next Steps

### 1. **Set Up Dev Environment** (First Time Only)
   → Read [SETUP.md](SETUP.md) - Takes 30 minutes

### 2. **Build Locally**
   ```bash
   ./build.bat          # Windows
   ./build.sh           # macOS/Linux
   ```

### 3. **Test the Plugin**
   - Copy DLL to `%APPDATA%\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\`
   - Launch FFXIV
   - Type `/penumbrasort` in-game

### 4. **Make Changes**
   - Edit source files in `src/`
   - Rebuild with `./build.bat`
   - Reload with `/xlreload` in-game

### 5. **Release to Users**
   - Update version numbers (see [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md))
   - Push tag: `git tag v1.0.1 && git push origin v1.0.1`
   - GitHub Actions builds and publishes automatically

---

## 🐛 Troubleshooting

### "Build fails - Dalamud.dll not found"
→ Launch FFXIV via XIVLauncher once (installs Dalamud)

### "Plugin doesn't appear in /xlplugins"
→ Verify DLL copied to correct folder and try `/xlreload`

### "Import Penumbra not working"
→ Plugin works offline too - tags save locally

**For more help:**
- Check [DEVELOPMENT.md](DEVELOPMENT.md#troubleshooting)
- Review [SETUP.md](SETUP.md#troubleshooting)
- Check `/xllog` output for errors

---

## 💡 Architecture Overview

```
EVENT LOOP                          DATA FLOW
════════════════════════════════════════════════════════════
Every Frame:
  PluginUI.Draw()
    ↓
  ImGui Rendering
    ↓
  User Interaction
    ↓
  TagManager.SaveTags()
    ↓
  Configuration.Save()
    ↓
  (Repeat)

When user clicks "Apply Sort":
  TagManager.GroupMods()
    ↓
  PenumbraIpc.ApplySortedOrder()
    ↓
  Penumbra Updates
```

---

## 📦 Distribution

Users install via:
1. **Custom Repository URL:**
   ```
   https://raw.githubusercontent.com/moralfiberffxiv/PenumbraSort/main/repo.json
   ```

2. **In XIVLauncher:**
   - `/xlplugins` → Settings → Custom Repos
   - Paste URL above
   - Search "PenumbraSort"
   - Click Install

3. **Automatic Updates:**
   - XIVLauncher checks repo.json periodically
   - New releases auto-download and install

---

## ✨ Key Accomplishments

✅ Complete Dalamud plugin structure  
✅ Production-ready build system  
✅ GitHub Actions CI/CD pipeline  
✅ Comprehensive documentation  
✅ Developer and user guides  
✅ Release management setup  
✅ Debug configuration for VS Code  
✅ All code verified and error-free  

---

## 🎮 Features Summary

👗 **Clothing Types** - 10+ categories (tops, bottoms, dresses, etc)
🌸 **Seasons** - Spring, Summer, Autumn, Winter, All-Season
🎉 **Occasions** - Casual, Formal, Combat, Festival, Evening, Beach, Fantasy, Wedding
🏷 **Auto-Detection** - Tags detected from mod names
⭐ **Custom Tags** - Add your own categories
🔍 **Search** - Instant filtering
💾 **Persistent** - Tags save automatically
🔌 **Penumbra IPC** - Direct mod communication

---

## 📝 License

MIT License - Free to use, modify, and distribute

---

**Everything is ready! Start with [SETUP.md](SETUP.md) for your Dalamud dev environment, then check out [DEVELOPMENT.md](DEVELOPMENT.md) for more details.**

Made with 💜 for the FFXIV community
