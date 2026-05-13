using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace PenumbraSort;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // Penumbra IPC path (auto-detected or manually set)
    public string PenumbraModDirectory { get; set; } = string.Empty;

    // User-defined tag assignments: ModName -> List of tags
    public Dictionary<string, List<string>> ModTags { get; set; } = new();

    // User-defined custom tags beyond the defaults
    public List<string> CustomClothingTypes { get; set; } = new();
    public List<string> CustomSeasons { get; set; } = new();
    public List<string> CustomOccasions { get; set; } = new();

    // Sort preferences
    public SortMode LastSortMode { get; set; } = SortMode.ClothingType;
    public bool SortAscending { get; set; } = true;
    public bool AutoApplyOnSort { get; set; } = false;

    // UI state
    public bool ShowTagEditor { get; set; } = true;
    public bool ShowPreview { get; set; } = true;

    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public void Save()
    {
        _pluginInterface?.SavePluginConfig(this);
    }
}

public enum SortMode
{
    ClothingType,
    Season,
    Occasion,
    Alphabetical,
    Custom
}
