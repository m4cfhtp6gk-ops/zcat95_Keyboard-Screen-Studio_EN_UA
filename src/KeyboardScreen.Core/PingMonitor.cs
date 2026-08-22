using System.Net.NetworkInformation;

namespace KeyboardScreen.Core;

public sealed class PingSettings
{
    /// <summary>Hosts to watch; empty entries are skipped, at most four are used.</summary>
    public List<string> Hosts { get; set; } = ["1.1.1.1", string.Empty, string.Empty, string.Empty];
}

public sealed record PingHostSnapshot(
    string Host,
    double? LatencyMs,
    IReadOnlyList<double?> History)
{
    public bool IsReachable => LatencyMs is not null;
}

public sealed record PingSnapshot(
    bool Available,
    IReadOnlyList<PingHostSnapshot> Hosts,
    DateTimeOffset UpdatedAt)
{
    public static PingSnapshot Unavailable { get; } = new(false, [], DateTimeOffset.MinValue);
}

/// <summary>
/// ICMP pings to up to four hosts with a rolling latency history per host.
/// A failed or filtered ping records a gap, never an exception.
/// </summary>
public sealed class PingMonitorSource
{
    public const int HistoryLength = 24;
    private const int TimeoutMs = 1500;

    private readonly Dictionary<string, List<double?>> _history = new(StringComparer.OrdinalIgnoreCase);

    public async Task<PingSnapshot> ReadAsync(PingSettings? settings, CancellationToken cancellationToken = default)
    {
        string[] hosts = (settings?.Hosts ?? [])
            .Select(host => (host ?? string.Empty).Trim())
            .Where(host => host.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        if (hosts.Length == 0)
        {
            return PingSnapshot.Unavailable;
        }

        double?[] latencies = await Task.WhenAll(hosts.Select(host => PingOnceAsync(host, cancellationToken)));

        var results = new List<PingHostSnapshot>(hosts.Length);
        lock (_history)
        {
            // Drop history for hosts no longer on the list.
            foreach (string stale in _history.Keys.Except(hosts, StringComparer.OrdinalIgnoreCase).ToArray())
            {
                _history.Remove(stale);
            }

            for (int index = 0; index < hosts.Length; index++)
            {
                if (!_history.TryGetValue(hosts[index], out List<double?>? samples))
                {
                    samples = [];
                    _history[hosts[index]] = samples;
                }

                samples.Add(latencies[index]);
                if (samples.Count > HistoryLength)
                {
                    samples.RemoveAt(0);
                }

                results.Add(new PingHostSnapshot(hosts[index], latencies[index], samples.ToArray()));
            }
        }

        return new PingSnapshot(true, results, DateTimeOffset.Now);
    }

    private static async Task<double?> PingOnceAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            PingReply reply = await ping.SendPingAsync(host, TimeSpan.FromMilliseconds(TimeoutMs),
                cancellationToken: cancellationToken);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (Exception ex) when (ex is PingException or InvalidOperationException
            or System.Net.Sockets.SocketException or ArgumentException or PlatformNotSupportedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}

/// <summary>Latency per host with a sparkline of the recent samples.</summary>
public sealed class PingTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color CardColor = Color.FromRgb(15, 20, 27);
    private static readonly Color CardStroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color GoodGreen = Color.FromRgb(64, 186, 120);
    private static readonly Color FairOrange = Color.FromRgb(232, 158, 74);
    private static readonly Color BadRed = Color.FromRgb(224, 68, 68);

    public string Id => "ping";
    public string DisplayName => Loc.T("ThemePingName");
    public string Description => Loc.T("ThemePingDescription");
    public string Details => Loc.T("ThemePingDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitlePing"));

        IReadOnlyList<PingHostSnapshot> hosts = snapshot.Ping?.Hosts ?? [];
        if (snapshot.Ping is not { Available: true } || hosts.Count == 0)
        {
            canvas.AlignedText(Loc.T("ScreenPingEmpty"), 13, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(Loc.T("ScreenPingHint"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 180, safe.Width, 30), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        double rowHeight = hosts.Count <= 2 ? 118 : hosts.Count == 3 ? 96 : 80;
        const double gap = 7;
        double top = safe.Top + 42;
        foreach (PingHostSnapshot host in hosts.Take(4))
        {
            var row = new Rect(safe.Left, top, safe.Width, rowHeight);
            canvas.RoundedRect(row, 12, CardColor, CardStroke);
            var content = new Rect(row.Left + 12, row.Top + 8, row.Width - 24, row.Height - 16);

            Color state = StateColor(host.LatencyMs);
            canvas.Ellipse(new Rect(content.Left, content.Top + 4, 7, 7), state);
            canvas.AlignedText(host.Host, 11, Colors.White,
                new Rect(content.Left + 13, content.Top, content.Width - 13, 15), FontWeights.SemiBold, TextAlignment.Left);

            canvas.AlignedText(
                host.LatencyMs is { } latency
                    ? Loc.T("ScreenPingMs", latency.ToString("0"))
                    : Loc.T("ScreenPingTimeout"),
                rowHeight >= 96 ? 26 : 21, state,
                new Rect(content.Left, content.Top + 16, content.Width, rowHeight >= 96 ? 34 : 26),
                FontWeights.SemiBold, TextAlignment.Left);

            IReadOnlyList<double?> history = host.History;
            if (history.Count >= 2)
            {
                DrawHistory(canvas,
                    new Rect(content.Left, content.Bottom - 16, content.Width, 14), history, state);
            }

            top += rowHeight + gap;
        }
    }

    private static Color StateColor(double? latencyMs) => latencyMs switch
    {
        null => BadRed,
        < 60 => GoodGreen,
        < 150 => FairOrange,
        _ => BadRed
    };

    /// <summary>Bars rather than a line, so gaps (lost pings) stay visible.</summary>
    private static void DrawHistory(ScreenCanvas canvas, Rect bounds, IReadOnlyList<double?> history, Color color)
    {
        double max = Math.Max(history.Max(sample => sample ?? 0), 1);
        double slot = bounds.Width / PingMonitorSource.HistoryLength;
        double barWidth = Math.Max(2, slot - 1.6);
        for (int index = 0; index < history.Count; index++)
        {
            double x = bounds.Right - (history.Count - index) * slot;
            if (history[index] is { } sample)
            {
                double height = Math.Max(2, sample / max * bounds.Height);
                canvas.RoundedRect(new Rect(x, bounds.Bottom - height, barWidth, height), 1, color);
            }
            else
            {
                canvas.RoundedRect(new Rect(x, bounds.Bottom - 2, barWidth, 2), 1, BadRed);
            }
        }
    }
}
