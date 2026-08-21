using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Crypto quotes from Binance's public market-data mirror. The
/// data-api.binance.vision host serves market data without an API key, so no
/// account or credential is involved. Symbols are Binance pairs (BTCUSDT);
/// prices are therefore denominated in the quote asset, usually USDT.
/// </summary>
public sealed class BinanceStockSnapshotSource : IStockSnapshotSource, IDisposable
{
    /// <summary>Crypto moves faster than the 15-minute equity cache warrants.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private StockSnapshot? _cached;
    private string? _cachedKey;
    private DateTimeOffset _lastFetch;

    public BinanceStockSnapshotSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("KeyboardScreenStudio/1.0");
    }

    /// <summary>Overridable for tests; production always talks to the public mirror.</summary>
    public string BaseUrl { get; init; } = "https://data-api.binance.vision";

    public async Task<StockSnapshot> ReadAsync(StockSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var items = (settings.Items ?? [])
            .Where(item => item.Enabled && !string.IsNullOrWhiteSpace(item.Symbol))
            .Take(5)
            .Select(item => new StockItemSettings
            {
                Enabled = true,
                Symbol = NormalizeSymbol(item.Symbol),
                Alias = item.Alias?.Trim() ?? string.Empty
            })
            .ToArray();
        string key = string.Join("|", items.Select(item => $"{item.Symbol}:{item.Alias}")) + $"|{settings.RedForGain}";
        DateTimeOffset now = DateTimeOffset.Now;

        if (_cached is not null && string.Equals(_cachedKey, key, StringComparison.Ordinal)
            && now - _lastFetch < CacheDuration)
        {
            return _cached;
        }

        if (items.Length == 0)
        {
            return new StockSnapshot([], now, settings.RedForGain);
        }

        try
        {
            IReadOnlyDictionary<string, (double Price, double ChangePercent)> tickers =
                await ReadTickersAsync(items.Select(item => item.Symbol).ToArray(), cancellationToken);

            var quotes = new List<StockQuoteSnapshot>(items.Length);
            foreach (StockItemSettings item in items)
            {
                if (!tickers.TryGetValue(item.Symbol, out (double Price, double ChangePercent) ticker))
                {
                    throw new InvalidOperationException(Loc.T("StockSymbolNotFound", item.Symbol));
                }

                quotes.Add(new StockQuoteSnapshot(
                    item.Symbol,
                    string.IsNullOrWhiteSpace(item.Alias) ? item.Symbol : item.Alias,
                    ticker.Price,
                    ticker.ChangePercent,
                    now));
            }

            // The two-symbol comparison chart wants five daily closes, exactly
            // what one small klines request per symbol yields. A failed trend
            // fetch must not hide the quotes themselves.
            if (quotes.Count == 2)
            {
                for (int index = 0; index < quotes.Count; index++)
                {
                    try
                    {
                        IReadOnlyList<double> closes = await ReadDailyClosesAsync(quotes[index].Symbol, cancellationToken);
                        if (closes.Count > 0)
                        {
                            quotes[index] = quotes[index] with { FiveDayCloses = closes };
                        }
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
                    {
                    }
                }
            }

            _cached = new StockSnapshot(quotes, now, settings.RedForGain);
            _cachedKey = key;
            _lastFetch = now;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return _cached is not null
                ? _cached with { IsStale = true, ErrorMessage = ex.Message }
                : new StockSnapshot([], now, settings.RedForGain, false, ex.Message);
        }
    }

    /// <summary>
    /// Accepts a Binance pair as-is and folds the Yahoo crypto form onto it, so
    /// a symbol list written for the Yahoo source keeps working: BTC-USD and
    /// BTC/USDT both become BTCUSDT.
    /// </summary>
    public static string NormalizeSymbol(string symbol)
    {
        string trimmed = symbol.Trim().ToUpperInvariant().Replace("/", string.Empty);
        if (trimmed.EndsWith("-USD", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^4] + "USDT";
        }

        return trimmed.Replace("-", string.Empty);
    }

    private async Task<IReadOnlyDictionary<string, (double, double)>> ReadTickersAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken)
    {
        string list = "[" + string.Join(",", symbols.Distinct().Select(symbol => "%22" + symbol + "%22")) + "]";
        string uri = BaseUrl + "/api/v3/ticker/24hr?symbols=" + list;
        using HttpResponseMessage response = await _client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(Loc.T("BinanceRequestFailed", (int)response.StatusCode));
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(Loc.T("StockParseFailed"));
        }

        var result = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement ticker in root.EnumerateArray())
        {
            string? symbol = ticker.TryGetProperty("symbol", out JsonElement symbolElement)
                ? symbolElement.GetString()
                : null;
            if (string.IsNullOrEmpty(symbol))
            {
                continue;
            }

            result[symbol] = (ReadNumber(ticker, "lastPrice"), ReadNumber(ticker, "priceChangePercent"));
        }

        return result;
    }

    private async Task<IReadOnlyList<double>> ReadDailyClosesAsync(string symbol, CancellationToken cancellationToken)
    {
        string uri = BaseUrl + "/api/v3/klines?symbol=" + symbol + "&interval=1d&limit=5";
        using HttpResponseMessage response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        // Each kline is an array; index 4 is the close, serialized as a string.
        var closes = new List<double>(5);
        foreach (JsonElement kline in root.EnumerateArray())
        {
            if (kline.ValueKind == JsonValueKind.Array && kline.GetArrayLength() > 4
                && double.TryParse(kline[4].GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double close))
            {
                closes.Add(close);
            }
        }

        return closes;
    }

    /// <summary>Binance serializes every number as a string.</summary>
    private static double ReadNumber(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : 0;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
