using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KeyboardScreen.Core;

/// <summary>
/// 腾讯行情源（qt.gtimg.cn，国内可直连）。响应为 GBK 编码文本，
/// 每行形如 v_sh600000="1~名称~代码~现价~昨收~..."，字段 3=现价、
/// 4=昨收、30=时间戳、32=涨跌幅（%）。
/// </summary>
public sealed class TencentStockSnapshotSource : IStockSnapshotSource, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static readonly Regex QuoteLineRegex = new(
        @"v_(?<code>\w+)=""(?<payload>[^""]*)""",
        RegexOptions.Compiled);
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private StockSnapshot? _cached;
    private string? _cachedKey;
    private DateTimeOffset _lastFetch;

    static TencentStockSnapshotSource()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public TencentStockSnapshotSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 KeyboardScreenStudio/1.0");
    }

    public async Task<StockSnapshot> ReadAsync(
        StockSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var items = (settings.Items ?? [])
            .Where(item => item.Enabled && !string.IsNullOrWhiteSpace(item.Symbol))
            .Take(5)
            .Select(item => new StockItemSettings
            {
                Enabled = true,
                Symbol = item.Symbol.Trim().ToUpperInvariant(),
                Alias = item.Alias?.Trim() ?? string.Empty
            })
            .ToArray();
        var key = string.Join("|", items.Select(item => $"{item.Symbol}:{item.Alias}")) + $"|{settings.RedForGain}";
        var now = DateTimeOffset.Now;

        if (_cached is not null && string.Equals(_cachedKey, key, StringComparison.Ordinal)
            && now - _lastFetch < CacheDuration)
        {
            return _cached;
        }

        if (items.Length == 0)
        {
            return new StockSnapshot([], now, settings.RedForGain);
        }

        var mapped = items
            .Select(item => (Item: item, Code: MapSymbol(item.Symbol)))
            .ToArray();
        var supported = mapped.Where(entry => entry.Code is not null).ToArray();

        try
        {
            if (supported.Length == 0)
            {
                return new StockSnapshot([], now, settings.RedForGain, false, "暂不支持当前代码格式");
            }

            var uri = "https://qt.gtimg.cn/q=" + string.Join(",", supported.Select(entry => entry.Code));
            using var response = await _client.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            string text = Encoding.GetEncoding("GBK").GetString(payload);

            var byCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in QuoteLineRegex.Matches(text))
            {
                byCode[match.Groups["code"].Value] = match.Groups["payload"].Value;
            }

            var quoteEntries = new List<(string Code, StockQuoteSnapshot Quote)>(supported.Length);
            foreach (var (item, code) in supported)
            {
                if (byCode.TryGetValue(code!, out string? line)
                    && TryParseQuote(item, line, out StockQuoteSnapshot quote))
                {
                    quoteEntries.Add((code!, quote));
                }
            }

            if (quoteEntries.Count == 0)
            {
                return new StockSnapshot([], now, settings.RedForGain, false, "未能解析行情数据");
            }

            if (quoteEntries.Count == 2)
            {
                var trendByCode = await ReadTrendAsync(
                    quoteEntries.Select(entry => entry.Code),
                    cancellationToken);
                for (int index = 0; index < quoteEntries.Count; index++)
                {
                    if (trendByCode.TryGetValue(quoteEntries[index].Code, out var closes))
                    {
                        quoteEntries[index] = (
                            quoteEntries[index].Code,
                            quoteEntries[index].Quote with { FiveDayCloses = closes });
                    }
                }
            }

            var quotes = quoteEntries.Select(entry => entry.Quote).ToList();
            _cached = new StockSnapshot(quotes, now, settings.RedForGain);
            _cachedKey = key;
            _lastFetch = now;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            bool matchingCache = _cached is not null &&
                string.Equals(_cachedKey, key, StringComparison.Ordinal);
            return !matchingCache
                ? new StockSnapshot([], now, settings.RedForGain, false, ex.Message)
                : _cached! with { IsStale = true, ErrorMessage = ex.Message };
        }
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<double>>> ReadTrendAsync(
        IEnumerable<string> codes,
        CancellationToken cancellationToken)
    {
        var closesByCode = new Dictionary<string, IReadOnlyList<double>>(StringComparer.OrdinalIgnoreCase);
        foreach (string code in codes)
        {
            // 腾讯日K对美股使用带 .OQ 后缀的内部代码（如 usAAPL.OQ），其余市场用原代码。
            string klineCode = code.StartsWith("us", StringComparison.OrdinalIgnoreCase)
                ? code + ".OQ"
                : code;
            var uri = "https://web.ifzq.gtimg.cn/appstock/app/fqkline/get?param="
                + Uri.EscapeDataString(klineCode + ",day,,,5,qfq");
            try
            {
                using var response = await _client.GetAsync(uri, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty(klineCode, out var codeNode)
                    && TryReadKlineCloses(codeNode, out var closes))
                {
                    closesByCode[code] = closes;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                // 走势数据失败不影响行情显示
            }
        }

        return closesByCode;
    }

    private static bool TryReadKlineCloses(JsonElement codeNode, out IReadOnlyList<double> closes)
    {
        closes = [];
        foreach (string key in new[] { "qfqday", "day" })
        {
            if (!codeNode.TryGetProperty(key, out var rows))
            {
                continue;
            }

            var values = new List<double>();
            foreach (var row in rows.EnumerateArray())
            {
                if (row.GetArrayLength() >= 3 && TryGetDouble(row[2], out double close) && close > 0)
                {
                    values.Add(close);
                }
            }

            if (values.Count >= 1)
            {
                closes = values;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return double.TryParse(
                element.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// 将 Yahoo 风格代码映射为腾讯代码：600519.SS→sh600519、
    /// 000001.SZ→sz000001、0700.HK→hk00700、AAPL→usAAPL；
    /// 其余格式（如 BTC-USD）返回 null。
    /// </summary>
    public static string? MapSymbol(string symbol)
    {
        if (symbol.EndsWith(".SS", StringComparison.Ordinal))
        {
            return "sh" + symbol[..^3];
        }

        if (symbol.EndsWith(".SZ", StringComparison.Ordinal))
        {
            return "sz" + symbol[..^3];
        }

        if (symbol.EndsWith(".HK", StringComparison.Ordinal))
        {
            return "hk" + symbol[..^3].PadLeft(5, '0');
        }

        if (symbol.Contains('.') || symbol.Contains('-'))
        {
            return null;
        }

        return "us" + symbol;
    }

    private static bool TryParseQuote(
        StockItemSettings item,
        string line,
        out StockQuoteSnapshot quote)
    {
        quote = null!;
        string[] fields = line.Split('~');
        if (fields.Length <= 32
            || !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double price)
            || !double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double previous)
            || !double.TryParse(fields[32], NumberStyles.Float, CultureInfo.InvariantCulture, out double changePercent)
            || previous == 0)
        {
            return false;
        }

        var displayName = string.IsNullOrWhiteSpace(item.Alias) ? item.Symbol : item.Alias;
        quote = new StockQuoteSnapshot(
            item.Symbol,
            displayName,
            price,
            changePercent,
            ParseTimestamp(fields[30]));
        return true;
    }

    private static DateTimeOffset ParseTimestamp(string raw)
    {
        if (DateTimeOffset.TryParseExact(
                raw,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset compact))
        {
            return compact;
        }

        if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out DateTimeOffset parsed))
        {
            return parsed;
        }

        return DateTimeOffset.Now;
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
