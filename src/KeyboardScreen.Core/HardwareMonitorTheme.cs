namespace KeyboardScreen.Core;

/// <summary>
/// A two-page hardware readout: CPU and GPU with temperature, clock and fan on
/// the first page; memory, disk and network on the second. Pages rotate on the
/// wall clock so the dwell survives refresh ticks, and every reading the
/// platform cannot provide draws as an em dash instead of a fake number.
/// </summary>
public sealed class HardwareMonitorTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color Panel = Color.FromRgb(15, 20, 27);
    private static readonly Color Stroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);
    private static readonly Color Hot = Color.FromRgb(238, 118, 78);
    private static readonly Color IdleDot = Color.FromRgb(48, 56, 66);

    private const int PageCount = 2;

    /// <summary>Dwell per page; the view model applies the saved setting here.</summary>
    public int PageSeconds { get; set; } = 5;

    public string Id => "hardware";
    public string DisplayName => Loc.T("ThemeHardwareName");
    public string Description => Loc.T("ThemeHardwareDescription");
    public string Details => Loc.T("ThemeHardwareDetails");

    public static int PageIndex(DateTimeOffset now, int pageSeconds, int pageCount) =>
        (int)(now.ToUnixTimeSeconds() / Math.Clamp(pageSeconds, 3, 10) % Math.Max(1, pageCount));

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleHardware"));

        int page = PageIndex(snapshot.Timestamp, PageSeconds, PageCount);
        if (page == 0)
        {
            DrawDevicesPage(canvas, safe, snapshot);
        }
        else
        {
            DrawSystemPage(canvas, safe, snapshot);
        }

        DrawPageDots(canvas, safe, page);
    }

    private static void DrawDevicesPage(ScreenCanvas canvas, Rect safe, SystemSnapshot snapshot)
    {
        HardwareSnapshot? hardware = snapshot.Hardware;

        // Without a hardware source the load figures still come from the
        // system counters, so the page never goes blank.
        HardwareComponentSnapshot cpu = hardware?.Cpu
            ?? new HardwareComponentSnapshot(Loc.T("ScreenLabelProcessor"), snapshot.CpuPercent);
        HardwareComponentSnapshot? gpu = hardware?.Gpu
            ?? (snapshot.GpuPercent is { } gpuPercent
                ? new HardwareComponentSnapshot("GPU", gpuPercent)
                : null);

        double top = safe.Top + 36;
        DrawDeviceCard(canvas, new Rect(safe.Left, top, safe.Width, 122), cpu, showVram: false);

        if (gpu is not null)
        {
            DrawDeviceCard(canvas, new Rect(safe.Left, top + 130, safe.Width, 144), gpu, showVram: true);
        }
    }

    private static void DrawDeviceCard(ScreenCanvas canvas, Rect card, HardwareComponentSnapshot device, bool showVram)
    {
        canvas.RoundedRect(card, 11, Panel, Stroke);
        double inset = 11;
        double contentWidth = card.Width - inset * 2;

        canvas.Text(device.Name, 10, Secondary,
            new Point(card.Left + inset, card.Top + 8), FontWeights.SemiBold,
            TextAlignment.Left, contentWidth, 15);

        double half = contentWidth / 2;
        DrawMetricCell(canvas, card.Left + inset, card.Top + 28, half, Loc.T("ScreenHwLoad"),
            FormatPercent(device.LoadPercent), Colors.White);
        DrawMetricCell(canvas, card.Left + inset + half, card.Top + 28, half, Loc.T("ScreenHwTemp"),
            FormatTemperature(device.TemperatureC),
            device.TemperatureC >= 80 ? Hot : Colors.White);
        DrawMetricCell(canvas, card.Left + inset, card.Top + 74, half, Loc.T("ScreenHwClock"),
            FormatClock(device.ClockGhz), Colors.White);
        DrawMetricCell(canvas, card.Left + inset + half, card.Top + 74, half, Loc.T("ScreenHwFan"),
            FormatFan(device.FanRpm), Colors.White);

        if (showVram)
        {
            canvas.Text(Loc.T("ScreenHwVram"), 9, Muted,
                new Point(card.Left + inset, card.Top + 118), FontWeights.SemiBold);
            canvas.Text(FormatVram(device.MemoryUsedMb, device.MemoryTotalMb), 11, Colors.White,
                new Point(card.Left + inset, card.Top + 116), FontWeights.SemiBold,
                TextAlignment.Right, contentWidth);
        }
    }

    private static void DrawMetricCell(ScreenCanvas canvas, double left, double top, double width, string label, string value, Color color)
    {
        canvas.Text(label, 9, Muted, new Point(left, top), FontWeights.SemiBold);
        canvas.Text(value, 15, color, new Point(left, top + 14), FontWeights.SemiBold,
            TextAlignment.Left, width - 4, 22);
    }

    private static void DrawSystemPage(ScreenCanvas canvas, Rect safe, SystemSnapshot snapshot)
    {
        HardwareSnapshot? hardware = snapshot.Hardware;
        double top = safe.Top + 36;

        // Memory: the percentage always exists; the GB detail needs the source.
        var ramCard = new Rect(safe.Left, top, safe.Width, 104);
        canvas.RoundedRect(ramCard, 11, Panel, Stroke);
        canvas.Text(Loc.T("ScreenLabelMemory"), 10, Secondary,
            new Point(ramCard.Left + 11, ramCard.Top + 8), FontWeights.SemiBold);
        canvas.Text($"{snapshot.MemoryPercent:0}%", 24, Colors.White,
            new Point(ramCard.Left + 11, ramCard.Top + 26), FontWeights.SemiBold);
        canvas.Text(FormatRam(hardware?.RamUsedGb, hardware?.RamTotalGb), 10, Secondary,
            new Point(ramCard.Left + 11, ramCard.Top + 58), FontWeights.Medium,
            TextAlignment.Left, ramCard.Width - 22);
        canvas.ProgressBar(new Rect(ramCard.Left + 11, ramCard.Top + 78, ramCard.Width - 22, 7),
            snapshot.MemoryPercent, Color.FromRgb(35, 42, 51), canvas.AccentColor);

        // Disk: only with the hardware source.
        var diskCard = new Rect(safe.Left, top + 112, safe.Width, 76);
        canvas.RoundedRect(diskCard, 11, Panel, Stroke);
        canvas.Text(Loc.T("ScreenHwDisk"), 10, Secondary,
            new Point(diskCard.Left + 11, diskCard.Top + 8), FontWeights.SemiBold);
        canvas.Text(FormatPercent(hardware?.DiskUsedPercent), 18, Colors.White,
            new Point(diskCard.Left + 11, diskCard.Top + 26), FontWeights.SemiBold);
        canvas.ProgressBar(new Rect(diskCard.Left + 11, diskCard.Top + 56, diskCard.Width - 22, 7),
            hardware?.DiskUsedPercent ?? 0, Color.FromRgb(35, 42, 51), canvas.AccentColor);

        // Network: straight from the system counters.
        var netCard = new Rect(safe.Left, top + 196, safe.Width, 78);
        canvas.RoundedRect(netCard, 11, Panel, Stroke);
        double half = netCard.Width / 2;
        canvas.CenteredText(Loc.T("ScreenLabelDownload"), 9, Secondary,
            new Rect(netCard.Left, netCard.Top + 10, half, 14), FontWeights.SemiBold);
        canvas.CenteredText($"{snapshot.DownloadMbps:0.0}", 17, Colors.White,
            new Rect(netCard.Left, netCard.Top + 28, half, 24), FontWeights.SemiBold);
        canvas.CenteredText("Mbps", 9, Muted,
            new Rect(netCard.Left, netCard.Top + 56, half, 13), FontWeights.Medium);
        canvas.Line(new Point(netCard.Left + half, netCard.Top + 12),
            new Point(netCard.Left + half, netCard.Bottom - 12), Stroke);
        canvas.CenteredText(Loc.T("ScreenLabelUpload"), 9, Secondary,
            new Rect(netCard.Left + half, netCard.Top + 10, half, 14), FontWeights.SemiBold);
        canvas.CenteredText($"{snapshot.UploadMbps:0.0}", 17, Colors.White,
            new Rect(netCard.Left + half, netCard.Top + 28, half, 24), FontWeights.SemiBold);
        canvas.CenteredText("Mbps", 9, Muted,
            new Rect(netCard.Left + half, netCard.Top + 56, half, 13), FontWeights.Medium);
    }

    private static void DrawPageDots(ScreenCanvas canvas, Rect safe, int page)
    {
        double centerX = safe.Left + safe.Width / 2;
        double y = safe.Bottom - 8;
        for (int index = 0; index < PageCount; index++)
        {
            double x = centerX + (index - (PageCount - 1) / 2.0) * 14;
            canvas.Ellipse(new Rect(x - 3, y - 3, 6, 6), index == page ? canvas.AccentColor : IdleDot);
        }
    }

    public static string FormatPercent(double? value) =>
        value is { } percent ? $"{percent:0}%" : "—";

    // A flat zero from a sensor is a failed read, never a real value.
    public static string FormatTemperature(double? value) =>
        value is { } celsius and > 0 ? DisplayUnits.TemperatureShort(celsius) : "—";

    /// <summary>The unit lives in the cell label, so the value stays narrow.</summary>
    public static string FormatClock(double? value) =>
        value is { } ghz and > 0.01 ? (ghz < 10 ? $"{ghz:0.00}" : $"{ghz:0}") : "—";

    public static string FormatFan(double? value) =>
        value is { } rpm and > 0 ? $"{rpm:0}" : "—";

    public static string FormatVram(double? usedMb, double? totalMb) =>
        usedMb is { } used && totalMb is { } total && total > 0
            ? $"{used / 1024:0.0} / {total / 1024:0.0} GB"
            : "—";

    public static string FormatRam(double? usedGb, double? totalGb) =>
        usedGb is { } used && totalGb is { } total && total > 0
            ? $"{used:0.0} / {total:0.0} GB"
            : string.Empty;
}
