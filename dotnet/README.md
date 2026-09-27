# kirtis-mcp — C# (.NET)

.NET 9 implementation of the kirtis-mcp MCP server.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet build
```

## Run

```
KirtisMcp                     MCP stdio server (default, for Claude Code)
KirtisMcp --sse [--port N]    MCP SSE + REST server (default port: 8020)
KirtisMcp --help              Show this help
```

Via `dotnet run`:

```bash
dotnet run                        # stdio
dotnet run -- --sse               # SSE + REST on port 8020
dotnet run -- --sse --port 8021   # custom port
dotnet run -- --help
```

Via bat files (from repo root):

```
start.bat          # stdio
start-sse.bat      # SSE on port 8020
```

## SSE mode endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /mcp` | MCP over SSE (for Claude Code / Unsloth) |
| `POST /lookup_words` | REST: `{"word": "..."}` |
| `POST /get_stress` | REST: `{"word": "..."}` |
| `POST /get_word_info` | REST: `{"word": "..."}` (preferred) |

## Publish (optional — faster startup)

```bash
dotnet publish -c Release -r win-x64 --self-contained -o publish
```

Then register `publish\KirtisMcp.exe` directly in `~/.claude.json` instead of `dotnet run`.

## Project structure

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point — mode detection, DI setup |
| `KirtisTools.cs` | MCP tool implementations |
| `Models.cs` | `WordEntry`, `WordInfo` record types |
| `KirtisMcp.csproj` | Project file — `ModelContextProtocol.AspNetCore` |
