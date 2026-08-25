# v1.10.3

v1.10.2 made the Claude screen say what it actually saw. What it said was
"read the file, but there is no access token in it" - which moved the problem
from finding the login to understanding it.

## Fixed

- **The credentials file was being read and then not understood.** On a
  machine with Claude Code installed and signed in, the file is found, opened
  and parsed - and the walk through it came back empty. Three reasons, all in
  the reading: underscores were not folded when comparing names, names were
  compared for exact equality so a prefixed or wrapped one missed, and a
  credential kept as a serialized JSON document inside a string value was
  invisible.

  All three are fixed. A second pass also accepts weaker names like a bare
  `token`, but only after the whole file has been searched for a real access
  token, so a file holding both never answers with the weaker one.

  A refresh token is never picked up. It does not work as a bearer, and it is
  the more sensitive half of the pair, so loosening the match without that
  exclusion would have made things worse rather than better.

- **When there is still no token, the check now names the fields the file does
  hold.** Which login your machine has is what decides whether this screen can
  work at all: a Claude subscription login has an access token, a Console
  API-key login does not. The app has never once said which one it was looking
  at. Now it does.

  Field names only. No value is read, the walk stops at three levels and
  twelve names, and a test asserts that a secret-looking value never reaches
  the screen.

## Notes

- Your Claude Code token is read fresh when needed, sent to api.anthropic.com
  and nowhere else, and never written into settings, an exported backup or a
  log. The diagnostic line reports only where it looked and what shape it
  found - paths and field names, never contents, never the token. See
  PRIVACY.md.
- The access token expires about once an hour and only Claude Code refreshes
  it. If the screen says the login expired, run Claude Code and it recovers on
  its own.
- If the file cannot be found at all, `claude setup-token` prints a long-lived
  token for the `CLAUDE_CODE_OAUTH_TOKEN` environment variable, which this app
  reads ahead of any file.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
