using LibreHardwareMonitor.Hardware;
using KeyboardScreen.Core;

namespace KeyboardScreen.App.Avalonia.Platform;

/// <summary>
/// Sensor readings via LibreHardwareMonitorLib. Opening the driver can fail
/// and CPU temperatures usually need administrator rights, so every reading
/// is optional; the theme shows an em dash for whatever is missing.
/// </summary>
public sealed class LibreHardwareMonitorSource : IHardwareSnapshotSource, IDisposable
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private Computer? _computer;
    private bool _openFailed;
    private string? _openError;
    private HardwareSnapshot? _cached;
    private DateTimeOffset _lastUpdate;

    public HardwareSnapshot Read()
    {
        lock (_gate)
        {
            if (_openFailed)
            {
                return HardwareSnapshot.Unavailable(_openError);
            }

            DateTimeOffset now = DateTimeOffset.Now;
            if (_cached is not null && now - _lastUpdate < UpdateInterval)
            {
                return _cached;
            }

            try
            {
                if (_computer is null)
                {
                    _computer = new Computer
                    {
                        IsCpuEnabled = true,
                        IsGpuEnabled = true,
                        IsMemoryEnabled = true,
                        IsStorageEnabled = true,
                        IsMotherboardEnabled = true
                    };
                    _computer.Open();
                }

                _cached = Collect(_computer, now);
                _lastUpdate = now;
                return _cached;
            }
            catch (Exception ex)
            {
                // A failed driver open never recovers within the process, so
                // remember the failure instead of retrying every second.
                if (_computer is null || _cached is null)
                {
                    _openFailed = true;
                    _openError = ex.Message;
                    TryClose();
                    return HardwareSnapshot.Unavailable(ex.Message);
                }

                return _cached with { ErrorMessage = ex.Message };
            }
        }
    }

    private static HardwareSnapshot Collect(Computer computer, DateTimeOffset now)
    {
        HardwareComponentSnapshot? cpu = null;
        HardwareComponentSnapshot? gpu = null;
        double? ramUsed = null;
        double? ramTotal = null;
        double? diskUsed = null;
        double? motherboardFan = null;

        foreach (IHardware hardware in computer.Hardware)
        {
            hardware.Update();
            foreach (IHardware sub in hardware.SubHardware)
            {
                sub.Update();
            }

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    cpu = ReadCpu(hardware);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    // Prefer a discrete GPU over an integrated one when both exist.
                    if (gpu is null || hardware.HardwareType != HardwareType.GpuIntel)
                    {
                        gpu = ReadGpu(hardware);
                    }
                    break;
                case HardwareType.Memory:
                    (ramUsed, ramTotal) = ReadMemory(hardware);
                    break;
                case HardwareType.Storage:
                    diskUsed ??= FindValue(hardware, SensorType.Load, "Used Space");
                    break;
                case HardwareType.Motherboard:
                    foreach (IHardware sub in hardware.SubHardware)
                    {
                        motherboardFan ??= FirstValue(sub, SensorType.Fan, minimum: 1);
                    }
                    break;
            }
        }

        if (cpu is { FanRpm: null } && motherboardFan is not null)
        {
            cpu = cpu with { FanRpm = motherboardFan };
        }

        return new HardwareSnapshot(
            cpu is not null || gpu is not null || ramTotal is not null,
            cpu,
            gpu,
            ramUsed,
            ramTotal,
            diskUsed,
            now);
    }

    private static HardwareComponentSnapshot ReadCpu(IHardware hardware)
    {
        double? load = FindValue(hardware, SensorType.Load, "CPU Total")
            ?? FirstValue(hardware, SensorType.Load);
        double? temperature = FindValue(hardware, SensorType.Temperature, "Core (Tctl/Tdie)")
            ?? FindValue(hardware, SensorType.Temperature, "CPU Package")
            ?? FirstValue(hardware, SensorType.Temperature);
        double? clockMhz = MaxValue(hardware, SensorType.Clock, nameContains: "Core", nameExcludes: "Bus");
        return new HardwareComponentSnapshot(
            hardware.Name,
            load,
            temperature,
            clockMhz / 1000.0);
    }

    private static HardwareComponentSnapshot ReadGpu(IHardware hardware)
    {
        return new HardwareComponentSnapshot(
            hardware.Name,
            FindValue(hardware, SensorType.Load, "GPU Core") ?? FirstValue(hardware, SensorType.Load),
            FindValue(hardware, SensorType.Temperature, "GPU Core") ?? FirstValue(hardware, SensorType.Temperature),
            (FindValue(hardware, SensorType.Clock, "GPU Core") ?? FirstValue(hardware, SensorType.Clock)) / 1000.0,
            FirstValue(hardware, SensorType.Fan, minimum: 1),
            FindValue(hardware, SensorType.SmallData, "GPU Memory Used"),
            FindValue(hardware, SensorType.SmallData, "GPU Memory Total"));
    }

    private static (double? Used, double? Total) ReadMemory(IHardware hardware)
    {
        double? used = FindValue(hardware, SensorType.Data, "Memory Used");
        double? available = FindValue(hardware, SensorType.Data, "Memory Available");
        return used is { } u && available is { } a
            ? (u, u + a)
            : (used, null);
    }

    private static double? FindValue(IHardware hardware, SensorType type, string name)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType == type &&
                string.Equals(sensor.Name, name, StringComparison.OrdinalIgnoreCase) &&
                sensor.Value is { } value && !double.IsNaN(value))
            {
                return value;
            }
        }

        return null;
    }

    private static double? FirstValue(IHardware hardware, SensorType type, double minimum = double.MinValue)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType == type && sensor.Value is { } value && !double.IsNaN(value) && value >= minimum)
            {
                return value;
            }
        }

        return null;
    }

    private static double? MaxValue(IHardware hardware, SensorType type, string nameContains, string nameExcludes)
    {
        double? best = null;
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType == type &&
                sensor.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase) &&
                !sensor.Name.Contains(nameExcludes, StringComparison.OrdinalIgnoreCase) &&
                sensor.Value is { } value && !double.IsNaN(value))
            {
                best = best is { } current ? Math.Max(current, value) : value;
            }
        }

        return best;
    }

    private void TryClose()
    {
        try
        {
            _computer?.Close();
        }
        catch
        {
            // Closing a partially opened driver must never take the app down.
        }
        finally
        {
            _computer = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            TryClose();
        }
    }
}
