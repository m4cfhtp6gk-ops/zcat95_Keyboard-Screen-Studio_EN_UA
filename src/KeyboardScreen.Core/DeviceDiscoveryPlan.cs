using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace KeyboardScreen.Core;

/// <summary>One of this machine's own IPv4 addresses, as the planner sees it.</summary>
public sealed record LanInterface(
    string Name,
    string Description,
    IPAddress Address,
    IPAddress Mask,
    NetworkInterfaceType InterfaceType,
    OperationalStatus Status);

public enum ScanSkipReason
{
    None,
    NotUp,
    Loopback,
    Tunnel,
    LinkLocal,
    NotPrivate,
    VirtualAdapter,
    OddMask
}

/// <summary>A range this machine may sweep, already narrowed to something a person can wait for.</summary>
public sealed record ScanSubnet(
    IPAddress Network,
    int PrefixLength,
    IPAddress LocalAddress,
    string AdapterName,
    int HostCount,
    bool WasClamped,
    int ClampedFromPrefix)
{
    public override string ToString() => $"{Network}/{PrefixLength}";
}

public sealed record ScanPlan(
    IReadOnlyList<ScanSubnet> Subnets,
    IReadOnlyList<SkippedAdapter> Skipped);

public sealed record SkippedAdapter(string Adapter, ScanSkipReason Reason);

/// <summary>
/// Decides what a "look for my keyboard" sweep is allowed to touch, before any
/// socket exists. Every rule here errs towards probing less:
///
/// <list type="bullet">
/// <item>private IPv4 only - a public or carrier-NAT address is somebody else's
/// network, not a home LAN;</item>
/// <item>anything wider than a /24 is narrowed to the /24 around this machine,
/// so a corporate /16 or a Hyper-V switch never becomes 65k probes;</item>
/// <item>VPN and virtual adapters are dropped by name as well as by type,
/// because the enterprise VPN clients present themselves as plain Ethernet.</item>
/// </list>
///
/// Pure and injectable: the smoke tests build fabricated interfaces and assert
/// the plan without a network.
/// </summary>
public static class DeviceDiscoveryPlan
{
    /// <summary>Nothing wider is swept; a wider mask is narrowed to this.</summary>
    public const int ClampPrefixLength = 24;

    /// <summary>
    /// Substrings that mark an adapter as virtual or a VPN. Matched against both
    /// the name and the description: Cisco AnyConnect, GlobalProtect, FortiClient
    /// and Ivanti all report as ordinary Ethernet, so the interface type alone
    /// would let a corporate network through.
    /// </summary>
    public static IReadOnlyList<string> VirtualAdapterMarkers { get; } =
    [
        "hyper-v", "vethernet", "wsl", "vmware", "virtualbox", "docker",
        "loopback adapter", "tap-", "tap adapter", "tunngle", "radmin",
        "tailscale", "zerotier", "npcap", "anyconnect", "cisco", "globalprotect",
        "palo alto", "forticlient", "fortinet", "pulse secure", "ivanti",
        "juniper", "netextender", "sonicwall", "check point", "openvpn",
        "wireguard", "wintun", "nordlynx", "proton", "expressvpn", "zscaler",
        "bluestacks", "genymotion", "parallels", "utm ", "qemu"
    ];

