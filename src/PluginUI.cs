using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PenumbraSort;

public class PluginUI : IDisposable
{
    private readonly Configuration _config;
    private readonly PenumbraIpc    _ipc;
    private readonly TagManager     _tagManager;
    private readonly AiTagger       _aiTagger;
    private readonly LiveWatcher    _liveWatcher;
    private readonly ModPreviewCache _preview;

    private List<ModEntry>  _allMods = new();
    private List<SortGroup> _groups  = new();
    private ModEntry?       _editingMod;
    private string          _searchFilter  = string.Empty;
    private int             _sortModeIdx   = 0;
    private string          _newCustomTag  = string.Empty;
    private string          _statusMessage = string.Empty;
    private float           _statusTimer   = 0f;
    private bool            _showSettings  = false;
    private bool            _showRevert    = false;
    private bool            _showAiReview  = false;
    private string          _collectionName = string.Empty;
    private CancellationTokenSource? _aiCts;
    private bool _pendingLiveRefresh = false;

    // Colours
    private static readonly Vector4 Gold      = new(0.90f, 0.75f, 0.35f, 1.0f);
    private static readonly Vector4 Accent    = new(0.45f, 0.75f, 0.90f, 1.0f);
    private static readonly Vector4 Panel     = new(0.15f, 0.15f, 0.20f, 1.0f);
    private static readonly Vector4 Green     = new(0.35f, 0.80f, 0.45f, 1.0f);
    private static readonly Vector4 Red       = new(0.90f, 0.35f, 0.35f, 1.0f);
    private static readonly Vector4 Subtext   = new(0.65f, 0.62f, 0.70f, 1.0f);
    private static readonly Vector4 Warning   = new(0.95f, 0.65f, 0.20f, 1.0f);

    private static readonly string[] SortLabels = { "Clothing Type", "Season", "Occasion", "A–Z" };

    public bool Visible { get; set; }

    public PluginUI(Configuration config, LiveWatcher liveWatcher, ITextureProvider texProvider)
    {
        _config      = config;
        _ipc         = new PenumbraIpc(Plugin.PluginInterface);
        _tagManager  = new TagManager(config);
        _aiTagger    = new AiTagger();
        _liveWatcher = liveWatcher;
        _preview     = new ModPreviewCache(texProvider, Plugin.PluginInterface, config);

        // Subscribe to live mod detection — fires from background thread,
        // so we set a flag and handle it on the next Draw() call.
        _liveWatcher.NewModDetected += OnNewModDetected;

        Refresh();
    }

    public void Dispose()
    {
        _liveWatcher.NewModDetected -= OnNewModDetected;
        _aiCts?.Cancel();
        _aiTagger.Dispose();
        _preview.Dispose();
        _ipc.Dispose();
    }

    // ── Main Draw ─────────────────────────────────────────────────────────────

    public void Draw()
    {
        if (!Visible) return;
        if (_statusTimer > 0f) _statusTimer -= ImGui.GetIO().DeltaTime;

        // Handle live-detected new mod (flag set from background thread)
        if (_pendingLiveRefresh)
        {
            _pendingLiveRefresh = false;
            Refresh();
            SetStatus("New mod detected! Tags auto-suggested.");
        }

        PushStyle();
        ImGui.SetNextWindowSize(new Vector2(860, 640), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(640, 440), new Vector2(1400, 1000));

        bool windowOpen = Visible;
        if (ImGui.Begin("✦ PenumbraSort — Mod Organizer", ref windowOpen,
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar))
        {
            Visible = windowOpen;
            DrawMenuBar();
            DrawToolbar();
            ImGui.Separator();

            // Settings overlay
            if (_showSettings) { DrawSettings(); ImGui.End(); PopStyle(); return; }
            // Revert overlay
            if (_showRevert)   { DrawRevertPanel(); ImGui.End(); PopStyle(); return; }
            // AI review overlay
            if (_showAiReview) { DrawAiReviewPanel(); ImGui.End(); PopStyle(); return; }

            DrawMainLayout();
        }
        else { Visible = false; }

        ImGui.End();
        PopStyle();
    }

