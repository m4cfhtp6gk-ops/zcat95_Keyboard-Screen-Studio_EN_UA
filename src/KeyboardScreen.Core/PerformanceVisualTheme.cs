using System;
using System.Collections.Generic;

namespace KeyboardScreen.Core;

/// <summary>
/// Performance visualization theme: rolling sparkline curves for CPU, memory,
/// download/upload speed and GPU utilization on the 142x428 keyboard display.
/// </summary>
public sealed class PerformanceVisualTheme : IScreenTheme
{
    private const int HistoryCapacity = 60;
    private static readonly Color Background = Color.FromRgb(7, 9, 12);
    private static readonly Color PanelBackground = Color.FromRgb(24, 28, 34);
    private static readonly Color LabelColor = Color.FromRgb(153, 165, 178);
    private static readonly Color MemoryColor = Color.FromRgb(255, 197, 66);
    private static readonly Color DownloadColor = Color.FromRgb(94, 209, 150);
    private static readonly Color UploadColor = Color.FromRgb(86, 156, 214);
    private static readonly Color GpuColor = Color.FromRgb(200, 122, 255);

    private readonly object _sync = new();
    private readonly List<double?>[] _series =
    [
        new List<double?>(),
        new List<double?>(),
        new List<double?>(),
        new List<double?>(),
        new List<double?>()
    ];
    private readonly bool[] _enabled = [true, true, true, true, true];

    public string Id => "performance-visual";
    public string DisplayName => "性能可视化";
    public string Description => "CPU、内存、网络上下行与 GPU 使用率曲线";
    public string Details => "以最近 60 秒的滚动曲线展示系统性能；GPU 数据不可用时显示为“不可用”。";

    public void SetModulesEnabled(bool cpu, bool memory, bool download, bool upload, bool gpu)
    {
        lock (_sync)
        {
            _enabled[0] = cpu;
            _enabled[1] = memory;
            _enabled[2] = download;
            _enabled[3] = upload;
            _enabled[4] = gpu;
        }
    }

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Push(snapshot);
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, DisplayName);

        bool[] enabled;
        lock (_sync)
        {
            enabled = (bool[])_enabled.Clone();
        }

        var panels = new List<(string Label, string Value, IReadOnlyList<double?> Samples, Color Line, double Max)>();
        if (enabled[0])
        {
            panels.Add(("CPU", $"{snapshot.CpuPercent:0}%", _series[0], canvas.AccentColor, 100.0));
        }
        if (enabled[1])
        {
            panels.Add(("内存", $"{snapshot.MemoryPercent:0}%", _series[1], MemoryColor, 100.0));
        }
        if (enabled[2])
        {
            panels.Add(("下载", $"▼ {snapshot.DownloadMbps:0.0}", _series[2], DownloadColor, 100.0));
        }
        if (enabled[3])
        {
            panels.Add(("上传", $"▲ {snapshot.UploadMbps:0.0}", _series[3], UploadColor, 100.0));
        }
        if (enabled[4])
        {
            panels.Add(("GPU", snapshot.GpuPercent is double gpu ? $"{gpu:0}%" : "不可用", _series[4], GpuColor, 100.0));
        }

        double headerHeight = 32;
        double gap = 5;
        double panelHeight = panels.Count == 0
            ? 0
            : (safe.Height - headerHeight - gap * (panels.Count - 1)) / panels.Count;
        double y = safe.Top + headerHeight;
        foreach ((string label, string value, IReadOnlyList<double?> samples, Color line, double max) in panels)
        {
            DrawPanel(
                canvas,
                label,
                value,
                samples,
                line,
                max,
                new Rect(safe.Left, y, safe.Width, panelHeight));
            y += panelHeight + gap;
        }
    }

    private void Push(SystemSnapshot snapshot)
    {
        lock (_sync)
        {
            Add(_series[0], snapshot.CpuPercent);
            Add(_series[1], snapshot.MemoryPercent);
            Add(_series[2], snapshot.DownloadMbps);
            Add(_series[3], snapshot.UploadMbps);
            Add(_series[4], snapshot.GpuPercent);
        }
    }

    private static void Add(List<double?> series, double? value)
    {
        series.Add(value);
        if (series.Count > HistoryCapacity)
        {
            series.RemoveAt(0);
        }
    }

    private static void DrawPanel(
        ScreenCanvas canvas,
        string label,
        string valueText,
        IReadOnlyList<double?> samples,
        Color line,
        double maxValue,
        Rect bounds)
    {
        canvas.RoundedRect(bounds, 6, PanelBackground);
        canvas.Text(label, 10.5, LabelColor, new Point(bounds.Left + 6, bounds.Top + 4), FontWeights.Medium);
        canvas.Text(
            valueText,
            12.0,
            Colors.White,
            new Point(bounds.Right - 6, bounds.Top + 4),
            FontWeights.SemiBold,
            TextAlignment.Right);
        DrawSparkline(
            canvas,
            new Rect(bounds.Left + 6, bounds.Top + 22, bounds.Width - 12, bounds.Height - 28),
            samples,
            line,
            maxValue);
    }

    private static void DrawSparkline(
        ScreenCanvas canvas,
        Rect bounds,
        IReadOnlyList<double?> samples,
        Color line,
        double maxValue)
    {
        if (samples.Count < 2 || maxValue <= 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            bool started = false;
            int step = samples.Count - 1;
            for (int index = 0; index < samples.Count; index++)
            {
                if (samples[index] is not double value)
                {
                    continue;
                }

                double x = bounds.Left + (double)index / step * bounds.Width;
                double y = bounds.Bottom - Math.Clamp(value, 0, maxValue) / maxValue * bounds.Height;
                if (!started)
                {
                    context.BeginFigure(new Point(x, y), isFilled: false, isClosed: false);
                    started = true;
                }
                else
                {
                    context.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
                }
            }
        }

        geometry.Freeze();
        canvas.Path(geometry, line, 2.0);
    }
}
