# v1.2.0

The interface and every rendered theme are now available in English, Ukrainian
and Simplified Chinese, and a new theme shows your Claude limits on the
keyboard screen.

## Added

- Full localization: the desktop UI, all 21 screen themes, and service
  messages ship in English, Ukrainian and Simplified Chinese. A language
  picker lives in Other settings; the first run follows your operating
  system's language, and switching applies immediately without a restart.
- Claude limits theme: the rolling 5-hour session window, the weekly window,
  and the weekly window for one model (Fable by default) as three horizontal
  meters — the percentage your account reports, a countdown to the reset, and
  the tokens counted from the Claude Code transcripts on this machine. It
  needs a claude.ai session cookie you paste in yourself; the app explains
  exactly what is stored and where it is sent before asking for it, and the
  cookie never leaves your machine except to claude.ai.

## Changed

- Dates, weekday names and numbers follow the selected language, and weather
  place names are requested in it.
- The default weather city is no longer hardcoded to a single region.

## Fixed

- The dot-matrix analog clock's DOWN / UP row now lines up with the CPU / MEM
  columns above it.

## Platforms

- Windows x64: this release ships a self-contained portable build — unpack
  and run `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: the project builds and is checked in CI, but still needs verification
  on real hardware and is not part of this release.
