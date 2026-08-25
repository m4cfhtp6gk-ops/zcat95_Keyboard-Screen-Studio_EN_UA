# v1.11.0

The Claude screen gains a second way in - signing in from the app itself - and
says when each window comes back.

## Added

- **Sign in with your Claude account, no command-line tool required.** Until
  now the screen could only borrow the login the Claude Code command-line tool
  stores, which left out anyone who uses just the Claude desktop or web app.
  The Claude settings now have a sign-in button: your browser opens, you
  approve, you paste back a short code, and the screen reads your limits
  directly. It is the same sign-in Claude Code itself performs.

  Because this token belongs to the app, it is stored - encrypted with Windows
  DPAPI under your user account, in its own file that never enters the
  settings or an exported backup. It can only read your usage and profile; it
  cannot create API keys. It renews itself hourly, and "Sign out" deletes it.

  The borrowed Claude Code login still works and is still used when present;
  this is an additional way in, not a replacement.

- **Each meter now shows when it resets.** "In 37 minutes" and "at 12:34"
  answer different questions, and the second is the one you act on. Windows
  more than a day out show the weekday.

- **A "How to set this up" guide in the Claude settings**, collapsed until
  needed: the whole path from nothing to numbers, including why the Claude
  desktop app alone is not enough.

## Fixed

- **A moment's throttling no longer blanks a working screen.** Refused calls
  used to replace real figures with "not connected". A good reading now stays
  up for twenty minutes through failures, labelled stale.
- **The connection test no longer causes the failure it explains.** It made a
  live call on every press; three impatient presses were three requests to an
  endpoint that rate-limits on frequency. Within a minute it now repeats the
  last answer.
- An empty model row reported having no window for `""`; it now names the
  scope actually used.
- An expired app sign-in says "sign in again" rather than claiming Claude Code
  is not installed.

## Notes

- Whichever way you signed in, the token is sent to api.anthropic.com and
  nowhere else, and the diagnostic line never contains it. See PRIVACY.md for
  what is stored and where.
- The endpoint behind the limits is not a documented public API and may change
  without notice; the screen reports that rather than inventing figures.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
