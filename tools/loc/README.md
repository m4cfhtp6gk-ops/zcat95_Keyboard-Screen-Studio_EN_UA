# Localization generator

The three embedded catalogues in `src/KeyboardScreen.Core/Localization/`
(`Strings.en.json`, `Strings.uk.json`, `Strings.zh-Hans.json`) are generated
from one side-by-side table so the languages can never drift apart.

## Layout

| File | Contents |
| --- | --- |
| `strings_core.py` | Theme catalogue names, descriptions, formats |
| `strings_screen.py` | Text drawn on the 142×428 keyboard screen |
| `strings_ui.py` | Settings window, hints, statuses, errors |

Each entry is a `(key, en, uk, zh)` tuple. The Chinese column keeps the
upstream project's original wording verbatim where one exists.

## Regenerating

```
python3 tools/loc/gen.py
```

Runs from any directory — output paths resolve relative to this script.
The generator refuses duplicate keys and placeholder (`{0}`) mismatches
between languages; the smoke tests additionally enforce key parity across
the three JSONs, so CI catches a stale regeneration.

## Rules

- **Never edit the JSONs by hand.** Edit the tables here and regenerate;
  a hand edit is silently reverted by the next regeneration. If a JSON was
  edited directly anyway (e.g. in a quick fix), port that edit into the
  table before the next run.
- Ukrainian apostrophes are normalised to `'` automatically.
- Every user-visible string added to the app needs a key in all three
  languages — the smoke test fails on missing keys.
