# v1.10.5

With the limits finally connecting, the Claude screen turned out to be cutting
off the one number it exists to show.

## Fixed

- **The percentages were truncated.** The figure and the reset countdown were
  each given a fixed fraction of the same line, and those fractions overlapped
  by a quarter of the row. Only a single digit ever fitted: 63% drew as "6…"
  and 100% as "10…".

  The countdown moves up beside the label - a line that held one short word and
  had room to spare - and the figure takes the full width. Both are measured
  rather than apportioned, with the label given priority since it names which
  window you are reading.

## Changed

- **The bars run green to red instead of jumping.** The old rule drew the
  accent colour below 75%, amber to 90%, then red, so a bar at 4% and a bar at
  74% looked the same and only the number carried the change. The fill now
  moves continuously through amber, so the colour says what the length says.

  The screen builder's Claude block keeps its own accent colour instead, since
  that is a setting you chose per block.

- **Asking for a model your account is not metered on now explains itself.**
  Which models get their own weekly window is Anthropic's decision. A name it
  does not report - "fable", for instance - used to leave the third row
  silently missing. The connection test now names the scopes your account does
  report, so you can put one of those in the model row.

## Platforms

- Windows x64: self-contained portable build - unpack and run
  `KeyboardScreenStudio.exe`, no .NET installation required.
- macOS: builds and is checked in CI; no macOS binary is published.
