# Changelog

## v1.2.0 - 2026-08-21

### Added

- English, Ukrainian and Simplified Chinese localization for the whole
  application: desktop UI, the themes rendered onto the keyboard screen, and
  service and diagnostic messages.
- A language picker in *Other settings*. The choice is saved with the rest of
  the settings; on first run the app follows the operating system's UI culture.
- Switching language takes effect immediately — open windows, theme names, the
  font list and the rendered preview all update without a restart.

- A Claude limits theme: the rolling session window, the weekly window across
  every model, and the weekly window for one model, drawn as three horizontal
  meters with a reset countdown and a token count on each row. It reads
  claude.ai with a session cookie you paste in yourself, and shows a notice
  explaining what is stored and sent before asking for it.

### Changed

- Dates, weekday names and numbers follow the selected language.
- Open-Meteo and BigDataCloud are asked for place names in the active language.
- The default weather city is a language-neutral constant instead of 北京.
- The stock gain/loss colour and data-source preferences are stored as values
  rather than as their display text, so changing language cannot invalidate a
  saved choice.

### Fixed

- The dot-matrix analog clock's DOWN / UP row now shares the CPU / MEM column
  grid instead of pushing UP to the right edge.

## v1.1.2 - 2026-08-10

### Improved

- Much smaller download: the single-file portable build went from roughly
  208 MB to roughly 121 MB.
- The theme renderer moved to Skia, so Windows and macOS render identically.
  A few themes may look slightly different than before.
- MiSans is bundled and used as the default font, for more consistent text and
  numerals.

### Fixed

- The dot-matrix themes (dot-matrix clock, dot-matrix analog clock, dot-matrix
  progress, dot-matrix weather clock) rendered numerals as fused blocks
  instead of separate dots.

## v1.1.0 - 2026-08-10

### Added

- A "Performance graphs" theme: rolling curves for CPU, memory, network
  download/upload and GPU usage, with each of the five modules switchable.
- Rebuilt stock theme: subscribe to 2–5 symbols, each individually switchable.
  Quotes use a divider layout so very large moves no longer overflow, and with
  exactly two symbols that both have trend data, a five-day closing-price
  comparison chart is drawn automatically.
- A Tencent quote source (the default, reachable directly inside mainland
  China). The Yahoo Finance source is kept and can be selected in the theme
  settings.
- "Check for updates" on the About page: one click looks for a newer GitHub
  release and offers to open the download.

### Improved

- The scheduled push interval accepts any value from 1 second to 1 hour.
- The window resizes from all four edges and corners while keeping its
  borderless, large-radius look.
- Music theme status wording is consistent (playing / paused / not playing);
  "Windows Media", "LIVE", "PAUSED" and "ON AIR" are gone, and the MUSIC
  placeholder on the poster artwork is centred.
- Consistent header title and clock layout across themes; the music poster
  theme no longer shows a header title.
- Spacing and alignment cleanups in the settings UI (font buttons, stock rows,
  the scheduled-refresh card).

### Fixed

- The performance-graph module switches had no effect.
- Stock theme issues: overlapping rows with several symbols, the five-day trend
  crossing the change-percent text, missing two-symbol trends, and trend data
  not being fetched for some markets (A-shares / Hong Kong / US).
- Settings UI details: the focus style on the scheduled-refresh input and the
  spacing of the fonts-folder button.

## v1.0.2 - 2026-08-09

- Fixed a crash in the guide window on a first run with no settings file, which
  made the application exit immediately.
- Fixed the main window flashing and disappearing after launch at startup when
  the tray icon was clicked, leaving the settings unreachable.
- Fixed the same class of crash in the accent-colour picker, and removed the
  dependency on compile-time generated fields so a local SDK can build directly.

## v1.0.1 - 2026-08-04

- The desktop interface moved to Avalonia UI; Windows remains the officially
  supported platform.
- Unified windows, cards, drop-downs, dialogs, dark mode and the common
  interaction motion.
- Added and refined the dot-matrix analog clock, dot-matrix progress, five-day
  weather, stocks and picture-clock themes.
- AI usage switched to a general Tokscale integration and is marked as in
  development.
- Improved the first-run guide, automation settings, tray behaviour and device
  address configuration.
- Fixed a range of issues with inputs, switches, scrolling, corner radii, the
  preview safe area and theme data bleeding between themes.
- The macOS project keeps Intel and Apple Silicon build paths; it still needs
  testing on real hardware.
