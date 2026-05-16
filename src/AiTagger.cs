using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PenumbraSort;

/// <summary>
/// Runs local pattern-matching tag suggestions on untagged mods.
/// Uses LocalPatternTagger — no internet or API key required.
/// Results are proposals; user must approve before they are saved.
/// </summary>
public class AiTagger : IDisposable
{
    private const int BatchSize = 50; // process in chunks to stay responsive

    public bool   IsBusy      { get; private set; }
    public int    TotalToTag  { get; private set; }
    public int    TaggedSoFar { get; private set; }
    public string StatusText  { get; private set; } = string.Empty;

    // Suggestions waiting for user review: DirectoryName -> suggestion
    public Dictionary<string, AiSuggestion> PendingSuggestions { get; } = new();

    /// <summary>
    /// Suggest tags for all untagged mods using local pattern matching.
    /// Runs asynchronously so the UI stays responsive.
    /// The apiKey parameter is accepted for API compatibility but unused.
    /// </summary>
    public async Task SuggestTagsAsync(
        List<ModEntry> mods,
        string apiKey = "",
        CancellationToken ct = default)
    {
        var untagged = mods
            .Where(m => !m.HasManualTags && m.PendingSuggestion == null)
            .ToList();

        if (!untagged.Any())
        {
            StatusText = "All mods already have tags.";
            return;
        }

        IsBusy      = true;
        TotalToTag  = untagged.Count;
        TaggedSoFar = 0;
        StatusText  = $"Scanning {TotalToTag} untagged mods...";

        try
        {
            // Process in batches, yielding between each so the UI doesn't hitch
            var batches = untagged
                .Select((m, i) => (m, i))
                .GroupBy(x => x.i / BatchSize)
                .Select(g => g.Select(x => x.m).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                if (ct.IsCancellationRequested) break;

                // Yield to UI thread between batches
                await Task.Delay(1, ct);

                foreach (var mod in batch)
                {
                    if (ct.IsCancellationRequested) break;
                    var sug = LocalPatternTagger.Suggest(mod);
                    if (sug != null)
                    {
                        mod.PendingSuggestion = sug;
                        PendingSuggestions[mod.DirectoryName] = sug;
                    }
                }

                TaggedSoFar += batch.Count;
                StatusText = $"Scanned {TaggedSoFar}/{TotalToTag} mods...";
            }

            var found = PendingSuggestions.Count;
            StatusText = found > 0
                ? $"Found tags for {found} mods — review in menu bar."
                : $"Scanned {TotalToTag} mods — no patterns matched. Try adding tags manually.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Tag scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Tag scan error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose() { /* no resources to release */ }
}
