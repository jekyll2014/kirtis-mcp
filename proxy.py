#!/usr/bin/env python3
"""FastAPI proxy — exposes kirtis MCP tools as OpenAPI endpoints for Open WebUI."""

import uvicorn
from fastapi import FastAPI
from pydantic import BaseModel

import server as kirtis

app = FastAPI(
    title="Kirtis Lithuanian Stress API",
    description="Lithuanian stress marks and grammatical info from kirtis.info",
    version="1.0.0",
)


class WordInput(BaseModel):
    word: str


@app.post("/lookup_words", summary="Prefix-search Lithuanian words")
async def lookup_words(body: WordInput) -> list[str]:
    """Search for related/similar Lithuanian words via /api/zodynas/.

    Returns list of matching words. Use before get_stress to expand a query word.
    Input is lowercased automatically.
    """
    return await kirtis.lookup_words(body.word)


@app.post("/get_stress", summary="Get stress marks and grammatical tags")
async def get_stress(body: WordInput) -> list[dict]:
    """Get stress marks and grammatical tags for one Lithuanian word via /api/krc/.

    First letter is capitalized automatically (API requirement).

    Each entry contains:
    - word  : stressed form with diacritics (e.g. "Žmogùs")
    - class : part-of-speech abbreviation (e.g. "dktv." = noun, "vksm." = verb)
    - state : list of grammatical tags (e.g. ["vyr.gim.", "vnsk.", "V."])
    """
    return await kirtis.get_stress(body.word)


@app.post("/get_word_info", summary="Full two-phase lookup (preferred)")
async def get_word_info(body: WordInput) -> dict:
    """Full lookup: related words + stress info for each. Preferred entry point.

    Returns:
    - query_word     : normalised input
    - related_words  : words from prefix search
    - stress_entries : combined stress+grammar entries (deduped)
    - has_stress_info: bool
    """
    return await kirtis.get_word_info(body.word)


if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8010)
