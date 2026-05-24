using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PenumbraSort;

/// <summary>
/// Resolves and caches preview images for mods via a three-tier pipeline:
///   Tier 1 — Local file   : preview.png / preview.jpg / thumb.png in the mod folder
///   Tier 2 — Heliosphere  : parse heliosphere.toml UUID → official CDN thumbnail
///   Tier 3 — Web search   : Bing Image Search using the mod's display name (opt-in)
///
/// Images are downloaded once and cached to disk under the plugin config directory.
/// ITextureProvider handles GPU upload and frame-lifetime management.
/// </summary>
public class ModPreviewCache : IDisposable
{
    // ── Constants ────────────────────────────────────────────────────────────

    private static readonly string[] LocalPreviewNames =
    {
        "preview.png", "preview.jpg", "preview.jpeg", "preview.webp",
        "thumb.png",   "thumb.jpg",   "cover.png",    "cover.jpg",
    };

    private const string HelioTomlFile    = "heliosphere.toml";
    private const string HelioApiBase     = "https://heliosphere.app/api/v1/mods/";
    private const string BingSearchUrl    = "https://www.bing.com/images/search?q={0}+FFXIV+mod&form=HDRSC2&first=1";
    private const int    TooltipImageSize = 220; // pixels, square

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly ITextureProvider _texProvider;
    private readonly string            _cacheDir;
    private readonly Configuration     _config;
    private readonly HttpClient        _http;

    // DirectoryName → current load state
    private readonly Dictionary<string, PreviewState> _states = new();
    private readonly HashSet<string>                  _inflight = new();

    public ModPreviewCache(
        ITextureProvider texProvider,
        IDalamudPluginInterface pi,
        Configuration config)
    {
        _texProvider = texProvider;
        _config      = config;
        _cacheDir    = Path.Combine(pi.GetPluginConfigDirectory(), "PreviewCache");
        Directory.CreateDirectory(_cacheDir);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "PenumbraSort/1.0 (Dalamud plugin; mod preview cache)");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current preview wrap for a mod (may be null while loading).
    /// Kicks off async resolution if not yet started.
    /// Safe to call every frame — idempotent.
    /// </summary>
    public IDalamudTextureWrap? GetPreview(ModEntry mod)
    {
        var key = mod.DirectoryName;

        if (_states.TryGetValue(key, out var state))
        {
            // Return whatever we have — may be null if still loading or failed
            return state.Wrap;
        }

        // Not started — kick off async resolution (fire and forget)
        if (!_inflight.Contains(key))
        {
            _inflight.Add(key);
            _ = ResolveAsync(mod, CancellationToken.None);
        }

        return null;
    }

    /// <summary>Returns the load status string for display in the tooltip.</summary>
    public string GetStatus(ModEntry mod)
    {
        if (!_states.TryGetValue(mod.DirectoryName, out var s)) return "Loading...";
        return s.Status;
    }

    /// <summary>Clears cached state for a single mod (forces re-fetch on next hover).</summary>
    public void Invalidate(string dirName)
    {
        if (_states.TryGetValue(dirName, out var s))
        {
            s.Wrap?.Dispose();
            _states.Remove(dirName);
        }
        _inflight.Remove(dirName);
    }

    // ── Resolution pipeline ───────────────────────────────────────────────────

