using System.Collections.ObjectModel;

namespace KeyboardScreen.Core;

public sealed record PushDiagnosticsEntry(
    DateTimeOffset Timestamp,
    bool Success,
    int? StatusCode,
    string Message,
    TimeSpan Latency,
    int FrameBytes);

/// <summary>
/// Ring buffer of the most recent device pushes, for the diagnostics card.
/// Kept in memory only — this is a debugging aid, not telemetry.
/// </summary>
public sealed class PushDiagnostics
{
    private const int Capacity = 20;

    private readonly object _gate = new();
    private readonly List<PushDiagnosticsEntry> _entries = new(Capacity);

    /// <summary>Raised after a record lands, so the UI can refresh its card.</summary>
    public event EventHandler? Updated;

    public void Record(DevicePushResult result, int frameBytes)
    {
        var entry = new PushDiagnosticsEntry(
            DateTimeOffset.Now,
            result.Success,
            result.StatusCode,
            result.Message,
            result.Elapsed,
            frameBytes);

        lock (_gate)
        {
            if (_entries.Count == Capacity)
            {
                _entries.RemoveAt(0);
            }

            _entries.Add(entry);
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public PushDiagnosticsEntry? Latest
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count > 0 ? _entries[^1] : null;
            }
        }
    }

    /// <summary>Most recent failures, newest first.</summary>
    public IReadOnlyList<PushDiagnosticsEntry> RecentErrors(int count = 5)
    {
        lock (_gate)
        {
            return _entries.Where(entry => !entry.Success)
                .Reverse()
                .Take(count)
                .ToArray();
        }
    }
}
