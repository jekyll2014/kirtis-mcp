# kirtis-mcp — Python (legacy)

Python implementation of the kirtis-mcp MCP server. Kept for reference.  
For new deployments use the C# implementation in `../dotnet/`.

## Requirements

- Python 3.11+
- [uv](https://docs.astral.sh/uv/getting-started/installation/)

## Install

```bash
cd python
uv sync
```

## Run

### stdio MCP (Claude Code)

```bash
uv run python server.py
```

Or via `start.bat` — but note: `start.bat` runs `proxy.py`, not `server.py`.  
For stdio use the command above directly.

### SSE server

```bash
uv run python server.py --sse --port 8020
# or:
start-sse.bat
```

### REST proxy (Open WebUI)

```bash
uv run python proxy.py
# or:
start.bat
```

Proxy starts at `http://localhost:8010`. Swagger UI at `http://localhost:8010/docs`.

## Files

| File | Purpose |
|------|---------|
| `server.py` | MCP server — stdio and SSE modes, 3 tools |
| `proxy.py` | FastAPI REST proxy for Open WebUI (port 8010) |
| `pyproject.toml` | Dependencies: `mcp[cli]`, `httpx`, `fastapi`, `uvicorn` |
| `start.bat` | Launch REST proxy (`proxy.py`) |
| `start-sse.bat` | Launch SSE server (`server.py --sse --port 8020`) |

## Note on ports

Python runs two separate processes for SSE and REST:
- SSE server: port 8020 (`server.py --sse`)
- REST proxy: port 8010 (`proxy.py`)

The C# implementation serves both from a single process on port 8020.
