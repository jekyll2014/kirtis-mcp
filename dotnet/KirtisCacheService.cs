using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KirtisMcp;

public sealed class KirtisCacheService(CacheSettings settings) : BackgroundService
{
    // volatile: written by ExecuteAsync (background thread), read by request threads.
    // Without volatile, ARM CPUs can serve stale values from per-core caches.
    private volatile FrozenDictionary<string, WordInfo> _cache = FrozenDictionary<string, WordInfo>.Empty;
    private readonly ConcurrentDictionary<string, WordInfo> _newWordEntries = new(StringComparer.OrdinalIgnoreCase);

    private volatile FrozenDictionary<string, List<WordEntry>> _stressCache = FrozenDictionary<string, List<WordEntry>>.Empty;
    private readonly ConcurrentDictionary<string, List<WordEntry>> _newStressEntries = new(StringComparer.OrdinalIgnoreCase);

    private volatile FrozenDictionary<string, List<string>> _lookupCache = FrozenDictionary<string, List<string>>.Empty;
    private readonly ConcurrentDictionary<string, List<string>> _newLookups = new(StringComparer.OrdinalIgnoreCase);

    private volatile string? _cacheDir;
    private volatile bool _isLoaded;

    // SemaphoreSlim prevents concurrent flushes (timer vs StopAsync race).
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    private static readonly JsonSerializerOptions CacheOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = false };

    public bool IsLoaded => _isLoaded;

    // --- Public API ---

    public bool TryGet(string word, out WordInfo? result)
    {
        result = null;
        if (!settings.ReadEnabled) return false;
        var key = word.ToLowerInvariant().Trim();
        if (_newWordEntries.TryGetValue(key, out result)) return true;
        if (!_isLoaded) return false;
        return _cache.TryGetValue(key, out result);
    }

    public void Add(WordInfo entry) =>
        _newWordEntries[entry.QueryWord.ToLowerInvariant().Trim()] = entry;

    public bool TryGetStress(string word, out List<WordEntry>? result)
    {
        result = null;
        if (!settings.ReadEnabled) return false;
        var key = word.ToLowerInvariant().Trim();
        if (_newStressEntries.TryGetValue(key, out result)) return true;
        if (!_isLoaded) return false;
        return _stressCache.TryGetValue(key, out result);
    }

    public void AddStress(string word, List<WordEntry> entries) =>
        _newStressEntries[word.ToLowerInvariant().Trim()] = entries;

    public bool TryGetLookup(string word, out List<string>? result)
    {
        result = null;
        if (!settings.ReadEnabled) return false;
        var key = word.ToLowerInvariant().Trim();
        if (_newLookups.TryGetValue(key, out result)) return true;
        if (_newWordEntries.TryGetValue(key, out var wi) && wi.RelatedWords.Count > 0)
            { result = wi.RelatedWords; return true; }
        if (!_isLoaded) return false;
        if (_lookupCache.TryGetValue(key, out result)) return true;
        if (_cache.TryGetValue(key, out wi) && wi.RelatedWords.Count > 0)
            { result = wi.RelatedWords; return true; }
        return false;
    }

    public void AddLookup(string word, List<string> related) =>
        _newLookups[word.ToLowerInvariant().Trim()] = related;

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
        var wordDict   = new Dictionary<string, WordInfo>(200_000, StringComparer.OrdinalIgnoreCase);
        var stressDict = new Dictionary<string, List<WordEntry>>(StringComparer.OrdinalIgnoreCase);
        var lookupDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(_cacheDir, "kirtis_batch_*.json").Order())
        {
            if (stoppingToken.IsCancellationRequested) break;
            await using var stream = File.OpenRead(file);
            var entries = await JsonSerializer.DeserializeAsync<List<CacheEntry>>(stream, CacheOpts, stoppingToken);
            if (entries is null) continue;
            foreach (var e in entries)
                wordDict[e.QueryWord] = new WordInfo(e.QueryWord, e.RelatedWords, Intern(e.StressEntries), e.HasStressInfo);
        }

        await LoadJsonFile<List<WordInfo>>(Path.Combine(_cacheDir, "kirtis_updates.json"), stoppingToken,
            updates => { foreach (var u in updates) wordDict[u.QueryWord] = u; });

        _cache = wordDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        await LoadJsonFile<Dictionary<string, List<WordEntry>>>(Path.Combine(_cacheDir, "kirtis_stress_updates.json"), stoppingToken,
            updates => { foreach (var (k, v) in updates) stressDict[k] = v; });

        _stressCache = stressDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        await LoadJsonFile<Dictionary<string, List<string>>>(Path.Combine(_cacheDir, "kirtis_lookup_updates.json"), stoppingToken,
            updates => { foreach (var (k, v) in updates) lookupDict[k] = v; });

        _lookupCache = lookupDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        _isLoaded = true;
        await Console.Error.WriteLineAsync(
            $"[kirtis-cache] Loaded {_cache.Count:N0} word, {_stressCache.Count:N0} stress, {_lookupCache.Count:N0} lookup entries in {sw.ElapsedMilliseconds}ms");

        if (!settings.WriteEnabled) return;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await FlushAllAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task LoadJsonFile<T>(string path, CancellationToken ct, Action<T> apply)
    {
        if (!File.Exists(path) || ct.IsCancellationRequested) return;
        try
        {
            await using var stream = File.OpenRead(path);
            var data = await JsonSerializer.DeserializeAsync<T>(stream, CacheOpts, ct);
            if (data is not null) apply(data);
        }
        catch { }
    }

    // --- Flushing ---

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (settings.WriteEnabled && _cacheDir is not null)
            await FlushAllAsync();
    }

    private async Task FlushAllAsync()
    {
        await _flushLock.WaitAsync();
        try
        {
            if (_newWordEntries.Count > 0)   await FlushWordEntriesAsync();
            if (_newStressEntries.Count > 0) await FlushStressEntriesAsync();
            if (_newLookups.Count > 0)       await FlushLookupEntriesAsync();
        }
        finally
        {
            _flushLock.Release();
        }
    }

    // File writes use CancellationToken.None — cancelling mid-write would corrupt JSON.
    private async Task FlushWordEntriesAsync()
    {
        var file = Path.Combine(_cacheDir!, "kirtis_updates.json");
        var merged = new Dictionary<string, WordInfo>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(file))
        {
            try
            {
                await using var rs = File.OpenRead(file);
                var existing = await JsonSerializer.DeserializeAsync<List<WordInfo>>(rs, CacheOpts);
                if (existing is not null) foreach (var e in existing) merged[e.QueryWord] = e;
            }
            catch { }
        }
        foreach (var (k, v) in _newWordEntries) merged[k] = v;
        await using var ws = File.Create(file);
        await JsonSerializer.SerializeAsync(ws, merged.Values.ToList(), WriteOpts);
        await Console.Error.WriteLineAsync($"[kirtis-cache] Flushed {_newWordEntries.Count} word entries → {file}");
    }

    private async Task FlushStressEntriesAsync()
    {
        var file = Path.Combine(_cacheDir!, "kirtis_stress_updates.json");
        var merged = new Dictionary<string, List<WordEntry>>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(file))
        {
            try
            {
                await using var rs = File.OpenRead(file);
                var existing = await JsonSerializer.DeserializeAsync<Dictionary<string, List<WordEntry>>>(rs, CacheOpts);
                if (existing is not null) foreach (var (k, v) in existing) merged[k] = v;
            }
            catch { }
        }
        foreach (var (k, v) in _newStressEntries) merged[k] = v;
        await using var ws = File.Create(file);
        await JsonSerializer.SerializeAsync(ws, merged, WriteOpts);
        await Console.Error.WriteLineAsync($"[kirtis-cache] Flushed {_newStressEntries.Count} stress entries → {file}");
    }

    private async Task FlushLookupEntriesAsync()
    {
        var file = Path.Combine(_cacheDir!, "kirtis_lookup_updates.json");
        var merged = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(file))
        {
            try
            {
                await using var rs = File.OpenRead(file);
                var existing = await JsonSerializer.DeserializeAsync<Dictionary<string, List<string>>>(rs, CacheOpts);
                if (existing is not null) foreach (var (k, v) in existing) merged[k] = v;
            }
            catch { }
        }
        foreach (var (k, v) in _newLookups) merged[k] = v;
        await using var ws = File.Create(file);
        await JsonSerializer.SerializeAsync(ws, merged, WriteOpts);
        await Console.Error.WriteLineAsync($"[kirtis-cache] Flushed {_newLookups.Count} lookup entries → {file}");
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
