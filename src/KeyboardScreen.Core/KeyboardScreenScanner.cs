using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace KeyboardScreen.Core;

public sealed record DiscoveredDevice(
    IPAddress Address,
    DeviceProbeVerdict Verdict,
    string Evidence);

public sealed record DiscoveryProgress(int Probed, int Total, int Found);

public sealed record DiscoveryOutcome(
    IReadOnlyList<DiscoveredDevice> Matches,
    IReadOnlyList<DiscoveredDevice> Possible,
    int Probed,
    int Total,
    int Responded,
    bool Cancelled,
    bool BudgetExpired);

/// <summary>
/// Asks each address on the chosen ranges whether it is the keyboard.
///
/// The probe is a plain <c>GET</c> of the upload path. That matters: a GET
/// cannot be mistaken for an upload by any firmware, so the sweep cannot change
/// what is on this - or anyone else's - screen, and the informative answer
/// (<c>405</c> with <c>Allow: POST</c>) becomes decisive rather than ambiguous.
/// Uploading a real frame stays where it belongs, in the transport.
///
/// Nothing here runs on its own: there is no timer and no startup hook. The view
/// model calls it only after the user has seen which ranges would be touched and
/// pressed the button a second time.
/// </summary>
public sealed class KeyboardScreenScanner : IDisposable
{
    public const string ProbePath = "/image/upload";

    /// <summary>Wide enough to finish a /24 in well under a minute, narrow enough to stay polite.</summary>
    public const int Concurrency = 24;

