# Changelog

All notable changes to PenumbraSort are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.0.0] — Initial Release

### Added
- Sort mods by **Clothing Type**: Tops, Bottoms, Dresses, Outerwear, Footwear, Accessories, Armor, Headwear, Costumes, Underwear
- Sort mods by **Season**: Spring, Summer, Autumn, Winter, All Season
- Sort mods by **Occasion**: Casual, Formal, Combat, Festival, Evening, Beach, Fantasy, Wedding
- Sort mods **A–Z** alphabetically
- Ascending / descending toggle
- Real-time **search filter** across mod names and tags
- **Tag Editor** side panel — click any mod to open, toggle tags, add custom tags
- **Auto-detection** of tags from mod names (keyword matching)
- **Persistent tags** saved to Dalamud plugin config
- **Penumbra IPC** integration — reads mod list directly if Penumbra is running
- Disk fallback — scans mod directory when Penumbra is offline
- **Apply Sort** writes `.penumbrasort.json` to mod directory for persistence
- FF14-themed dark ImGui UI with color-coded group headers
- `/penumbrasort` chat command to toggle window
