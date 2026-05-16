# ✦ PenumbraSort

[![Build](https://github.com/moralfiberffxiv/PenumbraSort/actions/workflows/build.yml/badge.svg)](https://github.com/moralfiberffxiv/PenumbraSort/actions/workflows/build.yml)
[![Latest Release](https://img.shields.io/github/v/release/moralfiberffxiv/PenumbraSort?label=release&color=blueviolet)](https://github.com/moralfiberffxiv/PenumbraSort/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Dalamud API](https://img.shields.io/badge/Dalamud%20API-10-informational)](https://github.com/goatcorp/Dalamud)

> A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FFXIV that organizes your [Penumbra](https://github.com/xivdev/Penumbra) mods by clothing type, season, and occasion.

---

## Install via Custom Repository

1. Open XIVLauncher → `/xlplugins` → ⚙ Settings → **Custom Plugin Repositories**
2. Paste this URL and click **Save**:
   ```
   https://raw.githubusercontent.com/moralfiberffxiv/PenumbraSort/main/repo.json
   ```
3. Search for **PenumbraSort** → **Install**
4. Type `/penumbrasort` in-game

Updates are delivered automatically through XIVLauncher whenever a new release is published.

---

## Features

| | |
|---|---|
| 👗 **Clothing Types** | Tops, Bottoms, Dresses, Outerwear, Footwear, Accessories, Armor, Headwear, Costumes, Underwear |
| 🌸 **Seasons** | Spring, Summer, Autumn, Winter, All Season |
| 🎉 **Occasions** | Casual, Formal, Combat, Festival, Evening, Beach, Fantasy, Wedding |
| 🏷 **Auto-Tag** | Detects tags from mod names automatically |
| ⭐ **Custom Tags** | Add your own categories to any mod |
| 🔍 **Live Search** | Filter by name or tag instantly |
| 💾 **Persistent** | Tags survive game restarts |
| 🔌 **Penumbra IPC** | Reads your real mod list when Penumbra is active; falls back to disk scan |

---

## How to Use

**Sort modes** — click a button in the toolbar: Clothing Type, Season, Occasion, or A–Z. Toggle ascending/descending with the arrow button. Search filters the list in real time.

**Tagging a mod** — click **🏷 Tag** on any row. The Tag Editor opens on the right. Click tag buttons to toggle (colored = active), add free-form custom tags at the bottom, then **💾 Save Tags**.

Tags are auto-detected from mod names on first load — a mod called *"Autumn Harvest Skirt"* gets Bottoms + Autumn + Casual automatically. Just review and adjust.

**Apply Sort** — click **📋 Apply Sort** in the menu bar to write the sorted order to Penumbra (via IPC if connected, or as `.penumbrasort.json` to your mod directory).

---



## Troubleshooting

### Plugin doesn't appear in `/xlplugins`
- Ensure Dalamud is installed (launch FFXIV through XIVLauncher once)
- Check that you've added the custom repository correctly
- Try `/xlreload` if already in-game

### "Penumbra Offline"
- Verify Penumbra plugin is installed and enabled
- Some features work offline (mods from disk scan)
- Tags still save locally

### Mod names aren't auto-detected
- Auto-detection runs on first load only
- Edit tags manually with 🏷 **Tag** button
- Tags are saved automatically when you click **Done**

---

## License

MIT License - See [LICENSE](LICENSE) file for details.

---

## Acknowledgments

- **Dalamud** team for the plugin framework
- **Penumbra** developers for IPC support
- **XIVLauncher** team for the launcher

Made with 💜 for the FFXIV community


GitHub Actions builds the plugin, creates a release with the zip attached, and updates `repo.json` automatically. Users receive the update through XIVLauncher.

---

## Project Layout

```
src/
  Plugin.cs          entry point, /penumbrasort command
  Configuration.cs   saved tags and preferences
  ModData.cs         tag definitions, ModEntry, SortGroup models
  TagManager.cs      auto-detection and grouping logic
  PenumbraIpc.cs     Penumbra IPC + disk fallback
  PluginUI.cs        full ImGui UI
.github/
  workflows/build.yml   CI/CD — build + release on tag push
repo.json              Dalamud custom repo manifest
```

---

## Contributing

PRs welcome. Open an issue first for anything beyond small fixes. Fill out the PR template and make sure `dotnet build` passes.

---

[MIT License](LICENSE)
