using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace PenumbraSort;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public string PenumbraModDirectory { get; set; } = string.Empty;

    // ModDirectoryName -> confirmed tags
    public Dictionary<string, List<string>> ModTags { get; set; } = new();

    // Snapshots for revert (keyed by timestamp string)
    public List<PenumbraSnapshot> Snapshots { get; set; } = new();
    public int MaxSnapshots { get; set; } = 10;

    // Sort preferences
    public SortMode LastSortMode  { get; set; } = SortMode.ClothingType;
    public bool     SortAscending { get; set; } = true;

    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
        => _pluginInterface = pluginInterface;

    public void Save() => _pluginInterface?.SavePluginConfig(this);

    public void AddSnapshot(PenumbraSnapshot snap)
    {
        Snapshots.Insert(0, snap);
        while (Snapshots.Count > MaxSnapshots)
            Snapshots.RemoveAt(Snapshots.Count - 1);
        Save();
    }
}

public enum SortMode
{
    ClothingType,
    Season,
    Occasion,
    Alphabetical,
}
