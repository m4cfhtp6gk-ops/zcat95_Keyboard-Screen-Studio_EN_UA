namespace KeyboardScreen.Core;

public enum CurrencySourceKind
{
    /// <summary>fawazahmed0 currency-api on the jsDelivr CDN - daily, 200+ currencies, no key.</summary>
    CurrencyApi = 0,

    /// <summary>ExchangeRate-API open endpoint.</summary>
    ExchangeRateApi = 1,

    /// <summary>The National Bank of Ukraine's official daily rates, crossed through UAH.</summary>
    Nbu = 2
}

public sealed class CurrencySettings
{
    public CurrencySourceKind SourceKind { get; set; } = CurrencySourceKind.CurrencyApi;

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
    string? ErrorMessage = null,
    DateOnly? DataDate = null)
{
    public static CurrencySnapshot Unavailable(string? message = null) =>
        new(false, "USD", [], DateTimeOffset.MinValue, false, message);
}
