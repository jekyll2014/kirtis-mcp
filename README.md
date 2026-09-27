# kirtis-mcp

MCP server for [kirtis.info](https://kirtis.info) — Lithuanian stress marks and grammatical information.

Two implementations, same MCP tools:

| Implementation | Folder | Requires |
|----------------|--------|---------|
| **C# (.NET)** — recommended | `dotnet/` | .NET 9 SDK |
| Python (legacy) | `python/` | Python 3.11+, uv |

## Tools

| Tool | Description |
|------|-------------|
| `get_word_info` | Full lookup: related words + stress entries for all forms. **Use this one.** |
| `get_stress` | Stress marks and grammar tags for one exact word. |
| `lookup_words` | Prefix search — returns related word forms. |

### Example

```
get_word_info("žmogus")
→ word: "Žmogùs", class: dktv. (noun), state: vyr.gim. vnsk. V. (masc. sg. nominative)
  stress: grave on last syllable = falling tone
```

## Quick start

**C# (.NET):**
```bash
cd dotnet
dotnet run            # stdio MCP (Claude Code)
dotnet run -- --sse   # SSE + REST server on port 8020
dotnet run -- --help  # show all options
```

**Python:**
```bash
cd python
uv sync
uv run python server.py           # stdio MCP
uv run python server.py --sse     # SSE server on port 8020
uv run python proxy.py            # REST proxy on port 8010
```

## Integrations

| Client | Transport | .NET | Python |
|--------|-----------|------|--------|
| Claude Code | stdio | `dotnet run` | `uv run python server.py` |
| Open WebUI | HTTP (OpenAPI) | `--sse`, port 8020 | `proxy.py`, port 8010 |
| Unsloth / SSE clients | SSE | `--sse`, `/sse` endpoint | `--sse`, `/sse` endpoint |

See [SETUP.md](SETUP.md) for full instructions.

GitHub: https://github.com/jekyll2014/kirtis-mcp
