# v1.10.2

v1.10.1 sent the Claude request to the right place. This release finds the
login it needs, and stops lying about it when it cannot.

## Fixed

- **The login is looked for wherever it might be, not in one guessed place.**
  `%USERPROFILE%\.claude\.credentials.json` is the documented path and it is
  right for a login made by the Windows CLI under your account. It is wrong
  for one made from WSL, where the file lives in the Linux home; wrong for one
  moved with `CLAUDE_CONFIG_DIR`; wrong for an install under AppData. When the
  one guess missed, the screen could only quote that same path back at you.

  All of those are searched now, in order. More usefully, the connection test
  **names every path it tried**. If your login is somewhere this still does
  not look, you can see that at a glance and say so - which was impossible
  while the app only ever repeated its own assumption.

- **A login being refreshed read as a login that was not there.** Claude Code
  rewrites its credentials file about once an hour and can hold it open while
  it does. The read did not share write access, so a refresh in flight failed,
  the error was swallowed, and the screen reported a missing file.

- **The connection test blamed the wrong thing.** It said "no Claude Code
  login found at <path>" for six different situations: no folder, no file, a
  file it was not allowed to open, a file held open mid-refresh, half-written
  JSON, and a file with no token in it. Being told the wrong reason is worse
  than being told nothing - it sends you off to reinstall something that was
  never missing. Each case now says what it actually is, and "no file" lists
  what the folder does hold, which is what separates "never signed in" from
  "this machine keeps its login somewhere else".

- **Environment variables set after the app started are picked up.** A program
  inherits its environment when it launches, so setting
  `CLAUDE_CODE_OAUTH_TOKEN` or `CLAUDE_CONFIG_DIR` in System Properties did
  nothing until your next sign-out. On Windows the stored user and machine
  values are read as well.

## Changed

- The Claude settings card mentions the documented fallback for a machine
  where the file cannot be found: `claude setup-token` prints a long-lived
  token for the `CLAUDE_CODE_OAUTH_TOKEN` environment variable, which this app
  reads ahead of any file. Anthropic documents that token as being for model
  requests, so it may not be accepted for limits - the card says so rather
  than promising it.

## Notes

- Your Claude Code token is read fresh when needed, sent to api.anthropic.com
  and nowhere else, and never written into settings, an exported backup or a
  log. The diagnostic line reports only where it looked and what it found -
  folder and file names, never contents, never the token. See PRIVACY.md.
- The access token expires about once an hour and only Claude Code refreshes
  it. If the screen says the login expired, run Claude Code and it recovers on
  its own.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
