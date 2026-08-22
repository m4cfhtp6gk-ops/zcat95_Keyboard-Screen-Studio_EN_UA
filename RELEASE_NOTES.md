# v1.5.1

A fix for stale hryvnia exchange rates: the currency screen was showing about
39 UAH per USD from a stale upstream feed while the actual rate was around 45.

## Fixed

- **Currency rates source is now selectable**, like the stock source:
  - **Currency-API** (daily dataset on the jsDelivr CDN, 200+ currencies, no
    key) — the new default, with a silent fallback to ExchangeRate-API when
    the CDN is unreachable;
  - **ExchangeRate-API** open endpoint — the previous source, still available;
  - **NBU official rate** — Ukraine's official daily table; USD→UAH is the
    official number itself, other pairs cross through UAH.
  Existing setups switch to the fresh default automatically.
- **The data date is now on the screen**: the currency footer shows which day
  the rates are for ("Дані 22 серп. · 08:53"), so a stale feed is visible
  instead of silently wrong.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
