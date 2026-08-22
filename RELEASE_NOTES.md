# v1.5.2

Two fixes from the field: the hardware monitor showing zeros instead of
missing readings, and the Claude limits screen stuck behind Cloudflare.

## Fixed

- **Hardware monitor** (AMD showing 0.00 GHz, 0° and no fan):
  - Flat-zero sensor readings — what LibreHardwareMonitor reports on AMD
    without the kernel driver — now draw as the em dash instead of posing as
    real values.
  - CPU frequency now works without administrator rights: when the sensor
    clock is missing, it falls back to Windows' own performance counter times
    the base clock — the same figure Task Manager shows, boost included.
  - When temperatures need more rights, the theme's summary and the settings
    card now say so plainly: run the app as administrator for CPU temperature
    and fan speeds.
  - The sensor library moved to LibreHardwareMonitor's current build with two
    years of newer AMD support, engineering samples included.
- **Claude limits blocked by Cloudflare**:
  - The request now carries the complete header set of the browser the
    session cookie came from, over HTTP/2 — the half-browser fingerprint was
    what kept tripping the challenge.
  - After a challenge the app pauses for five minutes instead of retrying
    every refresh, keeps the last good reading on screen, and the message
    says the key is fine.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
