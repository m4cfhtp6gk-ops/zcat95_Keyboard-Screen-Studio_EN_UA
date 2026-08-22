using System.IO;

namespace KeyboardScreen.Core;

public sealed record DiskVolume(string Name, string Label, double UsedGb, double TotalGb)
{
    public double UsedPercent => TotalGb > 0 ? Math.Clamp(UsedGb / TotalGb * 100, 0, 100) : 0;
}

public sealed record DiskSnapshot(
    bool Available,
    IReadOnlyList<DiskVolume> Volumes,
    DateTimeOffset UpdatedAt)
{
    public static DiskSnapshot Unavailable { get; } = new(false, [], DateTimeOffset.MinValue);
}

/// <summary>All ready fixed volumes via DriveInfo, cached for half a minute.</summary>
public sealed class DiskUsageSource
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private DiskSnapshot? _cached;
    private DateTimeOffset _lastRead;

    public DiskSnapshot Read()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        if (_cached is not null && now - _lastRead < CacheDuration)
        {
            return _cached;
        }

        var volumes = new List<DiskVolume>();
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                    {
                        continue;
                    }

                    double totalGb = drive.TotalSize / 1_073_741_824.0;
                    if (totalGb < 0.5)
                    {
                        continue;
                    }

                    double freeGb = drive.TotalFreeSpace / 1_073_741_824.0;
                    volumes.Add(new DiskVolume(
                        drive.Name.TrimEnd('\\', '/'),
                        drive.VolumeLabel,
                        totalGb - freeGb,
                        totalGb));
                }
                catch (IOException)
                {
                    // A single unreadable volume must not hide the others.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (IOException)
        {
        }

        _cached = new DiskSnapshot(volumes.Count > 0, volumes, now);
        _lastRead = now;
        return _cached;
    }
}

/// <summary>Fill bars for every ready volume, up to six rows.</summary>
public sealed class DiskUsageTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color CardColor = Color.FromRgb(15, 20, 27);
    private static readonly Color CardStroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Track = Color.FromRgb(31, 39, 48);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color WarnOrange = Color.FromRgb(232, 158, 74);
    private static readonly Color FullRed = Color.FromRgb(224, 68, 68);

    public string Id => "disks";
    public string DisplayName => Loc.T("ThemeDisksName");
    public string Description => Loc.T("ThemeDisksDescription");
    public string Details => Loc.T("ThemeDisksDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleDisks"));

        DiskSnapshot? disks = snapshot.Disks;
        if (disks is not { Available: true } || disks.Volumes.Count == 0)
        {
            canvas.AlignedText(Loc.T("ScreenDisksWaiting"), 14, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            return;
        }

        const double rowHeight = 52;
        const double gap = 6;
        double top = safe.Top + 40;
        foreach (DiskVolume volume in disks.Volumes.Take(6))
        {
            var row = new Rect(safe.Left, top, safe.Width, rowHeight);
            canvas.RoundedRect(row, 10, CardColor, CardStroke);
            var content = new Rect(row.Left + 11, row.Top + 7, row.Width - 22, row.Height - 14);

            string title = volume.Label.Length > 0 ? $"{volume.Name} · {volume.Label}" : volume.Name;
            canvas.AlignedText(title, 11, Colors.White,
                new Rect(content.Left, content.Top, content.Width - 40, 15), FontWeights.SemiBold, TextAlignment.Left);
            canvas.AlignedText($"{volume.UsedPercent:0}%", 11, PercentColor(canvas, volume.UsedPercent),
                new Rect(content.Left, content.Top, content.Width, 15), FontWeights.SemiBold, TextAlignment.Right);

            canvas.ProgressBar(new Rect(content.Left, content.Top + 19, content.Width, 6),
                volume.UsedPercent, Track, PercentColor(canvas, volume.UsedPercent));

            canvas.AlignedText(
                Loc.T("ScreenDisksUsage", FormatGb(volume.UsedGb), FormatGb(volume.TotalGb)),
                9, Secondary,
                new Rect(content.Left, content.Top + 27, content.Width, 13), FontWeights.Normal, TextAlignment.Left);
            top += rowHeight + gap;
        }

        if (disks.Volumes.Count > 6)
        {
            canvas.AlignedText("+" + (disks.Volumes.Count - 6), 10, Secondary,
                new Rect(safe.Left, top + 2, safe.Width, 14), FontWeights.Medium, TextAlignment.Center);
        }
    }

    private static Color PercentColor(ScreenCanvas canvas, double percent) => percent switch
    {
        >= 95 => FullRed,
        >= 85 => WarnOrange,
        _ => canvas.AccentColor
    };

    private static string FormatGb(double gb) =>
        gb >= 1024 ? $"{gb / 1024:0.0} TB" : $"{gb:0} GB";
}
