using System.Text.Json.Serialization;

namespace KirtisMcp;

public record CacheSettings(bool ReadEnabled, bool WriteEnabled);

public record WordEntry(
    [property: JsonPropertyName("word")]  string Word,
    [property: JsonPropertyName("class")] string? Class,
    [property: JsonPropertyName("state")] List<string> State
);

public record WordInfo(
    [property: JsonPropertyName("query_word")]    string QueryWord,
    [property: JsonPropertyName("related_words")] List<string> RelatedWords,
    [property: JsonPropertyName("stress_entries")]List<WordEntry> StressEntries,
    [property: JsonPropertyName("has_stress_info")]bool HasStressInfo
);
