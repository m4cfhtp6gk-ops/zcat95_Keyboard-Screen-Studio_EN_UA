using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Daily exchange rates from a selectable provider: the fawazahmed0
/// currency-api on the jsDelivr CDN (default, with a silent fallback to
/// ExchangeRate-API when the CDN is unreachable), the ExchangeRate-API open
/// endpoint, or the National Bank of Ukraine's official rates crossed through
/// UAH. Every provider reports the date its data is for, and the screen shows
/// it - a stale feed is visible instead of silently wrong.
/// </summary>
public sealed class CurrencySnapshotSource : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private CurrencySnapshot? _cached;
    private string _cachedKey = string.Empty;
    private DateTimeOffset _lastFetch;

    public CurrencySnapshotSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyboardScreenStudio/1.0");
    }

    /// <summary>Overridable for tests.</summary>
    public string BaseUrl { get; init; } = "https://open.er-api.com/v6/latest/";

    /// <summary>Overridable for tests. The {base} is appended lowercase with ".json".</summary>
    public string CurrencyApiBaseUrl { get; init; } =
        "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/";

    /// <summary>Overridable for tests.</summary>
    public string NbuBaseUrl { get; init; } =
        "https://bank.gov.ua/NBUStatService/v1/statdirectory/exchange?json";

    public async Task<CurrencySnapshot> ReadAsync(CurrencySettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string baseCurrency = NormalizeCode(settings.BaseCurrency, "USD");
        string[] quotes = (settings.QuoteCurrencies ?? [])
            .Select(code => NormalizeCode(code, string.Empty))
            .Where(code => code.Length == 3 && code != baseCurrency)
            .Distinct()
            .Take(4)
            .ToArray();

        string key = (int)settings.SourceKind + "|" + baseCurrency + "|" + string.Join(",", quotes);
        DateTimeOffset now = DateTimeOffset.Now;
        if (_cached is not null && _cachedKey == key && now - _lastFetch < CacheDuration)
        {
            return _cached;
        }

        if (quotes.Length == 0)
        {
            return new CurrencySnapshot(false, baseCurrency, [], now, false, Loc.T("CurrencyNoQuotes"));
        }

        try
        {
            (List<CurrencyRate> list, DateOnly? dataDate) = settings.SourceKind switch
            {
                CurrencySourceKind.ExchangeRateApi => await ReadExchangeRateApiAsync(baseCurrency, quotes, cancellationToken),
                CurrencySourceKind.Nbu => await ReadNbuAsync(baseCurrency, quotes, cancellationToken),
                _ => await ReadCurrencyApiWithFallbackAsync(baseCurrency, quotes, cancellationToken)
            };

            if (list.Count == 0)
            {
                throw new InvalidOperationException(Loc.T("CurrencyUnknownCodes"));
            }

            _cached = new CurrencySnapshot(true, baseCurrency, list, now, DataDate: dataDate);
            _cachedKey = key;
            _lastFetch = now;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return _cached is not null
                ? _cached with { IsStale = true, ErrorMessage = ex.Message }
                : CurrencySnapshot.Unavailable(ex.Message) with { BaseCurrency = baseCurrency, UpdatedAt = now };
        }
    }

    /// <summary>The CDN mirror can be blocked on some networks; fall back quietly.</summary>
    private async Task<(List<CurrencyRate>, DateOnly?)> ReadCurrencyApiWithFallbackAsync(
        string baseCurrency, string[] quotes, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadCurrencyApiAsync(baseCurrency, quotes, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return await ReadExchangeRateApiAsync(baseCurrency, quotes, cancellationToken);
        }
    }

    private async Task<(List<CurrencyRate>, DateOnly?)> ReadCurrencyApiAsync(
        string baseCurrency, string[] quotes, CancellationToken cancellationToken)
    {
        using JsonDocument document = await FetchJsonAsync(
            CurrencyApiBaseUrl + baseCurrency.ToLowerInvariant() + ".json", cancellationToken);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty(baseCurrency.ToLowerInvariant(), out JsonElement rates)
            || rates.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(Loc.T("CurrencyRequestFailed", 0));
        }

        DateOnly? dataDate = root.TryGetProperty("date", out JsonElement date)
            && DateOnly.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly parsed)
            ? parsed
            : null;

        var list = new List<CurrencyRate>(quotes.Length);
        foreach (string quote in quotes)
        {
            if (rates.TryGetProperty(quote.ToLowerInvariant(), out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out double rate))
            {
                list.Add(new CurrencyRate(quote, rate));
            }
        }

        return (list, dataDate);
    }

    private async Task<(List<CurrencyRate>, DateOnly?)> ReadExchangeRateApiAsync(
        string baseCurrency, string[] quotes, CancellationToken cancellationToken)
    {
        using JsonDocument document = await FetchJsonAsync(BaseUrl + baseCurrency, cancellationToken);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("rates", out JsonElement rates) || rates.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(Loc.T("CurrencyRequestFailed", 0));
        }

        DateOnly? dataDate = root.TryGetProperty("time_last_update_unix", out JsonElement stamp)
            && stamp.TryGetInt64(out long unix)
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().Date)
            : null;

        var list = new List<CurrencyRate>(quotes.Length);
        foreach (string quote in quotes)
        {
            if (rates.TryGetProperty(quote, out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out double rate))
            {
                list.Add(new CurrencyRate(quote, rate));
            }
        }

        return (list, dataDate);
    }

    /// <summary>
    /// The NBU publishes how many UAH one unit of each currency costs; any
    /// pair is crossed through UAH (UAH itself counts as 1).
    /// </summary>
    private async Task<(List<CurrencyRate>, DateOnly?)> ReadNbuAsync(
        string baseCurrency, string[] quotes, CancellationToken cancellationToken)
    {
        using JsonDocument document = await FetchJsonAsync(NbuBaseUrl, cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(Loc.T("CurrencyRequestFailed", 0));
        }

        var uahPerUnit = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["UAH"] = 1.0 };
        DateOnly? dataDate = null;
        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            if (entry.TryGetProperty("cc", out JsonElement code)
                && entry.TryGetProperty("rate", out JsonElement rate)
                && rate.ValueKind == JsonValueKind.Number
                && code.GetString() is { Length: 3 } currencyCode)
            {
                uahPerUnit[currencyCode] = rate.GetDouble();
            }

            if (dataDate is null
                && entry.TryGetProperty("exchangedate", out JsonElement date)
                && DateOnly.TryParseExact(date.GetString(), "dd.MM.yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateOnly parsed))
            {
                dataDate = parsed;
            }
        }

        if (!uahPerUnit.TryGetValue(baseCurrency, out double uahPerBase) || uahPerBase <= 0)
        {
            throw new InvalidOperationException(Loc.T("CurrencyUnknownCodes"));
        }

        var list = new List<CurrencyRate>(quotes.Length);
        foreach (string quote in quotes)
        {
            if (uahPerUnit.TryGetValue(quote, out double uahPerQuote) && uahPerQuote > 0)
            {
                list.Add(new CurrencyRate(quote, uahPerBase / uahPerQuote));
            }
        }

        return (list, dataDate);
    }

    private async Task<JsonDocument> FetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(Loc.T("CurrencyRequestFailed", (int)response.StatusCode));
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    public static string NormalizeCode(string? code, string fallback)
    {
        string trimmed = (code ?? string.Empty).Trim().ToUpperInvariant();
        return trimmed.Length == 3 && trimmed.All(char.IsAsciiLetterUpper) ? trimmed : fallback;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
