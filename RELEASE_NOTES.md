# v1.10.1

The Claude screen was still not working in v1.10.0. This fixes it.

## Fixed

- **Claude limits: the right token, sent to the wrong host.** v1.10.0 found
  the credential that is actually meant for a non-browser client - the OAuth
  token Claude Code stores when you sign in - and then presented it to
  `claude.ai`, which authenticates a browser session and has no use for a
  bearer token.

  Each design so far had one half of this. The first had the right host for a
  cookie a desktop app cannot present. The second gave up on the server and
  counted tokens in local transcripts. The third found the right token and
  posted it to the cookie's address.

  The limits now come from `api.anthropic.com/api/oauth/usage`, which is the
  endpoint that takes that token. It is scoped by the token, so there is no
  organization to look up and one request does the whole job. Two headers
  turned out not to be optional: one selects the OAuth contract, and the
  request has to identify as the Claude Code client whose login it borrows -
  anything else is served by a bucket that throttles hard enough to look like
  a broken feature.

  The screen is also far more patient now. It asks every three minutes rather
  than every thirty seconds, and when it is refused it waits five minutes
  instead of asking again on the next tick, which is what turns a minute of
  throttling into an hour of it. Being throttled now says so, rather than
  reading as a generic failure.

- **The scrollbar was swallowing clicks along the right edge of every list.**
  It is a 10 px strip drawn on top of the content, not beside it, so anything
  stretched to the full width lost its right edge to it. In the theme list
  that was the rightmost 16 px of every row. On the settings pages there was
  no reservation at all, and those pages put every switch, dropdown and
  spinner against the right edge - so the bar covered part of the controls
  themselves.

## Notes

- The Claude screen needs Claude Code signed in on this computer. The access
  token it borrows expires about once an hour and only Claude Code refreshes
  it, so if the screen says the login expired, run Claude Code and it recovers
  on its own.
- Your Claude Code token is read fresh when needed, sent to api.anthropic.com
  and nowhere else, and never written into settings, an exported backup or a
  log. KSS does not refresh or modify it, so it cannot affect your Claude Code
  sign-in. See PRIVACY.md.
- The endpoint behind the limits is not a documented public API and may change
  without notice. If it does, the screen reports that it is not connected
  instead of showing an invented figure.

## For contributors

- `tools/loc/` is back in step with the catalogues it generates. It had gone
  stale across v1.9.0 and v1.10.0, so the regeneration its README documents
  would have quietly dropped 20 strings and reverted 13 more.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
