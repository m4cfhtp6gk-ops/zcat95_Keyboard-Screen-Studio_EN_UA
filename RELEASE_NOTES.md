# v1.1.2

This release fixes dot-matrix theme rendering, bundles MiSans as the default
font, and cuts the download size substantially.

## Improved

- Much smaller download: the single-file portable build went from roughly
  208 MB to roughly 121 MB.
- The theme renderer moved to Skia, so Windows and macOS render identically.
  A few themes may look slightly different than before.
- MiSans is bundled and used as the default font, for more consistent text and
  numerals.

## Fixed

- The dot-matrix themes rendered numerals as fused blocks instead of separate
  dots.

## Platforms

- Windows x64: this release ships a self-contained portable build.
- macOS: the Intel and Apple Silicon build paths are ready but still need
  verification on real hardware, so they are not part of this release.
