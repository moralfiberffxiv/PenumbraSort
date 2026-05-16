using System;
using System.Collections.Generic;
using System.Linq;

namespace PenumbraSort;

public class TagManager
{
    private readonly Configuration _config;

    // ── Keyword tables ────────────────────────────────────────────────────────
    // SWIMWEAR is its own category now — not Underwear.
    // Bikini = swimwear. Lingerie/bra = underwear.

    private static readonly Dictionary<string, string> ClothingKeywords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Tops
        ["shirt"] = "Tops", ["blouse"] = "Tops", ["top"] = "Tops",
        ["sweater"] = "Tops", ["hoodie"] = "Tops", ["turtleneck"] = "Tops",
        ["tanktop"] = "Tops", ["tank"] = "Tops", ["crop"] = "Tops",
        // Bottoms
        ["pants"] = "Bottoms", ["trouser"] = "Bottoms", ["shorts"] = "Bottoms",
        ["skirt"] = "Bottoms", ["legging"] = "Bottoms", ["jeans"] = "Bottoms",
        ["chaps"] = "Bottoms",
        // Dresses / full-body
        ["dress"] = "Dresses", ["gown"] = "Dresses", ["robe"] = "Dresses",
        ["yukata"] = "Dresses", ["kimono"] = "Dresses", ["qipao"] = "Dresses",
        ["jumpsuit"] = "Dresses", ["bodysuit"] = "Dresses",
        // Outerwear
        ["jacket"] = "Outerwear", ["coat"] = "Outerwear", ["cape"] = "Outerwear",
        ["cloak"] = "Outerwear", ["poncho"] = "Outerwear", ["blazer"] = "Outerwear",
        // Footwear
        ["boots"] = "Footwear", ["shoes"] = "Footwear", ["heels"] = "Footwear",
        ["sandal"] = "Footwear", ["socks"] = "Footwear", ["sneaker"] = "Footwear",
        ["stiletto"] = "Footwear", ["loafer"] = "Footwear", ["pump"] = "Footwear",
        // Accessories (non-jewelry)
        ["gloves"] = "Accessories", ["scarf"] = "Accessories", ["belt"] = "Accessories",
        ["bag"] = "Accessories", ["purse"] = "Accessories", ["glasses"] = "Accessories",
        ["mask"] = "Accessories", ["wings"] = "Accessories", ["tail"] = "Accessories",
        // Jewelry
        ["ring"] = "Jewelry", ["necklace"] = "Jewelry", ["earring"] = "Jewelry",
        ["bracelet"] = "Jewelry", ["anklet"] = "Jewelry", ["choker"] = "Jewelry",
        ["tiara"] = "Jewelry", ["pendant"] = "Jewelry",
        // Neck & Waist
        ["collar"] = "NeckWaist", ["sash"] = "NeckWaist", ["obi"] = "NeckWaist",
        ["corset"] = "NeckWaist", ["waistband"] = "NeckWaist",
        // Headwear
        ["hat"] = "Headwear", ["helmet"] = "Headwear", ["cap"] = "Headwear",
        ["crown"] = "Headwear", ["hood"] = "Headwear", ["hairpin"] = "Headwear",
        ["veil"] = "Headwear", ["beret"] = "Headwear", ["tiara"] = "Headwear",
        ["horns"] = "Headwear", ["ears"] = "Headwear",
        // Armor
        ["armor"] = "Armor", ["mail"] = "Armor", ["plate"] = "Armor",
        ["gauntlet"] = "Armor", ["greave"] = "Armor", ["pauldron"] = "Armor",
        ["shield"] = "Armor", ["chainmail"] = "Armor",
        // Costumes
        ["costume"] = "Costumes", ["uniform"] = "Costumes", ["outfit"] = "Costumes",
        ["set"] = "Costumes", ["suit"] = "Costumes",
        // SWIMWEAR — intentionally separate from underwear
        ["swimsuit"] = "Swimwear", ["swimwear"] = "Swimwear", ["bikini"] = "Swimwear",
        ["monokini"] = "Swimwear", ["tankini"] = "Swimwear", ["wetsuit"] = "Swimwear",
        ["swim"] = "Swimwear",
        // UNDERWEAR — intimate only, not swimwear
        ["bra"] = "Underwear", ["lingerie"] = "Underwear", ["panties"] = "Underwear",
        ["thong"] = "Underwear", ["boyshort"] = "Underwear",
    };

    private static readonly Dictionary<string, string> SeasonKeywords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["spring"] = "Spring", ["sakura"] = "Spring", ["floral"] = "Spring",
        ["bloom"] = "Spring", ["cherry"] = "Spring",
        ["summer"] = "Summer", ["tropical"] = "Summer", ["sun"] = "Summer",
        ["tanning"] = "Summer",
        ["autumn"] = "Autumn", ["fall"] = "Autumn", ["harvest"] = "Autumn",
        ["maple"] = "Autumn", ["pumpkin"] = "Autumn",
        ["winter"] = "Winter", ["snow"] = "Winter", ["christmas"] = "Winter",
        ["festive"] = "Winter", ["holiday"] = "Winter", ["yule"] = "Winter",
        ["cozy"] = "Winter",
    };

    private static readonly Dictionary<string, string> OccasionKeywords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["casual"] = "Casual", ["everyday"] = "Casual", ["streetwear"] = "Casual",
        ["formal"] = "Formal", ["elegant"] = "Formal", ["gala"] = "Formal",
        ["business"] = "Formal", ["office"] = "Formal",
        ["combat"] = "Combat", ["battle"] = "Combat", ["warrior"] = "Combat",
        ["tactical"] = "Combat", ["military"] = "Combat",
        ["festival"] = "Festival", ["carnival"] = "Festival",
        ["parade"] = "Festival", ["matsuri"] = "Festival",
        ["evening"] = "Evening", ["night"] = "Evening", ["cocktail"] = "Evening",
        ["gown"] = "Evening", ["dinner"] = "Evening",
        ["beach"] = "Beach", ["resort"] = "Beach", ["poolside"] = "Beach",
        ["fantasy"] = "Fantasy", ["magical"] = "Fantasy", ["witch"] = "Fantasy",
        ["fairy"] = "Fantasy", ["elf"] = "Fantasy", ["dragon"] = "Fantasy",
        ["wedding"] = "Wedding", ["bride"] = "Wedding", ["bridal"] = "Wedding",
        ["lounge"] = "Lounge", ["pajama"] = "Lounge", ["sleepwear"] = "Lounge",
        ["comfy"] = "Lounge", ["homewear"] = "Lounge",
        ["cultural"] = "Cultural", ["traditional"] = "Cultural",
        ["japanese"] = "Cultural", ["chinese"] = "Cultural", ["korean"] = "Cultural",
    };

    private static readonly Dictionary<string, string> RaceKeywords =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["miqote"] = "Miqote", ["miqo"] = "Miqote", ["miqo'te"] = "Miqote",
        ["elezen"] = "Elezen",
        ["viera"] = "Viera", ["bunny"] = "Viera",
        ["hrothgar"] = "Hrothgar", ["ronso"] = "Hrothgar",
        ["roegadyn"] = "Roegadyn", ["roe"] = "Roegadyn",
        ["hyur"] = "Hyur", ["human"] = "Hyur",
        ["aura"] = "AuRa", ["au ra"] = "AuRa", ["auri"] = "AuRa",
        ["lalafell"] = "Lalafell", ["lala"] = "Lalafell",
    };

    public TagManager(Configuration config) => _config = config;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ApplyTags(List<ModEntry> mods)
    {
        foreach (var mod in mods)
        {
            if (_config.ModTags.TryGetValue(mod.DirectoryName, out var saved))
            {
                mod.ClothingTags = saved.Where(IsClothingTag).ToList();
                mod.SeasonTags   = saved.Where(IsSeasonTag).ToList();
                mod.OccasionTags = saved.Where(IsOccasionTag).ToList();
                mod.RaceTags     = saved.Where(IsRaceTag).ToList();
                mod.CustomTags   = saved.Where(t =>
                    !IsClothingTag(t) && !IsSeasonTag(t) && !IsOccasionTag(t) && !IsRaceTag(t)
                ).ToList();
                continue; // user has manually tagged — don't overwrite
            }

            // Auto-detect from name + description
            var text = $"{mod.Name} {mod.Description}";
            AutoDetect(text, ClothingKeywords, mod.ClothingTags);
            AutoDetect(text, SeasonKeywords,   mod.SeasonTags);
            AutoDetect(text, OccasionKeywords, mod.OccasionTags);
            AutoDetect(text, RaceKeywords,     mod.RaceTags);
        }
    }

    public void SaveTags(ModEntry mod)
    {
        var all = mod.ClothingTags
            .Concat(mod.SeasonTags)
            .Concat(mod.OccasionTags)
            .Concat(mod.RaceTags)
            .Concat(mod.CustomTags)
            .Distinct().ToList();
        _config.ModTags[mod.DirectoryName] = all;
        _config.Save();
    }

    public void SaveAllTags(List<ModEntry> mods)
    {
        foreach (var m in mods) SaveTags(m);
    }

    /// <summary>Applies an approved AI suggestion to a mod's tags.</summary>
    public void ApplyAiSuggestion(ModEntry mod, AiSuggestion suggestion)
    {
        // AI never overwrites existing manual tags — only fills empty categories
        if (!mod.ClothingTags.Any()) mod.ClothingTags = suggestion.ClothingTags.ToList();
        if (!mod.SeasonTags.Any())   mod.SeasonTags   = suggestion.SeasonTags.ToList();
        if (!mod.OccasionTags.Any()) mod.OccasionTags = suggestion.OccasionTags.ToList();
        if (!mod.RaceTags.Any())     mod.RaceTags     = suggestion.RaceTags.ToList();
        mod.PendingSuggestion = null;
        SaveTags(mod);
    }

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

        var dict = new Dictionary<string, List<ModEntry>>();
        foreach (var mod in mods)
            foreach (var key in keySelector(mod))
            {
                if (!dict.ContainsKey(key)) dict[key] = new();
                dict[key].Add(mod);
            }

        var tagList = mode switch
        {
            SortMode.ClothingType => DefaultTags.ClothingTypes,
            SortMode.Season       => DefaultTags.Seasons,
            SortMode.Occasion     => DefaultTags.Occasions,
            _                     => new List<TagCategory>()
        };

        var groups = new List<SortGroup>();
        foreach (var cat in tagList)
        {
            if (!dict.TryGetValue(cat.Key, out var catMods)) continue;
            groups.Add(new SortGroup
            {
                GroupName    = cat.Display,
                GroupColor   = cat.Color,
                GroupIcon    = cat.Icon,
                FolderTarget = cat.Key,
                Mods = (ascending ? catMods.OrderBy(m => m.Name)
                                  : catMods.OrderByDescending(m => m.Name)).ToList()
            });
            dict.Remove(cat.Key);
        }

        foreach (var (key, remaining) in dict.OrderBy(x => x.Key))
            groups.Add(new SortGroup
            {
                GroupName    = key,
                GroupColor   = "#AAAAAA",
                GroupIcon    = "📦",
                FolderTarget = key,
                Mods = (ascending ? remaining.OrderBy(m => m.Name)
                                  : remaining.OrderByDescending(m => m.Name)).ToList()
            });

        return ascending ? groups : Enumerable.Reverse(groups).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AutoDetect(string text, Dictionary<string, string> kw, List<string> target)
    {
        foreach (var (keyword, tag) in kw)
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                if (!target.Contains(tag)) target.Add(tag);
    }

    public static bool IsClothingTag(string t) => DefaultTags.ClothingTypes.Any(c => c.Key == t);
    public static bool IsSeasonTag(string t)   => DefaultTags.Seasons.Any(c => c.Key == t);
    public static bool IsOccasionTag(string t) => DefaultTags.Occasions.Any(c => c.Key == t);
    public static bool IsRaceTag(string t)     => DefaultTags.Races.Any(c => c.Key == t);
}
