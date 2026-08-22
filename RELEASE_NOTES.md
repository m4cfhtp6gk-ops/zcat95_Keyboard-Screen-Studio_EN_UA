# v1.4.0

An air-raid alerts screen for Ukraine, a theme carousel, per-theme refresh
intervals, 12/24-hour and °C/°F display units, and settings backup with
secrets stripped.

## Added

- **Air Alerts theme**: Ukrainian civil-defence alert status from the
  alerts.in.ua API. With a location set it shows a red **ТРИВОГА** or green
  **Чисто** state with the alert's start time; with the location left empty it
  shows the country-wide picture — the number of regions under alert and their
  list. Requires a free API token from alerts.in.ua (stored locally, sent to
  that service only, excluded from settings export). The screen is
  informational and must never be your only air-raid warning source.
- **Theme carousel**: rotate through a chosen set of themes on a fixed
  interval (10–600 s). Rotation follows the wall clock, so it stays in step
  after sleep or restart; media automation and the night schedule still win
  while they apply.
- **Per-theme refresh interval**: every theme's settings page now has its own
  refresh interval (1–600 s), so the clock can tick every second while
  weather or currency rates refresh once a minute. Sensible defaults per
  theme; the global interval remains the fallback.
- **Display units**: a 12/24-hour clock toggle and a Celsius/Fahrenheit
  toggle in Other settings, applied across every theme that shows a time or a
  temperature.
- **Settings backup**: export all settings to a JSON file and import them on
  another machine. Secrets — the claude.ai session key, GitHub token and
  Telegram credentials — are stripped from the export; importing a file with
  blank secret fields keeps the ones already on the machine.

## Changed

- The theme catalog grew from 26 to 27 built-in screens.
- The string catalogues grew to 624 keys, still fully translated into
  English, Ukrainian and Simplified Chinese.
- Third-party notices now cover the alerts.in.ua data service.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; the new themes and settings work there
  too, but the Telegram card, desktop notifications and hardware sensors are
  Windows-only for now, and no macOS binary is published.
