<p align="center"><img src="docs/images/keyboard-screen-studio-hero.png" alt="Keyboard Screen Studio for Linx68" /></p>

# Keyboard Screen Studio

*Читати [українською](README.uk.md) · [简体中文](README.zh-Hans.md)*

Keyboard Screen Studio (KSS) is a desktop tool for the Linx68 keyboard's portrait
display. It renders system status, clocks, weather, market quotes, media
information and picture themes onto the 142×428 screen and pushes them to the
keyboard automatically through its image API.

The interface and the rendered themes are available in **English, Ukrainian and
Simplified Chinese**; the language is switchable at runtime under *Other settings*.

## Status

- **Windows:** v1.3.0, on Avalonia UI. This is the released platform.
- **macOS:** the cross-platform project and packaging scripts are kept in the
  tree. Intel and Apple Silicon builds still need testing on real hardware and
  are not published as a stable release.
- **Linux:** not released. The data, rendering and platform layers leave room
  for it.

## Features

- 26 built-in themes across monitoring, time, information, music, dot matrix
  and picture-clock categories — including a detailed hardware monitor with
  rotating pages, currency rates, Binance crypto with sparklines, a pomodoro
  timer, and a GitHub contribution grid.
- A night schedule that can switch the theme and dim the screen between
  configurable hours, Telegram message popups from your own account drawn
  over any theme, and opt-in Windows notifications (Claude limits, keyboard
  offline, price alerts) with anti-spam cooldowns.
- Live 142×428 preview that keeps the keyboard firmware's status-bar safe area.
- Custom accent colour, screen font, content safe area and picture-clock layout.
- Light, dark and follow-the-system application themes, independent of what the
  keyboard renders.
- Scheduled refresh and push, automatic media theme switching, tray residency
  and launch at startup.
- A first-run guide that asks only for the keyboard's IP address.
- AI usage (in development) reads from a Tokscale installation you set up
  yourself; KSS never stores platform credentials.
- A Claude limits theme showing the session, weekly and per-model windows as
  three horizontal meters. It needs a claude.ai session cookie you paste in
  yourself — see [PRIVACY.md](PRIVACY.md) for exactly what is stored and sent.

## Requirements

- Windows 10 19041 or newer.
- The computer and the keyboard on the same local network.
- "Image API" enabled in the keyboard menu, with the address it shows entered
  into KSS.

Download `KeyboardScreenStudio-v1.3.0-win-x64.zip` from Releases, unpack it and
run `KeyboardScreenStudio.exe`. The release is self-contained — no separate .NET
installation is required.

## Building from source

```powershell
dotnet restore KeyboardScreenStudio.sln
dotnet build src/KeyboardScreen.App.Avalonia/KeyboardScreen.App.Avalonia.csproj -c Release
dotnet run --project tests/KeyboardScreen.SmokeTests/KeyboardScreen.SmokeTests.csproj -c Release
```

Create the portable Windows package:

```powershell
./tools/Publish-Portable.ps1
```

> Install the .NET 9 SDK locally for the full set of Avalonia compile-time
> diagnostics. The .NET 8 SDK also builds the solution, with one analyzer
> version warning.

## Translations

User-facing strings live in three embedded catalogues under
`src/KeyboardScreen.Core/Localization/`:

| File | Language |
| --- | --- |
| `Strings.en.json` | English |
| `Strings.uk.json` | Ukrainian |
| `Strings.zh-Hans.json` | Simplified Chinese |

All three files carry the same keys. To add a language, add an entry to
`AppLanguageInfo.All`, add a matching `Strings.<id>.json`, and register it as an
`EmbeddedResource` in `KeyboardScreen.Core.csproj`. The smoke tests fail the
build if the catalogues drift apart, if a value is empty, or if a translation
does not take the same `{0}`…`{n}` placeholders as English.

Screen text is drawn onto a 142 px wide display, so keys prefixed `Screen*` must
stay short in every language — the renderer truncates with an ellipsis rather
than wrapping.

## Data and privacy

KSS keeps settings and sign-in state on the local machine only. Weather, quotes
and AI usage come from third-party sources and may be delayed, inaccurate or
change without notice; stock information is shown for display purposes and is
not investment advice. See [PRIVACY.md](PRIVACY.md) and
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licence

The source code is under the [MIT License](LICENSE). The application and tray
icons are covered separately — see [ASSET_LICENSE.md](ASSET_LICENSE.md).