    // ── Menu Bar ──────────────────────────────────────────────────────────────

    private void DrawMenuBar()
    {
        if (!ImGui.BeginMenuBar()) return;

        if (ImGui.MenuItem("⚙ Settings"))  _showSettings = !_showSettings;
        if (ImGui.MenuItem("🔄 Refresh"))   Refresh();
        if (ImGui.MenuItem("💾 Save All"))  { _tagManager.SaveAllTags(_allMods); SetStatus("All tags saved!"); }

        // AI tagging
        var aiPending = _aiTagger.PendingSuggestions.Count;
        if (_aiTagger.IsBusy)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Warning);
            ImGui.Text($"  🤖 AI tagging {_aiTagger.TaggedSoFar}/{_aiTagger.TotalToTag}...");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            if (ImGui.SmallButton("✕ Cancel")) { _aiCts?.Cancel(); }
        }
        else
        {
            if (ImGui.MenuItem("🔍 Auto-Tag Untagged")) RunAiTagging();
            if (aiPending > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Warning);
                if (ImGui.MenuItem($"⚡ Review {aiPending} AI Suggestions"))
                    _showAiReview = true;
                ImGui.PopStyleColor();
            }
        }

        // Apply & Revert
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Text, Green);
        if (ImGui.MenuItem("📁 Apply Folders to Penumbra")) ApplyFolders();
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Warning);
        if (ImGui.MenuItem("⏪ Revert")) _showRevert = true;
        ImGui.PopStyleColor();

        // IPC indicator
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 195);
        ImGui.PushStyleColor(ImGuiCol.Text, _ipc.IsAvailable ? Green : Red);
        ImGui.Text(_ipc.IsAvailable ? "● Penumbra Connected" : "● Penumbra Offline");
        ImGui.PopStyleColor();

        ImGui.EndMenuBar();
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void DrawToolbar()
    {
        ImGui.SetNextItemWidth(200);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Panel);
        if (ImGui.InputTextWithHint("##Search", "🔍 Search mods…", ref _searchFilter, 128))
            RebuildGroups();
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.Text("Sort:");
        for (int i = 0; i < SortLabels.Length; i++)
        {
            ImGui.SameLine();
            bool sel = _sortModeIdx == i;
            if (sel) ImGui.PushStyleColor(ImGuiCol.Button, Accent with { W = 0.28f });
            if (ImGui.SmallButton($" {SortLabels[i]} "))
            {
                _sortModeIdx = i;
                _config.LastSortMode = (SortMode)i;
                RebuildGroups();
            }
            if (sel) ImGui.PopStyleColor();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(_config.SortAscending ? " ↑ Asc " : " ↓ Desc "))
        {
            _config.SortAscending = !_config.SortAscending;
            RebuildGroups();
        }

        if (_statusTimer > 0f)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Gold);
            ImGui.Text(_statusMessage);
            ImGui.PopStyleColor();
        }

        if (_aiTagger.IsBusy || !string.IsNullOrEmpty(_aiTagger.StatusText))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text($"  {_aiTagger.StatusText}");
            ImGui.PopStyleColor();
        }
    }

    // ── Main Layout ───────────────────────────────────────────────────────────

    private void DrawMainLayout()
    {
        var avail = ImGui.GetContentRegionAvail();
        float leftW = _editingMod != null ? avail.X * 0.56f : avail.X;

        ImGui.BeginChild("##ModList", new Vector2(leftW - (_editingMod != null ? 6 : 0), avail.Y - 30), false);
        DrawModList();
        ImGui.EndChild();

        if (_editingMod != null)
        {
            ImGui.SameLine();
            ImGui.BeginChild("##TagEditor", new Vector2(avail.X - leftW - 2, avail.Y - 30), true);
            DrawTagEditor();
            ImGui.EndChild();
        }

        DrawBottomBar();
    }

    // ── Mod List ──────────────────────────────────────────────────────────────

    private void DrawModList()
    {
        var filtered = FilterGroups();
        int total = filtered.Sum(g => g.Mods.Count);

        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.Text($"  {total} mods · {filtered.Count} groups");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        foreach (var (group, gi) in filtered.Select((g, i) => (g, i)))
        {
            var bg = HexAlpha(group.GroupColor, 0.14f);
            ImGui.PushStyleColor(ImGuiCol.Header,        bg);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, HexAlpha(group.GroupColor, 0.25f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive,  HexAlpha(group.GroupColor, 0.35f));
            ImGui.PushStyleColor(ImGuiCol.Text,          HexToVec4(group.GroupColor) with { W = 1f });

            bool open = ImGui.CollapsingHeader(
                $"  {group.GroupName}  ({group.Mods.Count})##grp{gi}",
                ImGuiTreeNodeFlags.DefaultOpen);
            ImGui.PopStyleColor(4);

            if (!open) continue;
            foreach (var mod in group.Mods) DrawModRow(mod);
            ImGui.Spacing();
        }

        if (!filtered.Any())
        {
            ImGui.Spacing();
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 200) / 2);
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text("No mods found.");
            ImGui.PopStyleColor();
        }
    }

    private void DrawModRow(ModEntry mod)
    {
        bool isEdit = _editingMod == mod;
        float rowW  = ImGui.GetContentRegionAvail().X - 12;

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,
            isEdit ? Accent with { W = 0.10f } : Panel with { W = 0.35f });
        ImGui.BeginChild($"##mr_{mod.DirectoryName}", new Vector2(rowW, 40), false);

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 6);
        ImGui.Text($"  {mod.Name}");

        // Tooltip on hover — check IsItemHovered after the name text
        if (_config.EnablePreviewTooltip && ImGui.IsItemHovered())
            DrawModTooltip(mod);

        // AI suggestion indicator
        if (mod.PendingSuggestion != null)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Warning);
            ImGui.Text("🤖");
            ImGui.PopStyleColor();
        }

        // Tag chips
        foreach (var tag in mod.AllTags.Take(4))
        {
            ImGui.SameLine();
            DrawTagChip(tag);
        }
        if (mod.AllTags.Count > 4)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.SmallButton($"+{mod.AllTags.Count - 4}");
            ImGui.PopStyleColor();
        }
        if (!mod.HasAnyTags)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text("  no tags");
            ImGui.PopStyleColor();
        }

        // Tag button right-aligned
        float btnW = 60;
        float pad  = rowW - ImGui.GetCursorPosX() - btnW - 8;
        if (pad > 0) { ImGui.SameLine(); ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad); }

        if (isEdit)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Accent with { W = 0.4f });
            if (ImGui.SmallButton(" ✓ Done "))
            { _tagManager.SaveTags(mod); _editingMod = null; SetStatus("Saved!"); RebuildGroups(); }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Panel);
            if (ImGui.SmallButton(" 🏷 Tag ")) _editingMod = mod;
            ImGui.PopStyleColor();
        }

        ImGui.EndChild();

        // Also check hover on the whole child region for the tooltip
        if (_config.EnablePreviewTooltip && ImGui.IsItemHovered())
            DrawModTooltip(mod);

        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    private void DrawModTooltip(ModEntry mod)
    {
        const float TooltipWidth = 280f;
        const float ImgSize      = 220f;

        ImGui.BeginTooltip();
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.10f, 0.09f, 0.14f, 0.97f));

        // ── Header ────────────────────────────────────────────────────────────
        ImGui.SetNextItemWidth(TooltipWidth);
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.TextWrapped(mod.Name.Length > 0 ? mod.Name : mod.DirectoryName);
        ImGui.PopStyleColor();

        if (!string.IsNullOrEmpty(mod.Author))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text($"by {mod.Author}");
            if (!string.IsNullOrEmpty(mod.Version))
            { ImGui.SameLine(); ImGui.Text($"  v{mod.Version}"); }
            ImGui.PopStyleColor();
        }

        ImGui.Separator();

        // ── Description ───────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(mod.Description))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.83f, 0.90f, 1.0f));
            ImGui.SetNextItemWidth(TooltipWidth);
            ImGui.TextWrapped(mod.Description.Length > 200
                ? mod.Description[..200] + "..."
                : mod.Description);
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        // ── Tags ──────────────────────────────────────────────────────────────
        if (mod.AllTags.Any())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.TextWrapped(string.Join("  ", mod.AllTags.Select(t => $"• {t}")));
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        ImGui.Separator();

        // ── Preview image ─────────────────────────────────────────────────────
        var wrap = _preview.GetPreview(mod);
        var (stage, status) = _preview.GetLoadState(mod);

        if (wrap != null)
        {
            var texSize  = new Vector2(wrap.Width, wrap.Height);
            float scale  = Math.Min(TooltipWidth / texSize.X, ImgSize / texSize.Y);
            var dispSize = new Vector2(texSize.X * scale, texSize.Y * scale);

            float indent = (TooltipWidth - dispSize.X) / 2f;
            if (indent > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);

            ImGui.Image(wrap.Handle, dispSize);

            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text($"  {status}");
            ImGui.PopStyleColor();
        }
        else if (stage is ModPreviewCache.LoadStage.CheckingLocal
                       or ModPreviewCache.LoadStage.CheckingHeliosphere
                       or ModPreviewCache.LoadStage.SearchingWeb
                       or ModPreviewCache.LoadStage.Idle)
        {
            // Animated progress bar while actively loading
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Accent with { W = 0.7f });
            ImGui.PushStyleColor(ImGuiCol.FrameBg,       Panel);
            float t = (float)(ImGui.GetTime() % 2.0) / 2.0f; // 0→1 over 2s, loops
            ImGui.ProgressBar(-t, new Vector2(TooltipWidth, 6), "");
            ImGui.PopStyleColor(2);
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text($"  {status}");
            ImGui.PopStyleColor();
        }
        else
        {
            // Failed or not found
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text($"  {status}");
            ImGui.PopStyleColor();
        }

        ImGui.PopStyleColor(); // PopupBg
        ImGui.EndTooltip();
    }

    // ── Tag Editor ────────────────────────────────────────────────────────────

    private void DrawTagEditor()
    {
        if (_editingMod == null) return;
        var mod = _editingMod;

        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("🏷 Tag Editor");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.TextWrapped(mod.Name);
        if (!string.IsNullOrEmpty(mod.Author)) ImGui.Text($"by {mod.Author}");
        ImGui.PopStyleColor();

        // Show AI suggestion if pending
        if (mod.PendingSuggestion != null)
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Warning with { W = 0.08f });
            ImGui.BeginChild("##aiSug", new Vector2(ImGui.GetContentRegionAvail().X, 90), true);
            ImGui.PushStyleColor(ImGuiCol.Text, Warning);
            ImGui.Text($"🤖 AI Suggestion  ({mod.PendingSuggestion.Confidence * 100:0}% confidence)");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.TextWrapped(mod.PendingSuggestion.Reasoning);
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Button, Green with { W = 0.3f });
            if (ImGui.SmallButton(" ✓ Accept AI Tags "))
            { _tagManager.ApplyAiSuggestion(mod, mod.PendingSuggestion); SetStatus("AI tags accepted!"); RebuildGroups(); }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Red with { W = 0.3f });
            if (ImGui.SmallButton(" ✕ Dismiss "))
            { mod.PendingSuggestion = null; _aiTagger.PendingSuggestions.Remove(mod.DirectoryName); }
            ImGui.PopStyleColor();
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        ImGui.Separator();
        ImGui.Spacing();

        DrawTagSection("👗 Clothing Type", DefaultTags.ClothingTypes, mod.ClothingTags);
        ImGui.Spacing();
        DrawTagSection("🌸 Season",        DefaultTags.Seasons,       mod.SeasonTags);
        ImGui.Spacing();
        DrawTagSection("🎉 Occasion",      DefaultTags.Occasions,     mod.OccasionTags);
        ImGui.Spacing();
        DrawTagSection("🐱 Race",          DefaultTags.Races,         mod.RaceTags);

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("⭐ Custom Tags");
        ImGui.PopStyleColor();
        foreach (var ct in mod.CustomTags.ToList())
        {
            DrawTagChip(ct); ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Red with { W = 0.3f });
            if (ImGui.SmallButton($"×##{ct}")) mod.CustomTags.Remove(ct);
            ImGui.PopStyleColor();
        }
        ImGui.Spacing();
        ImGui.SetNextItemWidth(130);
        ImGui.InputTextWithHint("##nc", "New tag…", ref _newCustomTag, 32);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Accent with { W = 0.3f });
        if (ImGui.SmallButton("＋ Add") && !string.IsNullOrWhiteSpace(_newCustomTag))
        {
            if (!mod.CustomTags.Contains(_newCustomTag)) mod.CustomTags.Add(_newCustomTag.Trim());
            _newCustomTag = string.Empty;
        }
        ImGui.PopStyleColor();

        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, Gold with { W = 0.25f });
        if (ImGui.Button("💾 Save Tags", new Vector2(ImGui.GetContentRegionAvail().X, 28)))
        { _tagManager.SaveTags(mod); _editingMod = null; SetStatus("Saved!"); RebuildGroups(); }
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, Red with { W = 0.15f });
        if (ImGui.Button("🗑 Clear All Tags", new Vector2(ImGui.GetContentRegionAvail().X, 22)))
        { mod.ClothingTags.Clear(); mod.SeasonTags.Clear(); mod.OccasionTags.Clear();
          mod.RaceTags.Clear(); mod.CustomTags.Clear(); }
        ImGui.PopStyleColor();
    }

    private void DrawTagSection(string header, List<TagCategory> options, List<string> current)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text(header);
        ImGui.PopStyleColor();

        float avail = ImGui.GetContentRegionAvail().X;
        float x0    = ImGui.GetCursorPosX();
        float x     = x0;
        bool  first = true;

        foreach (var cat in options)
        {
            bool active = current.Contains(cat.Key);
            var  size   = ImGui.CalcTextSize($" {cat.Display} ") + new Vector2(8, 4);

            if (x + size.X > x0 + avail - 4) { ImGui.NewLine(); x = x0; first = true; }
            if (!first) ImGui.SameLine();
            first = false;
            x += size.X + 4;

            if (active)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        HexToVec4(cat.Color) with { W = 0.55f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, HexToVec4(cat.Color) with { W = 0.75f });
                ImGui.PushStyleColor(ImGuiCol.Text,          Vector4.One);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        Panel with { W = 0.6f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, HexToVec4(cat.Color) with { W = 0.20f });
                ImGui.PushStyleColor(ImGuiCol.Text,          Subtext);
            }
            if (ImGui.SmallButton($" {cat.Display} ##t_{cat.Key}"))
            {
                if (active) current.Remove(cat.Key);
                else        current.Add(cat.Key);
            }
            ImGui.PopStyleColor(3);
        }
    }

    // ── AI Review Panel ───────────────────────────────────────────────────────

    private void DrawAiReviewPanel()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("🤖 AI Tag Suggestions — Review & Approve");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.TextWrapped("AI suggestions are proposals only. They never overwrite your manual tags. Approve individually or use Approve All / Reject All.");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        // Bulk actions
        ImGui.PushStyleColor(ImGuiCol.Button, Green with { W = 0.3f });
        if (ImGui.Button("✓ Approve All", new Vector2(130, 24)))
        {
            foreach (var mod in _allMods.Where(m => m.PendingSuggestion != null))
                _tagManager.ApplyAiSuggestion(mod, mod.PendingSuggestion!);
            _aiTagger.PendingSuggestions.Clear();
            SetStatus("All AI suggestions accepted!");
            _showAiReview = false;
            RebuildGroups();
        }
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Red with { W = 0.3f });
        if (ImGui.Button("✕ Reject All", new Vector2(130, 24)))
        {
            foreach (var mod in _allMods) mod.PendingSuggestion = null;
            _aiTagger.PendingSuggestions.Clear();
            _showAiReview = false;
        }
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Panel);
        if (ImGui.Button("← Back", new Vector2(80, 24))) _showAiReview = false;
        ImGui.PopStyleColor();

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.BeginChild("##AiList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 10), false);
        foreach (var mod in _allMods.Where(m => m.PendingSuggestion != null))
        {
            var sug = mod.PendingSuggestion!;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel with { W = 0.5f });
            ImGui.BeginChild($"##ai_{mod.DirectoryName}", new Vector2(ImGui.GetContentRegionAvail().X - 4, 80), true);

            ImGui.PushStyleColor(ImGuiCol.Text, Gold);
            ImGui.Text(mod.Name);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text($"  {sug.Confidence * 100:0}% confidence — {sug.Reasoning}");
            ImGui.PopStyleColor();

            // Show proposed tags
            var allSugTags = sug.ClothingTags.Concat(sug.SeasonTags).Concat(sug.OccasionTags).Concat(sug.RaceTags);
            foreach (var tag in allSugTags) { DrawTagChip(tag); ImGui.SameLine(); }

            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Green with { W = 0.3f });
            if (ImGui.SmallButton($" ✓ Accept ##{mod.DirectoryName}"))
            { _tagManager.ApplyAiSuggestion(mod, sug); _aiTagger.PendingSuggestions.Remove(mod.DirectoryName); RebuildGroups(); }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Red with { W = 0.25f });
            if (ImGui.SmallButton($" ✕ Skip ##{mod.DirectoryName}"))
            { mod.PendingSuggestion = null; _aiTagger.PendingSuggestions.Remove(mod.DirectoryName); }
            ImGui.PopStyleColor();

            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }
        ImGui.EndChild();
    }

    // ── Revert Panel ──────────────────────────────────────────────────────────

    private void DrawRevertPanel()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("⏪ Revert Penumbra Folders");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.TextWrapped("Select a snapshot to restore. Snapshots are taken automatically before every Apply Folders operation.");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Panel);
        if (ImGui.Button("← Back", new Vector2(80, 24))) _showRevert = false;
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Warning with { W = 0.25f });
        if (ImGui.Button("Take Snapshot Now", new Vector2(160, 24)))
        {
            var snap = _ipc.TakeSnapshot(_allMods, "Manual snapshot");
            _config.AddSnapshot(snap);
            SetStatus("Snapshot taken.");
        }
        ImGui.PopStyleColor();

        ImGui.Separator();
        ImGui.Spacing();

        if (!_config.Snapshots.Any())
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text("  No snapshots yet. Apply Folders once to create the first snapshot.");
            ImGui.PopStyleColor();
            ImGui.End(); PopStyle(); return;
        }

        ImGui.BeginChild("##SnapList", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 10), false);
        foreach (var snap in _config.Snapshots)
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel with { W = 0.5f });
            ImGui.BeginChild($"##snap_{snap.TakenAt}", new Vector2(ImGui.GetContentRegionAvail().X - 4, 56), true);

            ImGui.PushStyleColor(ImGuiCol.Text, Gold);
            ImGui.Text(snap.TakenAt);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
            ImGui.Text($"  {snap.Description}  ({snap.ModPaths.Count} mods)");
            ImGui.PopStyleColor();

            ImGui.PushStyleColor(ImGuiCol.Button, Warning with { W = 0.3f });
            if (ImGui.SmallButton($" ⏪ Revert to This ##{snap.TakenAt}"))
            {
                var (s, f, msg) = _ipc.RevertToSnapshot(snap);
                SetStatus(msg);
                _showRevert = false;
                Refresh();
            }
            ImGui.PopStyleColor();

            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }
        ImGui.EndChild();
    }

    // ── Settings Panel ────────────────────────────────────────────────────────

    private void DrawSettings()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("⚙ Settings");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Panel);
        if (ImGui.Button("← Back", new Vector2(80, 24))) _showSettings = false;
        ImGui.PopStyleColor();

        ImGui.Separator();
        ImGui.Spacing();

        // ── Preview Tooltip ───────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("🖼 Mod Preview Tooltip");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.TextWrapped("Shows a popup with mod info and image when hovering over a mod name.");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        var enableTooltip = _config.EnablePreviewTooltip;
        if (ImGui.Checkbox("Enable preview tooltip on hover", ref enableTooltip))
        {
            _config.EnablePreviewTooltip = enableTooltip;
            _config.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Web Search Opt-in ─────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("🌐 Web Image Search (Fallback)");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.TextWrapped("If no local preview or Heliosphere image is found, PenumbraSort can search Bing Images using your mod's display name.");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        if (!_config.WebSearchPrivacyAcknowledged)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Warning);
            ImGui.TextWrapped("Privacy notice: Enabling web search sends mod display names to Bing (Microsoft). Images are cached to disk after the first fetch. Directory names are never sent.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            if (ImGui.SmallButton("I understand — enable web search"))
            {
                _config.WebSearchPrivacyAcknowledged = true;
                _config.EnableWebSearch              = true;
                _config.Save();
                SetStatus("Web search enabled. Images cached after first hover.");
            }
        }
        else
        {
            var webSearch = _config.EnableWebSearch;
            if (ImGui.Checkbox("Enable web image search fallback", ref webSearch))
            {
                _config.EnableWebSearch = webSearch;
                _config.Save();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Mod Directory Override ────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("📁 Mod Directory Override");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.Text("Leave blank to use Penumbra's detected path.");
        ImGui.PopStyleColor();

        var dir = _config.PenumbraModDirectory;
        ImGui.SetNextItemWidth(400);
        if (ImGui.InputText("##moddir", ref dir, 512))
        {
            _config.PenumbraModDirectory = dir;
            _config.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ── Live Watcher Status ───────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.Text("📡 Live Mod Detection");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.TextWrapped("Watches your mod folder and auto-suggests tags when a new mod is installed.");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, _liveWatcher.IsWatching ? Green : Warning);
        ImGui.Text(_liveWatcher.IsWatching
            ? $"● Watching: {_liveWatcher.WatchedPath}"
            : "● Not watching — Refresh or set mod directory above to start.");
        ImGui.PopStyleColor();
    }

    // ── Bottom Bar ────────────────────────────────────────────────────────────

    private void DrawBottomBar()
    {
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Text, Subtext);
        ImGui.Text($"  {_allMods.Count} mods");
        int untagged = _allMods.Count(m => !m.HasAnyTags);
        if (untagged > 0)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Warning);
            ImGui.Text($"  · {untagged} untagged");
            ImGui.PopStyleColor();
        }
        int pending = _aiTagger.PendingSuggestions.Count;
        if (pending > 0)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Warning);
            ImGui.Text($"  · {pending} AI suggestions pending");
            ImGui.PopStyleColor();
        }
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 250);
        ImGui.Text("/penumbrasort to toggle");
        ImGui.PopStyleColor();
    }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private void Refresh()
    {
        _allMods = _ipc.GetMods(
            string.IsNullOrEmpty(_config.PenumbraModDirectory) ? null : _config.PenumbraModDirectory);
        _tagManager.ApplyTags(_allMods);
        RebuildGroups();

        // Start/restart live watcher on the mod directory
        var modDir = _config.PenumbraModDirectory.Length > 0
            ? _config.PenumbraModDirectory
            : _ipc.ModDirectory;
        if (!string.IsNullOrEmpty(modDir))
            _liveWatcher.Start(modDir);

        SetStatus($"Loaded {_allMods.Count} mods.");
    }

    private void RebuildGroups() =>
        _groups = _tagManager.GroupMods(_allMods, (SortMode)_sortModeIdx, _config.SortAscending);

    private List<SortGroup> FilterGroups()
    {
        if (string.IsNullOrWhiteSpace(_searchFilter)) return _groups;
        return _groups
            .Select(g => new SortGroup
            {
                GroupName    = g.GroupName,
                GroupColor   = g.GroupColor,
                GroupIcon    = g.GroupIcon,
                FolderTarget = g.FolderTarget,
                Mods         = g.Mods.Where(m =>
                    m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    m.AllTags.Any(t => t.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                ).ToList()
            })
            .Where(g => g.Mods.Any()).ToList();
    }

    private void ApplyFolders()
    {
        // Take snapshot before applying
        var snap = _ipc.TakeSnapshot(_allMods, $"Before apply folders ({DateTime.Now:HH:mm:ss})");
        _config.AddSnapshot(snap);

        var (s, f, msg) = _ipc.ApplyFolders(_groups);
        SetStatus(msg);
    }

    private void RunAiTagging()
    {
        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();
        Task.Run(() => _aiTagger.SuggestTagsAsync(_allMods, string.Empty, _aiCts.Token));
    }

    /// <summary>
    /// Called from background thread by LiveWatcher.
    /// Sets a flag — actual refresh happens on next Draw() call on the main thread.
    /// </summary>
    private void OnNewModDetected(string dirName)
    {
        _pendingLiveRefresh = true;
    }

    private void SetStatus(string msg) { _statusMessage = msg; _statusTimer = 5f; }

    // ── Tag chip ──────────────────────────────────────────────────────────────

    private void DrawTagChip(string tag)
    {
        var cat = DefaultTags.All.FirstOrDefault(c => c.Key == tag);
        var col = cat != null ? HexToVec4(cat.Color) with { W = 0.35f } : Panel;
        ImGui.PushStyleColor(ImGuiCol.Button,        col);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, col with { W = 0.50f });
        ImGui.PushStyleColor(ImGuiCol.Text,          Vector4.One);
        ImGui.SmallButton($" {cat?.Icon ?? "⭐"} {tag} ");
        ImGui.PopStyleColor(3);
    }

    // ── Style ─────────────────────────────────────────────────────────────────

    private static void PushStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,      new Vector4(0.10f, 0.09f, 0.13f, 0.97f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg,       new Vector4(0.14f, 0.12f, 0.18f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.20f, 0.17f, 0.28f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.MenuBarBg,     new Vector4(0.13f, 0.11f, 0.17f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Separator,     new Vector4(0.35f, 0.30f, 0.45f, 0.60f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,       new Vector4(0.15f, 0.13f, 0.20f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,       new Vector4(0.00f, 0.00f, 0.00f, 0.00f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,  4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,    new Vector2(6, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,  new Vector2(10, 8));
    }

    private static void PopStyle()
    {
        ImGui.PopStyleColor(7);
        ImGui.PopStyleVar(4);
    }

    private static Vector4 HexToVec4(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            return new Vector4(
                Convert.ToInt32(hex[..2], 16) / 255f,
                Convert.ToInt32(hex[2..4], 16) / 255f,
                Convert.ToInt32(hex[4..6], 16) / 255f, 1f);
        }
        catch { return Vector4.One; }
    }

    private static Vector4 HexAlpha(string hex, float a) =>
        HexToVec4(hex) with { W = a };
}
