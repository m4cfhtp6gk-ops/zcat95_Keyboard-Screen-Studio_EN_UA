namespace KeyboardScreen.Core;

/// <summary>Exchange rates for one base currency as up to four large rows.</summary>
public sealed class CurrencyTheme : IScreenTheme
{
    private static readonly Color Background = Color.FromRgb(5, 9, 14);
    private static readonly Color Panel = Color.FromRgb(15, 20, 27);
    private static readonly Color Stroke = Color.FromRgb(29, 36, 45);
    private static readonly Color Secondary = Color.FromRgb(150, 160, 172);
    private static readonly Color Muted = Color.FromRgb(102, 112, 124);

    public string Id => "currency";
    public string DisplayName => Loc.T("ThemeCurrencyName");
    public string Description => Loc.T("ThemeCurrencyDescription");
    public string Details => Loc.T("ThemeCurrencyDetails");

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        canvas.Fill(Background);
        ThemeHeader.Draw(canvas, snapshot, Loc.T("ScreenTitleCurrency"));

        CurrencySnapshot? currency = snapshot.Currency;
        if (currency is not { Available: true } || currency.Rates.Count == 0)
        {
            canvas.AlignedText(Loc.T("ScreenCurrencyEmpty"), 15, Colors.White,
                new Rect(safe.Left, safe.Top + 150, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText(currency?.ErrorMessage ?? Loc.T("ScreenCurrencyHint"), 10, Secondary,
                new Rect(safe.Left, safe.Top + 182, safe.Width, 18), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        canvas.AlignedText("1 " + currency.BaseCurrency, 16, Colors.White,
            new Rect(safe.Left, safe.Top + 36, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Left);

        var rows = currency.Rates.Take(4).ToArray();
        double top = safe.Top + 70;
        double available = safe.Bottom - 26 - top;
        double gap = 10;
        double rowHeight = Math.Min(74, (available - gap * (rows.Length - 1)) / rows.Length);

        foreach (CurrencyRate rate in rows)
        {
            var card = new Rect(safe.Left, top, safe.Width, rowHeight);
            canvas.RoundedRect(card, 11, Panel, Stroke);
            canvas.AlignedText(rate.Code, 12, Secondary,
                new Rect(card.Left + 11, card.Top + 8, card.Width - 22, 16),
                FontWeights.SemiBold, TextAlignment.Left);
            canvas.AlignedText(FormatRate(rate.Rate), 24, Colors.White,
                new Rect(card.Left + 11, card.Top + rowHeight - 42, card.Width - 22, 34),
                FontWeights.SemiBold, TextAlignment.Right);
            top += rowHeight + gap;
        }

        string footer = currency.IsStale
            ? Loc.T("ScreenClaudeStale")
            : Loc.T("ScreenWeatherUpdated", DisplayUnits.Time(currency.UpdatedAt));
        canvas.AlignedText(footer, 9, Muted,
            new Rect(safe.Left, safe.Bottom - 14, safe.Width, 13), FontWeights.Normal, TextAlignment.Center);
    }

    /// <summary>Four significant figures read best across JPY-scale and BTC-scale rates.</summary>
    public static string FormatRate(double rate) => rate switch
    {
        >= 1000 => rate.ToString("0", Loc.Culture),
        >= 100 => rate.ToString("0.0", Loc.Culture),
        >= 1 => rate.ToString("0.00", Loc.Culture),
        _ => rate.ToString("0.0000", Loc.Culture)
    };
}
