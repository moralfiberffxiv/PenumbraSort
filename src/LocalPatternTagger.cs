using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PenumbraSort;

/// <summary>
/// Local pattern-matching tagger using weighted keyword scoring.
///
/// Design principles:
///   1. Name matches score 3x description matches (names are authoritative)
///   2. Veto rules block clothing tags when the mod is clearly not clothing
///   3. Exclusion rules prevent generic words ("outfit", "set") from over-triggering
///   4. Description is parsed for slot mentions ("replaces chest/legs/feet")
///   5. MinScore threshold prevents single weak-signal matches from tagging
/// </summary>
public static class LocalPatternTagger
{
    // ── Veto patterns — if the mod name matches these, skip clothing tagging ──
    // These are mod *types*, not clothing items. Tagging them as clothing is always wrong.
    private static readonly string[] ClothingVetoPatterns =
    {
        "dance", "animation", "motion", "pose", "emote", "idle",  // animation mods
        "hair", "hairstyle", "haircut", "haido",                   // hair mods
        "face", "facial", "makeup", "freckle", "blush", "tattoo",  // face/skin
        "skin", "body texture", "body mod", "body replace",        // body texture mods
        "eye", "iris", "pupil", "sclera",                          // eye mods
        "horn replacement", "tail replacement",                     // race feature replacements
        "sound", "sfx", "bgm", "music",                            // audio mods
        "ui mod", "hud", "minion", "mount", "housing",             // non-character mods
        "weapon", "sword", "axe", "spear", "staff", "bow",         // weapon mods
        "reshade", "shader", "gshade",                             // graphics mods
    };

