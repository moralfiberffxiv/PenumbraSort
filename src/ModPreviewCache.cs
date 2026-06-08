using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PenumbraSort;

/// <summary>
/// Resolves and caches preview images via a three-tier pipeline:
///   Tier 1 — Local file   : preview.png / thumb.png etc. in the mod folder
///   Tier 2 — Heliosphere  : heliosphere.toml UUID → CDN thumbnail
///   Tier 3 — Web search   : DuckDuckGo image search by mod display name (opt-in)
///
/// Key design decisions:
/// - States are invalidated when web search is toggled on, so newly-enabled
///   search actually runs on mods that previously returned "no preview found".
/// - LoadStage enum drives a progress indicator in the tooltip.
/// - All network IO is async and fires from a background thread; GPU upload
///   is delegated to ITextureProvider so the game thread is never blocked.
/// </summary>
public class ModPreviewCache : IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private static readonly string[] LocalPreviewNames =
    {
        "preview.png", "preview.jpg", "preview.jpeg", "preview.webp",
        "thumb.png",   "thumb.jpg",   "cover.png",    "cover.jpg",
        "screenshot.png", "screenshot.jpg",
    };

    private const string HelioTomlFile = "heliosphere.toml";
    private const string HelioApiBase  = "https://heliosphere.app/api/v1/mods/";

    // ── Load stage — drives progress display in tooltip ───────────────────────

    public enum LoadStage
    {
        Idle,
        CheckingLocal,
        CheckingHeliosphere,
        SearchingWeb,
        Done,
        Failed,
    }

    // ── State ──────────────────────────────────────────────────────────────────

    private readonly ITextureProvider _texProvider;
    private readonly string           _cacheDir;
    private readonly Configuration    _config;
    private readonly HttpClient       _http;

    private readonly Dictionary<string, PreviewState> _states   = new();
    private readonly HashSet<string>                  _inflight = new();

    // Track whether web search was enabled on last resolution attempt.
    // If it changes, stale "no preview" states must be invalidated.
    private bool _lastWebSearchEnabled;

    public ModPreviewCache(
        ITextureProvider texProvider,
        IDalamudPluginInterface pi,
        Configuration config)
    {
        _texProvider            = texProvider;
        _config                 = config;
        _cacheDir               = Path.Combine(pi.GetPluginConfigDirectory(), "PreviewCache");
        _lastWebSearchEnabled   = config.EnableWebSearch;
        Directory.CreateDirectory(_cacheDir);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (compatible; PenumbraSort/1.0; mod preview fetcher)");
        _http.Timeout = TimeSpan.FromSeconds(12);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns current texture wrap (null while loading or if not found).
    /// Kicks off async resolution on first call. Idempotent per mod.
    /// Also detects when web search is freshly enabled and re-runs failed states.
    /// </summary>
    public IDalamudTextureWrap? GetPreview(ModEntry mod)
    {
        // If web search was just enabled, invalidate mods stuck at "no preview" so they retry
        bool webSearchNowEnabled = _config.EnableWebSearch && _config.WebSearchPrivacyAcknowledged;
        if (webSearchNowEnabled && !_lastWebSearchEnabled)
        {
            _lastWebSearchEnabled = true;
            InvalidateFailedStates();
        }
        else if (!webSearchNowEnabled)
        {
            _lastWebSearchEnabled = false;
        }

        var key = mod.DirectoryName;

        if (_states.TryGetValue(key, out var state))
            return state.Wrap;

        if (!_inflight.Contains(key))
        {
            _inflight.Add(key);
            _ = ResolveAsync(mod, CancellationToken.None);
        }

        return null;
    }

    /// <summary>Returns (stage, statusText) for tooltip progress display.</summary>
    public (LoadStage Stage, string Status) GetLoadState(ModEntry mod)
    {
        if (_inflight.Contains(mod.DirectoryName))
        {
            if (_states.TryGetValue(mod.DirectoryName, out var s))
                return (s.Stage, s.Status);
            return (LoadStage.Idle, "Starting...");
        }

        if (_states.TryGetValue(mod.DirectoryName, out var state))
            return (state.Stage, state.Status);

        return (LoadStage.Idle, "Waiting...");
    }

    public void Invalidate(string dirName)
    {
        if (_states.TryGetValue(dirName, out var s))
        {
            s.Wrap?.Dispose();
            _states.Remove(dirName);
        }
        _inflight.Remove(dirName);
    }

    /// <summary>Clears all failed/no-preview states so they re-resolve on next hover.</summary>
    public void InvalidateFailedStates()
    {
        var toRemove = new List<string>();
        foreach (var (key, state) in _states)
            if (state.Wrap == null && state.Stage == LoadStage.Failed)
                toRemove.Add(key);
        foreach (var key in toRemove)
            _states.Remove(key);
    }

    // ── Resolution pipeline ────────────────────────────────────────────────────

    private async Task ResolveAsync(ModEntry mod, CancellationToken ct)
    {
        var key = mod.DirectoryName;
        try
        {
            // ── Tier 1: Local file ────────────────────────────────────────────
            SetState(key, null, LoadStage.CheckingLocal, "Checking local preview...");
            var local = FindLocalPreview(mod);
            if (local != null)
            {
                var wrap = await LoadFromFileAsync(local, key, ct);
                if (wrap != null)
                {
                    mod.LocalPreviewPath = local;
                    SetState(key, wrap, LoadStage.Done, "Local preview");
                    return;
                }
            }

            // ── Tier 2: Heliosphere UUID ──────────────────────────────────────
            SetState(key, null, LoadStage.CheckingHeliosphere, "Checking Heliosphere...");
            var uuid = FindHelioUuid(mod);
            if (uuid != null)
            {
                mod.HelioUuid = uuid;
                var wrap = await LoadFromHelioAsync(uuid, key, ct);
                if (wrap != null)
                {
                    SetState(key, wrap, LoadStage.Done, "Heliosphere");
                    return;
                }
            }

            // ── Tier 3: Web search (opt-in) ───────────────────────────────────
            if (_config.EnableWebSearch && _config.WebSearchPrivacyAcknowledged)
            {
                // Use cached result if it exists on disk
                var cached = GetDiskCachedPath(mod);
                if (cached != null)
                {
                    var wrap = await LoadFromFileAsync(cached, key, ct);
                    if (wrap != null)
                    {
                        SetState(key, wrap, LoadStage.Done, "Web search (cached)");
                        return;
                    }
                }

                SetState(key, null, LoadStage.SearchingWeb, $"Searching web for \"{mod.Name}\"...");
                var webWrap = await LoadFromWebSearchAsync(mod, key, ct);
                if (webWrap != null)
                {
                    SetState(key, webWrap, LoadStage.Done, "Web search");
                    return;
                }
            }

            // Nothing found
            SetState(key, null, LoadStage.Failed,
                _config.EnableWebSearch
                    ? "No preview found"
                    : "No local preview  (enable web search in Settings)");
        }
        catch (OperationCanceledException)
        {
            _states.Remove(key);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"[PreviewCache] {mod.Name}: {ex.Message}");
            SetState(key, null, LoadStage.Failed,
                $"Error: {ex.Message[..Math.Min(60, ex.Message.Length)]}");
        }
        finally
        {
            _inflight.Remove(key);
        }
    }

    // ── Tier 1: Local file ─────────────────────────────────────────────────────

    private static string? FindLocalPreview(ModEntry mod)
    {
        var dir = mod.LocalPreviewPath;
        if (string.IsNullOrEmpty(dir)) return null;
        if (!Directory.Exists(dir)) dir = Path.GetDirectoryName(dir);
        if (dir == null || !Directory.Exists(dir)) return null;

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
            Plugin.Log.Debug($"[PreviewCache] File load failed ({Path.GetFileName(path)}): {ex.Message}");
            return null;
        }
    }

    // ── Tier 2: Heliosphere ────────────────────────────────────────────────────

    private static string? FindHelioUuid(ModEntry mod)
    {
        var dir = mod.LocalPreviewPath;
        if (string.IsNullOrEmpty(dir)) return null;
        if (!Directory.Exists(dir)) dir = Path.GetDirectoryName(dir);
        if (dir == null) return null;

        var toml = Path.Combine(dir, HelioTomlFile);
        if (!File.Exists(toml)) return null;

        try
        {
            foreach (var line in File.ReadAllLines(toml))
            {
                var t = line.Trim();
                if (t.StartsWith("id", StringComparison.OrdinalIgnoreCase) && t.Contains('='))
                {
                    var val = t.Split('=', 2)[1].Trim().Trim('"', '\'', ' ');
                    if (Guid.TryParse(val, out _)) return val;
                }
            }
        }
        catch { }

        return null;
    }

    private async Task<IDalamudTextureWrap?> LoadFromHelioAsync(
        string uuid, string debugKey, CancellationToken ct)
    {
        var cachePath = Path.Combine(_cacheDir, $"helio_{uuid}.jpg");
        if (File.Exists(cachePath))
            return await LoadFromFileAsync(cachePath, debugKey, ct);

        try
        {
            var json    = await _http.GetStringAsync($"{HelioApiBase}{uuid}", ct);
            var doc     = JsonDocument.Parse(json);
            string? url = null;

            if (doc.RootElement.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0)
                url = imgs[0].TryGetProperty("url", out var u) ? u.GetString() : null;
            else if (doc.RootElement.TryGetProperty("cover_image_url", out var cover))
                url = cover.GetString();
            else if (doc.RootElement.TryGetProperty("thumbnail_url", out var thumb))
                url = thumb.GetString();

            if (url == null) return null;

            var bytes = await _http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(cachePath, bytes, ct);
            return await _texProvider.CreateFromImageAsync(new ReadOnlyMemory<byte>(bytes), debugKey, ct);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[PreviewCache] Heliosphere failed ({uuid}): {ex.Message}");
            return null;
        }
    }

    // ── Tier 3: Web search (DuckDuckGo) ───────────────────────────────────────

    private string? GetDiskCachedPath(ModEntry mod)
    {
        var path = Path.Combine(_cacheDir, $"web_{SanitizePath(mod.Name)}.jpg");
        return File.Exists(path) ? path : null;
    }

    private async Task<IDalamudTextureWrap?> LoadFromWebSearchAsync(
        ModEntry mod, string debugKey, CancellationToken ct)
    {
        var cachePath = Path.Combine(_cacheDir, $"web_{SanitizePath(mod.Name)}.jpg");

        try
        {
            // DuckDuckGo image search — more reliable HTML structure than Bing
            // Query uses display name + "FFXIV mod" to bias toward mod screenshots
            var query     = Uri.EscapeDataString($"{mod.Name} FFXIV mod");
            var searchUrl = $"https://duckduckgo.com/?q={query}&iax=images&ia=images";

            // DDG requires a cookie/token from the main page first
            var tokenHtml = await _http.GetStringAsync("https://duckduckgo.com/", ct);
            var token     = ExtractDdgToken(tokenHtml);

            string? imgUrl;
            if (token != null)
            {
                // Use the DDG image API endpoint
                var apiUrl  = $"https://duckduckgo.com/i.js?q={query}&o=json&p=1&vqd={token}&f=,,,,,&l=us-en";
                var apiJson = await _http.GetStringAsync(apiUrl, ct);
                imgUrl      = ExtractFirstDdgImageUrl(apiJson);
            }
            else
            {
                // Fallback: scrape the search page HTML for og:image or first img src
                var html = await _http.GetStringAsync(searchUrl, ct);
                imgUrl   = ExtractFirstImageFromHtml(html);
            }

            if (imgUrl == null)
            {
                Plugin.Log.Debug($"[PreviewCache] Web search returned no image URL for: {mod.Name}");
                return null;
            }

            var bytes = await _http.GetByteArrayAsync(imgUrl, ct);
            if (bytes.Length < 1024)
            {
                // Too small — likely a placeholder/error image, not a real result
                Plugin.Log.Debug($"[PreviewCache] Web search image too small ({bytes.Length} bytes), skipping");
                return null;
            }

            await File.WriteAllBytesAsync(cachePath, bytes, ct);
            mod.CachedImagePath = cachePath;

            return await _texProvider.CreateFromImageAsync(new ReadOnlyMemory<byte>(bytes), debugKey, ct);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[PreviewCache] Web search failed ({mod.Name}): {ex.Message}");
            return null;
        }
    }

    private static string? ExtractDdgToken(string html)
    {
        // DDG embeds vqd token as: vqd="<token>" or vqd='<token>'
        var m = Regex.Match(html, @"vqd[=\s]+['""]([^'""]+)['""]");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractFirstDdgImageUrl(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var results) &&
                results.GetArrayLength() > 0)
            {
                // Prefer "image" over "thumbnail" — better quality
                var first = results[0];
                if (first.TryGetProperty("image", out var img)) return img.GetString();
                if (first.TryGetProperty("thumbnail", out var thumb)) return thumb.GetString();
            }
        }
        catch { }
        return null;
    }

    private static string? ExtractFirstImageFromHtml(string html)
    {
        // Try og:image meta tag first
        var og = Regex.Match(html, @"<meta[^>]+property=['""]og:image['""][^>]+content=['""]([^'""]+)['""]");
        if (og.Success) return og.Groups[1].Value;

        // Try first img src that looks like a real URL
        var img = Regex.Match(html, @"<img[^>]+src=['""]?(https://[^'"">\s]+\.(jpg|jpeg|png|webp))['""]?");
        if (img.Success) return img.Groups[1].Value;

        return null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetState(string key, IDalamudTextureWrap? wrap, LoadStage stage, string status)
    {
        if (_states.TryGetValue(key, out var old) && old.Wrap != wrap)
            old.Wrap?.Dispose();
        _states[key] = new PreviewState(wrap, stage, status);
    }

    private static string SanitizePath(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Length > 60 ? name[..60] : name;
    }

    public void Dispose()
    {
        foreach (var s in _states.Values) s.Wrap?.Dispose();
        _states.Clear();
        _http.Dispose();
    }

    private record PreviewState(IDalamudTextureWrap? Wrap, LoadStage Stage, string Status);
}
