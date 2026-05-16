using System;
using System.Collections.Generic;
using System.Linq;

namespace PenumbraSort;

/// <summary>
/// Local pattern-matching tagger. No internet required.
/// Uses weighted keyword matching across mod name, author, and description
/// to produce tag suggestions with confidence scores and human-readable reasoning.
/// </summary>
public static class LocalPatternTagger
{
    // ── Weighted keyword tables ───────────────────────────────────────────────
    // Each entry: keyword -> (tag, weight)
    // Weight 3 = strong signal (exact category word)
    // Weight 2 = good signal (closely related word)
    // Weight 1 = weak signal (contextual hint)

    private static readonly List<(string Keyword, string Tag, int Weight)> ClothingRules = new()
    {
        // Tops
        ("shirt",       "Tops", 3), ("blouse",    "Tops", 3), ("top",       "Tops", 2),
        ("sweater",     "Tops", 3), ("hoodie",    "Tops", 3), ("turtleneck","Tops", 3),
        ("tanktop",     "Tops", 3), ("tank top",  "Tops", 3), ("crop top",  "Tops", 3),
        ("crop",        "Tops", 2), ("cardigan",  "Tops", 3), ("polo",      "Tops", 3),
        ("jersey",      "Tops", 2), ("tube top",  "Tops", 3),

        // Bottoms
        ("pants",       "Bottoms", 3), ("trousers",  "Bottoms", 3), ("shorts",   "Bottoms", 3),
        ("skirt",       "Bottoms", 3), ("leggings",  "Bottoms", 3), ("jeans",    "Bottoms", 3),
        ("chaps",       "Bottoms", 3), ("culottes",  "Bottoms", 3), ("miniskirt","Bottoms", 3),
        ("hotpants",    "Bottoms", 3), ("legging",   "Bottoms", 2),

        // Dresses / Full body
        ("dress",       "Dresses", 3), ("gown",      "Dresses", 3), ("robe",     "Dresses", 2),
        ("yukata",      "Dresses", 3), ("kimono",    "Dresses", 3), ("qipao",    "Dresses", 3),
        ("cheongsam",   "Dresses", 3), ("jumpsuit",  "Dresses", 2), ("bodysuit", "Dresses", 2),
        ("sundress",    "Dresses", 3), ("minidress", "Dresses", 3), ("maxi",     "Dresses", 2),

        // Outerwear
        ("jacket",      "Outerwear", 3), ("coat",     "Outerwear", 3), ("cape",    "Outerwear", 3),
        ("cloak",       "Outerwear", 3), ("poncho",   "Outerwear", 3), ("blazer",  "Outerwear", 3),
        ("trench",      "Outerwear", 2), ("parka",    "Outerwear", 3), ("vest",    "Outerwear", 2),
        ("overcoat",    "Outerwear", 3), ("windbreaker","Outerwear",3),

        // Footwear
        ("boots",       "Footwear", 3), ("shoes",    "Footwear", 3), ("heels",   "Footwear", 3),
        ("sandals",     "Footwear", 3), ("socks",    "Footwear", 3), ("sneakers","Footwear", 3),
        ("stilettos",   "Footwear", 3), ("loafers",  "Footwear", 3), ("pumps",   "Footwear", 2),
        ("flats",       "Footwear", 2), ("mules",    "Footwear", 3), ("wedges",  "Footwear", 3),
        ("thigh high",  "Footwear", 2), ("stockings","Footwear", 2),

        // Swimwear — intentionally separated from underwear
        ("swimsuit",    "Swimwear", 3), ("swimwear",  "Swimwear", 3), ("bikini",   "Swimwear", 3),
        ("monokini",    "Swimwear", 3), ("tankini",   "Swimwear", 3), ("wetsuit",  "Swimwear", 3),
        ("swim",        "Swimwear", 2), ("one piece", "Swimwear", 2), ("bathing suit","Swimwear",3),

        // Underwear — intimate only
        ("bra",         "Underwear", 3), ("lingerie",  "Underwear", 3), ("panties",  "Underwear", 3),
        ("thong",       "Underwear", 3), ("boyshorts", "Underwear", 3), ("corset",   "Underwear", 2),
        ("teddy",       "Underwear", 2), ("bustier",   "Underwear", 3),

        // Accessories
        ("gloves",      "Accessories", 3), ("scarf",    "Accessories", 3), ("belt",    "Accessories", 3),
        ("bag",         "Accessories", 2), ("purse",    "Accessories", 3), ("glasses", "Accessories", 3),
        ("sunglasses",  "Accessories", 3), ("mask",     "Accessories", 2), ("wings",   "Accessories", 2),
        ("tail",        "Accessories", 2), ("fan",      "Accessories", 2), ("parasol", "Accessories", 3),

        // Jewelry
        ("ring",        "Jewelry", 3), ("necklace",  "Jewelry", 3), ("earring",  "Jewelry", 3),
        ("bracelet",    "Jewelry", 3), ("anklet",    "Jewelry", 3), ("choker",   "Jewelry", 3),
        ("tiara",       "Jewelry", 3), ("pendant",   "Jewelry", 3), ("brooch",   "Jewelry", 3),
        ("cuff",        "Jewelry", 2), ("chain",     "Jewelry", 2),

        // Headwear
        ("hat",         "Headwear", 3), ("helmet",   "Headwear", 3), ("cap",     "Headwear", 3),
        ("crown",       "Headwear", 3), ("hood",     "Headwear", 2), ("hairpin", "Headwear", 3),
        ("veil",        "Headwear", 3), ("beret",    "Headwear", 3), ("horns",   "Headwear", 2),
        ("ears",        "Headwear", 1), ("headband", "Headwear", 3), ("fascinator","Headwear",3),
        ("hair clip",   "Headwear", 3), ("headdress","Headwear", 3),

        // Neck & Waist
        ("collar",      "NeckWaist", 3), ("sash",     "NeckWaist", 3), ("obi",    "NeckWaist", 3),
        ("waistband",   "NeckWaist", 3), ("cummerbund","NeckWaist",3),

        // Armor
        ("armor",       "Armor", 3), ("mail",      "Armor", 2), ("plate",    "Armor", 2),
        ("gauntlet",    "Armor", 3), ("greave",    "Armor", 3), ("pauldron", "Armor", 3),
        ("chainmail",   "Armor", 3), ("breastplate","Armor",3), ("vambraces","Armor", 3),

        // Traditional / Cultural
        ("kimono",      "Traditional", 3), ("yukata",   "Traditional", 3), ("hanfu",   "Traditional", 3),
        ("qipao",       "Traditional", 3), ("cheongsam","Traditional", 3), ("haori",   "Traditional", 3),
        ("hakama",      "Traditional", 3), ("hanbok",   "Traditional", 3),

        // Costumes
        ("costume",     "Costumes", 3), ("uniform",  "Costumes", 3), ("outfit",  "Costumes", 2),
        ("cosplay",     "Costumes", 3), ("suit",     "Costumes", 1),
    };