    /// <summary>
    /// Generous on purpose: an ESP in Wi-Fi power saving is slow to answer its
    /// first request, and a shorter cut-off simply misses the keyboard.
    /// </summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(1500);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public KeyboardScreenScanner(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient(new SocketsHttpHandler
        {
            // A LAN sweep must never be funnelled through (or logged by) a
            // corporate proxy, and must not follow a redirect off the network.
            UseProxy = false,
            AllowAutoRedirect = false,
            UseCookies = false,
            MaxConnectionsPerServer = 1,
            PooledConnectionLifetime = TimeSpan.FromSeconds(5)
        })
        {
            // Each probe carries its own deadline; a client-wide timeout would
            // cut the connect phase that a sleeping device is slowest in.
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestVersion = HttpVersion.Version11
        };
    }

    /// <summary>How long the whole sweep may take before it gives up and reports what it has.</summary>
    public static TimeSpan BudgetFor(int hostCount)
    {
        double seconds = (double)Math.Max(hostCount, 1) / Concurrency * ProbeTimeout.TotalSeconds * 1.5;
        return TimeSpan.FromSeconds(Math.Max(45, seconds));
    }

    /// <summary>This machine's own IPv4 addresses, for the planner to filter.</summary>
    public static IReadOnlyList<LanInterface> EnumerateLocalInterfaces()
    {
        var found = new List<LanInterface>();
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties properties;
                try
                {
                    properties = adapter.GetIPProperties();
                }
                catch (NetworkInformationException)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    found.Add(new LanInterface(
                        adapter.Name,
                        adapter.Description,
                        unicast.Address,
                        unicast.IPv4Mask ?? IPAddress.None,
                        adapter.NetworkInterfaceType,
                        adapter.OperationalStatus));
                }
            }
        }
        catch (NetworkInformationException)
        {
            // An adapter list we cannot read is an empty plan, not a crash.
        }

        return found;
    }

    /// <summary>Asks one address. Never throws: an unreachable host is an answer too.</summary>
    public async Task<DeviceProbeResponse> ProbeAsync(
        IPAddress address,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, new Uri($"http://{address}{ProbePath}"));
            using HttpResponseMessage response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);

            (string preview, bool truncated) = await ReadPreviewAsync(response, deadline.Token);
            return new DeviceProbeResponse(
                (int)response.StatusCode,
                // Allow is an entity header: HttpClient files it under Content,
                // and reading it off response.Headers silently returns nothing.
                [.. response.Content.Headers.Allow],
                response.Headers.Server?.ToString(),
                Single(response.Headers, "Access-Control-Allow-Origin"),
                Single(response.Headers, "Cache-Control"),
                response.Content.Headers.ContentType?.MediaType,
                preview,
                truncated,
                ProbePath);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or OperationCanceledException or InvalidOperationException or UriFormatException)
        {
            return new DeviceProbeResponse(null, [], null, null, null, null, string.Empty, false, ProbePath);
        }
    }

    /// <summary>
    /// Sweeps the given addresses. Reports what it found even when stopped or
    /// out of time, so a cancelled scan still shows the keyboard it had already
    /// identified.
    /// </summary>
    public async Task<DiscoveryOutcome> ScanAsync(
        IReadOnlyList<ScanTarget> targets,
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var matches = new List<DiscoveredDevice>();
        var possible = new List<DiscoveredDevice>();
        var gate = new object();
        int probed = 0;
        int responded = 0;
        bool budgetExpired = false;

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan allowance = BudgetFor(targets.Count);
        budget.CancelAfter(allowance);

        try
        {
            await Parallel.ForEachAsync(
                targets,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Concurrency,
                    CancellationToken = budget.Token
                },
                async (target, token) =>
                {
                    DeviceProbeResponse response = await ProbeAsync(target.Address, ProbeTimeout, token);
                    DeviceProbeVerdict verdict = DeviceProbeRules.Evaluate(response);
                    lock (gate)
                    {
                        probed++;
                        if (verdict != DeviceProbeVerdict.NoResponse)
                        {
                            responded++;
                        }

                        if (verdict == DeviceProbeVerdict.Keyboard)
                        {
                            matches.Add(new DiscoveredDevice(
                                target.Address, verdict, DeviceProbeRules.DescribeEvidence(response)));
                        }
                        else if (verdict == DeviceProbeVerdict.Possible)
                        {
                            possible.Add(new DiscoveredDevice(
                                target.Address, verdict, DeviceProbeRules.DescribeEvidence(response)));
                        }

                        if (probed % 8 == 0 || probed == targets.Count)
                        {
                            progress?.Report(new DiscoveryProgress(probed, targets.Count, matches.Count));
                        }
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // Parallel.ForEachAsync throws when its own token fires. Swallowing
            // it here is what lets a stopped or timed-out sweep still report the
            // devices it found instead of losing them to an exception.
            budgetExpired = !cancellationToken.IsCancellationRequested;
        }

        lock (gate)
        {
            return new DiscoveryOutcome(
                [.. matches.OrderBy(device => Key(device.Address))],
                [.. possible.OrderBy(device => Key(device.Address))],
                probed,
                targets.Count,
                responded,
                cancellationToken.IsCancellationRequested,
                budgetExpired);
        }
    }

    private static async Task<(string Preview, bool Truncated)> ReadPreviewAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            byte[] buffer = new byte[DeviceProbeRules.BodyPreviewLimit + 1];
            int read = 0;
            while (read < buffer.Length)
            {
                int chunk = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
                if (chunk == 0)
                {
                    break;
                }
                read += chunk;
            }

            bool truncated = read > DeviceProbeRules.BodyPreviewLimit;
            int length = Math.Min(read, DeviceProbeRules.BodyPreviewLimit);
            return (Encoding.ASCII.GetString(buffer, 0, length), truncated);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or OperationCanceledException or ObjectDisposedException)
        {
            return (string.Empty, false);
        }
    }

    private static string? Single(System.Net.Http.Headers.HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out IEnumerable<string>? values)
            ? string.Join(", ", values)
            : null;

    private static uint Key(IPAddress address)
    {
        byte[] octets = address.GetAddressBytes();
        return ((uint)octets[0] << 24) | ((uint)octets[1] << 16) | ((uint)octets[2] << 8) | octets[3];
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
