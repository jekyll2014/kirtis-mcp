#!/usr/bin/env python3
"""MCP server — kirtis.info Lithuanian stress and grammatical info."""

from urllib.parse import quote

import httpx
from mcp.server.fastmcp import FastMCP

BASE_URL = "https://kirtis.info/api"
HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
    ),
    "Accept": "application/json",
    "Accept-Language": "lt-LT,lt;q=0.9,en;q=0.8",
}

mcp = FastMCP("kirtis")


def _capitalize_first(word: str) -> str:
    return word[0].upper() + word[1:] if word else word


@mcp.tool()
async def lookup_words(word: str) -> list[str]:
    """Prefix-search for Lithuanian words. Calls GET /api/zodynas/{word}.

    Use as Phase 1 before get_stress — expands a query word to its related forms.
    Input is lowercased automatically.
    Returns [] on no results or error.

    Args:
        word: Lowercase word or prefix (e.g. "eiti", "ei").
    """
    normalized = word.lower().strip()
    url = f"{BASE_URL}/zodynas/{quote(normalized, safe='')}"
    async with httpx.AsyncClient(headers=HEADERS, timeout=60.0) as client:
        try:
            r = await client.get(url)
            r.raise_for_status()
            return r.json()
        except Exception:
            return []


@mcp.tool()
async def get_stress(word: str) -> list[dict]:
    """Get stress marks and grammatical tags for one Lithuanian word. Calls GET /api/krc/{Word}.

    First letter is capitalized automatically (API requirement).
    Returns [] on no results or error.

    Each entry in the returned list contains:
    - word  : stressed form with combining diacritics (e.g. "Ẽiti")
    - class : part-of-speech abbreviation (e.g. "vksm." = verb, "dktv." = noun)
    - state : list of grammatical tags (e.g. ["mot.gim.", "vnsk.", "V."])

    Args:
        word: Lithuanian word, any capitalisation (e.g. "eiti").
    """
    capitalized = _capitalize_first(word.strip())
    url = f"{BASE_URL}/krc/{quote(capitalized, safe='')}"
    async with httpx.AsyncClient(headers=HEADERS, timeout=60.0) as client:
        try:
            r = await client.get(url)
            r.raise_for_status()
            return r.json()
        except Exception:
            return []


@mcp.tool()
async def get_word_info(word: str) -> dict:
    """Full two-phase lookup: related words + stress info for each. Preferred entry point.

    Phase 1: lookup_words(word)           → related word list
    Phase 2: get_stress(w) for each w     → stress + grammar entries

    Returns dict:
    - query_word     : normalised input
    - related_words  : list[str] from prefix search
    - stress_entries : list[dict] combined entries for all related words (deduped by word)
    - has_stress_info: bool

    Args:
        word: Lithuanian word to look up (e.g. "eiti", "žmogus").
    """
    normalized = word.lower().strip()
    related = await lookup_words(normalized)

    all_entries: list[dict] = []
    for rw in related:
        entries = await get_stress(rw)
        all_entries.extend(entries)

    seen: set[str] = set()
    deduped: list[dict] = []
    for entry in all_entries:
        key = entry.get("word", "").lower()
        if key not in seen:
            seen.add(key)
            deduped.append(entry)

    return {
        "query_word": normalized,
        "related_words": related,
        "stress_entries": deduped,
        "has_stress_info": bool(deduped),
    }


if __name__ == "__main__":
    import sys
    if "--sse" in sys.argv:
        port = int(sys.argv[sys.argv.index("--port") + 1]) if "--port" in sys.argv else 8020
        sse_server = FastMCP("kirtis", host="127.0.0.1", port=port)
        sse_server.add_tool(lookup_words)
        sse_server.add_tool(get_stress)
        sse_server.add_tool(get_word_info)
        sse_server.run(transport="sse")
    else:
        mcp.run()
