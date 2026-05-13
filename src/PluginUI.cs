using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace PenumbraSort;

public class PluginUI : IDisposable
{
    private readonly Configuration _config;
    private readonly PenumbraIpc    _ipc;
    private readonly TagManager     _tagManager;
    private readonly WindowSystem   _windowSystem;

    // Mod state
    private List<ModEntry>   _allMods   = new();
    private List<SortGroup>  _groups    = new();
    private ModEntry?        _editingMod = null;
    private string           _searchFilter = string.Empty;
    private bool             _isDirty = false;

    // UI state
    private int _selectedSortMode = 0;
    private string _newCustomTag  = string.Empty;
    private string _statusMessage = string.Empty;
    private float  _statusTimer   = 0f;
    private bool   _showSettings  = false;
    private int    _collapsedGroups_flags = 0; // bitmask

    // Colors matching FF14 aesthetic
    private static readonly Vector4 ColorGold     = new(0.90f, 0.75f, 0.35f, 1.0f);
    private static readonly Vector4 ColorAccent   = new(0.45f, 0.75f, 0.90f, 1.0f);
    private static readonly Vector4 ColorDark     = new(0.10f, 0.10f, 0.14f, 0.97f);
    private static readonly Vector4 ColorPanel    = new(0.15f, 0.15f, 0.20f, 1.0f);
    private static readonly Vector4 ColorBorder   = new(0.35f, 0.30f, 0.45f, 0.80f);
    private static readonly Vector4 ColorGreen    = new(0.35f, 0.80f, 0.45f, 1.0f);
    private static readonly Vector4 ColorRed      = new(0.90f, 0.35f, 0.35f, 1.0f);
    private static readonly Vector4 ColorSubtext  = new(0.65f, 0.62f, 0.70f, 1.0f);

    private static readonly string[] SortModeLabels = { "Clothing Type", "Season", "Occasion", "A–Z" };

    public bool Visible { get; set; }

    public PluginUI(Configuration config)
    {
        _config     = config;
        _ipc        = new PenumbraIpc(Plugin.PluginInterface);
        _tagManager = new TagManager(config);

        _windowSystem = new WindowSystem("PenumbraSort");
        Refresh();
    }

    public void Dispose()
    {
        _ipc.Dispose();
    }

    // ── Frame ─────────────────────────────────────────────────────────────────

    public void Draw()
    {
        if (!Visible) return;

        // Tick status message
        if (_statusTimer > 0f)
            _statusTimer -= ImGui.GetIO().DeltaTime;

        // Push dark FF14-style theme
        PushStyle();

        ImGui.SetNextWindowSize(new Vector2(780, 620), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(600, 400), new Vector2(1200, 900));

        if (ImGui.Begin("✦ PenumbraSort — Mod Organizer", ref Visible,
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar))
        {
            DrawMenuBar();
            DrawTopBar();

            ImGui.Separator();

            // Two-pane layout: left = sorted list, right = tag editor
            var avail = ImGui.GetContentRegionAvail();
            float leftW = _editingMod != null ? avail.X * 0.55f : avail.X;

            ImGui.BeginChild("##ModList", new Vector2(leftW - (_editingMod != null ? 6 : 0), avail.Y - 32), false);
            DrawModList();
            ImGui.EndChild();

            if (_editingMod != null)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 2);
                ImGui.BeginChild("##TagEditor", new Vector2(avail.X - leftW - 2, avail.Y - 32), true,
                    ImGuiWindowFlags.AlwaysAutoResize);
                DrawTagEditor();
                ImGui.EndChild();
            }

