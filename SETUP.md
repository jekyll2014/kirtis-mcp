# Kirtis MCP — Setup

GitHub: https://github.com/jekyll2014/kirtis-mcp

Two implementations available. C# (.NET) is recommended — no Python environment needed.

---

## 1. Clone

```bash
git clone https://github.com/jekyll2014/kirtis-mcp.git
cd kirtis-mcp
```

---

## 2. Build

### C# (.NET) — recommended

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
cd dotnet
dotnet build
```

### Python (legacy)

Requires Python 3.11+ and [uv](https://docs.astral.sh/uv/getting-started/installation/).

```bash
cd python
uv sync
```

---

## Claude Code (stdio MCP)

### C# (.NET)

Register the MCP server:

```bash
claude mcp add kirtis -- dotnet run --project E:\WORK\programming\kirtis-mcp\dotnet
```

Or edit `~/.claude.json` manually:

```json
"kirtis": {
  "type": "stdio",
  "command": "dotnet",
  "args": ["run", "--project", "E:/WORK/programming/kirtis-mcp/dotnet"],
  "env": {}
}
```

### Python (legacy)

```bash
claude mcp add kirtis -- uv run --project E:\WORK\programming\kirtis-mcp\python python E:\WORK\programming\kirtis-mcp\python\server.py
```

### Verify

Restart Claude Code, run `/mcp` — `kirtis` should appear with 3 tools.

### Install the skill (optional)

The skill teaches Claude how to interpret stress marks and grammar abbreviations.

**Windows:**
```
copy .claude\skills\kirtis\SKILL.md %USERPROFILE%\.claude\skills\kirtis\SKILL.md
```

**macOS / Linux:**
```bash
mkdir -p ~/.claude/skills/kirtis
cp .claude/skills/kirtis/SKILL.md ~/.claude/skills/kirtis/SKILL.md
```

---

## Open WebUI (HTTP proxy)

### C# (.NET)

Start the SSE server — it also serves REST endpoints:

```bash
dotnet\start-sse.bat
# or:
cd dotnet && dotnet run -- --sse --port 8020
```

Verify: `http://localhost:8020/get_word_info` (POST `{"word":"test"}` should return JSON).

Connect to Open WebUI:

1. Open WebUI → **Admin Panel** → **Tools**
2. Click **"+"** (Add tool server)
3. Set URL: `http://localhost:8020`
4. Save — three tools appear: `lookup_words`, `get_stress`, `get_word_info`

### Python (legacy)

Start the REST proxy:

```bash
python\start.bat
# or:
cd python && uv run python proxy.py
```

Proxy starts at `http://localhost:8010`.  
Verify: open `http://localhost:8010/docs`.

Connect to Open WebUI — same steps as above, but URL is `http://localhost:8010`.

### Enable tools on a model

1. Open WebUI → **Workspace** → **Models** → edit model
2. Under **Tools**, enable the Kirtis tools → Save

Or enable per-chat: click the tools icon in the chat input bar.

---

## Unsloth / SSE clients

### C# (.NET)

```bash
dotnet\start-sse.bat
# or:
cd dotnet && dotnet run -- --sse --port 8020
```

In Unsloth → **Add MCP**:
- **URL**: `http://localhost:8020/sse`
- **Headers**: *(leave empty)*

### Python (legacy)

```bash
python\start-sse.bat
# or:
cd python && uv run python server.py --sse --port 8020
```

Same URL: `http://localhost:8020/sse`

---

## Tools reference

| Tool | Input | Returns |
|------|-------|---------|
| `get_word_info` | `word` | `query_word`, `related_words`, `stress_entries`, `has_stress_info` |
| `get_stress` | `word` | list of `{word, class, state}` entries |
| `lookup_words` | `word` or prefix | list of related words |

**`stress_entries` fields:**
- `word` — stressed form with diacritics (e.g. `Žmogùs`)
- `class` — part-of-speech: `dktv.` noun · `vksm.` verb · `būdv.` adjective · `prv.` adverb
- `state` — grammatical tags: e.g. `["vyr.gim.", "vnsk.", "V."]` = masculine singular nominative

**Stress diacritics on the `word` field:**
- Grave `` ` `` — falling tone (e.g. `žmogùs`)
- Tilde `~` — rising/mixed tone (e.g. `Ẽiti`)
- Acute — short stress

---

## Notes

- kirtis.info has no auth — no API key needed
- `/api/krc/` requires capitalized first letter — handled automatically by both implementations
- C# SSE mode serves both MCP (`/mcp`) and REST (`/lookup_words`, `/get_stress`, `/get_word_info`) on the same port
