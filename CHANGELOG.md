# Changelog

## Unreleased

### Changed

- Claude limits now come from Claude Code's own transcripts on this PC. No
  cookie, no session key, no status-line setup, no request to claude.ai -
  and nothing that can be rate-limited or challenged. The meters fill
  against a token budget you set, with plan presets as a starting point,
  because no local file records the account's real quota. Counts cover this
  machine only, so they are a floor on real usage.
- "Screen setup" and "Theme settings" were the wrong way round: the first
  was a read-only status page, the second held the actual screen setup.
  They are now "Overview" and "Screen setup", and the accent hint no longer
  points at the wrong page.
- The screen builder's editor moved from Automation onto the theme page,
  next to the other theme settings.
- Telegram, notifications, the knob and diagnostics collapse in Other
  settings, so the device address is not below an MTProto login form.
- Scrollbars are visible and draggable again, at the 10 px the design system
  always specified. They had been set to zero opacity and no hit-testing, so
  nothing indicated that 25 of the 33 themes were below the fold.

### Fixed

- The first-run IP field corrupted any address with a three-digit octet -
  192.168.1.50 became 192..168.150 - and "Save and continue" then did
  nothing at all, silently, because the validation line was never filled in.
  Pasting a whole address works now too.
- The picture theme's clock was frozen: the refresh loop skipped that screen
  as "static", but it always draws a clock. Fixed on both platforms; the
  photo is cached so the restored refresh does not re-read it every frame.
- A failed push was recorded as delivered, so a network blip on an unchanged
  frame meant the keyboard never received that content again.
- The device badge no longer claims "Disconnected" before anything has been
  tried, or forever when automatic push is off.
- There is now a manual "Send now", by the preview and in the tray menu. The
  command existed but had never been bound to anything.
- A bad weather city reported "this theme uses only local time and settings"
  instead of the real error.
- Secondary text in the light theme now meets WCAG AA; it measured 3.89:1.
- The shipped default font id matched nothing, so the font list was empty on
  every first launch.
- An exception during startup no longer wedges every later settings change
  into a silent no-op, and is written to the crash log instead of a
  Debug.WriteLine that Release builds compile out.

## v1.8.0 - 2026-08-23

### Added

- Claude limits read from Claude Code's own status-line report: no
  cookie, no token, no request to claude.ai. Now the default source,
  with one-click setup that preserves any existing status line and every
  other Claude Code setting.

### Fixed

- The limits screen no longer depends on getting past Cloudflare. Absent
  windows and the known epoch-in-percentage bug are reported as "no
  data", and an elapsed window reads as 0% instead of vanishing.
- The cookie source no longer impersonates Chrome, recognises a
  Cloudflare challenge by its `cf-mitigated` header instead of an English
  body phrase (a challenge was being misreported as a rejected key),
  keys its cache on the cookie's value rather than its length, cannot
  report a stale HTTP status, and no longer extends its own backoff when
  the diagnostic button is pressed.

## v1.7.2 - 2026-08-23

### Fixed

- Claude limits: cookies issued by claude.ai and Cloudflare are kept and
  sent with the following requests, and the session-key field accepts a
  whole browser `Cookie:` line (the one carrying `cf_clearance`). This
  helps but cannot be a cure: `cf_clearance` is bound to the browser that
  solved the challenge, TLS fingerprint included.

### Added

- A "Test connection" button in the Claude settings reporting the failing
  call, HTTP status, challenge detection and the cookies sent (names and
  lengths only).

## v1.7.1 - 2026-08-23

### Fixed

- Claude limits: requests identify as the current Chrome (151) instead of
  a year-old build, Cloudflare challenges back off gradually (5 minutes
  doubling up to an hour), the usage is polled half as often, and the
  composer widget names the Cloudflare state instead of a generic "not
  connected".

## v1.7.0 - 2026-08-23

### Added

- Screen builder: a new "My Screen" theme assembled from widgets. Pick
  and order 21 widget kinds - clock, date, CPU/RAM/GPU load, network
  speed, hardware sensors, weather, currency rates, crypto, ping,
  air-alert status, Claude limits, pomodoro, now playing, next calendar
  event, nearest countdown, world clocks, GitHub today, custom text and
  a spacer - in the theme's settings, with a live height budget and an
  instant preview. The assembled screen is a normal catalog theme: the
  carousel, the knob, per-theme accent and refresh intervals, and the
  alert takeover all work with it unchanged.

### Fixed

- Settings survive crashes: saves swap in atomically and keep the
  previous good file as a backup; a settings file torn by a power loss
  restores from that backup instead of silently resetting every token
  and layout to defaults.
- The auto-refresh loop now survives an unexpected push error instead
  of stopping until the app restarts.

## v1.6.0 - 2026-08-22

### Added

- Knob theme switching (off by default): the Linx68 volume knob steps
  through the themes - right for next, left for previous, press to pause
  or resume the carousel - following the carousel's set when configured,
  otherwise the whole catalog. A toggle mutes the volume keys while the
  knob drives themes; an optional VID:PID binds the feature to this
  keyboard alone; and a dedicated-keys mode listens for VIA/QMK-remapped
  F13-F24 so volume is never involved.

### Fixed

