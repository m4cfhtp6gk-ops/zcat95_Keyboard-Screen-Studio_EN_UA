namespace KeyboardScreen.Core;

/// <summary>What one host said when asked about the upload path.</summary>
public sealed record DeviceProbeResponse(
    int? StatusCode,
    IReadOnlyList<string> Allow,
    string? Server,
    string? AccessControlAllowOrigin,
    string? CacheControl,
    string? ContentType,
    string BodyPreview,
    bool BodyTruncated,
    string RequestPath);

public enum DeviceProbeVerdict
{
    /// <summary>Nothing answered; almost certainly no host at this address.</summary>
    NoResponse,

    /// <summary>Something answered, and it is definitely not a keyboard screen.</summary>
    NotKeyboard,

    /// <summary>Something answered and could be the keyboard, but nothing proves it.</summary>
    Possible,

    /// <summary>The answer carries firmware-specific evidence.</summary>
    Keyboard
}

/// <summary>
/// Decides what a probe answer means.
///
/// The keyboard firmware has no device-information endpoint, so identity has to
/// be inferred - and inferring it wrongly is the expensive mistake: offering a
/// stranger's device as "your keyboard" turns this app into something that
/// uploads pictures to it every second. Every rule below is therefore written to
/// refuse rather than guess:
///
/// <list type="bullet">
/// <item>the probe is a GET, so <c>405</c> together with <c>Allow: POST</c> is a
/// clean statement that this path takes uploads and nothing else;</item>
/// <item>a bare <c>405</c> proves nothing - nginx, printers and cameras answer
/// that for any path they do not know;</item>
/// <item>an <c>esp</c> server banner is not a verdict either: ESPHome, Tasmota,
/// WLED and Shelly all ship the same banner;</item>
/// <item>known third-party banners veto every weaker signal.</item>
/// </list>
///
/// Anything that answered but did not prove itself is reported as
/// <see cref="DeviceProbeVerdict.Possible"/> rather than discarded, because a
/// real keyboard can legitimately land there and the user knows their own network.
/// </summary>
public static class DeviceProbeRules
{
    /// <summary>How much of a body is worth reading; enough to recognise, too little to stall on.</summary>
    public const int BodyPreviewLimit = 256;

    /// <summary>Server banners that settle the question in the negative.</summary>
    public static IReadOnlyList<string> ForeignServerBanners { get; } =
    [
        "nginx", "apache", "lighttpd", "iis", "microsoft-httpapi", "boa",
        "mini_httpd", "goahead", "jetty", "kestrel", "werkzeug", "gunicorn",
        "hipcam", "hp http server", "canon", "epson", "brother", "synology",
        "qnap", "openresty", "caddy", "traefik", "cloudflare", "routeros"
    ];

    public static DeviceProbeVerdict Evaluate(DeviceProbeResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode is not { } status)
        {
            return DeviceProbeVerdict.NoResponse;
        }

        // Whole-token match: "POSTMAN-PING" must not read as POST.
        bool allowsPost = response.Allow.Any(value => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => token.Equals("POST", StringComparison.OrdinalIgnoreCase)));
        if (allowsPost)
        {
            return DeviceProbeVerdict.Keyboard;
        }

        // A device demanding credentials is somebody's camera or NAS.
        if (status is 401 or 403 or 407)
        {
            return DeviceProbeVerdict.NotKeyboard;
        }

        if (status >= 500)
        {
            return DeviceProbeVerdict.NotKeyboard;
        }

        string server = response.Server ?? string.Empty;
        if (ForeignServerBanners.Any(banner => server.Contains(banner, StringComparison.OrdinalIgnoreCase)))
        {
            return DeviceProbeVerdict.NotKeyboard;
        }

        // The firmware's own no-content answer: all four parts, or it is not evidence.
        if (status == 204
            && string.Equals(response.AccessControlAllowOrigin?.Trim(), "*", StringComparison.Ordinal)
            && (response.CacheControl ?? string.Empty).Contains("no-store", StringComparison.OrdinalIgnoreCase)
            && (response.ContentType ?? string.Empty).StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceProbeVerdict.Keyboard;
        }

        if (MentionsJpeg(response))
        {
            return DeviceProbeVerdict.Keyboard;
        }

        // Suggestive, never conclusive.
        if (server.Split(' ', '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(token => token.StartsWith("esp", StringComparison.OrdinalIgnoreCase)))
        {
            return DeviceProbeVerdict.Possible;
        }

        return DeviceProbeVerdict.Possible;
    }

    /// <summary>
    /// A body that names the JPEG format. The request path itself contains
    /// "image", so the path and the method are stripped before looking, and a
    /// bare "image" is not enough - only "jpeg" counts.
    /// </summary>
    private static bool MentionsJpeg(DeviceProbeResponse response)
    {
        if (response.BodyTruncated || string.IsNullOrWhiteSpace(response.BodyPreview))
        {
            return false;
        }

        string trimmed = response.BodyPreview.TrimStart('﻿', ' ', '\t', '\r', '\n');
        if (trimmed.Length == 0 || trimmed[0] is '<' or '{' or '[')
        {
            return false;
        }

        string haystack = trimmed.ToLowerInvariant();
        if (haystack.Contains("<html", StringComparison.Ordinal))
        {
            return false;
        }

        haystack = haystack
            .Replace(response.RequestPath.ToLowerInvariant(), " ", StringComparison.Ordinal)
            .Replace("get", " ", StringComparison.Ordinal)
            .Replace("post", " ", StringComparison.Ordinal);
        return haystack.Contains("jpeg", StringComparison.Ordinal);
    }

    /// <summary>A short line saying why this host was listed, so two rows are never identical.</summary>
    public static string DescribeEvidence(DeviceProbeResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.StatusCode is not { } status)
        {
            return Loc.T("DeviceScanEvidenceSilent");
        }

        if (response.Allow.Count > 0)
        {
            return Loc.T("DeviceScanEvidenceAllow", status, string.Join(", ", response.Allow));
        }

        if (!string.IsNullOrWhiteSpace(response.Server))
        {
            return Loc.T("DeviceScanEvidenceServer", status, response.Server.Trim());
        }

        if (status == 204)
        {
            return Loc.T("DeviceScanEvidenceNoContent", status);
        }

        return Loc.T("DeviceScanEvidenceStatus", status);
    }
}
