using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KirtisMcp;

public sealed class KirtisCacheService(CacheSettings settings) : BackgroundService
{
    private FrozenDictionary<string, WordInfo> _cache = FrozenDictionary<string, WordInfo>.Empty;
    private readonly ConcurrentDictionary<string, WordInfo> _newWordEntries = new(StringComparer.OrdinalIgnoreCase);

    // Keyed by lowercase word (as passed to GetStress before capitalization)
    private FrozenDictionary<string, List<WordEntry>> _stressCache = FrozenDictionary<string, List<WordEntry>>.Empty;
    private readonly ConcurrentDictionary<string, List<WordEntry>> _newStressEntries = new(StringComparer.OrdinalIgnoreCase);

    private string? _cacheDir;

    private static readonly JsonSerializerOptions CacheOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = false };

    public bool IsLoaded { get; private set; }

    // --- WordInfo (get_word_info / lookup_words) ---

    public bool TryGet(string word, out WordInfo? result)
    {
        result = null;
        if (!settings.ReadEnabled) return false;
        var key = word.ToLowerInvariant().Trim();
        if (_newWordEntries.TryGetValue(key, out result)) return true;
        if (!IsLoaded) return false;
        return _cache.TryGetValue(key, out result);
    }

    public void Add(WordInfo entry)
    {
        if (!settings.WriteEnabled) return;
        _newWordEntries[entry.QueryWord] = entry;
    }

    // --- Stress entries (get_stress) ---

    public bool TryGetStress(string word, out List<WordEntry>? result)
    {
        result = null;
        if (!settings.ReadEnabled) return false;
        var key = word.ToLowerInvariant().Trim();
        if (_newStressEntries.TryGetValue(key, out result)) return true;
        if (!IsLoaded) return false;
        return _stressCache.TryGetValue(key, out result);
    }

    public void AddStress(string word, List<WordEntry> entries)
    {
        if (!settings.WriteEnabled) return;
        _newStressEntries[word.ToLowerInvariant().Trim()] = entries;
    }

    // --- Loading ---

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cacheDir = FindCacheDir();
        if (_cacheDir is null)
        {
            await Console.Error.WriteLineAsync("[kirtis-cache] Not found. Set KIRTIS_CACHE_PATH or place Kirtis/ folder near executable.");
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var wordDict = new Dictionary<string, WordInfo>(200_000, StringComparer.OrdinalIgnoreCase);

        // Load batch files (camelCase format from downloader)
        foreach (var file in Directory.EnumerateFiles(_cacheDir, "kirtis_batch_*.json").Order())
        {
            if (stoppingToken.IsCancellationRequested) break;
            await using var stream = File.OpenRead(file);
            var entries = await JsonSerializer.DeserializeAsync<List<CacheEntry>>(stream, CacheOpts, stoppingToken);
            if (entries is null) continue;
            foreach (var e in entries)
                wordDict[e.QueryWord] = new WordInfo(e.QueryWord, e.RelatedWords, Intern(e.StressEntries), e.HasStressInfo);
        }

        // Load word updates (snake_case WordInfo format written by this service)
        var wordUpdates = Path.Combine(_cacheDir, "kirtis_updates.json");
        if (File.Exists(wordUpdates) && !stoppingToken.IsCancellationRequested)
        {
            await using var stream = File.OpenRead(wordUpdates);
            var updates = await JsonSerializer.DeserializeAsync<List<WordInfo>>(stream, CacheOpts, stoppingToken);
            if (updates is not null)
                foreach (var u in updates)
                    wordDict[u.QueryWord] = u;
        }

        _cache = wordDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        // Load stress updates (dictionary: lowercase_word → entries[])
        var stressDict = new Dictionary<string, List<WordEntry>>(StringComparer.OrdinalIgnoreCase);
        var stressUpdates = Path.Combine(_cacheDir, "kirtis_stress_updates.json");
        if (File.Exists(stressUpdates) && !stoppingToken.IsCancellationRequested)
        {
            await using var stream = File.OpenRead(stressUpdates);
            var updates = await JsonSerializer.DeserializeAsync<Dictionary<string, List<WordEntry>>>(stream, CacheOpts, stoppingToken);
            if (updates is not null)
                foreach (var (k, v) in updates)
                    stressDict[k] = v;
        }

        _stressCache = stressDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        IsLoaded = true;
        await Console.Error.WriteLineAsync(
            $"[kirtis-cache] Loaded {_cache.Count:N0} word entries, {_stressCache.Count:N0} stress entries in {sw.ElapsedMilliseconds}ms");
    }

    // --- Flushing ---

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (!settings.WriteEnabled || _cacheDir is null) return;
        if (_newWordEntries.Count > 0) await FlushWordEntriesAsync(cancellationToken);
        if (_newStressEntries.Count > 0) await FlushStressEntriesAsync(cancellationToken);
    }

    private async Task FlushWordEntriesAsync(CancellationToken cancellationToken)
    {
        var updatesFile = Path.Combine(_cacheDir!, "kirtis_updates.json");

        var existing = new List<WordInfo>();
        if (File.Exists(updatesFile))
        {
            try
            {
                await using var rs = File.OpenRead(updatesFile);
                existing = await JsonSerializer.DeserializeAsync<List<WordInfo>>(rs, CacheOpts, cancellationToken) ?? [];
            }
            catch { }
        }

        var merged = new Dictionary<string, WordInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in existing) merged[e.QueryWord] = e;
        foreach (var (_, v) in _newWordEntries) merged[v.QueryWord] = v;

        await using var ws = File.Create(updatesFile);
        await JsonSerializer.SerializeAsync(ws, merged.Values.ToList(), WriteOpts, cancellationToken);

        await Console.Error.WriteLineAsync($"[kirtis-cache] Flushed {_newWordEntries.Count} word entries → {updatesFile}");
    }

    private async Task FlushStressEntriesAsync(CancellationToken cancellationToken)
    {
        var stressFile = Path.Combine(_cacheDir!, "kirtis_stress_updates.json");

        var existing = new Dictionary<string, List<WordEntry>>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(stressFile))
        {
            try
            {
                await using var rs = File.OpenRead(stressFile);
                var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, List<WordEntry>>>(rs, CacheOpts, cancellationToken);
                if (loaded is not null)
                    foreach (var (k, v) in loaded)
                        existing[k] = v;
            }
            catch { }
        }

        foreach (var (k, v) in _newStressEntries) existing[k] = v;

        await using var ws = File.Create(stressFile);
        await JsonSerializer.SerializeAsync(ws, existing, WriteOpts, cancellationToken);

        await Console.Error.WriteLineAsync($"[kirtis-cache] Flushed {_newStressEntries.Count} stress entries → {stressFile}");
    }

    // Intern class names ("dktv.", "bdvr.", ...) and state strings ("vyr.gim.", "V.", ...)
    // — a few dozen unique values used millions of times across entries.
    private static List<WordEntry> Intern(List<WordEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var cls = string.Intern(e.Class ?? "");
            var state = e.State;
            for (var j = 0; j < state.Count; j++)
                state[j] = string.Intern(state[j]);
            entries[i] = e with { Class = cls };
        }
        return entries;
    }

    private static string? FindCacheDir()
    {
        var env = Environment.GetEnvironmentVariable("KIRTIS_CACHE_PATH");
        if (env is not null && Directory.Exists(env)) return env;

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "Kirtis");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        return null;
    }

    private record CacheEntry(
        [property: JsonPropertyName("queryWord")]     string QueryWord,
        [property: JsonPropertyName("relatedWords")]  List<string> RelatedWords,
        [property: JsonPropertyName("stressEntries")] List<WordEntry> StressEntries,
        [property: JsonPropertyName("hasStressInfo")] bool HasStressInfo
    );
}
