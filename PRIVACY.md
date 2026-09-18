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
- The Claude limits theme asks Anthropic for your subscription's own usage
  windows, at `https://api.anthropic.com/api/oauth/usage`. It does not ask you
  to paste anything: it reads the OAuth token that Claude Code already stores
  when you sign in - `%USERPROFILE%\.claude\.credentials.json` on Windows, or
  the directory `CLAUDE_CONFIG_DIR` points at - and presents it as a bearer
  token. The endpoint is scoped by the token, so KSS does not send, resolve or
  store an organization id.
- The request identifies itself as `claude-code/<version>`, which that endpoint
  requires: any other user agent is served by a bucket that throttles hard
  enough to make the screen useless. KSS is acting as a client of your own
  Claude Code login, reading only your own account's numbers.
- That token is read fresh each time it is needed and is sent to
  `api.anthropic.com` and to no other host. KSS never copies it into its own
  settings file, never includes it in an exported backup, and never writes it to
  a log or to the screen; the theme's diagnostic line reports only where the
  token was found and what the server answered. KSS does not refresh or modify
  the token, so it cannot affect your Claude Code sign-in. Clearing it is done
  by signing out of Claude Code. Because only Claude Code refreshes it, and it
  expires about once an hour, the screen reports an expired login until Claude
  Code next runs.
- You can instead sign in from the app itself, without Claude Code. This runs
  the same browser sign-in Claude Code uses: you approve in your browser and
  paste back a short code. Unlike the borrowed login above, the token this
  produces belongs to the app, so it is stored on this computer - in its own
  file under your local application data, separate from the settings file, and
  sealed with Windows DPAPI under your user account, so another user on the
  machine cannot read it and it does not survive being copied elsewhere. It is
  never written into the settings file and never included in an exported or
  synced settings backup. This sign-in also keeps a refresh token, which is
  what lets the app renew the hourly access token without asking you again;
  "Sign out" deletes both from disk. The app requests only the permissions to
  read your usage and profile - not the permission to create API keys.
- The percentages and reset times shown are the account's own figures. The
  endpoint they come from is not a documented public API and may change or stop
  working without notice; when it does, the screen says it is not connected
  rather than showing a substitute number.

Images sent to the keyboard are posted directly from the computer to the
configured device address over unencrypted HTTP. Use this feature only on a
trusted local network.

The application does not intentionally transmit settings, media information,
system metrics, local image paths, or platform credentials to the project
author. Third-party services process requests according to their own privacy
policies and terms.