            DrawBottomBar();
        }

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
        if (ImGui.MenuItem("📋 Apply Sort"))
        {
            var applied = _ipc.ApplySortedOrder(_groups);
            SetStatus(applied ? "Sort order applied to Penumbra!" : "Saved sort file (IPC unavailable).");
        }

        // IPC status indicator
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 190);
        ImGui.PushStyleColor(ImGuiCol.Text, _ipc.IsAvailable ? ColorGreen : ColorRed);
        ImGui.Text(_ipc.IsAvailable ? "● Penumbra Connected" : "● Penumbra Offline");
        ImGui.PopStyleColor();

        ImGui.EndMenuBar();
    }

    // ── Top Bar ───────────────────────────────────────────────────────────────

    private void DrawTopBar()
    {
        // Search
        ImGui.SetNextItemWidth(220);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, ColorPanel);
        if (ImGui.InputTextWithHint("##Search", "🔍 Search mods…", ref _searchFilter, 128))
            RebuildGroups();
        ImGui.PopStyleColor();

        ImGui.SameLine();

        // Sort mode tabs
        ImGui.Text("Sort by:");
        ImGui.SameLine();

        for (int i = 0; i < SortModeLabels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            bool selected = _selectedSortMode == i;
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ColorAccent with { W = 0.25f });
            if (ImGui.SmallButton($" {SortModeLabels[i]} "))
            {
                _selectedSortMode = i;
                _config.LastSortMode = (SortMode)i;
                RebuildGroups();
            }
            if (selected) ImGui.PopStyleColor();
        }

        ImGui.SameLine();

        // Asc/Desc toggle
        bool asc = _config.SortAscending;
        if (ImGui.SmallButton(asc ? " ↑ Asc " : " ↓ Desc "))
        {
            _config.SortAscending = !_config.SortAscending;
            RebuildGroups();
        }

        // Status message
        if (_statusTimer > 0f)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, ColorGold);
            ImGui.Text(_statusMessage);
            ImGui.PopStyleColor();
        }
    }

    // ── Mod List ──────────────────────────────────────────────────────────────

    private void DrawModList()
    {
        var filtered = string.IsNullOrWhiteSpace(_searchFilter)
            ? _groups
            : _groups.Select(g => new SortGroup
            {
                GroupName  = g.GroupName,
                GroupColor = g.GroupColor,
                GroupIcon  = g.GroupIcon,
                Mods       = g.Mods.Where(m =>
                    m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    m.AllTags.Any(t => t.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                ).ToList()
            }).Where(g => g.Mods.Any()).ToList();

        int totalMods = filtered.Sum(g => g.Mods.Count);
        ImGui.PushStyleColor(ImGuiCol.Text, ColorSubtext);
        ImGui.Text($"  {totalMods} mods  ·  {filtered.Count} groups");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        foreach (var (group, gi) in filtered.Select((g, i) => (g, i)))
        {
            DrawGroupHeader(group, gi);

            bool collapsed = (_collapsedGroups_flags & (1 << gi)) != 0;
            if (!collapsed)
            {
                foreach (var mod in group.Mods)
                    DrawModRow(mod);
            }

            ImGui.Spacing();
        }

        if (!filtered.Any())
        {
            ImGui.Spacing();
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 200) / 2);
            ImGui.PushStyleColor(ImGuiCol.Text, ColorSubtext);
            ImGui.Text("No mods found. Try refreshing.");
            ImGui.PopStyleColor();
        }
    }

    private void DrawGroupHeader(SortGroup group, int gi)
    {
        bool collapsed = (_collapsedGroups_flags & (1 << gi)) != 0;

        // Parse hex color
        var col = HexToVec4(group.GroupColor) with { W = 0.18f };
        ImGui.PushStyleColor(ImGuiCol.Header,        col);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, col with { W = 0.30f });
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,  col with { W = 0.40f });

        bool open = ImGui.CollapsingHeader(
            $"  {group.GroupName}  ({group.Mods.Count})",
            ImGuiTreeNodeFlags.DefaultOpen | (collapsed ? ImGuiTreeNodeFlags.None : 0));

        ImGui.PopStyleColor(3);

        if (!open && !collapsed)  _collapsedGroups_flags |= (1 << gi);
        if (open  && collapsed)   _collapsedGroups_flags &= ~(1 << gi);
    }

    private void DrawModRow(ModEntry mod)
    {
        bool isEditing = _editingMod == mod;

        // Row background
        ImGui.PushStyleColor(ImGuiCol.ChildBg, isEditing
            ? ColorAccent with { W = 0.10f }
            : ColorPanel  with { W = 0.40f });

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 12);
        float rowW = ImGui.GetContentRegionAvail().X - 12;

        ImGui.BeginChild($"##mod_{mod.DirectoryName}", new Vector2(rowW, 38), false);

        // Mod name
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
        ImGui.Text($"  {mod.Name}");

        // Tag chips
        ImGui.SameLine();
        float tagsStartX = ImGui.GetCursorPosX();
        foreach (var tag in mod.AllTags.Take(5))
        {
            ImGui.SameLine();
            DrawTagChip(tag);
        }
        if (mod.AllTags.Count > 5)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, ColorSubtext);
            ImGui.SmallButton($"+{mod.AllTags.Count - 5}");
            ImGui.PopStyleColor();
        }

        // Edit button (right-aligned)
        float btnW = 55;
        ImGui.SameLine();
        float pad = rowW - ImGui.GetCursorPosX() - btnW - 8;
        if (pad > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pad);

        if (isEditing)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ColorAccent with { W = 0.4f });
            if (ImGui.SmallButton("  ✓ Done  "))
            {
                _tagManager.SaveTags(mod);
                _editingMod = null;
                SetStatus($"Tags saved for \"{mod.Name}\"");
                RebuildGroups();
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ColorPanel);
            if (ImGui.SmallButton(" 🏷 Tag "))
                _editingMod = mod;
            ImGui.PopStyleColor();
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    // ── Tag Editor (right panel) ──────────────────────────────────────────────

    private void DrawTagEditor()
    {
        if (_editingMod == null) return;
        var mod = _editingMod;

        // Header
        ImGui.PushStyleColor(ImGuiCol.Text, ColorGold);
        ImGui.Text($"🏷 Tag Editor");
        ImGui.PopStyleColor();
        ImGui.PushStyleColor(ImGuiCol.Text, ColorSubtext);
        ImGui.TextWrapped(mod.Name);
        ImGui.PopStyleColor();

        if (!string.IsNullOrEmpty(mod.Author))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColorSubtext);
            ImGui.Text($"by {mod.Author}");
            ImGui.PopStyleColor();
        }

        ImGui.Separator();
        ImGui.Spacing();

        DrawTagSection("👗 Clothing Type", DefaultTags.ClothingTypes, mod.ClothingTags);
        ImGui.Spacing();
        DrawTagSection("🌸 Season",        DefaultTags.Seasons,       mod.SeasonTags);
        ImGui.Spacing();
        DrawTagSection("🎉 Occasion",      DefaultTags.Occasions,     mod.OccasionTags);

        ImGui.Separator();
        ImGui.Spacing();

        // Custom tags
        ImGui.PushStyleColor(ImGuiCol.Text, ColorGold);
        ImGui.Text("⭐ Custom Tags");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        foreach (var ct in mod.CustomTags.ToList())
        {
            DrawTagChip(ct);
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, ColorRed with { W = 0.3f });
            if (ImGui.SmallButton($"×##{ct}"))
                mod.CustomTags.Remove(ct);
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(130);
        ImGui.InputTextWithHint("##NewCustom", "New tag…", ref _newCustomTag, 32);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, ColorAccent with { W = 0.3f });
        if (ImGui.SmallButton("＋ Add") && !string.IsNullOrWhiteSpace(_newCustomTag))
        {
            if (!mod.CustomTags.Contains(_newCustomTag))
                mod.CustomTags.Add(_newCustomTag.Trim());
            _newCustomTag = string.Empty;
        }
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Save/Clear buttons
        ImGui.PushStyleColor(ImGuiCol.Button, ColorGold with { W = 0.25f });
        if (ImGui.Button("💾 Save Tags", new Vector2(ImGui.GetContentRegionAvail().X, 28)))
        {
            _tagManager.SaveTags(mod);
            _editingMod = null;
            SetStatus($"Saved!");
            RebuildGroups();
        }
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, ColorRed with { W = 0.15f });
        if (ImGui.Button("🗑 Clear All Tags", new Vector2(ImGui.GetContentRegionAvail().X, 22)))
        {
            mod.ClothingTags.Clear();
            mod.SeasonTags.Clear();
            mod.OccasionTags.Clear();
            mod.CustomTags.Clear();
        }
        ImGui.PopStyleColor();
    }

    private void DrawTagSection(string header, List<TagCategory> options, List<string> currentTags)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ColorGold);
        ImGui.Text(header);
        ImGui.PopStyleColor();

        float avail = ImGui.GetContentRegionAvail().X;
        float x0 = ImGui.GetCursorPosX();
        float x = x0;

        foreach (var cat in options)
        {
            bool active = currentTags.Contains(cat.Key);

            Vector2 size = ImGui.CalcTextSize($" {cat.Display} ") + new Vector2(8, 4);

            if (x + size.X > x0 + avail - 4)
            {
                ImGui.NewLine();
                x = x0;
            }
            else if (x > x0)
            {
                ImGui.SameLine();
            }

            x += size.X + 4;

            if (active)
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        HexToVec4(cat.Color) with { W = 0.55f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, HexToVec4(cat.Color) with { W = 0.75f });
                ImGui.PushStyleColor(ImGuiCol.Text,          Vector4.One);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button,        ColorPanel with { W = 0.60f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, HexToVec4(cat.Color) with { W = 0.20f });
                ImGui.PushStyleColor(ImGuiCol.Text,          ColorSubtext);
            }

            if (ImGui.SmallButton($" {cat.Display} ##tag_{cat.Key}"))
            {
                if (active) currentTags.Remove(cat.Key);
                else        currentTags.Add(cat.Key);
            }

            ImGui.PopStyleColor(3);
        }
    }

    // ── Bottom Bar ────────────────────────────────────────────────────────────

    private void DrawBottomBar()
    {
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Text, ColorSubtext);
        ImGui.Text($"  {_allMods.Count} total mods");
        ImGui.SameLine();

        if (_isDirty)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColorGold);
            ImGui.Text("  ● Unsaved changes");
            ImGui.PopStyleColor();
        }

        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 250);
        ImGui.Text("Type /penumbrasort to toggle");
        ImGui.PopStyleColor();
    }

    // ── Tag Chip Helper ───────────────────────────────────────────────────────

    private void DrawTagChip(string tag)
    {
        var cat = DefaultTags.ClothingTypes
            .Concat(DefaultTags.Seasons)
            .Concat(DefaultTags.Occasions)
            .FirstOrDefault(c => c.Key == tag);

        var color = cat != null ? HexToVec4(cat.Color) with { W = 0.35f } : ColorPanel;

        ImGui.PushStyleColor(ImGuiCol.Button,        color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color with { W = 0.50f });
        ImGui.PushStyleColor(ImGuiCol.Text,          Vector4.One);
        ImGui.SmallButton($" {cat?.Icon ?? "⭐"} {tag} ");
        ImGui.PopStyleColor(3);
    }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private void Refresh()
    {
        _allMods = _ipc.GetMods(_config.PenumbraModDirectory.Length > 0
            ? _config.PenumbraModDirectory : null);

        if (!_allMods.Any())
            _allMods = GenerateDemoMods();

        _tagManager.ApplyTags(_allMods);
        RebuildGroups();
        SetStatus($"Loaded {_allMods.Count} mods.");
    }

    private void RebuildGroups()
    {
        _groups = _tagManager.GroupMods(_allMods, (SortMode)_selectedSortMode, _config.SortAscending);
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTimer   = 4f;
    }

    /// <summary>Returns sample mods so the UI isn't empty without Penumbra.</summary>
    private static List<ModEntry> GenerateDemoMods() => new()
    {
        new() { Name = "Midnight Lace Dress",       DirectoryName = "midnight_lace",   Author = "LaceWeaver"  },
        new() { Name = "Summer Bikini Set",          DirectoryName = "summer_bikini",   Author = "BeachBabe"   },
        new() { Name = "Winter Coat – Velvet",       DirectoryName = "winter_coat_v",   Author = "FashionX"    },
        new() { Name = "Spring Floral Blouse",       DirectoryName = "spring_blouse",   Author = "PetalCraft"  },
        new() { Name = "Combat Armor Mark IV",       DirectoryName = "combat_armor_4",  Author = "IronForge"   },
        new() { Name = "Autumn Harvest Skirt",       DirectoryName = "autumn_skirt",    Author = "LoamStudio"  },
        new() { Name = "Festival Yukata",            DirectoryName = "festival_yukata", Author = "KyotoMods"   },
        new() { Name = "Evening Gown – Starlight",   DirectoryName = "eve_gown_star",   Author = "NightDress"  },
        new() { Name = "Casual Hoodie & Pants",      DirectoryName = "casual_hoodie",   Author = "StreetStyle" },
        new() { Name = "Leather Boots – Tall",       DirectoryName = "leather_boots_t", Author = "SoleWorks"   },
        new() { Name = "Crystal Crown",              DirectoryName = "crystal_crown",   Author = "RegalMods"   },
        new() { Name = "Beach Shorts & Flip Flops",  DirectoryName = "beach_shorts",    Author = "SunFun"      },
        new() { Name = "Witch Hat & Cape",           DirectoryName = "witch_set",       Author = "HexHatter"   },
        new() { Name = "Bridal Veil & Dress",        DirectoryName = "bridal_set",      Author = "VowsDesign"  },
        new() { Name = "Tactical Plate Mail",        DirectoryName = "plate_mail",      Author = "ArmsSmith"   },
    };

    // ── Style ─────────────────────────────────────────────────────────────────

    private static void PushStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg,       new Vector4(0.10f, 0.09f, 0.13f, 0.97f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg,        new Vector4(0.14f, 0.12f, 0.18f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive,  new Vector4(0.20f, 0.17f, 0.28f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.MenuBarBg,      new Vector4(0.13f, 0.11f, 0.17f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Separator,      new Vector4(0.35f, 0.30f, 0.45f, 0.60f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,    new Vector4(0.08f, 0.07f, 0.10f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,  new Vector4(0.35f, 0.30f, 0.50f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,        new Vector4(0.15f, 0.13f, 0.20f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,        new Vector4(0.00f, 0.00f, 0.00f, 0.00f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,    6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,     4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,       new Vector2(6, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,     new Vector2(10, 8));
    }

    private static void PopStyle()
    {
        ImGui.PopStyleColor(9);
        ImGui.PopStyleVar(4);
    }

    private static Vector4 HexToVec4(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            float r = Convert.ToInt32(hex[..2], 16) / 255f;
            float g = Convert.ToInt32(hex[2..4], 16) / 255f;
            float b = Convert.ToInt32(hex[4..6], 16) / 255f;
            return new Vector4(r, g, b, 1f);
        }
        catch { return Vector4.One; }
    }
}