    /// <summary>10/8, 172.16/12 and 192.168/16 only. Carrier-grade NAT is not a home LAN.</summary>
    public static bool IsPrivateIPv4(IPAddress address)
    {
        if (address is null || address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        byte[] octets = address.GetAddressBytes();
        return octets[0] switch
        {
            10 => true,
            172 => octets[1] >= 16 && octets[1] <= 31,
            192 => octets[1] == 168,
            _ => false
        };
    }

    /// <summary>Prefix length of a contiguous mask, or -1 when the mask has holes in it.</summary>
    public static int PrefixLength(IPAddress mask)
    {
        if (mask is null || mask.AddressFamily != AddressFamily.InterNetwork)
        {
            return -1;
        }

        uint bits = ToUInt32(mask);
        int prefix = 0;
        uint cursor = 0x8000_0000;
        while (prefix < 32 && (bits & cursor) != 0)
        {
            prefix++;
            cursor >>= 1;
        }

        // Everything below the run of leading ones must be zero.
        uint expected = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        return bits == expected ? prefix : -1;
    }

    /// <summary>Why this address is not swept, or <see cref="ScanSkipReason.None"/>.</summary>
    public static ScanSkipReason Classify(LanInterface candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Status != OperationalStatus.Up)
        {
            return ScanSkipReason.NotUp;
        }

        if (candidate.InterfaceType == NetworkInterfaceType.Loopback
            || IPAddress.IsLoopback(candidate.Address))
        {
            return ScanSkipReason.Loopback;
        }

        if (candidate.InterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp)
        {
            return ScanSkipReason.Tunnel;
        }

        byte[] octets = candidate.Address.AddressFamily == AddressFamily.InterNetwork
            ? candidate.Address.GetAddressBytes()
            : [0, 0, 0, 0];
        if (octets[0] == 169 && octets[1] == 254)
        {
            return ScanSkipReason.LinkLocal;
        }

        if (!IsPrivateIPv4(candidate.Address))
        {
            return ScanSkipReason.NotPrivate;
        }

        string haystack = (candidate.Name + " " + candidate.Description);
        if (VirtualAdapterMarkers.Any(marker =>
                haystack.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return ScanSkipReason.VirtualAdapter;
        }

        return PrefixLength(candidate.Mask) < 0 ? ScanSkipReason.OddMask : ScanSkipReason.None;
    }

    /// <summary>The ranges to offer the user, and what was left out and why.</summary>
    public static ScanPlan Build(IReadOnlyList<LanInterface> interfaces)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        var subnets = new List<ScanSubnet>();
        var skipped = new List<SkippedAdapter>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (LanInterface candidate in interfaces)
        {
            ScanSkipReason reason = Classify(candidate);
            if (reason != ScanSkipReason.None)
            {
                skipped.Add(new SkippedAdapter(candidate.Name, reason));
                continue;
            }

            int prefix = PrefixLength(candidate.Mask);
            bool clamped = prefix < ClampPrefixLength;
            int effective = clamped ? ClampPrefixLength : prefix;
            // A /31 or /32 holds no other hosts to look at.
            if (effective > 30)
            {
                skipped.Add(new SkippedAdapter(candidate.Name, ScanSkipReason.OddMask));
                continue;
            }

            IPAddress network = NetworkAddress(candidate.Address, effective);
            int hosts = (1 << (32 - effective)) - 3; // minus network, broadcast, us
            // Two adapters on the same prefix are two different LANs (a docked
            // laptop on Ethernet and Wi-Fi), so the local address is part of the key.
            string key = $"{network}/{effective}|{candidate.Address}";
            if (!seen.Add(key))
            {
                continue;
            }

            subnets.Add(new ScanSubnet(
                network, effective, candidate.Address, candidate.Name,
                Math.Max(hosts, 0), clamped, clamped ? prefix : effective));
        }

        return new ScanPlan(subnets, skipped);
    }

    /// <summary>Every address worth probing in this range: no network, no broadcast, not us.</summary>
    public static IReadOnlyList<IPAddress> HostsIn(ScanSubnet subnet)
    {
        ArgumentNullException.ThrowIfNull(subnet);
        uint network = ToUInt32(subnet.Network);
        uint local = ToUInt32(subnet.LocalAddress);
        uint size = 1u << (32 - subnet.PrefixLength);
        var hosts = new List<IPAddress>((int)Math.Min(size, 4096));
        for (uint offset = 1; offset < size - 1; offset++)
        {
            uint address = network + offset;
            if (address != local)
            {
                hosts.Add(FromUInt32(address));
            }
        }
        return hosts;
    }

    /// <summary>
    /// Walks the chosen ranges round-robin, so a sweep the user stops halfway
    /// has covered a little of each network rather than all of the first one.
    /// </summary>
    public static IReadOnlyList<ScanTarget> Interleave(IReadOnlyList<ScanSubnet> chosen)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        IReadOnlyList<IPAddress>[] lists = chosen.Select(HostsIn).ToArray();
        var targets = new List<ScanTarget>(lists.Sum(list => list.Count));
        int longest = lists.Length == 0 ? 0 : lists.Max(list => list.Count);
        for (int index = 0; index < longest; index++)
        {
            for (int listIndex = 0; listIndex < lists.Length; listIndex++)
            {
                if (index < lists[listIndex].Count)
                {
                    targets.Add(new ScanTarget(lists[listIndex][index], chosen[listIndex]));
                }
            }
        }
        return targets;
    }

    private static IPAddress NetworkAddress(IPAddress address, int prefixLength)
    {
        uint mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return FromUInt32(ToUInt32(address) & mask);
    }

    private static uint ToUInt32(IPAddress address)
    {
        byte[] octets = address.GetAddressBytes();
        return ((uint)octets[0] << 24) | ((uint)octets[1] << 16) | ((uint)octets[2] << 8) | octets[3];
    }

    private static IPAddress FromUInt32(uint value) =>
        new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
}

public sealed record ScanTarget(IPAddress Address, ScanSubnet Subnet);
