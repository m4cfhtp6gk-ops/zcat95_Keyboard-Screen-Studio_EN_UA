# v1.10.4

v1.10.3 asked the Claude screen to say what the credentials file holds. What
it reported named a mistake in v1.10.3 itself.

## Fixed

- **A token belonging to another service could have been sent to Anthropic.**
  Claude Code's credentials file holds more than your Claude login: it also
  stores the OAuth state of every MCP server a plugin has connected - Linear,
  Notion, whatever else - and each of those has its own access token.

  Loosening how names were matched in v1.10.3 put those within reach. On a
  machine with a connected MCP server and no Claude login in that file, this
  app would have taken one of those tokens and presented it to
  api.anthropic.com. The request would have failed, and a credential belonging
  to one service would have been handed to another.

  Those sections are now skipped outright. A real Claude login sitting beside
  them is still found.

- **The diagnostic buried the one fact worth reporting.** It read the file
  depth first and stopped at its limit inside the first branch it entered, so
  a file whose first key opens a large plugin section spent every name it had
  on one server's internals and never reached the top level. Whether your file
  contains a Claude login at all is the entire question, and the report could
  not answer it. It reads breadth first now, so the top-level keys always come
  first.

## Notes

- If the check reports fields but no Claude login, that is now a reliable
  answer rather than an artifact: the file genuinely holds no subscription
  token, and Claude Code is keeping yours somewhere else.
- The fallback that works regardless: `claude setup-token` prints a long-lived
  token for the `CLAUDE_CODE_OAUTH_TOKEN` environment variable, which this app
  reads ahead of any file.
- Field names only ever leave the file as names. No value is read, and a test
  asserts that a secret-looking value never reaches the screen. See PRIVACY.md.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
