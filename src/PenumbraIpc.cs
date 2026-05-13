using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PenumbraSort;

/// <summary>
/// Handles all communication with Penumbra via IPC, plus reads mod metadata from disk.
/// Falls back gracefully if Penumbra is not installed.
/// </summary>
public class PenumbraIpc : IDisposable
{
    private readonly IDalamudPluginInterface _pi;

    // IPC Subscribers
    private ICallGateSubscriber<string>?               _getModDirectory;
    private ICallGateSubscriber<IList<(string, string)>>? _getMods;
    private ICallGateSubscriber<string, string, int, int, object>? _setModPosition;
    private ICallGateSubscriber<string, string, (int, int)>? _getModMeta;

    public bool IsAvailable { get; private set; }
    public string? ModDirectory { get; private set; }

    public PenumbraIpc(IDalamudPluginInterface pi)
    {
        _pi = pi;
        TryInitialize();
    }

    private void TryInitialize()
    {
        try
        {
            _getModDirectory = _pi.GetIpcSubscriber<string>("Penumbra.GetModDirectory");
            _getMods = _pi.GetIpcSubscriber<IList<(string, string)>>("Penumbra.GetMods");
            _setModPosition = _pi.GetIpcSubscriber<string, string, int, int, object>("Penumbra.MoveAbsolutePath");
            ModDirectory = _getModDirectory.InvokeFunc();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    /// <summary>
    /// Returns all mods from Penumbra IPC. Falls back to scanning disk if IPC unavailable.
    /// </summary>
    public List<ModEntry> GetMods(string? overrideDir = null)
    {
        var mods = new List<ModEntry>();
        var dir = overrideDir ?? ModDirectory;

        // Try IPC first
        if (IsAvailable && _getMods != null)
        {
            try
            {
                var ipcMods = _getMods.InvokeFunc();
                foreach (var (dirName, displayName) in ipcMods)
                {
                    var entry = new ModEntry
                    {
                        DirectoryName = dirName,
                        Name          = displayName,
                    };
                    // Enrich from disk meta if available
                    if (dir != null)
                        EnrichFromMeta(entry, Path.Combine(dir, dirName));
                    mods.Add(entry);
                }
                return mods;
            }
            catch { /* fall through to disk scan */ }
        }

        // Fallback: scan mod directory
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return mods;

        foreach (var modDir in Directory.EnumerateDirectories(dir))
        {
            var entry = new ModEntry
            {
                DirectoryName = Path.GetFileName(modDir),
                Name          = Path.GetFileName(modDir),
            };
            EnrichFromMeta(entry, modDir);
            mods.Add(entry);
        }

        return mods;
    }

    private static void EnrichFromMeta(ModEntry entry, string modPath)
    {
        var metaFile = Path.Combine(modPath, "meta.json");
        if (!File.Exists(metaFile)) return;

        try
        {
            var json = File.ReadAllText(metaFile);
            var obj  = JObject.Parse(json);
            entry.Name        = obj["Name"]?.ToString()        ?? entry.Name;
            entry.Author      = obj["Author"]?.ToString()      ?? string.Empty;
            entry.Version     = obj["Version"]?.ToString()     ?? string.Empty;
            entry.Description = obj["Description"]?.ToString() ?? string.Empty;
        }
        catch { /* ignore bad meta */ }
    }

    /// <summary>
    /// Renames the Penumbra group/folder for a mod to reflect sorted order.
    /// Format used: "Category/ModName"
    /// </summary>
    public bool ApplySortedOrder(List<SortGroup> groups)
    {
        if (!IsAvailable || ModDirectory == null) return false;

        // Penumbra doesn't expose a "rename mod path" IPC yet — instead we write
        // a sort-order file that PenumbraSort itself reads back for display.
        // When Penumbra adds folder IPC we can call _setModPosition here.
        var sortData = groups.SelectMany((g, gi) =>
            g.Mods.Select((m, mi) => new { g.GroupName, ModDir = m.DirectoryName, GroupIdx = gi, ModIdx = mi })
        ).ToList();

        var outputFile = Path.Combine(ModDirectory, ".penumbrasort.json");
        File.WriteAllText(outputFile, JsonConvert.SerializeObject(sortData, Formatting.Indented));
        return true;
    }

    public void Dispose() { }
}
