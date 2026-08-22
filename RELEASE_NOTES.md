# v1.5.0

Alert priority takeover, five new screens — calendar, countdown, world clocks,
ping monitor, disks — plus sunrise/sunset and air quality in the weather, a
stock portfolio row, per-theme accent colors and pushing to several keyboards.

## Added

- **Alert priority**: when an air-raid alert starts in your selected region,
  the screen shows it by itself — even if the alerts theme is not selected and
  not in the carousel. Full-screen or a Telegram-style popup banner (red while
  active, green at the all-clear), held until the all-clear or for a set number
  of minutes; the all-clear stays visible for a minute either way. The takeover
  outranks the carousel, media automation and the night schedule, renders at
  full brightness, and speeds polling up to every 30 s while armed. The
  location is now a dropdown of the 27 regions (raion- and city-level alerts
  count for their oblast), and after the all-clear the screen shows how long
  the alert lasted.
- **Calendar theme**: today's and the upcoming events from your calendar's ICS
  link (Google, Apple and Outlook all provide one; webcal:// works). Common
  repeat rules are understood — daily, weekly with weekdays, monthly, yearly,
  with exception dates. The link embeds an access key, so it is stored
  locally, sent only to its own host and stripped from settings exports.
- **Countdown theme**: up to three dates to look forward to — the nearest one
  large with the days, hours or minutes left, the rest in rows.
- **World Clock theme**: up to four cities with their local time, the offset
  from yours, ±day marks and a night tint between 22:00 and 07:00.
- **Ping Monitor theme**: ICMP latency to up to four hosts — the router, a DNS
  resolver, your server — green under 60 ms, orange under 150 ms, red beyond,
  with a bar history where lost pings stay visible as gaps.
- **Disks theme**: every ready volume as a fill bar — accent color until 85%,
  orange to 95%, red beyond.
- **Weather extras**: today's sunrise and sunset plus the European air-quality
  index on the five-day screen, colored by band; a failing air-quality request
  never breaks the weather.
- **Stock portfolio**: enter how many shares you hold per symbol and the
  stocks screen adds a portfolio row — total value and the day's change.
  Symbols in different currencies are summed as-is; the hint says so honestly.
- **Per-theme accent color**: any theme can keep its own accent, with a picker
  and a "use global" reset on the theme page.
- **More keyboards**: extra device addresses receive every pushed frame; their
  status shows in the push diagnostics card.

## Changed

- The theme catalog grew from 27 to 32 built-in screens.
- The string catalogues grew to 721 keys, still fully translated into
  English, Ukrainian and Simplified Chinese.
- Settings exports now also strip the alerts.in.ua token and the calendar ICS
  link; imports keep the local values when the file carries blanks.
- Third-party notices now cover Open-Meteo's air-quality endpoint (CAMS data)
  and the user-supplied calendar feed.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; the new themes and settings work there
  too, but the Telegram card, desktop notifications and hardware sensors are
  Windows-only for now, and no macOS binary is published.
