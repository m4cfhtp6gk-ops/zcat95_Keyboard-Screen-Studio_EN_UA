namespace KeyboardScreen.Core;

/// <summary>
/// Two to four crypto pairs, each with its price, 24-hour change and an hourly
/// sparkline for the day.
/// </summary>
public sealed class CryptoTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color Panel = Color.FromRgb(15, 20, 27);
    private static readonly Color Stroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);

    public string Id => "crypto";
    public string DisplayName => Loc.T("ThemeCryptoName");
    public string Description => Loc.T("ThemeCryptoDescription");
    public string Details => Loc.T("ThemeCryptoDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleCrypto"));

        CryptoSnapshot? crypto = snapshot.Crypto;
        var coins = (crypto?.Coins ?? []).Take(4).ToArray();
        if (crypto is null || coins.Length == 0)
        {
            canvas.AlignedText(Loc.T("ScreenStocksEmpty"), 15, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(crypto?.ErrorMessage ?? Loc.T("ScreenStocksAddSymbols"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 182, safe.Width, 18), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        bool redForGain = crypto.RedForGain;
        double top = safe.Top + 36;
        double available = safe.Bottom - 26 - top;
        double gap = 10;
        double cardHeight = Math.Min(96, (available - gap * (coins.Length - 1)) / coins.Length);

        foreach (CryptoCoinSnapshot coin in coins)
        {
            DrawCoin(canvas, new Rect(safe.Left, top, safe.Width, cardHeight), coin, redForGain);
            top += cardHeight + gap;
        }

        string footer = crypto.IsStale
            ? Loc.T("ScreenStocksStale")
            : Loc.T("ScreenWeatherUpdated", crypto.UpdatedAt.ToString("HH:mm"));
        canvas.AlignedText(footer, 9, Muted,
            new Rect(safe.Left, safe.Bottom - 14, safe.Width, 13), FontWeights.Normal, TextAlignment.Center);
    }

    private static void DrawCoin(ScreenCanvas canvas, Rect card, CryptoCoinSnapshot coin, bool redForGain)
    {
        canvas.RoundedRect(card, 11, Panel, Stroke);

        bool gain = coin.ChangePercent24h >= 0;
        Color gainColor = redForGain ? Color.FromRgb(238, 78, 88) : Color.FromRgb(47, 194, 116);
        Color lossColor = redForGain ? Color.FromRgb(47, 194, 116) : Color.FromRgb(238, 78, 88);
        Color changeColor = gain ? gainColor : lossColor;

        double inset = 11;
        double contentWidth = card.Width - inset * 2;

        canvas.AlignedText(coin.DisplayName, 12, Colors.White,
            new Rect(card.Left + inset, card.Top + 7, contentWidth * 0.55, 16),
            FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText($"{(gain ? "▲" : "▼")} {Math.Abs(coin.ChangePercent24h):0.0}%", 11, changeColor,
            new Rect(card.Left + inset + contentWidth * 0.45, card.Top + 8, contentWidth * 0.55, 15),
            FontWeights.SemiBold, TextAlignment.Right);

        canvas.AlignedText(CurrencyTheme.FormatRate(coin.Price), 20, Colors.White,
            new Rect(card.Left + inset, card.Top + 25, contentWidth, 26),
            FontWeights.SemiBold, TextAlignment.Left);

        if (coin.HourlyCloses.Count >= 2 && card.Height >= 76)
        {
            DrawSparkline(canvas,
                new Rect(card.Left + inset, card.Bottom - 22, contentWidth, 15),
                coin.HourlyCloses, changeColor);
        }
    }

    private static void DrawSparkline(ScreenCanvas canvas, Rect bounds, IReadOnlyList<double> closes, Color color)
    {
        double min = closes.Min();
        double max = closes.Max();
        double range = Math.Max(max - min, 1e-9);

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            for (int index = 0; index < closes.Count; index++)
            {
                double x = bounds.Left + bounds.Width * index / (closes.Count - 1);
                double y = bounds.Bottom - (closes[index] - min) / range * bounds.Height;
                var point = new Point(x, y);
                if (index == 0)
                {
                    context.BeginFigure(point, false, false);
                }
                else
                {
                    context.LineTo(point, true, true);
                }
            }
        }

        canvas.Path(geometry, color, 1.6);
    }
}
