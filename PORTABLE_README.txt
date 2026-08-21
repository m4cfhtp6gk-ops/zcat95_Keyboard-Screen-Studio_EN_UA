Keyboard Screen Studio v1.0.2 portable build

1. Double-click KeyboardScreenStudio.exe to start. No .NET installation needed.
2. The first run shows the connection guide. The program creates a Data folder
   and keeps this machine's settings there.
3. Drop TTF or OTF fonts you are licensed to use into the Fonts folder and the
   program picks them up automatically.
4. The program pushes JPEGs straight to the keyboard over HTTP POST.
5. The interface is available in English, Ukrainian and Simplified Chinese.
   Change it under Other settings.

Privacy

- Data\settings.json holds the device address, city, stock symbols and the path
  to your local image.
- AI usage relies on Tokscale, an open-source tool you install yourself. KSS
  only calls Tokscale's JSON commands locally; it does not read API keys and
  does not store platform credentials.
- The transfer to the device is unencrypted HTTP. Use it only on a trusted
  local network.

Third-party services

- Weather data comes from Open-Meteo under CC BY 4.0:
  https://open-meteo.com/en/license
- Auto-detected city names are resolved by BigDataCloud.
- The Yahoo Finance quotes and the Tokscale local usage data are unofficial,
  experimental integrations that may be delayed, inaccurate or stop working.
- Stock data is shown for information only and is not investment advice.
- This project has no affiliation with, authorization from, endorsement by or
  sponsorship from the Linx68 brand, the device manufacturer, or any of the
  platforms above.

Licence

The source code is under the MIT License. The application and tray icons are
covered by a separate visual-asset licence. The full project licence, the
privacy notice and the third-party licence files ship with the program.
