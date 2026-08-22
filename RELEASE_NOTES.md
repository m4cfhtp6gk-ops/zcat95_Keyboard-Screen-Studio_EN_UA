# v1.3.0

Seven new screens — currency rates, crypto, a pomodoro timer, a detailed
hardware monitor, GitHub activity — plus a night schedule, Telegram message
popups on the keyboard screen, desktop notifications and push diagnostics.

## Added

- **Currency Rates theme**: up to four currencies against a base of your
  choice, fed daily by the open ExchangeRate-API endpoint (no key needed).
- **Crypto theme**: two to four Binance pairs with live price, 24-hour change
  and an hourly sparkline; Binance is also available as a third data source in
  the stocks theme.
- **Pomodoro theme**: a focus/break timer as a 30-dot countdown ring with a
  cycle tally. It runs on the wall clock, so it survives restarts and sleep;
  start and stop it from the settings card or the tray menu.
- **Hardware Monitor theme**: two rotating pages — CPU and GPU with
  temperature, clock and fan speed plus VRAM, then memory, disk and network.
  The dwell is configurable (3–10 s). Sensor data comes from the bundled
  LibreHardwareMonitor library; CPU temperature usually needs the app run as
  administrator, and anything unavailable shows an em dash.
- **GitHub Activity theme**: the last 17 weeks of your contribution calendar
  as a dot grid with weekly totals. A username is enough for public data; an
  optional access token also counts private contributions.
- **Night schedule**: between a start and end time (may cross midnight) the
  screen can switch to a chosen theme and dim the render to a configurable
  brightness. Media automation still wins while music plays.
- **Telegram popups**: sign in to your own Telegram account (official MTProto
  API via the open-source WTelegramClient) and incoming messages pop up on the
  keyboard screen for a few seconds over any theme. Three privacy modes
  (count only / sender / sender + preview), muted chats never pop up, and the
  session key stays on this machine — logging out wipes it.
- **Desktop notifications** (Windows, off by default): Claude limits crossing
  80/90/95%, the keyboard going offline, and Binance price alerts with upper
  and lower bounds — every trigger has a cooldown, so nothing spams.
- **Push diagnostics** in Other settings: the device's last response, latency,
  frame size and recent errors.
- A step-by-step in-app guide for obtaining the claude.ai session key.

## Changed

- The theme catalog grew from 21 to 26 built-in screens.
- The string catalogues grew to 577 keys, still fully translated into
  English, Ukrainian and Simplified Chinese.
- Third-party notices now cover every data service the app can talk to.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; the new data themes work there too, but
  the Telegram card, desktop notifications and hardware sensors are
  Windows-only for now, and no macOS binary is published.
