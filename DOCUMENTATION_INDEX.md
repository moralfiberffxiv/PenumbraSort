# 📚 PenumbraSort Documentation Index

Complete guide to the PenumbraSort project - everything is here!

## 🎯 Start Here

### 👤 **For End Users**
- **Install:** Add custom repo `https://raw.githubusercontent.com/moralfiberffxiv/PenumbraSort/main/repo.json` in XIVLauncher
- **Use:** Type `/penumbrasort` in-game
- **Updates:** Automatic via XIVLauncher
- **Docs:** See [README.md](README.md)

### 👨‍💻 **For Developers**
1. **New to Dalamud?** → [SETUP.md](SETUP.md) (complete environment guide)
2. **Want to build locally?** → [QUICKSTART.md](QUICKSTART.md)
3. **Understanding the code?** → [DEVELOPMENT.md](DEVELOPMENT.md)
4. **How to release?** → [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)
5. **Build pipeline details?** → [BUILD.md](BUILD.md)

---

## 📖 Documentation Files

### Quick Reference
| Document | Purpose | Read Time |
|----------|---------|-----------|
| [QUICKSTART.md](QUICKSTART.md) | Overview of everything set up | 5 min |
| [README.md](README.md) | User features and installation | 3 min |
| [SETUP.md](SETUP.md) | Dev environment setup (step-by-step) | 20 min |

### Detailed Guides
| Document | Purpose | Read Time |
|----------|---------|-----------|
| [DEVELOPMENT.md](DEVELOPMENT.md) | Architecture, building, testing | 15 min |
| [BUILD.md](BUILD.md) | Build system, CI/CD, versioning | 10 min |
| [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) | Pre-release checklist & procedures | 5 min |

---

## 🏗️ Project Structure

```
PenumbraSort/
│
├── 📚 Documentation
│   ├── README.md                    ← User guide
│   ├── QUICKSTART.md                ← Overview
│   ├── SETUP.md                     ← Dev environment
│   ├── DEVELOPMENT.md               ← Developer guide
│   ├── BUILD.md                     ← Build details
│   ├── RELEASE_CHECKLIST.md         ← Release guide
│   └── DOCUMENTATION_INDEX.md       ← This file
│
├── 💻 Source Code
│   └── src/
│       ├── Plugin.cs                ← Main entry point
│       ├── PluginUI.cs              ← ImGui interface
│       ├── Configuration.cs         ← Settings
│       ├── TagManager.cs            ← Tag logic
│       ├── PenumbraIpc.cs           ← Mod communication
│       └── ModData.cs               ← Data structures
│
├── ⚙️ Build & Config
│   ├── PenumbraSort.csproj          ← C# project file
│   ├── PenumbraSort.json            ← Plugin manifest (individual)
│   ├── repo.json                    ← Plugin repository (published)
│   ├── build.bat                    ← Windows build script
│   └── build.sh                     ← Unix build script
│
├── 🔄 CI/CD
│   ├── .github/workflows/build.yml  ← GitHub Actions automation
│   ├── .gitignore                   ← Git ignore rules
│   └── .vscode/                     ← VS Code configuration
│       ├── settings.json            ← C# setup
│       └── launch.json              ← Debug setup
│
├── 📄 Metadata
│   ├── LICENSE                      ← MIT License
│   └── CHANGELOG.md                 ← Version history
│
└── 🎨 Assets
    └── assets/                      ← Plugin icon/screenshots

Total: 6 source files, 8 documentation files, full CI/CD setup
```

---

## 🚀 Quick Commands

### Building
```bash
# Windows
./build.bat              # Build Release configuration

# macOS/Linux
./build.sh               # Build Release configuration

# Manual (any OS)
dotnet build -c Release  # Explicit build command
```

### Running
```bash
# In-game
/penumbrasort            # Open plugin UI
/xlplugins               # Manage plugins
/xlreload                # Reload plugins
/xllog                   # View plugin logs
```

### Git/Release
```bash
git tag v1.0.1           # Create release tag
git push origin v1.0.1   # Push to trigger GitHub Actions
```

---

## 🔍 Feature Breakdown

### UI Components
- **Top Bar** - Search, sort mode selector, direction toggle
- **Mod List** - Grouped mods with inline tag chips
- **Tag Editor** - Right panel for editing individual mod tags
- **Menu Bar** - Settings, refresh, save, apply sort
- **Status** - Connection status, unsaved changes indicator

### Backend
- **TagManager** - Auto-detection from names, grouping, sorting
- **PenumbraIpc** - IPC communication, disk fallback
- **Configuration** - Persistent storage of tags and settings
- **ModData** - Data structures for mods and groups

### Default Tags
- **Clothing Types** (10): Tops, Bottoms, Dresses, Outerwear, Footwear, Accessories, Armor, Headwear, Costumes, Underwear
- **Seasons** (5): Spring, Summer, Autumn, Winter, All-Season
- **Occasions** (8): Casual, Formal, Combat, Festival, Evening, Beach, Fantasy, Wedding
- **Custom** - User-defined tags per mod

---

## 📋 Build Pipeline

