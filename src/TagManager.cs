using System;
using System.Collections.Generic;
using System.Linq;

namespace PenumbraSort;

/// <summary>
/// Manages mod tag assignments with auto-detection heuristics and user overrides.
/// </summary>
public class TagManager
{
    private readonly Configuration _config;

    // Simple keyword → tag mappings for auto-detection from mod name
    private static readonly Dictionary<string, string> ClothingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shirt"]    = "Tops",   ["blouse"]   = "Tops",   ["top"]      = "Tops",
        ["sweater"]  = "Tops",   ["hoodie"]   = "Tops",   ["jacket"]   = "Outerwear",
        ["coat"]     = "Outerwear", ["cape"]  = "Outerwear",
        ["pants"]    = "Bottoms", ["trouser"] = "Bottoms", ["shorts"]  = "Bottoms",
        ["skirt"]    = "Bottoms", ["legging"] = "Bottoms",
        ["dress"]    = "Dresses", ["gown"]    = "Dresses", ["robe"]    = "Dresses",
        ["boots"]    = "Footwear", ["shoes"]  = "Footwear", ["heels"]  = "Footwear",
        ["sandal"]   = "Footwear", ["socks"]  = "Footwear",
        ["gloves"]   = "Accessories", ["scarf"] = "Accessories", ["belt"] = "Accessories",
        ["ring"]     = "Accessories", ["necklace"] = "Accessories", ["earring"] = "Accessories",
        ["hat"]      = "Headwear", ["helmet"]  = "Headwear", ["cap"]   = "Headwear",
        ["crown"]    = "Headwear", ["hood"]    = "Headwear",
        ["bra"]      = "Underwear", ["lingerie"] = "Underwear", ["bikini"] = "Underwear",
        ["armor"]    = "Armor",   ["mail"]    = "Armor",   ["plate"]   = "Armor",
        ["swimsuit"] = "Underwear",
        ["costume"]  = "Costumes", ["uniform"] = "Costumes", ["outfit"] = "Costumes",
    };

    private static readonly Dictionary<string, string> SeasonKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spring"]  = "Spring", ["sakura"] = "Spring", ["floral"] = "Spring",
        ["summer"]  = "Summer", ["beach"]  = "Summer", ["tropical"] = "Summer", ["sun"] = "Summer",
        ["autumn"]  = "Autumn", ["fall"]   = "Autumn", ["harvest"] = "Autumn",
        ["winter"]  = "Winter", ["snow"]   = "Winter", ["christmas"] = "Winter", ["festive"] = "Winter",
        ["holiday"] = "Winter",
    };

    private static readonly Dictionary<string, string> OccasionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["casual"]   = "Casual",   ["everyday"] = "Casual",
        ["formal"]   = "Formal",   ["elegant"]  = "Formal",  ["gala"]    = "Formal",
        ["combat"]   = "Combat",   ["battle"]   = "Combat",  ["warrior"] = "Combat",
        ["festival"] = "Festival", ["carnival"] = "Festival", ["miqo"]   = "Festival",
        ["evening"]  = "Evening",  ["night"]    = "Evening",  ["gown"]   = "Evening",
        ["beach"]    = "Beach",    ["swim"]     = "Beach",
        ["fantasy"]  = "Fantasy",  ["magical"]  = "Fantasy",  ["witch"]  = "Fantasy",
        ["wedding"]  = "Wedding",  ["bride"]    = "Wedding",  ["bridal"] = "Wedding",
    };

    public TagManager(Configuration config) => _config = config;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Applies saved tags + auto-detects missing ones for a list of mods.</summary>
    public void ApplyTags(List<ModEntry> mods)
    {
        foreach (var mod in mods)
        {
            // Load saved tags
            if (_config.ModTags.TryGetValue(mod.DirectoryName, out var saved))
            {
                mod.ClothingTags = saved.Where(t => IsClothingTag(t)).ToList();
                mod.SeasonTags   = saved.Where(t => IsSeasonTag(t)).ToList();
                mod.OccasionTags = saved.Where(t => IsOccasionTag(t)).ToList();
                mod.CustomTags   = saved.Where(t => !IsClothingTag(t) && !IsSeasonTag(t) && !IsOccasionTag(t)).ToList();
            }

            // Auto-detect any missing category
            if (!mod.ClothingTags.Any()) AutoDetect(mod.Name, ClothingKeywords, mod.ClothingTags);
            if (!mod.SeasonTags.Any())   AutoDetect(mod.Name, SeasonKeywords,   mod.SeasonTags);
            if (!mod.OccasionTags.Any()) AutoDetect(mod.Name, OccasionKeywords, mod.OccasionTags);
        }
    }

    /// <summary>Persists tag assignments for a single mod.</summary>
    public void SaveTags(ModEntry mod)
    {
        var all = mod.ClothingTags
            .Concat(mod.SeasonTags)
            .Concat(mod.OccasionTags)
            .Concat(mod.CustomTags)
            .Distinct()
            .ToList();

        _config.ModTags[mod.DirectoryName] = all;
        _config.Save();
    }

    /// <summary>Saves ALL mod tags at once (batch).</summary>
    public void SaveAllTags(List<ModEntry> mods)
    {
        foreach (var m in mods) SaveTags(m);
    }

    /// <summary>Groups mods by the current sort mode.</summary>
    public List<SortGroup> GroupMods(List<ModEntry> mods, SortMode mode, bool ascending)
    {
        Func<ModEntry, IEnumerable<string>> keySelector = mode switch
        {
            SortMode.ClothingType => m => m.ClothingTags.Any() ? m.ClothingTags : new[] { "Untagged" },
            SortMode.Season       => m => m.SeasonTags.Any()   ? m.SeasonTags   : new[] { "Untagged" },
            SortMode.Occasion     => m => m.OccasionTags.Any() ? m.OccasionTags : new[] { "Untagged" },
            SortMode.Alphabetical => m => new[] { m.Name[..1].ToUpper() },
            _                     => m => new[] { m.Name[..1].ToUpper() }
        };

        // Build group dictionary (mods can appear in multiple groups if multi-tagged)
        var dict = new Dictionary<string, List<ModEntry>>();
        foreach (var mod in mods)
        {
            foreach (var key in keySelector(mod))
            {
                if (!dict.ContainsKey(key)) dict[key] = new();
                dict[key].Add(mod);
            }
        }

        var tagList = mode switch
        {
            SortMode.ClothingType => DefaultTags.ClothingTypes,
            SortMode.Season       => DefaultTags.Seasons,
            SortMode.Occasion     => DefaultTags.Occasions,
            _                     => new List<TagCategory>()
        };

        var groups = new List<SortGroup>();

        // Add known categories in defined order
        foreach (var cat in tagList)
        {
            if (dict.TryGetValue(cat.Key, out var catMods))
            {
                groups.Add(new SortGroup
                {
                    GroupName  = cat.Display,
                    GroupColor = cat.Color,
                    GroupIcon  = cat.Icon,
                    Mods       = ascending ? catMods.OrderBy(m => m.Name).ToList()
                                           : catMods.OrderByDescending(m => m.Name).ToList()
                });
                dict.Remove(cat.Key);
            }
        }

        // Add remaining (custom tags or alphabetical)
        foreach (var (key, remaining) in dict.OrderBy(x => x.Key))
        {
            groups.Add(new SortGroup
            {
                GroupName  = key,
                GroupColor = "#AAAAAA",
                GroupIcon  = "📦",
                Mods       = ascending ? remaining.OrderBy(m => m.Name).ToList()
                                       : remaining.OrderByDescending(m => m.Name).ToList()
            });
        }

        return ascending ? groups : Enumerable.Reverse(groups).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AutoDetect(string name, Dictionary<string, string> keywords, List<string> target)
    {
        foreach (var (keyword, tag) in keywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                if (!target.Contains(tag)) target.Add(tag);
            }
        }
    }

    private static bool IsClothingTag(string t) => DefaultTags.ClothingTypes.Any(c => c.Key == t);
    private static bool IsSeasonTag(string t)   => DefaultTags.Seasons.Any(c => c.Key == t);
    private static bool IsOccasionTag(string t) => DefaultTags.Occasions.Any(c => c.Key == t);
}
