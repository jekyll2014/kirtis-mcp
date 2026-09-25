# Kirtis MCP — Setup

GitHub: https://github.com/jekyll2014/kirtis-mcp

## Prerequisites

- Python 3.11+
- [uv](https://docs.astral.sh/uv/getting-started/installation/) installed
- Open WebUI (for proxy/Open WebUI integration only)

---

## 1. Clone and install

```bash
git clone https://github.com/jekyll2014/kirtis-mcp.git
cd kirtis-mcp
uv sync
```

---

## 2. Register with Claude Code (MCP — stdio)

```bash
claude mcp add kirtis -- uv run --project /path/to/kirtis-mcp python /path/to/kirtis-mcp/server.py
```

Replace `/path/to/kirtis-mcp` with the actual clone path (e.g. `E:\WORK\programming\kirtis-mcp` on Windows).

Verify: restart Claude Code, then run `/mcp` — `kirtis` should appear with 3 tools.

---

## 3. Install the Claude Code skill (optional)

The skill teaches Claude how to interpret kirtis results (stress marks, grammar abbreviations).

Copy `SKILL.md` into your Claude Code skills directory:

**Windows:**
```
copy .claude\skills\kirtis\SKILL.md %USERPROFILE%\.claude\skills\kirtis\SKILL.md
```

**macOS / Linux:**
```bash
mkdir -p ~/.claude/skills/kirtis
cp .claude/skills/kirtis/SKILL.md ~/.claude/skills/kirtis/SKILL.md
```

Or clone into a project's `.claude/skills/kirtis/` folder to scope it to that project.

---

## 4. Start the proxy

Double-click `start.bat`, or run:

```bash
cd kirtis-mcp
uv run python proxy.py
```

Proxy starts at `http://localhost:8010`.  
Verify: open `http://localhost:8010/docs` in browser — should show 3 endpoints.

---

## 5. Connect to Open WebUI

1. Open WebUI → **Admin Panel** → **Tools**
2. Click **"+"** (Add tool server)
3. Set URL: `http://localhost:8010`
4. Click **Save** — three tools appear:
   - `lookup_words` — prefix search
   - `get_stress` — stress marks + grammar for one word
   - `get_word_info` — full two-phase lookup (preferred)

---

## 6. Enable tools on a model

1. Open WebUI → **Workspace** → **Models**
2. Edit the model you want to use
3. Under **Tools**, enable the Kirtis tools
4. Save

Or enable per-chat: click the tools icon in the chat input bar.

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

- Proxy must be running before Open WebUI tries to call the tools
- kirtis.info has no auth — no API key needed
- `/api/krc/` requires capitalized first letter — handled automatically