- Performance graphs: the per-panel numbers drew off the canvas and had
  never been visible; they now render next to each curve with an
  on-by-default "numeric readout" toggle.
- Hardware monitor: the dashes explain themselves - the screen asks for
  administrator rights when that is the blocker, or says the sensors do
  not answer when the process is already elevated; a "restart as
  administrator" button relaunches the app elevated.

## v1.5.2 - 2026-08-22

### Fixed

- Hardware monitor: flat-zero sensor readings (AMD without the kernel
  driver) draw as missing instead of 0.00 GHz / 0°; CPU frequency falls
  back to the Windows performance counter and works without administrator
  rights; a plain hint says temperatures and fans need the app run as
  administrator; LibreHardwareMonitorLib moved to the current 0.9.7
  prerelease for newer AMD (engineering-sample) support.
- Claude limits: requests now carry the full browser header set over
  HTTP/2, and after a Cloudflare challenge the app backs off for five
  minutes instead of retrying every refresh - keeping the last good
  reading and saying the key is fine.

## v1.5.1 - 2026-08-22

### Fixed

- Stale hryvnia rates (about 39 UAH/USD shown against the actual ~45): the
  currency source is now selectable - Currency-API on the jsDelivr CDN (new
  default, daily, with a fallback to ExchangeRate-API), the ExchangeRate-API
  open endpoint, or the NBU official rate with other pairs crossed through
  UAH - and the screen footer shows which day the rates are for, so a stale
  feed is visible instead of silently wrong.

## v1.5.0 - 2026-08-22

### Added

- Alert priority: an alert in the selected region takes over the screen by
  itself - full-screen or a Telegram-style popup banner - until the all-clear
  or for a set number of minutes, outranking the carousel, media automation
  and the night schedule at full brightness. Region dropdown (raion/city
  alerts count for their oblast) and an alert-duration line after the
  all-clear.
- Calendar theme: upcoming events from a user-supplied ICS link (webcal://
  works) with the common repeat rules; the link is treated as a credential
  and stripped from settings exports.
- Countdown theme: up to three events, the nearest large in days/hours/
  minutes left.
- World Clock theme: up to four cities with offsets, day marks and a night
  tint.
- Ping Monitor theme: latency to up to four hosts with loss-preserving bar
  history.
- Disks theme: fill bars for every ready volume.
- Weather: sunrise/sunset and the European AQI (colored by band) on the
  five-day screen.
- Stocks: per-symbol quantities and a portfolio row with the day's change.
- Per-theme accent colors; extra keyboards mirroring every pushed frame.

### Changed

- 32 built-in themes (was 27); string catalogues at 721 keys in all three
  languages; exports also strip the alerts token and calendar link;
  third-party notices cover the air-quality endpoint and calendar feeds.

## v1.4.0 - 2026-08-22

### Added

- Air Alerts theme: Ukrainian civil-defence alert status from alerts.in.ua —
  a location's ALERT/Clear state with the start time, or the country-wide
  count and list of regions under alert. Needs a free alerts.in.ua token
  (stored locally, sent to that service only, excluded from settings export);
  informational only, never a sole warning source.
- Theme carousel: rotate a chosen set of themes on a wall-clock interval
  (10-600 s); media automation and the night schedule still take precedence.
- Per-theme refresh intervals (1-600 s) with sensible defaults, so the clock
  can tick every second while weather or rates refresh once a minute.
- Display units: 12/24-hour clock and Celsius/Fahrenheit toggles applied
  across all themes.
- Settings export/import as JSON with secrets (claude.ai session key, GitHub
  token, Telegram credentials) stripped from the export and preserved
  locally on import.

### Changed

- 27 built-in themes (was 26); string catalogues at 624 keys in all three
  languages; third-party notices now cover alerts.in.ua.

## v1.3.0 - 2026-08-22

### Added

- Currency Rates theme: up to four currencies against a chosen base, fed
  daily by the open ExchangeRate-API endpoint (no key).
- Crypto theme: two to four Binance pairs with live price, 24-hour change and
  an hourly sparkline; Binance also became a third source in the stocks theme.
- Pomodoro theme: a wall-clock focus/break timer with a 30-dot countdown ring
  and a cycle tally, controlled from settings or the tray menu.
- Hardware Monitor theme: CPU/GPU temperatures, clocks, fan speeds and VRAM
  plus a memory/disk/network page, rotating on a configurable 3-10 s dwell
  (LibreHardwareMonitor; missing readings draw as an em dash).
- GitHub Activity theme: 17 weeks of the contribution calendar as a dot grid
  with weekly totals; public by username, private with an optional token.
- Night schedule: an optional night theme and render dimming (20-100%)
  between configurable times; media automation still wins while music plays.
- Telegram popups: incoming messages from your own account (MTProto via
  WTelegramClient) shown over any theme, with three privacy modes, respect
  for muted chats, and a session that logging out wipes.
- Desktop notifications (Windows, opt-in): Claude limits at 80/90/95%, the
  keyboard going offline, and Binance price alerts - all with cooldowns.
- Push diagnostics card and a step-by-step claude.ai key guide.

### Changed

- 26 built-in themes (was 21); string catalogues at 577 keys in all three
  languages; third-party notices now cover every external data service.

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
