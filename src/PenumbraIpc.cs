using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PenumbraSort;

public class PenumbraIpc : IDisposable
{
    private readonly IDalamudPluginInterface _pi;

    private ICallGateSubscriber<string>?                    _getModDirectory;
    private ICallGateSubscriber<IList<(string, string)>>?   _getMods;
    // Penumbra.SetModPath(string collectionName, string modDir, string newPath)
    private ICallGateSubscriber<string, string, string, object>? _setModPath;

    public bool    IsAvailable  { get; private set; }
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
            _getMods         = _pi.GetIpcSubscriber<IList<(string, string)>>("Penumbra.GetMods");
            // SetModPath lets us rename the path prefix, which controls folder display
            _setModPath      = _pi.GetIpcSubscriber<string, string, string, object>("Penumbra.SetModPath");
            ModDirectory     = _getModDirectory.InvokeFunc();
            IsAvailable      = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public List<ModEntry> GetMods(string? overrideDir = null)
    {
        var mods = new List<ModEntry>();
        var dir  = overrideDir ?? ModDirectory;

        if (IsAvailable && _getMods != null)
        {
            try
            {
                foreach (var (dirName, displayName) in _getMods.InvokeFunc())
                {
                    var entry = new ModEntry { DirectoryName = dirName, Name = displayName };
                    if (dir != null) EnrichFromMeta(entry, Path.Combine(dir, dirName));
                    mods.Add(entry);
                }
                return mods;
            }
            catch { /* fall through */ }
        }

        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return mods;
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

    // ── Snapshot ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Takes a snapshot of current Penumbra mod paths so they can be reverted later.
    /// Reads from .penumbrasort.json if it exists, otherwise records current state.
    /// </summary>
    public PenumbraSnapshot TakeSnapshot(List<ModEntry> mods, string description)
    {
        var snap = new PenumbraSnapshot
        {
            TakenAt     = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Description = description,
        };
        foreach (var mod in mods)
            snap.ModPaths[mod.DirectoryName] = mod.PenumbraFolder;
        return snap;
    }

    // ── Apply Sort to Penumbra Folders ────────────────────────────────────────

    /// <summary>
    /// Writes folder assignments to Penumbra via IPC (SetModPath).
    /// Falls back to .penumbrasort.json if IPC unavailable.
    /// Returns (successCount, failCount, message).
    /// </summary>
    public (int success, int fail, string message) ApplyFolders(
        List<SortGroup> groups,
        string collectionName = "")
    {
        var sortData = groups.SelectMany((g, gi) =>
            g.Mods.Select((m, mi) => new
            {
                g.GroupName,
                g.FolderTarget,
                ModDir   = m.DirectoryName,
                GroupIdx = gi,
                ModIdx   = mi
            })
        ).ToList();

        // Always write the sort file for record-keeping
        if (ModDirectory != null)
        {
            var outFile = Path.Combine(ModDirectory, ".penumbrasort.json");
            File.WriteAllText(outFile, JsonConvert.SerializeObject(sortData, Formatting.Indented));
        }

        // Try IPC folder renaming
        if (!IsAvailable || _setModPath == null)
            return (0, 0, "Sort file saved. Connect Penumbra to apply folders in-game.");

        int success = 0, fail = 0;
        foreach (var group in groups)
        {
            var folderPath = group.FolderTarget; // e.g. "Tops"
            foreach (var mod in group.Mods)
            {
                try
                {
                    // Penumbra SetModPath: collectionName, modDirectory, newPath
                    // newPath format: "FolderName/ModName" or just "ModName" for no folder
                    var newPath = string.IsNullOrEmpty(folderPath)
                        ? mod.Name
                        : $"{folderPath}/{mod.Name}";
                    _setModPath.InvokeAction(collectionName, mod.DirectoryName, newPath);
                    mod.PenumbraFolder = folderPath;
                    success++;
                }
                catch { fail++; }
            }
        }

        var msg = fail == 0
            ? $"Applied folders to {success} mods in Penumbra."
            : $"Applied {success} mods, {fail} failed (may need Penumbra restart).";
        return (success, fail, msg);
    }

    /// <summary>Restores mod paths from a snapshot.</summary>
    public (int success, int fail, string message) RevertToSnapshot(
        PenumbraSnapshot snap,
        string collectionName = "")
    {
        if (!IsAvailable || _setModPath == null)
            return (0, 0, "Penumbra not connected — cannot revert folders.");

        int success = 0, fail = 0;
        foreach (var (dirName, oldPath) in snap.ModPaths)
        {
            try
            {
                _setModPath.InvokeAction(collectionName, dirName, oldPath);
                success++;
            }
            catch { fail++; }
        }

        return (success, fail,
            $"Reverted {success} mods to snapshot from {snap.TakenAt}.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnrichFromMeta(ModEntry entry, string modPath)
    {
        var metaFile = Path.Combine(modPath, "meta.json");
        if (!File.Exists(metaFile)) return;
        try
        {
            var obj = JObject.Parse(File.ReadAllText(metaFile));
            entry.Name        = obj["Name"]?.ToString()        ?? entry.Name;
            entry.Author      = obj["Author"]?.ToString()      ?? string.Empty;
            entry.Version     = obj["Version"]?.ToString()     ?? string.Empty;
            entry.Description = obj["Description"]?.ToString() ?? string.Empty;
        }
        catch { }
    }

    public void Dispose() { }
}
