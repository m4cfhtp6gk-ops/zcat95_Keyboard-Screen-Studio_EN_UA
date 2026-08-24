# v1.10.0

The Claude screen finally shows your account's real numbers.

## Changed

- **Claude limits come from your account, not from an estimate.** The
  screen asks claude.ai for your subscription's own windows, using the
  login Claude Code already holds on this PC. Nothing to paste, nothing
  to sign in to, no cookie for Cloudflare to block.

  Two earlier designs failed here for the same reason, and it was never
  the address. The first called the right endpoint with a browser session
  cookie, which Cloudflare binds to the browser that solved its
  challenge - a desktop app cannot present it. The second gave up on the
  server and counted tokens in local transcripts, which is honest
  arithmetic about the wrong question: a floor on one machine's usage,
  shown as a percentage of a budget you had to invent yourself.

  What both missed is that Claude Code stores an OAuth token for exactly
  this kind of client. That token is what the cookie was standing in for.

  The token budgets and plan presets are gone with the local tally they
  were a denominator for. Each meter now shows the time until it resets,
  in the space the token count used to take. When there is no login, or
  it has expired, the screen says so rather than drawing a substitute.

- **The knob takes a combination you record yourself.** The old mode
  offered a list of F13-F24, which the Linx68 does not have - it only
  ever worked if you could remap the encoder in VIA/QMK to emit one.
  Press Record, then press whatever the knob sends. A modifier is
  recommended and a binding without one is flagged in red, because the
  app swallows what it binds. Existing F13-F24 settings keep working.

- **Screen builder blocks can be styled one at a time.** A chevron on
  each row reveals a dot-matrix switch for that block's numbers and its
  own accent colour. Labels stay in the normal face - the dot font has
  no Cyrillic - and the switch only appears on blocks that draw a number.

## Fixed

- The weekday is no longer cut to "понеді…" next to the date on the
  dot-matrix analog clock. The line was split by a fixed ratio that gave
  the longer string the smaller half; it is now divided by what the two
  strings actually measure.
- The dot-matrix clock drops its "Hours / Minutes / Seconds" captions.
  Three numbers stacked largest to smallest, the last ticking in the
  accent colour, already say which is which. The column is centred in the
  space that frees.

## Notes

- The Claude screen needs Claude Code signed in on this computer. If the
  login expires, sign in again in Claude Code and the screen recovers on
  its own.
- Your Claude Code token is read fresh when needed, sent to claude.ai and
  nowhere else, and never written into settings, an exported backup or a
  log. KSS does not refresh or modify it, so it cannot affect your
  Claude Code sign-in. See PRIVACY.md.
- The endpoint behind the limits is not a documented public API and may
  change without notice. If it does, the screen reports that it is not
  connected instead of showing an invented figure.
- Upgrading is safe: settings removed in this release are simply ignored
  when your existing file is read.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
