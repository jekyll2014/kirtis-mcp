# Kirtis MCP — Setup

GitHub: https://github.com/jekyll2014/kirtis-mcp

## Prerequisites

- Python 3.11+
- [uv](https://docs.astral.sh/uv/getting-started/installation/) installed

---

## 1. Clone and install

```bash
git clone https://github.com/jekyll2014/kirtis-mcp.git
cd kirtis-mcp
uv sync
```

---

## Claude Code (stdio MCP)

### Register the MCP server

```bash
claude mcp add kirtis -- uv run --project /path/to/kirtis-mcp python /path/to/kirtis-mcp/server.py
```

Replace `/path/to/kirtis-mcp` with the actual clone path (e.g. `E:\WORK\programming\kirtis-mcp` on Windows).

Verify: restart Claude Code, run `/mcp` — `kirtis` should appear with 3 tools.

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

Or place into a project's `.claude/skills/kirtis/` to scope it to that project only.

---

## Open WebUI (HTTP proxy)

### Start the proxy

Double-click `start.bat`, or run:

```bash
cd kirtis-mcp
uv run python proxy.py
```

Proxy starts at `http://localhost:8010`.  
Verify: open `http://localhost:8010/docs` — should show 3 endpoints.

### Connect to Open WebUI

1. Open WebUI → **Admin Panel** → **Tools**
2. Click **"+"** (Add tool server)
3. Set URL: `http://localhost:8010`
4. Save — three tools appear: `lookup_words`, `get_stress`, `get_word_info`

### Enable tools on a model

1. Open WebUI → **Workspace** → **Models** → edit model
2. Under **Tools**, enable the Kirtis tools → Save

Or enable per-chat: click the tools icon in the chat input bar.

---

## Unsloth / SSE clients

### Start the SSE server

Double-click `start-sse.bat`, or run:

```bash
uv run --project /path/to/kirtis-mcp python /path/to/kirtis-mcp/server.py --sse --port 8020
```

Server starts at `http://localhost:8020`.

### Connect in Unsloth desktop

In Unsloth → **Add MCP** form:

- **URL**: `http://localhost:8020/sse`
- **Headers**: *(leave empty)*

### Connect via stdio (alternative)

If the client supports stdio commands:

```
uv run --project /path/to/kirtis-mcp python /path/to/kirtis-mcp/server.py
```

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

---

## Notes

- kirtis.info has no auth — no API key needed
- `/api/krc/` requires capitalized first letter — handled automatically
