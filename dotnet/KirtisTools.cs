using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace KirtisMcp;

[McpServerToolType]
public class KirtisTools(IHttpClientFactory httpFactory, KirtisCacheService cache)
{
    private readonly HttpClient _http = httpFactory.CreateClient("kirtis");
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [McpServerTool(Name = "lookup_words")]
    [Description(
        "Prefix-search for Lithuanian words. Calls GET /api/zodynas/{word}. " +
        "Use as Phase 1 before get_stress — expands a query word to its related forms. " +
        "Input is lowercased automatically. Returns [] on no results or error.")]
    public async Task<List<string>> LookupWords(
        [Description("Lowercase word or prefix (e.g. 'eiti', 'ei')")] string word)
    {
        var normalized = word.ToLowerInvariant().Trim();

        if (cache.TryGetLookup(normalized, out var cached))
            return cached!;

        try
        {
            var json = await _http.GetStringAsync($"zodynas/{Uri.EscapeDataString(normalized)}");
            var result = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            cache.AddLookup(normalized, result);
            return result;
        }
        catch { return []; }
    }

    [McpServerTool(Name = "get_stress")]
    [Description(
        "Get stress marks and grammatical tags for one Lithuanian word. Calls GET /api/krc/{Word}. " +
        "First letter is capitalized automatically (API requirement). Returns [] on no results or error. " +
        "Each entry: word (stressed form with combining diacritics), class (part-of-speech abbreviation), " +
        "state (list of grammatical tags).")]
    public async Task<List<WordEntry>> GetStress(
        [Description("Lithuanian word, any capitalisation (e.g. 'eiti')")] string word)
    {
        var normalized = word.ToLowerInvariant().Trim();

        if (cache.TryGetStress(normalized, out var cached))
            return cached!;

        var capitalized = CapitalizeFirst(word.Trim());
        try
        {
            var json = await _http.GetStringAsync($"krc/{Uri.EscapeDataString(capitalized)}");
            var entries = JsonSerializer.Deserialize<List<WordEntry>>(json, JsonOpts) ?? [];
            cache.AddStress(normalized, entries);
            return entries;
        }
        catch { return []; }
    }

    [McpServerTool(Name = "get_word_info")]
    [Description(
        "Full two-phase lookup: related words + stress info for each. Preferred entry point. " +
        "Phase 1: lookup_words(word) → related word list. " +
        "Phase 2: get_stress(w) for each w → stress + grammar entries. " +
        "Returns query_word, related_words, stress_entries (deduped by stressed form), has_stress_info.")]
    public async Task<WordInfo> GetWordInfo(
        [Description("Lithuanian word to look up (e.g. 'eiti', 'žmogus')")] string word)
    {
        var normalized = word.ToLowerInvariant().Trim();

        if (cache.TryGet(normalized, out var cached))
            return cached!;

        var related = await LookupWords(normalized);

        var allEntries = new List<WordEntry>();
        foreach (var rw in related)
            allEntries.AddRange(await GetStress(rw));

        var seen = new HashSet<string>();
        var deduped = new List<WordEntry>();
        foreach (var entry in allEntries)
        {
            if (seen.Add(entry.Word.ToLowerInvariant()))
                deduped.Add(entry);
        }

        var result = new WordInfo(normalized, related, deduped, deduped.Count > 0);
        cache.Add(result);
        return result;
    }

    private static string CapitalizeFirst(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
