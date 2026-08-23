# v1.8.0

Claude limits without a login, a cookie, or anything for Cloudflare to
block.

## Added

- **Claude limits straight from Claude Code.** Claude Code hands its
  status line the same 5-hour and weekly numbers `/usage` shows; the app
  now reads those, so the limits screen needs no session cookie, no
  token, and makes no request to claude.ai at all. This is the new
  default source, and "Set up Claude Code" in the Claude settings wires
  it up in one click - your existing status line is never replaced
  without asking, and every other Claude Code setting is left untouched.
- The claude.ai cookie source is still selectable, and is still the only
  one that reports the per-model weekly window.

## Fixed

- The limits screen no longer depends on getting past Cloudflare, which
  is what kept breaking it: the previous approaches - browser-coherent
  headers, a current Chrome fingerprint, and full cookie handling - are
  all still in, but none of them can survive `cf_clearance` being bound
  to the browser that solved the challenge.

## Notes

- Claude Code reports these limits only for Claude.ai subscribers, and
  only once a session has had its first response, so the screen says it
  is waiting until Claude Code has run at least once.
- Each window can be absent on its own, and a known Claude Code bug can
  put a timestamp where a percentage belongs; both are discarded rather
  than drawn as a wrong number.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
