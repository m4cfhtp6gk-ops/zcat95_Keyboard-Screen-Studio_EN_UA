namespace KeyboardScreen.Core;

public enum TelegramPrivacyMode
{
    /// <summary>Only "N new messages" - no names, no text.</summary>
    Counter,

    /// <summary>Sender name only.</summary>
    Sender,

    /// <summary>Sender name and a short text preview.</summary>
    SenderAndPreview
}

public sealed class TelegramSettings
{
    /// <summary>From my.telegram.org → API development tools; the guide card walks through it.</summary>
    public string ApiId { get; set; } = string.Empty;

    public string ApiHash { get; set; } = string.Empty;

    /// <summary>International format, e.g. +380501234567.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public bool PopupsEnabled { get; set; } = true;

    public TelegramPrivacyMode PrivacyMode { get; set; } = TelegramPrivacyMode.Sender;

    /// <summary>How long a popup stays on the keyboard screen, 3-15 seconds.</summary>
    public int PopupSeconds { get; set; } = 6;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiId) &&
        !string.IsNullOrWhiteSpace(ApiHash) &&
        !string.IsNullOrWhiteSpace(PhoneNumber);
}

public sealed record TelegramMessageEvent(string SenderName, string Preview, DateTimeOffset ReceivedAt);