    private static readonly List<(string Keyword, string Tag, int Weight)> SeasonRules = new()
    {
        ("spring",      "Spring", 3), ("sakura",   "Spring", 3), ("floral",   "Spring", 2),
        ("bloom",       "Spring", 2), ("cherry blossom","Spring",3),("pastel", "Spring", 1),
        ("summer",      "Summer", 3), ("tropical", "Summer", 3), ("sun",      "Summer", 1),
        ("tanning",     "Summer", 2), ("sunbather","Summer", 2), ("surf",     "Summer", 2),
        ("autumn",      "Autumn", 3), ("fall",     "Autumn", 2), ("harvest",  "Autumn", 2),
        ("maple",       "Autumn", 2), ("pumpkin",  "Autumn", 3), ("russet",   "Autumn", 2),
        ("winter",      "Winter", 3), ("snow",     "Winter", 2), ("christmas","Winter", 3),
        ("festive",     "Winter", 2), ("yule",     "Winter", 3), ("cozy",     "Winter", 2),
        ("holiday",     "Winter", 2), ("frost",    "Winter", 2),
    };

    private static readonly List<(string Keyword, string Tag, int Weight)> OccasionRules = new()
    {
        ("casual",      "Casual", 3), ("everyday", "Casual", 3), ("streetwear","Casual",3),
        ("daily",       "Casual", 2), ("comfy",    "Casual", 2),
        ("formal",      "Formal", 3), ("elegant",  "Formal", 3), ("gala",     "Formal", 3),
        ("business",    "Formal", 3), ("office",   "Formal", 3), ("professional","Formal",3),
        ("combat",      "Combat", 3), ("battle",   "Combat", 3), ("warrior",  "Combat", 2),
        ("tactical",    "Combat", 3), ("military", "Combat", 3), ("ranger",   "Combat", 2),
        ("festival",    "Festival",3), ("carnival","Festival",3), ("parade",  "Festival",2),
        ("matsuri",     "Festival",3), ("fair",    "Festival",2),
        ("evening",     "Evening", 3), ("night",   "Evening", 2), ("cocktail","Evening", 3),
        ("dinner",      "Evening", 2), ("party",   "Evening", 1),
        ("beach",       "Beach",   3), ("poolside","Beach",   3), ("resort",  "Beach",   2),
        ("vacation",    "Beach",   1), ("holiday", "Beach",   1),
        ("fantasy",     "Fantasy", 3), ("magical", "Fantasy", 2), ("witch",   "Fantasy", 3),
        ("fairy",       "Fantasy", 3), ("dragon",  "Fantasy", 3), ("mage",    "Fantasy", 3),
        ("wizard",      "Fantasy", 3), ("mystic",  "Fantasy", 2),
        ("wedding",     "Wedding", 3), ("bride",   "Wedding", 3), ("bridal",  "Wedding", 3),
        ("matrimony",   "Wedding", 2),
        ("lounge",      "Lounge",  3), ("pajama",  "Lounge",  3), ("sleepwear","Lounge", 3),
        ("homewear",    "Lounge",  3), ("lounging","Lounge",  2),
        ("cultural",    "Cultural",3), ("traditional","Cultural",3),
        ("japanese",    "Cultural",2), ("chinese", "Cultural",2), ("korean",  "Cultural",2),
        ("sporty",      "Sporty",  3), ("athletic","Sporty",  3), ("gym",     "Sporty",  3),
        ("workout",     "Sporty",  3), ("yoga",    "Sporty",  3), ("sport",   "Sporty",  2),
        ("resort",      "Resort",  3), ("cruise",  "Resort",  3), ("tropical","Resort",  2),
    };

