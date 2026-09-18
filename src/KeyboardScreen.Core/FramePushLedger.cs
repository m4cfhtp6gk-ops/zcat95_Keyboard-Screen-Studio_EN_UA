using System.Security.Cryptography;

namespace KeyboardScreen.Core;

public static class FrameHash
{
    /// <summary>Identifies a rendered frame by its bytes alone.</summary>
    public static string Compute(byte[] jpegBytes) =>
        Convert.ToHexString(SHA256.HashData(jpegBytes ?? []));
}

public enum FramePushOutcome
{
    /// <summary>The frame went to the device.</summary>
    Sent,

    /// <summary>The device already holds this exact frame.</summary>
    Skipped,

    /// <summary>The push was abandoned before an answer; nothing is known.</summary>
    Cancelled,

    /// <summary>The device refused or never answered.</summary>
    Failed
}

/// <summary>
/// Remembers, per device, which frame that device is known to be holding, so an
/// unchanged screen is not re-sent every tick.
///
/// Two invariants make this safe rather than merely cheap:
/// <list type="number">
/// <item>only a push the device acknowledged updates the record - a failed or
/// cancelled push must never be remembered as delivered, or the device sits on
/// a frame it never received and the identical next frame is skipped;</item>
/// <item>any failure drops the record entirely, so the very next tick re-sends
/// even though nothing on screen changed.</item>
/// </list>
///
/// The keep-alive covers what the app cannot observe: a keyboard that is
/// unplugged, power-cycled or resets itself while the picture is static would
/// otherwise stay blank forever, because the frame it lost is the frame we
/// believe it has.
///
/// In memory only - what a device is showing is not a setting, and a stale
/// record restored from disk at startup would suppress the first push.
/// </summary>
public sealed class FramePushLedger
{
    /// <summary>Re-send an unchanged frame at least this often, so a device that lost it recovers.</summary>
    public static readonly TimeSpan DefaultKeepAlive = TimeSpan.FromSeconds(120);

    private readonly record struct Record(string Hash, DateTimeOffset SentAt, DateTimeOffset FirstSentAt);

    private readonly object _gate = new();
    private readonly Dictionary<string, Record> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _keepAlive;

    /// <param name="keepAlive"><see cref="TimeSpan.Zero"/> disables the keep-alive; for tests.</param>
    public FramePushLedger(TimeSpan? keepAlive = null)
    {
        _keepAlive = keepAlive ?? DefaultKeepAlive;
    }

    public TimeSpan KeepAlive => _keepAlive;

    /// <summary>Whether this device needs this frame. Side-effect free.</summary>
    public bool ShouldSend(Uri endpoint, string frameHash, DateTimeOffset now, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (force)
        {
            return true;
        }

        lock (_gate)
        {
            if (!_records.TryGetValue(Key(endpoint), out Record record))
            {
                return true;
            }

            if (!string.Equals(record.Hash, frameHash, StringComparison.Ordinal))
            {
                return true;
            }

            // A clock that stepped backwards (NTP correction, DST, sleep/resume)
            // would otherwise suppress every push until real time caught up.
            if (now < record.SentAt)
            {
                return true;
            }

            return _keepAlive > TimeSpan.Zero && now - record.SentAt >= _keepAlive;
        }
    }

    /// <summary>Records a push the device acknowledged. The only method that writes a record.</summary>
    public void RecordSuccess(Uri endpoint, string frameHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        lock (_gate)
        {
            string key = Key(endpoint);
            // A keep-alive re-send of the same frame must not reset "unchanged
            // since": that timestamp is what tells a quiet screen from a stuck one.
            DateTimeOffset firstSent =
                _records.TryGetValue(key, out Record previous)
                && string.Equals(previous.Hash, frameHash, StringComparison.Ordinal)
                    ? previous.FirstSentAt
                    : now;
            _records[key] = new Record(frameHash, now, firstSent);
        }
    }

    /// <summary>
    /// Forgets what this device holds, after any failure or cancellation. The
    /// next frame is sent whatever it looks like.
    /// </summary>
    public void Invalidate(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        lock (_gate)
        {
            _records.Remove(Key(endpoint));
        }
    }

    /// <summary>Drops devices the user no longer pushes to, so records cannot pile up while an address is being typed.</summary>
    public void RetainOnly(IReadOnlyCollection<Uri> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var keep = new HashSet<string>(endpoints.Select(Key), StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (string stale in _records.Keys.Where(key => !keep.Contains(key)).ToArray())
            {
                _records.Remove(stale);
            }
        }
    }

    /// <summary>When this device last acknowledged a frame, or null if it never has.</summary>
    public DateTimeOffset? LastSentAt(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        lock (_gate)
        {
            return _records.TryGetValue(Key(endpoint), out Record record) ? record.SentAt : null;
        }
    }

    /// <summary>Since when this device has been holding the same picture, or null.</summary>
    public DateTimeOffset? UnchangedSince(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        lock (_gate)
        {
            return _records.TryGetValue(Key(endpoint), out Record record) ? record.FirstSentAt : null;
        }
    }

    private static string Key(Uri endpoint) => endpoint.AbsoluteUri;
}
