# PenumbraSort Development Guide

## Prerequisites

1. **Windows 10/11** (required for FFXIV and XIVLauncher)
2. **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
3. **XIVLauncher** - [Download](https://github.com/goatcorp/XIVLauncher.Core)
4. **Dalamud** - Installed via XIVLauncher
5. **Visual Studio Code** (optional) - [Download](https://code.visualstudio.com)
6. **C# Dev Kit** extension for VS Code (optional)

## Build Instructions

### Windows (Command Prompt/PowerShell)
```powershell
cd .\PenumbraSort
./build.bat
```

### macOS/Linux (Bash)
```bash
cd PenumbraSort
./build.sh
```

### Manual Build
```bash
dotnet build -c Release
```

After a successful build, the plugin DLL will be at:
```
bin/x64/Release/net8.0-windows/PenumbraSort.dll
```

## Installation

1. Build the plugin (see above)
2. Copy `PenumbraSort.dll` to your Dalamud plugin directory:
   - **Windows**: `%APPDATA%\XIVLauncher\addon\Hooks\dev\Plugins\PenumbraSort\`
   - Create the folder if it doesn't exist
3. Restart FFXIV or reload plugins

## Usage

- In-game, type `/penumbrasort` to open the plugin
- Click **🏷 Tag** to edit tags for a mod
- Select sort mode: Clothing Type, Season, Occasion, or A–Z
- Click **📋 Apply Sort** to save changes to Penumbra

## Development

### Project Structure
- `src/Plugin.cs` - Main Dalamud plugin entry point
- `src/PluginUI.cs` - ImGui interface
- `src/Configuration.cs` - Plugin settings persistence
- `src/TagManager.cs` - Tag logic and grouping
- `src/PenumbraIpc.cs` - Penumbra IPC communication
- `src/ModData.cs` - Data classes

### Key Dependencies
- **Dalamud** - Plugin framework
- **ImGui.NET** - UI rendering
- **Newtonsoft.Json** - JSON serialization

### IDE Setup (VS Code)

1. Install extensions:
   - C# Dev Kit
   - C# Extensions

2. Open workspace:
   ```bash
   code PenumbraSort
   ```

3. Press `F5` to debug (requires running FFXIV)

### Debugging

Set breakpoints in Visual Studio or VS Code and attach to the running FFX IV process. The `.vscode/launch.json` is pre-configured for this.

## Troubleshooting

### Plugin doesn't appear
- Ensure Dalamud is installed via XIVLauncher
- Place DLL in correct plugin directory
- Check `/xllog` in-game for errors

### Penumbra not detected
- Verify Penumbra plugin is installed and enabled
- Plugin shows "● Penumbra Offline" if IPC unavailable
- Check Penumbra version compatibility (DalamudApiLevel 10+)

### Build errors
- Ensure .NET 8 SDK is installed: `dotnet --version`
- Verify Dalamud libraries are present in XIVLauncher folder
- Run `dotnet restore` to download dependencies

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make changes and test thoroughly
4. Submit a pull request

## License

MIT License - See [LICENSE](LICENSE) file
