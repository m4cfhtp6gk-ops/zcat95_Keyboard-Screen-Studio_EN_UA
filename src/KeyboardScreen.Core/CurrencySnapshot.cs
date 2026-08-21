namespace KeyboardScreen.Core;

public sealed class CurrencySettings
{
    public string BaseCurrency { get; set; } = "USD";

    /// <summary>Quote currencies, comma-separated in the UI; at most four are shown.</summary>
    public List<string> QuoteCurrencies { get; set; } = ["EUR", "UAH", "PLN"];
}

public sealed record CurrencyRate(string Code, double Rate);

public sealed record CurrencySnapshot(
    bool Available,
    string BaseCurrency,
    IReadOnlyList<CurrencyRate> Rates,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    public static CurrencySnapshot Unavailable(string? message = null) =>
        new(false, "USD", [], DateTimeOffset.MinValue, false, message);
}
