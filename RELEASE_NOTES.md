# v1.9.0

Find the keyboard instead of typing its address, and a screen that comes
back by itself after the keyboard is unplugged.

## Fixed

- **A keyboard that went offline could stay blank.** The app remembered
  every push as delivered, including the ones that failed - so once a
  push failed, the identical next frame was skipped and the device was
  never sent the picture it had missed. A frame is now recorded only when
  the device acknowledged it, any failure forgets what that device holds,
  and each extra keyboard is tracked on its own, so one failing mirror no
  longer holds back the others.
- **An unchanged picture is re-sent every two minutes** even when nothing
  needs updating, so a keyboard that was unplugged, power-cycled or reset
  itself repaints on its own instead of waiting for the picture to change.
  This covers the static-wallpaper case, where nothing would otherwise be
  sent for hours.

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
  under their own heading, because a real keyboard can legitimately land
  there and the address is still worth trying.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
