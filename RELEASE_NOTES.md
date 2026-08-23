# v1.7.0

Assemble your own screen from widgets, and settings that survive crashes.

## Added

- **Screen builder** — a new "My Screen" theme built from widgets. In the
  theme's settings you stack widgets top to bottom and reorder them freely;
  the preview shows the result live and a height budget line says how much
  of the 142×428 screen is used.
  - **21 widget kinds**: clock, date, CPU / RAM / GPU load, network speed,
    hardware temperature/clock/fan, weather now, currency rates, crypto
    price, ping, air-alert status, Claude limits, pomodoro, now playing,
    next calendar event, nearest countdown, world clocks, GitHub today,
    custom text and a spacer.
  - Each widget reuses the data, formatting and translations of its parent
    theme, so everything you already configured (city, currencies, hosts,
    tokens) just shows up.
  - The assembled screen is a normal catalog theme: the carousel, the
    volume-knob switching, per-theme accent colour and refresh interval,
    and the air-alert takeover all work with it unchanged.
  - The app fetches only the data sources your placed widgets actually
    need — an alert widget polls alerts, a screen without one does not.

## Fixed

- **Settings now survive crashes and power loss.** Saves are written to a
  side file and swapped in atomically, and the previous good save is kept
  as a backup; a settings file torn mid-write restores from that backup
  instead of silently resetting every token, layout and binding to
  defaults.
- **The auto-refresh loop no longer dies on unexpected errors** in the
  push path; it reports the failure and retries on the next tick instead
  of stopping until the app restarts.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; the screen builder renders there
  too, while the knob listener stays Windows-only. No macOS binary is
  published.
