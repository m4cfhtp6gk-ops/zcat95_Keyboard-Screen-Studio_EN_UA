using System.Diagnostics;
using Microsoft.Win32;

namespace KeyboardScreen.App.Avalonia.Platform;

/// <summary>
/// Effective CPU frequency without the sensor driver: the base clock from the
/// registry scaled by the "% Processor Performance" counter - the same math
/// Task Manager shows, boost above 100% included. The counter's first sample
/// is always zero, so the first poll simply reports nothing.
/// </summary>
public sealed class ProcessorClockFallback : IDisposable
{
    private readonly object _gate = new();
    private PerformanceCounter? _counter;
    private bool _unavailable;
    private double _baseMhz;

    public double? ReadGhz()
    {
        lock (_gate)
        {
            if (_unavailable)
            {
                return null;
            }

            try
            {
                if (_counter is null)
                {
                    _baseMhz = ReadBaseMhz();
                    if (_baseMhz <= 0)
                    {
                        _unavailable = true;
                        return null;
                    }

                    _counter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", readOnly: true);
                }

                double percent = _counter.NextValue();
                double ghz = _baseMhz * percent / 100.0 / 1000.0;
                return ghz is > 0.1 and < 9.0 ? ghz : null;
            }
            catch (Exception)
            {
                // A missing counter category or a broken perf registry: give up
                // for the process lifetime rather than throwing every poll.
                _unavailable = true;
                Dispose();
                return null;
            }
        }
    }

    private static double ReadBaseMhz()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
            @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return key?.GetValue("~MHz") is int mhz ? mhz : 0;
    }

    public void Dispose()
    {
        _counter?.Dispose();
        _counter = null;
    }
}
