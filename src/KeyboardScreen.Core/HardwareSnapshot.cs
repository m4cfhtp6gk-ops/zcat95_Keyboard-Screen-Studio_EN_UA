namespace KeyboardScreen.Core;

public sealed class HardwareMonitorSettings
{
    /// <summary>How long each page stays on screen, 3-10 seconds.</summary>
    public int PageSeconds { get; set; } = 5;
}

/// <summary>
/// Why the driver-backed readings - CPU temperature and fan speed - are missing.
/// The two failures need different words: one the user can act on, one they cannot.
/// </summary>
public enum HardwareSensorAccess
{
    /// <summary>The sensor driver answered; the readings are real.</summary>
    Full,

    /// <summary>The driver needs administrator rights and this process has none.</summary>
    NeedsAdministrator,

    /// <summary>Already elevated, and the board still exposes no temperature.</summary>
    Unsupported
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
    string? ErrorMessage = null,
    HardwareSensorAccess SensorAccess = HardwareSensorAccess.Full)
{
    public static HardwareSnapshot Unavailable(
        string? message = null,
        HardwareSensorAccess access = HardwareSensorAccess.Full) =>
        new(false, null, null, null, null, null, DateTimeOffset.MinValue, message, access);
}

public interface IHardwareSnapshotSource
{
    HardwareSnapshot Read();
}
