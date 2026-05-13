using System.Collections.Generic;
using System.Linq;

namespace PenumbraSort;

// ── Tag Category Definitions ─────────────────────────────────────────────────

public static class DefaultTags
{
    public static readonly List<TagCategory> ClothingTypes = new()
    {
        new("👕 Tops",        "Tops",        "🟦", "#5B9BD5"),
        new("👖 Bottoms",     "Bottoms",     "🟩", "#70AD47"),
        new("👗 Dresses",     "Dresses",     "🟪", "#9B59B6"),
        new("🧥 Outerwear",   "Outerwear",   "🟫", "#A0522D"),
        new("👠 Footwear",    "Footwear",    "🟥", "#E74C3C"),
        new("🧤 Accessories", "Accessories", "🟨", "#F1C40F"),
        new("🎭 Costumes",    "Costumes",    "🔶", "#E67E22"),
        new("⚔️ Armor",       "Armor",       "⬜", "#95A5A6"),
        new("🩲 Underwear",   "Underwear",   "🌸", "#FF69B4"),
        new("🎩 Headwear",    "Headwear",    "🟤", "#8B4513"),
    };

    public static readonly List<TagCategory> Seasons = new()
    {
        new("🌸 Spring", "Spring", "🌸", "#FFB7C5"),
        new("☀️ Summer", "Summer", "☀️", "#FFD700"),
        new("🍂 Autumn", "Autumn", "🍂", "#FF8C00"),
        new("❄️ Winter", "Winter", "❄️", "#ADD8E6"),
        new("🌈 All Season", "All Season", "🌈", "#98FB98"),
    };

    public static readonly List<TagCategory> Occasions = new()
    {
        new("🎉 Casual",   "Casual",   "🎉", "#3CB371"),
        new("💼 Formal",   "Formal",   "💼", "#2F4F4F"),
        new("⚔️ Combat",   "Combat",   "⚔️", "#DC143C"),
        new("🎊 Festival", "Festival", "🎊", "#FF6347"),
        new("🌙 Evening",  "Evening",  "🌙", "#191970"),
        new("🏖️ Beach",    "Beach",    "🏖️", "#00CED1"),
        new("🎮 Fantasy",  "Fantasy",  "🎮", "#8A2BE2"),
        new("💍 Wedding",  "Wedding",  "💍", "#FFFACD"),
    };
}

public record TagCategory(string Display, string Key, string Icon, string Color);

// ── Mod Entry ─────────────────────────────────────────────────────────────────

public class ModEntry
{
    public string Name { get; set; } = string.Empty;
    public string DirectoryName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Assigned tags
    public List<string> ClothingTags { get; set; } = new();
    public List<string> SeasonTags   { get; set; } = new();
    public List<string> OccasionTags { get; set; } = new();
    public List<string> CustomTags   { get; set; } = new();

    public string PrimaryClothingTag => ClothingTags.FirstOrDefault() ?? "Untagged";
    public string PrimarySeasonTag   => SeasonTags.FirstOrDefault()   ?? "Untagged";
    public string PrimaryOccasionTag => OccasionTags.FirstOrDefault() ?? "Untagged";

    public string GetSortKey(SortMode mode) => mode switch
    {
        SortMode.ClothingType  => PrimaryClothingTag,
        SortMode.Season        => PrimarySeasonTag,
        SortMode.Occasion      => PrimaryOccasionTag,
        SortMode.Alphabetical  => Name,
        _                      => Name
    };

    public List<string> AllTags => ClothingTags
        .Concat(SeasonTags)
        .Concat(OccasionTags)
        .Concat(CustomTags)
        .Distinct()
        .ToList();
}

// ── Sort Result Group ──────────────────────────────────────────────────────────

public class SortGroup
{
    public string GroupName   { get; set; } = string.Empty;
    public string GroupColor  { get; set; } = "#FFFFFF";
    public string GroupIcon   { get; set; } = "📦";
    public List<ModEntry> Mods { get; set; } = new();
}
