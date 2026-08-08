using System;

namespace KeyboardScreen.Core;

public sealed record RenderedFrame(byte[] JpegBytes, int Width, int Height, string ThemeId, DateTimeOffset RenderedAt);
