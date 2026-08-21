using System.Net.Http;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Daily exchange rates from the ExchangeRate-API open endpoint — 170+
/// currencies, no API key. The upstream data refreshes once a day, so the
/// local cache holds an hour and failures fall back to the last good reading.
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

        string key = baseCurrency + "|" + string.Join(",", quotes);
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
            using HttpResponseMessage response = await _client.GetAsync(BaseUrl + baseCurrency, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(Loc.T("CurrencyRequestFailed", (int)response.StatusCode));
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("rates", out JsonElement rates) || rates.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(Loc.T("CurrencyRequestFailed", 0));
            }

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

            if (list.Count == 0)
            {
                throw new InvalidOperationException(Loc.T("CurrencyUnknownCodes"));
            }

            _cached = new CurrencySnapshot(true, baseCurrency, list, now);
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