    private static readonly List<(string Keyword, string Tag, int Weight)> RaceRules = new()
    {
        ("miqote",      "Miqote",  3), ("miqo",    "Miqote",  2), ("miqo'te", "Miqote",  3),
        ("elezen",      "Elezen",  3),
        ("viera",       "Viera",   3), ("bunny girl","Viera",  3), ("v'iera",  "Viera",   3),
        ("hrothgar",    "Hrothgar",3), ("ronso",   "Hrothgar",2),
        ("roegadyn",    "Roegadyn",3), ("roe",     "Roegadyn",1),
        ("hyur",        "Hyur",    3),
        ("au ra",       "AuRa",    3), ("aura",    "AuRa",    2), ("auri",    "AuRa",    3),
        ("raen",        "AuRa",    2), ("xaela",   "AuRa",    2),
        ("lalafell",    "Lalafell",3), ("lala",    "Lalafell",2), ("dunesfolk","Lalafell",2),
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Produces a tag suggestion for a single mod using local pattern matching.
    /// Returns null if no tags found with sufficient confidence.
    /// </summary>
    public static AiSuggestion? Suggest(ModEntry mod)
    {
        // Build search text: name is most important (weight x2), description secondary
        var name  = mod.Name.ToLowerInvariant();
        var desc  = mod.Description.ToLowerInvariant();
        var auth  = mod.Author.ToLowerInvariant();

        var clothing  = ScoreTags(name, desc, ClothingRules,  nameMultiplier: 2);
        var seasons   = ScoreTags(name, desc, SeasonRules,    nameMultiplier: 2);
        var occasions = ScoreTags(name, desc, OccasionRules,  nameMultiplier: 2);
        var races     = ScoreTags(name, desc, RaceRules,      nameMultiplier: 3);

        // Only keep tags above a minimum score threshold
        const int MinScore = 2;
        var topClothing  = clothing.Where(x => x.Score >= MinScore).OrderByDescending(x => x.Score).Take(2).Select(x => x.Tag).ToList();
        var topSeasons   = seasons.Where(x => x.Score >= MinScore).OrderByDescending(x => x.Score).Take(2).Select(x => x.Tag).ToList();
        var topOccasions = occasions.Where(x => x.Score >= MinScore).OrderByDescending(x => x.Score).Take(2).Select(x => x.Tag).ToList();
        var topRaces     = races.Where(x => x.Score >= MinScore).OrderByDescending(x => x.Score).Take(1).Select(x => x.Tag).ToList();

        // Nothing found
        if (!topClothing.Any() && !topSeasons.Any() && !topOccasions.Any() && !topRaces.Any())
            return null;

        // Confidence = ratio of matched categories (0.25 per category found, max 1.0)
        float confidence = Math.Min(1.0f,
            (topClothing.Any()  ? 0.35f : 0f) +
            (topSeasons.Any()   ? 0.25f : 0f) +
            (topOccasions.Any() ? 0.25f : 0f) +
            (topRaces.Any()     ? 0.15f : 0f));

        // Build reasoning string
        var reasons = new List<string>();
        if (topClothing.Any())
            reasons.Add($"clothing: {string.Join(", ", topClothing)}");
        if (topSeasons.Any())
            reasons.Add($"season: {string.Join(", ", topSeasons)}");
        if (topOccasions.Any())
            reasons.Add($"occasion: {string.Join(", ", topOccasions)}");
        if (topRaces.Any())
            reasons.Add($"race: {string.Join(", ", topRaces)}");

        return new AiSuggestion
        {
            ModDirectoryName = mod.DirectoryName,
            ClothingTags     = topClothing,
            SeasonTags       = topSeasons,
            OccasionTags     = topOccasions,
            RaceTags         = topRaces,
            Confidence       = confidence,
            Reasoning        = $"Detected from name: {string.Join(" | ", reasons)}",
        };
    }

    /// <summary>Runs Suggest on all mods that have no confirmed tags, in bulk.</summary>
    public static List<AiSuggestion> SuggestAll(List<ModEntry> mods)
    {
        var results = new List<AiSuggestion>();
        foreach (var mod in mods)
        {
            if (mod.HasManualTags) continue; // skip already-tagged
            var sug = Suggest(mod);
            if (sug != null) results.Add(sug);
        }
        return results;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private record TagScore(string Tag, int Score);

    private static List<TagScore> ScoreTags(
        string name,
        string desc,
        List<(string Keyword, string Tag, int Weight)> rules,
        int nameMultiplier = 2)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (keyword, tag, weight) in rules)
        {
            // Name match scores higher than description match
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                scores.TryGetValue(tag, out var cur);
                scores[tag] = cur + weight * nameMultiplier;
            }
            else if (desc.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                scores.TryGetValue(tag, out var cur);
                scores[tag] = cur + weight;
            }
        }

        return scores.Select(kv => new TagScore(kv.Key, kv.Value)).ToList();
    }
}
