# v1.7.1

The Claude limits source stopped getting through Cloudflare again; this
release refreshes how the app identifies itself and how it behaves when
challenged.

## Fixed

- **Claude limits: Cloudflare challenges again.** The requests now carry
  the fingerprint of the current Chrome (151) instead of a year-old one —
  an outdated browser version claiming fresh client hints is itself a bot
  signal. When a challenge still happens, the retry backs off gradually
  (5 minutes doubling up to an hour) instead of knocking every 5 minutes,
  the usage is fetched half as often (every 2 minutes by default), and
  the last good reading stays on screen meanwhile.
- The Claude widget on the assembled screen now says "Cloudflare block ·
  auto-retry soon" when that is what is happening, instead of a generic
  "not connected" — the session key is fine in that state and nothing
  needs re-entering.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
