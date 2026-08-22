# v1.6.0

Switch themes with the keyboard's own volume knob, visible numbers on the
performance graphs, and a hardware page that explains its dashes.

## Added

- **Knob theme switching** (off by default): turn the Linx68 volume knob
  right for the next theme, left for the previous, press it to pause or
  resume the carousel. The cycle follows the carousel's set when one is
  configured, otherwise the whole catalog.
  - A "mute the volume keys" toggle decides whether the knob only switches
    themes or also changes the system volume.
  - An optional **VID:PID** field binds the feature to the Linx68 alone, so
    other keyboards' and headsets' volume keys keep working normally.
  - A **dedicated-keys mode** for VIA/QMK users: remap the encoder to
    F13/F14 and its press to F15 in the keyboard's configurator, and volume
    is never involved at all. Software volume changes (tray slider, players)
    never touch themes in any mode.

## Fixed

- **Performance graphs**: the per-panel numbers had never been visible — a
  right-aligned text call without a width drew them off the canvas. They now
  render next to each curve, with a "numeric readout" toggle (on by default)
  in the theme's settings.
- **Hardware monitor**: the em dashes now say why. Without administrator
  rights the screen asks for them; already elevated with a board that still
  reports nothing, it says the sensors do not answer. The settings card
  gained a "restart as administrator" button that relaunches the app
  elevated — the only thing that loads the driver behind CPU temperatures
  and fan speeds.

## Platforms

- Windows x64: self-contained portable build — unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; the knob listener is Windows-only and
  no macOS binary is published.
