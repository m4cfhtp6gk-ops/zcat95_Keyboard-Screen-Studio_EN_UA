# v1.7.2

Cookie handling that matches what a browser actually does, and a button
that reports what is happening instead of leaving it to guesswork.

## Fixed

- **Cookies are kept, and can be pasted whole.** A browser sends
  `cf_clearance` and `__cf_bm` alongside `sessionKey`; the app sent only
  the session key and discarded every cookie the server handed back, so
  each request arrived looking like a first-time stranger. Cookies set by
  the server now ride along on the following requests, and the key field
  accepts the entire `Cookie:` line copied from the browser devtools.

## Added

- **"Test connection" in the Claude settings.** One live round-trip that
  reports which call failed, the HTTP status, whether the body was a
  Cloudflare challenge, and which cookies were sent (names and lengths
  only - never values, so the line is safe to share).

## Honest note on the Cloudflare block

This is a real improvement, not a guaranteed cure. `cf_clearance` is tied
to the browser that solved the challenge - including its TLS fingerprint,
which a .NET client cannot reproduce - so a pasted cookie line can stop
working when Cloudflare re-challenges. That is why this release adds the
diagnostic: the next step is chosen from what the button reports, and a
credential-free source that never touches claude.ai (Claude Code's own
rate-limit data) is being built for the release after this one.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
