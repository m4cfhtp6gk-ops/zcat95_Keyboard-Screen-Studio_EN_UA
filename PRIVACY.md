# Privacy

Keyboard Screen Studio does not include first-party analytics, advertising,
telemetry, or a project-operated cloud service.

The application processes system usage, Windows media-session information,
selected local images, device addresses, cities, stock symbols, and display
preferences on the local computer. Portable builds store settings under
`Data/settings.json`.

Optional online features contact third-party services directly:

- When automatic weather location is enabled, the app asks the Windows location service for an approximate position. The current coordinates are sent directly from the app to BigDataCloud to resolve the city name and to Open-Meteo for the weather request; coordinates are not saved in `settings.json`. When automatic location is disabled or unavailable, only the configured city query is sent to Open-Meteo.
- Stock symbols are sent to an unofficial Yahoo Finance web endpoint. This integration is experimental, may become unavailable, and must not be relied on for investment decisions.
- AI usage data is read from the user-installed open-source Tokscale tool
  through its local JSON commands. For that integration KSS does not sign in to
  any platform, does not read API keys, and does not upload usage data or
  credentials.
- The Claude limits theme is the one feature that handles a credential. It reads
  your limits from claude.ai using a session cookie you paste into Screen setup
  yourself. That cookie is stored in the local settings file as plain text, is
  sent to claude.ai and to no other host, and is never sent to the project
  author. Clearing the field in settings stops the integration. The usage
  endpoint it calls is unofficial and may change or stop working without notice.
- The token counts shown beside those limits are summed from the Claude Code
  transcripts under `~/.claude/projects` on this computer. They never leave the
  machine, and because they only cover Claude Code sessions here they are a
  floor on account usage rather than the account total.

Images sent to the keyboard are posted directly from the computer to the
configured device address over unencrypted HTTP. Use this feature only on a
trusted local network.

The application does not intentionally transmit settings, media information,
system metrics, local image paths, or platform credentials to the project
author. Third-party services process requests according to their own privacy
policies and terms.
