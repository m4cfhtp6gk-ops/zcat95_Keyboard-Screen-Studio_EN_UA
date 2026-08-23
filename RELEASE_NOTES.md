# v1.7.2

Cloudflare kept blocking the Claude limits after v1.7.1. This release
fixes the mechanism that was actually missing, and adds a button that
says what is happening instead of leaving it to guesswork.

## Fixed

- **Cookies are now kept, and can be pasted whole.** A browser sends
  `cf_clearance` and `__cf_bm` alongside `sessionKey`; the app sent only
  the session key and threw away every cookie Cloudflare handed back, so
  each request arrived looking like a first-time stranger. Cookies set by
  the server are now carried into the following requests, and the key
  field accepts the entire `Cookie:` line copied from the browser's
  devtools — that line carries `cf_clearance`, which is what a solved
  challenge actually leaves behind.

## Added

- **"Test connection" in the Claude settings.** One live round-trip that
  reports which call failed, the HTTP status, whether the body was a
  Cloudflare challenge, and which cookies were sent (names and lengths
  only — never values, so the line is safe to share).

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
