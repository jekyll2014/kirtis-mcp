# kirtis-mcp

MCP server for [kirtis.info](https://kirtis.info) — Lithuanian stress marks and grammatical information.

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

## Integrations

| Client | Transport | How |
|--------|-----------|-----|
| Claude Code | stdio | Register via `claude mcp add` |
| Open WebUI | HTTP (OpenAPI proxy) | Point tool server at `http://localhost:8010` |
| Unsloth / SSE clients | SSE | Point MCP URL at `http://localhost:8020/sse` |

See [SETUP.md](SETUP.md) for full instructions.

## Install

```bash
git clone https://github.com/jekyll2014/kirtis-mcp.git
cd kirtis-mcp
uv sync
```

Requires Python 3.11+ and [uv](https://docs.astral.sh/uv/getting-started/installation/).