    private async Task ResolveAsync(ModEntry mod, CancellationToken ct)
    {
        var key = mod.DirectoryName;
        SetState(key, null, "Searching...");

        try
        {
            // ── Tier 1: Local file ────────────────────────────────────────────
            var local = FindLocalPreview(mod);
            if (local != null)
            {
                var wrap = await LoadFromFileAsync(local, key, ct);
                if (wrap != null)
                {
                    mod.LocalPreviewPath = local;
                    SetState(key, wrap, "Local preview");
                    return;
                }
            }

            // ── Tier 2: Heliosphere UUID ──────────────────────────────────────
            var uuid = FindHelioUuid(mod);
            if (uuid != null)
            {
                mod.HelioUuid = uuid;
                var wrap = await LoadFromHelioAsync(uuid, key, ct);
                if (wrap != null)
                {
                    SetState(key, wrap, "Heliosphere");
                    return;
                }
            }

            // ── Tier 3: Web search (opt-in) ───────────────────────────────────
            if (_config.EnableWebSearch && _config.WebSearchPrivacyAcknowledged)
            {
                // Use the cached disk image if we already fetched it before
                if (!string.IsNullOrEmpty(mod.CachedImagePath) &&
                    File.Exists(mod.CachedImagePath))
                {
                    var cached = await LoadFromFileAsync(mod.CachedImagePath, key, ct);
                    if (cached != null)
                    {
                        SetState(key, cached, "Web search (cached)");
                        return;
                    }
                }

                var webWrap = await LoadFromWebSearchAsync(mod, key, ct);
                if (webWrap != null)
                {
                    SetState(key, webWrap, "Web search");
                    return;
                }
            }

            // No image found — set empty state so we stop trying
            SetState(key, null,
                _config.EnableWebSearch
                    ? "No preview found"
                    : "No local preview (enable web search in Settings)");
        }
        catch (OperationCanceledException)
        {
            _states.Remove(key);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"[PreviewCache] Failed to load preview for {mod.Name}: {ex.Message}");
            SetState(key, null, $"Load error: {ex.Message[..Math.Min(50, ex.Message.Length)]}");
        }
        finally
        {
            _inflight.Remove(key);
        }
    }

    // ── Tier 1: Local file ────────────────────────────────────────────────────

    private static string? FindLocalPreview(ModEntry mod)
    {
        if (string.IsNullOrEmpty(mod.LocalPreviewPath)) return null;
        var dir = Path.GetDirectoryName(mod.LocalPreviewPath) ?? mod.LocalPreviewPath;

        // mod.LocalPreviewPath is set to the mod's directory by PenumbraIpc.EnrichFromMeta
        // Check if it's actually a directory
        if (Directory.Exists(mod.LocalPreviewPath))
            dir = mod.LocalPreviewPath;

        foreach (var name in LocalPreviewNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private async Task<IDalamudTextureWrap?> LoadFromFileAsync(
        string path, string debugKey, CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, ct);
            return await _texProvider.CreateFromImageAsync(
                new ReadOnlyMemory<byte>(bytes), debugKey, ct);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[PreviewCache] Local load failed ({path}): {ex.Message}");
            return null;
        }
    }

    // ── Tier 2: Heliosphere UUID ──────────────────────────────────────────────

    private static string? FindHelioUuid(ModEntry mod)
    {
        // mod.LocalPreviewPath holds the mod directory path set by PenumbraIpc
        if (string.IsNullOrEmpty(mod.LocalPreviewPath)) return null;

        var dir = Directory.Exists(mod.LocalPreviewPath)
            ? mod.LocalPreviewPath
            : Path.GetDirectoryName(mod.LocalPreviewPath);

        if (dir == null) return null;
        var toml = Path.Combine(dir, HelioTomlFile);
        if (!File.Exists(toml)) return null;

        try
        {
            // heliosphere.toml is a simple TOML file; parse uuid line without a full TOML lib
            foreach (var line in File.ReadAllLines(toml))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("id", StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains('='))
                {
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var uuid = parts[1].Trim().Trim('"', '\'', ' ');
                        if (Guid.TryParse(uuid, out _)) return uuid;
                    }
                }
            }
        }
        catch { /* ignore malformed toml */ }

        return null;
    }

    private async Task<IDalamudTextureWrap?> LoadFromHelioAsync(
        string uuid, string debugKey, CancellationToken ct)
    {
        // Check disk cache first
        var cachePath = Path.Combine(_cacheDir, $"helio_{uuid}.jpg");
        if (File.Exists(cachePath))
            return await LoadFromFileAsync(cachePath, debugKey, ct);

        try
        {
            // Heliosphere API: GET /api/v1/mods/{uuid} → JSON with images array
            var url      = $"{HelioApiBase}{uuid}";
            var json     = await _http.GetStringAsync(url, ct);
            var doc      = JsonDocument.Parse(json);

            // Navigate: $.images[0].url or $.cover_image_url depending on API version
            string? imgUrl = null;
            if (doc.RootElement.TryGetProperty("images", out var images) &&
                images.GetArrayLength() > 0)
            {
                imgUrl = images[0].TryGetProperty("url", out var u) ? u.GetString() : null;
            }
            else if (doc.RootElement.TryGetProperty("cover_image_url", out var cover))
            {
                imgUrl = cover.GetString();
            }

            if (imgUrl == null) return null;

            var imgBytes = await _http.GetByteArrayAsync(imgUrl, ct);
            await File.WriteAllBytesAsync(cachePath, imgBytes, ct);

            return await _texProvider.CreateFromImageAsync(
                new ReadOnlyMemory<byte>(imgBytes), debugKey, ct);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[PreviewCache] Heliosphere fetch failed ({uuid}): {ex.Message}");
            return null;
        }
    }

    // ── Tier 3: Web search ────────────────────────────────────────────────────

    private async Task<IDalamudTextureWrap?> LoadFromWebSearchAsync(
        ModEntry mod, string debugKey, CancellationToken ct)
    {
        // Uses mod display NAME (not directory name) to reduce privacy exposure
        var query    = Uri.EscapeDataString($"{mod.Name} FFXIV mod glamour");
        var searchUrl = $"https://www.bing.com/images/search?q={query}&form=HDRSC2&first=1";

        var cachePath = Path.Combine(_cacheDir,
            $"web_{SanitizeForPath(mod.Name)}.jpg");

        try
        {
            // Fetch the search results HTML and extract first image URL
            var html = await _http.GetStringAsync(searchUrl, ct);
            var imgUrl = ExtractFirstBingImageUrl(html);
            if (imgUrl == null) return null;

            var imgBytes = await _http.GetByteArrayAsync(imgUrl, ct);
            await File.WriteAllBytesAsync(cachePath, imgBytes, ct);
            mod.CachedImagePath = cachePath;

            return await _texProvider.CreateFromImageAsync(
                new ReadOnlyMemory<byte>(imgBytes), debugKey, ct);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[PreviewCache] Web search failed ({mod.Name}): {ex.Message}");
            return null;
        }
    }

    /// <summary>Extracts the first murl (media URL) from Bing image search HTML.</summary>
    private static string? ExtractFirstBingImageUrl(string html)
    {
        // Bing embeds image URLs as: "murl":"https://..."
        const string marker = "\"murl\":\"";
        var idx = html.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return null;

        idx += marker.Length;
        var end = html.IndexOf('"', idx);
        if (end < 0) return null;

        var url = html[idx..end].Replace("\\u0026", "&");
        return Uri.IsWellFormedUriString(url, UriKind.Absolute) ? url : null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetState(string key, IDalamudTextureWrap? wrap, string status)
    {
        if (_states.TryGetValue(key, out var old) && old.Wrap != wrap)
            old.Wrap?.Dispose();

        _states[key] = new PreviewState(wrap, status);
    }

    private static string SanitizeForPath(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 60 ? name[..60] : name;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        foreach (var state in _states.Values)
            state.Wrap?.Dispose();
        _states.Clear();
        _http.Dispose();
    }

    private record PreviewState(IDalamudTextureWrap? Wrap, string Status);
}
