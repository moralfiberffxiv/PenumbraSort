# Setting Up Dalamud Development Environment

This guide will help you set up a complete Dalamud plugin development environment for PenumbraSort.

## Step 1: Install Prerequisites

### 1.1 Windows OS
Dalamud only runs on Windows 10/11 (64-bit). If you're on macOS or Linux, you'll need to either:
- Use a Windows VM
- Use WSL2 with Windows components
- Build only the code portions (testing requires Windows)

### 1.2 Install FFXIV
1. Download FINAL FANTASY XIV from the official site
2. Create a free trial account or use existing account
3. Patch the game to latest version

### 1.3 Install XIVLauncher
1. Download [XIVLauncher.Core](https://github.com/goatcorp/XIVLauncher.Core/releases)
   - Choose the Windows x64 version
2. Run the installer
3. Launch FFXIV through XIVLauncher (to install Dalamud)
   - Log in and click "Play"
   - Wait for Dalamud to initialize (~1-2 minutes)
   - You should see a message like "Dalamud v**** loaded successfully"

### 1.4 Install .NET 8 SDK
1. Download [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
   - Choose "Windows x64"
2. Run the installer with default options
3. Verify installation:
   ```powershell
   dotnet --version
   # Should output: 8.0.x or higher
   ```

### 1.5 Install Git
1. Download [Git for Windows](https://git-scm.com/download/win)
2. Run installer with default options
3. Verify:
   ```powershell
   git --version
   ```

### 1.6 Install Visual Studio Code (Optional)
1. Download [VS Code](https://code.visualstudio.com)
2. Install these extensions:
   - **C# Dev Kit** (ms-dotnettools.csharp)
   - **C# Extensions** (kreativ-software.csharp-extensions)

## Step 2: Clone and Build PenumbraSort

```powershell
# Clone the repository
git clone https://github.com/moralfiberffxiv/PenumbraSort.git
cd PenumbraSort

# Restore NuGet packages
dotnet restore

# Build the plugin
dotnet build -c Release
```

If successful, you'll see:
```
✔ PenumbraSort.csproj (in 2.3s)
Build succeeded with 0 Warning(s) (in 15.2s)
```

## Step 3: Install Plugin to Dalamud

### Find Your Dalamud Plugin Directory
```powershell
# The plugin directory is usually at:
$APPDATA\XIVLauncher\addon\Hooks\dev\Plugins\
```

### Install PenumbraSort
```powershell
# Create plugin directory
mkdir "$env:APPDATA\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort"

# Copy the built DLL
copy "bin\x64\Release\net8.0-windows\PenumbraSort.dll" `
     "$env:APPDATA\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\"

# Verify it's there
dir "$env:APPDATA\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\"
```

## Step 4: Load Plugin in Game

1. Launch FFXIV through XIVLauncher
2. Type `/xlplugins` in chat
3. Search for "PenumbraSort"
4. If found, click "Enable"
5. If not found, try `/xlreload` and search again
6. Type `/penumbrasort` to open the plugin UI

## Step 5: Verify Installation

You should see:
- ✅ PenumbraSort window opens
- ✅ Mod list loads (with demo mods or your actual mods)
- ✅ Penumbra connection status shows ("● Penumbra Connected" or "● Penumbra Offline")

## Troubleshooting

### "Dalamud plugins are not loaded"
**Solution**: Make sure Dalamud is fully initialized:
1. Close FFXIV
2. Delete `"$APPDATA\XIVLauncher\addon\Hooks\dev"` folder
3. Delete `"$APPDATA\XIVLauncher\addon.json"`
4. Launch FFXIV again through XIVLauncher
5. Wait for Dalamud to reinitialize

### Plugin DLL not found in `/xlplugins`
**Solution**: Verify the file is in the correct location:
```powershell
# Should list PenumbraSort.dll
dir "$env:APPDATA\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\"
```

### Build fails with "Dalamud.dll not found"
**Solution**: Ensure Dalamud is installed:
```powershell
# Check if Dalamud folder exists
dir "$env:APPDATA\XIVLauncher\addon\Hooks\dev\"
```
If not, launch FFXIV through XIVLauncher to initialize Dalamud.

### "System.NotSupportedException: Could not load type" error
**Solution**: This means plugin loaded but has runtime error:
1. Check `/xllog` for detailed error
2. Ensure all dependencies (Penumbra, etc.) are installed

## Development with Visual Studio Code

1. Open the project:
   ```powershell
   code /path/to/PenumbraSort
   ```

2. Install C# Dev Kit extension (it will prompt)

3. Press `F5` to start debugging:
   - Code will attach to running FFXIV process
   - Set breakpoints by clicking line numbers
   - Step through code with F10/F11

4. To reload plugin:
   - Type `/xlreload` in game
   - Or restart FFXIV

## Next Steps

- Check [DEVELOPMENT.md](DEVELOPMENT.md) for code architecture
- See [README.md](README.md) for feature documentation
- Join the Dalamud Discord for community support
- Check [Dalamud documentation](https://github.com/goatcorp/Dalamud) for API reference