    // ── Words that should NOT trigger clothing tags on their own ─────────────
    // These are too generic — they must appear alongside a stronger signal.
    private static readonly HashSet<string> WeakAloneWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "outfit", "set", "collection", "pack", "bundle", "ensemble",
        "recolor", "retexture", "replacement", "port", "conversion",
        "suit",  // "suit" is ambiguous: swimsuit, business suit, armor suit
    };

    // ── Slot keywords in descriptions → direct clothing type ─────────────────
    // Authors write "replaces chest slot" or "body: top/dress" in descriptions.
    private static readonly List<(string Keyword, string Tag)> SlotRules = new()
    {
        ("chest",    "Tops"),    ("body",     "Tops"),
        ("legs",     "Bottoms"), ("thighs",   "Bottoms"),
        ("feet",     "Footwear"),("shoes",    "Footwear"),
        ("hands",    "Accessories"), ("gloves", "Accessories"),
        ("head",     "Headwear"),("hat slot", "Headwear"),
        ("ears",     "Accessories"), ("neck",  "NeckWaist"),
        ("wrist",    "Accessories"), ("ring slot", "Jewelry"),
        ("full body","Dresses"), ("full-body","Dresses"),
    };

    // ── Main keyword tables ───────────────────────────────────────────────────

    private static readonly List<(string Keyword, string Tag, int Weight)> ClothingRules = new()
    {
        // Tops — explicit garment words only
        ("shirt",        "Tops", 3), ("blouse",    "Tops", 3), ("turtleneck","Tops", 3),
        ("sweater",      "Tops", 3), ("hoodie",    "Tops", 3), ("cardigan",  "Tops", 3),
        ("tanktop",      "Tops", 3), ("tank top",  "Tops", 3), ("crop top",  "Tops", 3),
        ("tube top",     "Tops", 3), ("polo",      "Tops", 3), ("henley",    "Tops", 3),
        ("camisole",     "Tops", 3), ("halter",    "Tops", 3), ("bustier top","Tops",3),

        // Bottoms
        ("pants",        "Bottoms", 3), ("trousers","Bottoms", 3), ("shorts",    "Bottoms", 3),
        ("skirt",        "Bottoms", 3), ("leggings","Bottoms", 3), ("jeans",     "Bottoms", 3),
        ("culottes",     "Bottoms", 3), ("miniskirt","Bottoms",3), ("hotpants",  "Bottoms", 3),
        ("chaps",        "Bottoms", 3), ("legging", "Bottoms", 2), ("stockings", "Bottoms", 2),
        ("thigh high",   "Bottoms", 2),

        // Dresses / Full body
        ("dress",        "Dresses", 3), ("gown",     "Dresses", 3),
        ("sundress",     "Dresses", 3), ("minidress","Dresses", 3),
        ("jumpsuit",     "Dresses", 3), ("bodysuit", "Dresses", 3),
        ("catsuit",      "Dresses", 3), ("playsuit", "Dresses", 3),
        ("overalls",     "Dresses", 3), ("romper",   "Dresses", 3),

        // Cultural / Traditional (also clothing)
        ("yukata",       "Traditional", 3), ("kimono",   "Traditional", 3),
        ("hanfu",        "Traditional", 3), ("qipao",    "Traditional", 3),
        ("cheongsam",    "Traditional", 3), ("haori",    "Traditional", 3),
        ("hakama",       "Traditional", 3), ("hanbok",   "Traditional", 3),
        ("maid",         "Traditional", 2), ("shrine maiden","Traditional",3),
        ("miko",         "Traditional", 3),

        // Outerwear
        ("jacket",       "Outerwear", 3), ("coat",    "Outerwear", 3), ("cape",  "Outerwear", 3),
        ("cloak",        "Outerwear", 3), ("blazer",  "Outerwear", 3), ("parka", "Outerwear", 3),
        ("trench coat",  "Outerwear", 3), ("poncho",  "Outerwear", 3), ("shawl", "Outerwear", 2),
        ("overcoat",     "Outerwear", 3), ("windbreaker","Outerwear",3),
        ("vest",         "Outerwear", 2), // vest is ambiguous; low weight

        // Footwear
        ("boots",        "Footwear", 3), ("heels",   "Footwear", 3), ("sandals","Footwear", 3),
        ("sneakers",     "Footwear", 3), ("stilettos","Footwear",3), ("loafers","Footwear", 3),
        ("wedges",       "Footwear", 3), ("mules",   "Footwear", 3), ("flats",  "Footwear", 2),
        ("pumps",        "Footwear", 2), ("ankle boots","Footwear",3),("flip flops","Footwear",3),
        ("socks",        "Footwear", 2), // socks alone is weak — could be an accessory

        // Swimwear — kept strictly separate from underwear
        ("swimsuit",     "Swimwear", 3), ("bikini",   "Swimwear", 3), ("monokini","Swimwear",3),
        ("tankini",      "Swimwear", 3), ("wetsuit",  "Swimwear", 3), ("one-piece","Swimwear",3),
        ("bathing suit", "Swimwear", 3), ("swimwear", "Swimwear", 3),

        // Underwear — intimate garments only, never swimwear
        ("lingerie",     "Underwear", 3), ("bra",      "Underwear", 3), ("panties","Underwear",3),
        ("thong",        "Underwear", 3), ("boyshorts","Underwear", 3), ("corset", "Underwear", 3),
        ("bustier",      "Underwear", 3), ("garter",   "Underwear", 3),

        // Accessories
        ("gloves",       "Accessories", 3), ("scarf",   "Accessories", 3), ("belt", "Accessories",3),
        ("purse",        "Accessories", 3), ("handbag", "Accessories", 3), ("bag",  "Accessories",2),
        ("sunglasses",   "Accessories", 3), ("glasses", "Accessories", 3), ("mask", "Accessories",2),
        ("fan",          "Accessories", 3), ("parasol", "Accessories", 3), ("wings","Accessories",2),
        ("tail accessory","Accessories",2),

        // Jewelry
        ("necklace",     "Jewelry", 3), ("earring",  "Jewelry", 3), ("bracelet","Jewelry", 3),
        ("ring",         "Jewelry", 3), ("anklet",   "Jewelry", 3), ("choker",  "Jewelry", 3),
        ("tiara",        "Jewelry", 3), ("pendant",  "Jewelry", 3), ("brooch",  "Jewelry", 3),
        ("cuff",         "Jewelry", 2), ("chain",    "Jewelry", 2),

        // Headwear
        ("hat",          "Headwear", 3), ("helmet",   "Headwear", 3), ("cap",    "Headwear", 3),
        ("crown",        "Headwear", 3), ("beret",    "Headwear", 3), ("veil",   "Headwear", 3),
        ("fascinator",   "Headwear", 3), ("headdress","Headwear", 3), ("headband","Headwear",3),
        ("hair clip",    "Headwear", 3), ("hairpin",  "Headwear", 3),
        ("horns",        "Headwear", 2), // ambiguous — could be race feature

        // Neck & Waist
        ("collar",       "NeckWaist", 3), ("sash",    "NeckWaist", 3), ("obi",   "NeckWaist", 3),
        ("waistband",    "NeckWaist", 3),

        // Armor
        ("armor",        "Armor", 3), ("chainmail","Armor", 3), ("breastplate","Armor",3),
        ("gauntlet",     "Armor", 3), ("greave",   "Armor", 3), ("pauldron",  "Armor", 3),
        ("vambraces",    "Armor", 3), ("plate mail","Armor",3),

        // Costumes — ONLY explicit costume/uniform words, NOT generic "outfit"
        ("costume",      "Costumes", 3), ("cosplay",  "Costumes", 3),
        ("uniform",      "Costumes", 3), // uniform is explicit enough
        ("maid outfit",  "Costumes", 3), ("nurse outfit","Costumes",3),
        ("bunny suit",   "Costumes", 3), ("halloween","Costumes", 2),
    };

    private static readonly List<(string Keyword, string Tag, int Weight)> SeasonRules = new()
    {
        ("spring",    "Spring", 3), ("sakura",  "Spring", 3), ("floral",      "Spring", 2),
        ("bloom",     "Spring", 2), ("cherry blossom","Spring",3),
        ("summer",    "Summer", 3), ("tropical","Summer", 2), ("beach",       "Summer", 1),
        ("tanning",   "Summer", 2), ("surf",    "Summer", 2),
        ("autumn",    "Autumn", 3), ("fall",    "Autumn", 2), ("harvest",     "Autumn", 2),
        ("maple",     "Autumn", 2), ("pumpkin", "Autumn", 3),
        ("winter",    "Winter", 3), ("snow",    "Winter", 2), ("christmas",   "Winter", 3),
        ("festive",   "Winter", 2), ("yule",    "Winter", 3), ("frost",       "Winter", 2),
        ("cozy",      "Winter", 2), ("holiday", "Winter", 2),
    };

    private static readonly List<(string Keyword, string Tag, int Weight)> OccasionRules = new()
    {
        ("casual",     "Casual",   3), ("everyday","Casual",   3), ("streetwear","Casual",   3),
        ("daily",      "Casual",   2), ("comfy",   "Casual",   2),
        ("formal",     "Formal",   3), ("elegant", "Formal",   3), ("gala",     "Formal",   3),
        ("business",   "Formal",   3), ("office",  "Formal",   3),
        ("combat",     "Combat",   3), ("battle",  "Combat",   3), ("warrior",  "Combat",   2),
        ("tactical",   "Combat",   3), ("military","Combat",   3),
        ("festival",   "Festival", 3), ("carnival","Festival", 3), ("matsuri",  "Festival", 3),
        ("evening",    "Evening",  3), ("cocktail","Evening",  3), ("dinner",   "Evening",  2),
        ("beach",      "Beach",    3), ("poolside","Beach",    3), ("resort",   "Beach",    2),
        ("fantasy",    "Fantasy",  3), ("magical", "Fantasy",  2), ("witch",    "Fantasy",  3),
        ("fairy",      "Fantasy",  3), ("mage",    "Fantasy",  3), ("wizard",   "Fantasy",  3),
        ("wedding",    "Wedding",  3), ("bride",   "Wedding",  3), ("bridal",   "Wedding",  3),
        ("pajama",     "Lounge",   3), ("sleepwear","Lounge",  3), ("homewear", "Lounge",   3),
        ("sporty",     "Sporty",   3), ("athletic","Sporty",   3), ("gym",      "Sporty",   3),
        ("workout",    "Sporty",   3), ("yoga",    "Sporty",   3),
    };

    private static readonly List<(string Keyword, string Tag, int Weight)> RaceRules = new()
    {
        ("miqo'te",   "Miqote",   3), ("miqote",  "Miqote",   3), ("miqo",     "Miqote",   2),
        ("elezen",    "Elezen",   3),
        ("viera",     "Viera",    3), ("v'iera",  "Viera",    3), ("bunny girl","Viera",    2),
        ("hrothgar",  "Hrothgar", 3), ("ronso",   "Hrothgar", 2),
        ("roegadyn",  "Roegadyn", 3),
        ("hyur",      "Hyur",     3),
        ("au ra",     "AuRa",     3), ("au'ra",   "AuRa",     3), ("auri",     "AuRa",     3),
        ("raen",      "AuRa",     2), ("xaela",   "AuRa",     2),
        ("lalafell",  "Lalafell", 3), ("lala",    "Lalafell", 2),
    };

    // ── Public API ────────────────────────────────────────────────────────────

    public static AiSuggestion? Suggest(ModEntry mod)
    {
        var name = mod.Name.ToLowerInvariant();
        var desc = mod.Description.ToLowerInvariant();

        // ── Step 1: Veto check — is this clearly not a clothing mod? ──────────
        bool isVetoed = ClothingVetoPatterns.Any(v =>
            name.Contains(v, StringComparison.OrdinalIgnoreCase));

        // ── Step 2: Score all categories ─────────────────────────────────────
        var clothingScores  = isVetoed ? new List<TagScore>()
                                       : ScoreTags(name, desc, ClothingRules, nameMulti: 3);
        var seasonScores    = ScoreTags(name, desc, SeasonRules,   nameMulti: 3);
        var occasionScores  = ScoreTags(name, desc, OccasionRules, nameMulti: 3);
        var raceScores      = ScoreTags(name, desc, RaceRules,     nameMulti: 4);

        // ── Step 3: Slot-based clothing boost from description ────────────────
        if (!isVetoed)
        {
            foreach (var (keyword, tag) in SlotRules)
            {
                if (desc.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    var existing = clothingScores.FirstOrDefault(s => s.Tag == tag);
                    if (existing != null)
                        existing.Score += 2;
                    else
                        clothingScores.Add(new TagScore(tag, 2));
                }
            }
        }

        // ── Step 4: Remove weak-alone words unless paired with stronger signal ─
        // "Dance Outfit" → "outfit" scores 2 for Costumes, but no other clothing
        // signal exists, so total Costumes score is 2. If it's the ONLY signal and
        // came from a WeakAloneWord, raise the threshold to require 6+.
        var clothingFiltered = clothingScores
            .Where(s =>
            {
                if (s.Score < 4) return false; // base threshold raised to 4
                // If the only reason for this tag is a weak-alone word, require score ≥ 6
                bool onlyWeakSignal = ClothingRules
                    .Where(r => r.Tag == s.Tag && WeakAloneWords.Contains(r.Keyword))
                    .Sum(r => r.Weight * 3) >= s.Score;
                return !onlyWeakSignal || s.Score >= 6;
            })
            .OrderByDescending(s => s.Score)
            .Take(2)
            .Select(s => s.Tag)
            .ToList();

        const int MinScore = 4;
        var topSeasons   = seasonScores  .Where(s => s.Score >= MinScore).OrderByDescending(s => s.Score).Take(2).Select(s => s.Tag).ToList();
        var topOccasions = occasionScores.Where(s => s.Score >= MinScore).OrderByDescending(s => s.Score).Take(2).Select(s => s.Tag).ToList();
        var topRaces     = raceScores    .Where(s => s.Score >= MinScore).OrderByDescending(s => s.Score).Take(1).Select(s => s.Tag).ToList();

        if (!clothingFiltered.Any() && !topSeasons.Any() && !topOccasions.Any() && !topRaces.Any())
            return null;

        float confidence = Math.Min(1.0f,
            (clothingFiltered.Any() ? 0.35f : 0f) +
            (topSeasons.Any()       ? 0.25f : 0f) +
            (topOccasions.Any()     ? 0.25f : 0f) +
            (topRaces.Any()         ? 0.15f : 0f));

        var reasons = new List<string>();
        if (clothingFiltered.Any()) reasons.Add($"clothing: {string.Join(", ", clothingFiltered)}");
        if (topSeasons.Any())       reasons.Add($"season: {string.Join(", ", topSeasons)}");
        if (topOccasions.Any())     reasons.Add($"occasion: {string.Join(", ", topOccasions)}");
        if (topRaces.Any())         reasons.Add($"race: {string.Join(", ", topRaces)}");
        if (isVetoed)               reasons.Add("(clothing tags suppressed — non-clothing mod type detected)");

        return new AiSuggestion
        {
            ModDirectoryName = mod.DirectoryName,
            ClothingTags     = clothingFiltered,
            SeasonTags       = topSeasons,
            OccasionTags     = topOccasions,
            RaceTags         = topRaces,
            Confidence       = confidence,
            Reasoning        = string.Join(" | ", reasons),
        };
    }

    public static List<AiSuggestion> SuggestAll(List<ModEntry> mods)
    {
        var results = new List<AiSuggestion>();
        foreach (var mod in mods)
        {
            if (mod.HasManualTags) continue;
            var sug = Suggest(mod);
            if (sug != null) results.Add(sug);
        }
        return results;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    // Mutable so slot-boost can add to score
    public class TagScore
    {
        public string Tag   { get; }
        public int    Score { get; set; }
        public TagScore(string tag, int score) { Tag = tag; Score = score; }
    }

    private static List<TagScore> ScoreTags(
        string name,
        string desc,
        List<(string Keyword, string Tag, int Weight)> rules,
        int nameMulti = 3)
    {
        var scores = new Dictionary<string, TagScore>(StringComparer.OrdinalIgnoreCase);

        foreach (var (keyword, tag, weight) in rules)
        {
            bool inName = name.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            bool inDesc = !inName && desc.Contains(keyword, StringComparison.OrdinalIgnoreCase);

            if (!inName && !inDesc) continue;

            int add = inName ? weight * nameMulti : weight;
            if (scores.TryGetValue(tag, out var existing))
                existing.Score += add;
            else
                scores[tag] = new TagScore(tag, add);
        }

        return scores.Values.ToList();
    }
}
