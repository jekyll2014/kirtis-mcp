---
name: kirtis
description: Use the Kirtis MCP tools to look up Lithuanian stress marks and grammatical info from kirtis.info
---

Use the `kirtis` MCP tools to look up Lithuanian stress and grammar.

## When to use which tool

- **Always use `get_word_info`** — it runs both phases and returns everything.
- Use `lookup_words` alone only for prefix/autocomplete search.
- Use `get_stress` alone only when you already have an exact word from `lookup_words`.

## Interpreting results

### `class` — part of speech

| Value | Meaning |
|-------|---------|
| `dktv.` | noun (daiktavardis) |
| `vksm.` | verb (veiksmažodis) |
| `būdv.` | adjective (būdvardis) |
| `prv.` | adverb (prieveiksmis) |
| `įvrd.` | pronoun (įvardis) |
| `sktvrd.` | numeral (skaitvardis) |
| `prln.` | preposition (prielinksnis) |
| `jng.` | conjunction (jungtukas) |
| `išt.` | interjection (ištiktžodis) |
| `dll.` | particle (dalelytė) |

### `state` — grammatical tags (may combine multiple)

**Gender:**
| Tag | Meaning |
|-----|---------|
| `vyr.gim.` | masculine |
| `mot.gim.` | feminine |

**Number:**
| Tag | Meaning |
|-----|---------|
| `vnsk.` | singular |
| `dgs.` | plural |

**Case:**
| Tag | Meaning |
|-----|---------|
| `V.` | nominative |
| `K.` | genitive |
| `N.` | dative |
| `G.` | accusative |
| `Įn.` | instrumental |
| `Vt.` | locative |
| `Š.` | vocative |

### Stress marks on the `word` field

The stressed syllable carries a combining diacritic:
- Grave `` ` `` — falling tone (e.g. `žmogùs`)
- Tilde `~` — rising/mixed tone (e.g. `Ẽiti`)
- Acute — short stress

## Example

Query: `get_word_info("žmogus")`

```json
{
  "query_word": "žmogus",
  "related_words": ["žmogus"],
  "stress_entries": [
    { "word": "Žmogùs", "class": "dktv.", "state": ["vyr.gim.", "vnsk.", "V."] }
  ],
  "has_stress_info": true
}
```

Reading: *žmogus* is a masculine singular nominative noun; stress falls on the final syllable (`ùs`), falling tone.
