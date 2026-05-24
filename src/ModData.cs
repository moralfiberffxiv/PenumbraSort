using System.Collections.Generic;
using System.Linq;

namespace PenumbraSort;

// ── Tag Category Definitions ──────────────────────────────────────────────────

public static class DefaultTags
{
    public static readonly List<TagCategory> ClothingTypes = new()
    {
        new("👕 Tops",         "Tops",       "👕", "#5B9BD5"),
        new("👖 Bottoms",      "Bottoms",    "👖", "#70AD47"),
        new("👗 Dresses",      "Dresses",    "👗", "#9B59B6"),
        new("🧥 Outerwear",    "Outerwear",  "🧥", "#A0522D"),
        new("👠 Footwear",     "Footwear",   "👠", "#E74C3C"),
        new("🧤 Accessories",  "Accessories","🧤", "#F1C40F"),
        new("🎭 Costumes",     "Costumes",   "🎭", "#E67E22"),
        new("⚔ Armor",        "Armor",      "⚔",  "#95A5A6"),
        new("🩱 Swimwear",     "Swimwear",   "🩱", "#00BCD4"),
        new("🩲 Underwear",    "Underwear",  "🩲", "#FF69B4"),
        new("🎩 Headwear",     "Headwear",   "🎩", "#8B4513"),
        new("💍 Jewelry",      "Jewelry",    "💍", "#B39DDB"),
        new("🧣 Neck & Waist", "NeckWaist",  "🧣", "#C0392B"),
        new("🧑 Full Body",    "FullBody",   "🧑", "#607D8B"),
        new("👘 Traditional",  "Traditional","👘", "#E91E63"),
    };

    public static readonly List<TagCategory> Seasons = new()
    {
        new("🌸 Spring",     "Spring",    "🌸", "#FFB7C5"),
        new("☀ Summer",     "Summer",    "☀",  "#FFD700"),
        new("🍂 Autumn",     "Autumn",    "🍂", "#FF8C00"),
        new("❄ Winter",     "Winter",    "❄",  "#ADD8E6"),
        new("🌈 All Season", "AllSeason", "🌈", "#98FB98"),
    };

    public static readonly List<TagCategory> Occasions = new()
    {
        new("🎉 Casual",     "Casual",   "🎉", "#3CB371"),
        new("💼 Formal",     "Formal",   "💼", "#708090"),
        new("⚔ Combat",     "Combat",   "⚔",  "#DC143C"),
        new("🎊 Festival",   "Festival", "🎊", "#FF6347"),
        new("🌙 Evening",    "Evening",  "🌙", "#6A5ACD"),
        new("🏖 Beach",      "Beach",    "🏖", "#00CED1"),
        new("🎮 Fantasy",    "Fantasy",  "🎮", "#8A2BE2"),
        new("💍 Wedding",    "Wedding",  "💍", "#DAA520"),
        new("🏠 Loungewear", "Lounge",   "🏠", "#8BC34A"),
        new("🎌 Cultural",   "Cultural", "🎌", "#E91E63"),
        new("🏃 Sporty",     "Sporty",   "🏃", "#29B6F6"),
        new("🌊 Resort",     "Resort",   "🌊", "#26C6DA"),
    };

    public static readonly List<TagCategory> Races = new()
    {
        new("🐱 Miqo'te",   "Miqote",   "🐱", "#FF8A65"),
        new("🧝 Elezen",    "Elezen",   "🧝", "#81C784"),
        new("🐰 Viera",     "Viera",    "🐰", "#F48FB1"),
        new("🦁 Hrothgar",  "Hrothgar", "🦁", "#FFB74D"),
        new("⚙ Roegadyn",  "Roegadyn", "⚙",  "#90A4AE"),
        new("👤 Hyur",      "Hyur",     "👤", "#A5D6A7"),
        new("🌟 Au Ra",     "AuRa",     "🌟", "#80DEEA"),
        new("🌸 Lalafell",  "Lalafell", "🌸", "#FFF176"),
        new("👥 All Races", "AllRaces", "👥", "#B0BEC5"),
    };

