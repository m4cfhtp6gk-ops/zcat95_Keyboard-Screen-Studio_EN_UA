using System;

namespace KeyboardScreen.Core;

public sealed record DevicePushResult(bool Success, int? StatusCode, string Message, TimeSpan Elapsed);