```
Developer Action              GitHub Actions               User
═══════════════════════════════════════════════════════════════════════
1. Push code                  2. Detect push
   │                             │
   └─ git push origin main ──────→ Checkout code
                                    │
                                    ├─ Setup .NET 8
                                    │
                                    ├─ Restore packages
                                    │
                                    ├─ Build Release
                                    │
                                    └─ ✅ Success
                                       
3. Create tag                 4. Detect tag
   │                             │
   └─ git tag v1.0.1 ───────────→ Build plugin
      git push v1.0.1            │
                                  ├─ Create Release
                                  │
                                  ├─ Attach DLL
                                  │
                                  └─ ✅ Published
                                     │
                                     └──────→ 5. Update available
                                              /xlplugins search
                                              Auto-installs
```

---

## 🔧 Development Workflow

### Local Setup (First Time)
```bash
# 1. Install prerequisites (see SETUP.md)
#    - Windows OS
#    - FFXIV + XIVLauncher
#    - .NET 8 SDK
#    - Git
#    - VS Code optional

# 2. Clone repo
git clone https://github.com/moralfiberffxiv/PenumbraSort.git
cd PenumbraSort

# 3. Build
./build.bat

# 4. Install to Dalamud
copy "bin\x64\Release\net8.0-windows\PenumbraSort.dll" ^
     "%APPDATA%\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\"

# 5. Test in game
# FFXIV → /xlreload → /penumbrasort
```

### Development Loop
```
Edit Code
    ↓
./build.bat
    ↓
/xlreload (in-game)
    ↓
Test changes
    ↓
Repeat
```

### Release Workflow
```
Update Version (3 files)
    ↓
Update CHANGELOG.md
    ↓
git commit
    ↓
git tag v1.x.x
    ↓
git push origin --tags
    ↓
GitHub Actions builds automatically
    ↓
Users receive update
```

---

## 🎓 Learning Path

### If You're New to Dalamud
1. Read [SETUP.md](SETUP.md) - Understand environment
2. Follow Build section - Get first plugin running
3. Read [DEVELOPMENT.md](DEVELOPMENT.md) - Understand architecture
4. Modify simple code (colors, UI text) to learn
5. Add new feature - tag category, sort mode, etc.

### If You Know C# But Not Dalamud
1. Skim [SETUP.md](SETUP.md) - Environment only
2. Read [DEVELOPMENT.md](DEVELOPMENT.md) - Dalamud specifics
3. Review [src/Plugin.cs](src/Plugin.cs) - Plugin lifecycle
4. Review [src/PluginUI.cs](src/PluginUI.cs#L50-L100) - ImGui usage
5. Implement your features

### If You Know Dalamud
1. [QUICKSTART.md](QUICKSTART.md) - Overview
2. [READ SOURCE CODE](src) directly
3. Build and test locally
4. Submit PRs!

---

## ❓ FAQ

### Q: How do users install this?
**A:** They add the custom repository in XIVLauncher and search for "PenumbraSort"

### Q: How often are updates pushed?
**A:** As needed - create a tag and GitHub Actions handles automated build/release

### Q: Can I modify this for my own use?
**A:** Yes! MIT License allows modifications. Just change the author name and repo URLs

### Q: Where do tags get saved?
**A:** In XIVLauncher config folder: `%APPDATA%\XIVLauncher\`

### Q: What if Penumbra isn't installed?
**A:** It gracefully falls back to disk scanning and local tag storage only

### Q: Can I customize tag categories?
**A:** Not yet (would need UI addition), but custom tags per-mod work
   
### Q: Does this work on Mac/Linux?
**A:** FFXIV only runs on Windows, but you can build the code on Mac/Linux for testing

---

## 🤝 Contributing

### Found a Bug?
1. Check [Issues](https://github.com/moralfiberffxiv/PenumbraSort/issues)
2. Create new issue with:
   - Steps to reproduce
   - Expected vs actual behavior
   - Screenshots if applicable

### Want to Add a Feature?
1. Create ["Feature Request" issue](https://github.com/moralfiberffxiv/PenumbraSort/issues)
2. Discuss approach
3. Create fork and feature branch
4. Implement feature (see [DEVELOPMENT.md](DEVELOPMENT.md))
5. Test thoroughly
6. Submit Pull Request

### Code Style Guidelines
- Use `// ──────` for section separators
- Keep methods under 50 lines
- Meaningful variable names
- Document public methods
- Use `$"string {interpolation}"` over concatenation

---

## 📞 Support

- **Documentation** - Check files above
- **GitHub Issues** - Report problems or request features
- **GitHub Discussions** - Ask questions or suggest ideas
- **Discord Communities** - FFXIV modding communities

---

## 🎯 Project Status

- ✅ Full source code complete
- ✅ Build system configured
- ✅ GitHub Actions CI/CD ready
- ✅ All documentation written
- ✅ Release system automated
- ⏳ Ready for first release!

---

## 📜 License

MIT License - See [LICENSE](LICENSE) for details

---

## 🙏 Acknowledgments

- **Dalamud Team** - Plugin framework and community
- **Penumbra Devs** - IPC and mod organization
- **XIVLauncher** - Distribution platform
- **FFXIV Community** - Support and feedback

---

**Everything is set up and ready to go!**

- 🎮 **Players**: [README.md](README.md) to install
- 👨‍💻 **Developers**: [SETUP.md](SETUP.md) to get started
- 🚀 **Maintainers**: [BUILD.md](BUILD.md) and [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md)

---

*Last updated: May 13, 2024*
*Version: 1.0.0 (ready for release)*
