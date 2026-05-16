using System;
using System.IO;
using System.Timers;
using Dalamud.Plugin.Services;

namespace PenumbraSort;

/// <summary>
/// Watches the Penumbra mod directory for newly added mod folders.
/// When a new mod appears, fires NewModDetected so the UI can
/// add it to the list and run LocalPatternTagger on it immediately.
/// Uses a short debounce timer to avoid firing during mid-copy.
/// </summary>
public class LiveWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _debounce;
    private string? _pendingPath;
    private readonly IPluginLog _log;

    /// <summary>
    /// Fires on the next Draw() call after a new mod folder is detected.
    /// Arg: the directory name of the new mod (just the folder name, not full path).
    /// </summary>
    public event Action<string>? NewModDetected;

    /// <summary>Whether the watcher is currently active.</summary>
    public bool IsWatching => _watcher?.EnableRaisingEvents == true;
    public string? WatchedPath { get; private set; }

    public LiveWatcher(IPluginLog log)
    {
        _log = log;
    }

    /// <summary>
    /// Start watching the given directory for new mod folders.
    /// Safe to call multiple times — restarts the watcher if path changes.
    /// </summary>
    public void Start(string modDirectory)
    {
        if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
        {
            _log.Warning($"[PenumbraSort] LiveWatcher: directory not found: {modDirectory}");
            return;
        }

        if (WatchedPath == modDirectory && IsWatching)
            return; // already watching this path

        Stop();

        WatchedPath = modDirectory;

        _watcher = new FileSystemWatcher(modDirectory)
        {
            NotifyFilter            = NotifyFilters.DirectoryName,
            IncludeSubdirectories   = false,
            EnableRaisingEvents     = true,
        };

        _watcher.Created += OnCreated;
        _watcher.Error   += OnError;

        // Debounce: wait 2s after detection before firing, in case the mod is
        // still being copied or extracted when we first see the folder appear.
        _debounce = new System.Timers.Timer(2000) { AutoReset = false };
        _debounce.Elapsed += OnDebounceElapsed;

        _log.Info($"[PenumbraSort] LiveWatcher started on: {modDirectory}");
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnCreated;
            _watcher.Error   -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }

        if (_debounce != null)
        {
            _debounce.Stop();
            _debounce.Elapsed -= OnDebounceElapsed;
            _debounce.Dispose();
            _debounce = null;
        }

        WatchedPath = null;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        // Only care about directories (new mod folders), not files
        if (!Directory.Exists(e.FullPath)) return;

        // Ignore hidden/system folders like .penumbrasort
        var name = Path.GetFileName(e.FullPath);
        if (string.IsNullOrEmpty(name) || name.StartsWith(".")) return;

        _log.Info($"[PenumbraSort] New mod folder detected: {name}");

        // Debounce: reset timer, store the latest path
        _pendingPath = name;
        _debounce?.Stop();
        _debounce?.Start();
    }

    private void OnDebounceElapsed(object? sender, ElapsedEventArgs e)
    {
        var dir = _pendingPath;
        _pendingPath = null;
        if (!string.IsNullOrEmpty(dir))
        {
            _log.Info($"[PenumbraSort] Firing NewModDetected for: {dir}");
            NewModDetected?.Invoke(dir);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _log.Error($"[PenumbraSort] LiveWatcher error: {e.GetException().Message}");
        // Try to restart
        if (WatchedPath != null)
        {
            Stop();
            Start(WatchedPath);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
