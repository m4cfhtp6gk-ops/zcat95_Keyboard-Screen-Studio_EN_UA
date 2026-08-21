namespace KeyboardScreen.Core;

public sealed class HardwareMonitorSettings
{
    /// <summary>How long each page stays on screen, 3-10 seconds.</summary>
    public int PageSeconds { get; set; } = 5;
}

/// <summary>One monitored device; any reading the platform cannot provide is null and draws as "—".</summary>
public sealed record HardwareComponentSnapshot(
    string Name,
    double? LoadPercent = null,
    double? TemperatureC = null,
    double? ClockGhz = null,
    double? FanRpm = null,
    double? MemoryUsedMb = null,
    double? MemoryTotalMb = null);

public sealed record HardwareSnapshot(
    bool Available,
    HardwareComponentSnapshot? Cpu,
    HardwareComponentSnapshot? Gpu,
    double? RamUsedGb,
    double? RamTotalGb,
    double? DiskUsedPercent,
    DateTimeOffset UpdatedAt,
    string? ErrorMessage = null)
{
    public static HardwareSnapshot Unavailable(string? message = null) =>
        new(false, null, null, null, null, null, DateTimeOffset.MinValue, message);
}

public interface IHardwareSnapshotSource
{
    HardwareSnapshot Read();
}
