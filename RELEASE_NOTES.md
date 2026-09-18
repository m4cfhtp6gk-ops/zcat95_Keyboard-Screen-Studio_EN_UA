# v1.12.0

Find the keyboard instead of typing its address, and a screen that comes
back by itself after the keyboard is unplugged.

## Added

- **"Find keyboard" next to the address field.** It lists the networks
  this computer is on and waits: nothing is sent until you press again.
  The sweep then asks each address whether it accepts keyboard images -
  a plain GET that cannot change what any screen is showing - and offers
  what answered, with the evidence for each.
- Only private home ranges are ever looked at, anything wider than a /24
  is narrowed to the part this computer is in, and VPN and virtual
  adapters are left alone - including the corporate VPN clients that
  present themselves as ordinary Ethernet. It never runs on its own:
  there is no timer and no startup scan.
- Devices that answered without identifying themselves are listed too,
  because a real keyboard can legitimately land there and the address is
  still worth trying.

## Fixed

- **An unchanged picture is re-sent every two minutes**, so a keyboard
  that was unplugged, power-cycled or reset itself repaints on its own
  instead of waiting for the picture to change. v1.10.0 stopped a failed
  push from being remembered as delivered; this covers the case it could
  not - a device that quietly lost the frame we still believe it holds,
  which on a static wallpaper meant a dark screen for hours.
- **Extra keyboards are tracked on their own.** They used to sit inside
  the primary device's de-duplication check, so an unchanged primary also
  silenced the mirrors, and a mirror that failed was never retried until
  the picture changed.
- A push abandoned mid-flight no longer counts as delivered, and a
  backwards clock step (NTP, daylight saving, resume from sleep) no
  longer suppresses pushing until real time catches up.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
