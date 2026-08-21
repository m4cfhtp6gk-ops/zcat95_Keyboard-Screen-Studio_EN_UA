# Third-Party Notices

Keyboard Screen Studio source code is licensed under the MIT License. Third-party components, fonts, services, names, and trademarks remain subject to their own terms.

## Bundled components

- **.NET 8 self-contained runtime** — Microsoft .NET Library License. The applicable runtime license and third-party notices are distributed in `Licenses/dotnet-LICENSE.txt` and `Licenses/dotnet-ThirdPartyNotices.txt`.
- **Doto** — Copyright 2024 The Doto Project Authors. Licensed under the SIL Open Font License 1.1. The license is distributed in `Licenses/Doto-OFL.txt`.
- **MiSans** — Copyright © Xiaomi Inc. (小米科技有限责任公司). Free for global commercial use, bundled as the default UI font with attribution. Licensed under the MiSans Font Intellectual Property License Agreement; the license is distributed in `Licenses/MiSans-LICENSE.pdf`.
- **LibreHardwareMonitorLib** — Copyright © LibreHardwareMonitor contributors. Licensed under the Mozilla Public License 2.0; the license is distributed in `Licenses/LibreHardwareMonitor-LICENSE.txt`. Source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- **WTelegramClient** — Copyright © Olivier Marcoux (Wizou). Licensed under the MIT License. Source: https://github.com/wiz0u/WTelegramClient

The project's MIT license does not relicense these components.

## Data services

- Automatic-location city names are resolved by the BigDataCloud free client-side reverse geocoding API under its fair-use terms: https://www.bigdatacloud.com/free-api
- Weather data is provided by Open-Meteo under CC BY 4.0 and requires attribution: https://open-meteo.com/en/license
- This personal project uses Open-Meteo's non-commercial free endpoint. Commercial redistributors and users must obtain an applicable Open-Meteo licence or configure another compliant service endpoint.
- The experimental stock integration retrieves display data from an unofficial Yahoo Finance web endpoint. It is not a stable or endorsed API and may become unavailable.
- The experimental AI-usage integration reads local usage data exposed by the user-installed open-source Tokscale tool (https://github.com/junhoyeo/tokscale). KSS does not bundle or sign in to it, and it may stop working when the tool or the underlying platforms change.
- The optional Claude usage screen calls claude.ai account endpoints with a session key the user supplies. This is not a public or documented API; it may stop working at any time, and the key is stored locally and sent to claude.ai only.
- Crypto prices come from the Binance public market-data endpoint (https://data-api.binance.vision) without an API key, subject to Binance's terms and rate limits.
- Exchange rates come from the ExchangeRate-API open endpoint (https://www.exchangerate-api.com), which requires attribution: rates by ExchangeRate-API. The open endpoint updates daily and may be rate-limited.
- The optional GitHub activity screen reads the public contribution page for a user-supplied username; with an optional token it uses the GitHub GraphQL API. The token is stored locally and sent to api.github.com only.
- The optional Telegram integration signs in to the user's own account through Telegram's official MTProto API using the bundled WTelegramClient library and the api_id/api_hash the user obtains from my.telegram.org. The session key, api_id, api_hash and phone number stay on this machine and are sent to Telegram's servers only; logging out wipes the session.

## Visual assets

The application and system-tray icons are original, AI-assisted assets owned by ZCat95 and are licensed separately under `ASSET_LICENSE.md`. They are not covered by the source-code MIT license.

Keyboard Screen Studio is an independent project and is not affiliated with, authorised by, endorsed by, or sponsored by the Linx68 brand or device manufacturer, Microsoft, BigDataCloud, Open-Meteo, Yahoo, Xiaomi, Nothing, or Apple. Product names and trademarks belong to their respective owners.
