# v1.9.0

The Claude screen finally works, and so does the first five minutes with
a new keyboard.

## Changed

- **Claude limits are read from Claude Code's own transcripts on this
  PC.** No login, no session key, no status-line setup, no request to
  claude.ai - and nothing left for Cloudflare to block. Every previous
  design asked a server for a number it would not hand a desktop app;
  this one reads files that are already on your disk.
- Because Anthropic publishes no quota that software can read, the three
  meters fill against a **token budget you set**, with plan presets as a
  starting point. Pick your plan, then adjust once you have seen a week
  of your own use. The counts only cover Claude Code on this machine, so
  they are a floor on real usage rather than an account total.
- "Screen setup" and "Theme settings" were the wrong way round - the
  first was a read-only status page, the second held the actual setup.
  They are now "Overview" and "Screen setup".
- The screen builder's editor moved out of Automation onto the theme
  page, beside the other theme settings.
- Telegram, notifications, the knob and diagnostics collapse in Other
  settings, so the device address is not buried under a login form.

## Fixed

- **The first-run IP field corrupted most home addresses.** Typing
  `192.168.1.50` produced `192..168.150`, because the focus advanced
  both when an octet filled and again on the dot you typed after it.
  "Save and continue" then did nothing at all - silently - because the
  red validation line was never filled in. Both fixed, and pasting a
  whole address now works.
- **The picture theme's clock was frozen.** The refresh loop skipped
  that screen as "static", but it always draws a clock. Fixed on both
  platforms, with the photo cached so the restored refresh does not
  re-read it from disk every second.
- A failed push was recorded as delivered, so a network blip on an
  unchanged frame meant the keyboard never received that content again.
- The device badge no longer reports "Disconnected" before anything has
  been tried, or permanently when automatic push is off.
- There is now a manual **Send now**, beside the preview and in the tray
  menu. The command existed but had never been bound to anything.
- A mistyped weather city reported "this theme uses only local time and
  settings" instead of the real error.
- Every scrollbar was invisible *and* unclickable, including the
  33-theme sidebar where only about eight rows fit. They are back at the
  10 px the design system always specified.
- Secondary text in the light theme now meets WCAG AA; it measured
  3.89:1.
- The shipped default font id matched nothing, so the font list was
  empty on every first launch.
- An exception during startup no longer wedges every later settings
  change into a silent no-op, and is written to the crash log instead of
  a `Debug.WriteLine` that Release builds compile out.

## Notes

- The Claude screen needs Claude Code to have run on this computer at
  least once. Until then it says so plainly rather than showing an
  error.
- Upgrading is safe: the removed Claude settings (session key,
  organization id, source choice) are simply ignored when your existing
  settings file is read, and the new budget fields take their defaults.
- The theme no longer stores any credential, so exported settings have
  nothing to strip for it. See PRIVACY.md.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