    public static IEnumerable<TagCategory> All =>
        ClothingTypes.Concat(Seasons).Concat(Occasions).Concat(Races);
}

public record TagCategory(string Display, string Key, string Icon, string Color);


// ── AI / Pattern Suggestion ───────────────────────────────────────────────────

/// <summary>A tag suggestion from either local pattern matching or AI API.</summary>
public class AiSuggestion
{
    public string       ModDirectoryName { get; set; } = string.Empty;
    public List<string> ClothingTags     { get; set; } = new();
    public List<string> SeasonTags       { get; set; } = new();
    public List<string> OccasionTags     { get; set; } = new();
    public List<string> RaceTags         { get; set; } = new();
    public float        Confidence       { get; set; }
    public string       Reasoning        { get; set; } = string.Empty;
    public bool         IsApproved       { get; set; }
    public bool         IsRejected       { get; set; }
}

// ── Penumbra Snapshot for Revert ──────────────────────────────────────────────

public class PenumbraSnapshot
{
    public string TakenAt         { get; set; } = string.Empty;
    public string Description     { get; set; } = string.Empty;
    public Dictionary<string, string> ModPaths { get; set; } = new();
}

// ── Mod Entry ─────────────────────────────────────────────────────────────────

public class ModEntry
{
    public string Name          { get; set; } = string.Empty;
    public string DirectoryName { get; set; } = string.Empty;
    public bool   IsEnabled     { get; set; }
    public string Author        { get; set; } = string.Empty;
    public string Version       { get; set; } = string.Empty;
    public string Description   { get; set; } = string.Empty;

    // Preview image state
    public string  LocalPreviewPath  { get; set; } = string.Empty; // disk path to preview.png etc
    public string  HelioUuid         { get; set; } = string.Empty; // Heliosphere mod UUID if present
    public string  CachedImagePath   { get; set; } = string.Empty; // downloaded web image on disk



    public List<string> ClothingTags { get; set; } = new();
    public List<string> SeasonTags   { get; set; } = new();
    public List<string> OccasionTags { get; set; } = new();
    public List<string> RaceTags     { get; set; } = new();
    public List<string> CustomTags   { get; set; } = new();

    // Pending pattern-match suggestion (null = none)
    public AiSuggestion? PendingSuggestion { get; set; }

    // Whether tags came from auto-detection (unconfirmed) vs manual save
    public bool TagsConfirmed { get; set; }

    public bool HasManualTags => ClothingTags.Any() || SeasonTags.Any() || OccasionTags.Any();
    public bool HasAnyTags    => HasManualTags || RaceTags.Any() || CustomTags.Any();
    public bool IsNewlyAdded  { get; set; } // flagged by live-watch

    public string PrimaryClothingTag => ClothingTags.FirstOrDefault() ?? "Untagged";
    public string PrimarySeasonTag   => SeasonTags.FirstOrDefault()   ?? "Untagged";
    public string PrimaryOccasionTag => OccasionTags.FirstOrDefault() ?? "Untagged";

    public List<string> AllTags => ClothingTags
        .Concat(SeasonTags).Concat(OccasionTags)
        .Concat(RaceTags).Concat(CustomTags)
        .Distinct().ToList();

    /// <summary>
    /// Builds the Penumbra folder path from primary tags.
    /// Format: Clothing/Season/Occasion  (omits empty levels)
    /// </summary>
    public string BuildFolderPath()
    {
        var parts = new List<string>();
        if (ClothingTags.Any())  parts.Add(ClothingTags[0]);
        if (SeasonTags.Any())    parts.Add(SeasonTags[0]);
        if (OccasionTags.Any())  parts.Add(OccasionTags[0]);
        return parts.Any() ? string.Join("/", parts) : "Unsorted";
    }
}

// ── Sort Result Group ─────────────────────────────────────────────────────────

public class SortGroup
{
    public string GroupName    { get; set; } = string.Empty;
    public string GroupColor   { get; set; } = "#FFFFFF";
    public string GroupIcon    { get; set; } = "📦";
    public string FolderTarget { get; set; } = string.Empty;
    public List<ModEntry> Mods { get; set; } = new();
}
