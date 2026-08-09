
namespace KeyboardScreen.Core;

public sealed class StockTheme : IScreenTheme
{
    public string Id => "stocks";
    public string DisplayName => "股票 (Beta)";
    public string Description => "2-5 项市场行情与双股走势对比";
    public string Details => "支持 A股、港股、美股与虚拟币代码，可配置别名、涨跌颜色与数据源；恰好两只股票时显示近 5 日走势对比。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var safe = canvas.SafeBounds;
        var stocks = snapshot.Stocks;
        var secondary = Color.FromRgb(150, 160, 172);
        var panel = Color.FromRgb(15, 20, 27);
        var stroke = Color.FromRgb(29, 36, 45);

        canvas.Fill(Color.FromRgb(5, 9, 14));
        ThemeHeader.Draw(canvas, snapshot, "股票");

        var quotes = (stocks?.Quotes ?? []).ToList();

        if (quotes.Count == 0)
        {
            canvas.AlignedText("暂无行情", 17, Colors.White,
                new Rect(safe.Left, safe.Top + 145, safe.Width, 26), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText("请在屏幕配置中添加代码", 10, secondary,
                new Rect(safe.Left, safe.Top + 180, safe.Width, 18), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        bool showVisual = quotes.Count == 2;
        const double maxRowHeight = 86;

        double top = safe.Top + 40;
        double bottom = safe.Bottom - 30;
        var redForGain = stocks?.RedForGain ?? true;

        if (showVisual)
        {
            DrawTwoStockBlocks(
                canvas,
                new Rect(safe.Left, top, safe.Width, bottom - top),
                quotes,
                redForGain,
                panel,
                stroke);
        }
        else
        {
            double rowsArea = bottom - top;
            double rowHeight = Math.Min(maxRowHeight, rowsArea / quotes.Count);
            double blockTop = top + Math.Max(0, (rowsArea - rowHeight * quotes.Count) / 2);
            const double contentHeight = 42;

            for (int index = 0; index < quotes.Count; index++)
            {
                var row = new Rect(safe.Left, blockTop + index * rowHeight, safe.Width, rowHeight);
                if (index > 0)
                {
                    canvas.Line(new Point(row.Left, row.Top), new Point(row.Right, row.Top), stroke);
                }

                double rowContentTop = row.Top + Math.Max(6, (rowHeight - contentHeight) / 2);
                var quote = quotes[index];
                var gain = quote.ChangePercent >= 0;
                var gainColor = redForGain ? Color.FromRgb(238, 78, 88) : Color.FromRgb(47, 194, 116);
                var lossColor = redForGain ? Color.FromRgb(47, 194, 116) : Color.FromRgb(238, 78, 88);
                var changeColor = gain ? gainColor : lossColor;
                var marker = gain ? "▲" : "▼";

                canvas.AlignedText(quote.DisplayName, 11, Colors.White,
                    new Rect(row.Left, rowContentTop, row.Width, 16),
                    FontWeights.SemiBold, TextAlignment.Left);
                canvas.AlignedText($"{marker} {Math.Abs(quote.ChangePercent):0.00}%", 16, changeColor,
                    new Rect(row.Left, rowContentTop + 20, row.Width, 22),
                    FontWeights.SemiBold, TextAlignment.Right);
            }
        }

        var footer = stocks is { IsStale: true } ? "上次行情数据" : $"更新 {stocks?.UpdatedAt:HH:mm}";
        canvas.AlignedText(footer, 9, secondary,
            new Rect(safe.Left, safe.Bottom - 21, safe.Width, 14), FontWeights.Normal, TextAlignment.Center);
    }

    private static void DrawTwoStockBlocks(
        ScreenCanvas canvas,
        Rect area,
        IReadOnlyList<StockQuoteSnapshot> quotes,
        bool redForGain,
        Color panel,
        Color stroke)
    {
        const double gap = 6;
        double blockHeight = (area.Height - gap) / 2;
        for (int index = 0; index < 2; index++)
        {
            var block = new Rect(area.Left, area.Top + index * (blockHeight + gap), area.Width, blockHeight);
            DrawStockBlock(canvas, block, quotes[index], redForGain, panel, stroke);
        }
    }

    private static void DrawStockBlock(
        ScreenCanvas canvas,
        Rect block,
        StockQuoteSnapshot quote,
        bool redForGain,
        Color panel,
        Color stroke)
    {
        canvas.RoundedRect(block, 10, panel, stroke);
        var gain = quote.ChangePercent >= 0;
        var gainColor = redForGain ? Color.FromRgb(238, 78, 88) : Color.FromRgb(47, 194, 116);
        var lossColor = redForGain ? Color.FromRgb(47, 194, 116) : Color.FromRgb(238, 78, 88);
        var changeColor = gain ? gainColor : lossColor;
        var marker = gain ? "▲" : "▼";

        canvas.AlignedText(quote.DisplayName, 12, Colors.White,
            new Rect(block.Left + 10, block.Top + 10, block.Width - 20, 18),
            FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText($"{marker} {Math.Abs(quote.ChangePercent):0.00}%", 17, changeColor,
            new Rect(block.Left + 10, block.Top + 30, block.Width - 20, 24),
            FontWeights.SemiBold, TextAlignment.Left);

        var closes = quote.FiveDayCloses;
        if (closes is { Count: > 0 })
        {
            DrawSparkline(canvas,
                new Rect(block.Left + 8, block.Top + 58, block.Width - 16, block.Height - 66),
                closes,
                changeColor);
        }
    }

    private static void DrawSparkline(
        ScreenCanvas canvas,
        Rect bounds,
        IReadOnlyList<double> values,
        Color line)
    {
        double min = values.Min();
        double max = values.Max();
        if (values.Count < 2 || max - min < 0.0001)
        {
            double midY = bounds.Top + bounds.Height / 2;
            canvas.Line(new Point(bounds.Left, midY), new Point(bounds.Right, midY), line, 2.0);
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            bool started = false;
            int step = values.Count - 1;
            for (int index = 0; index < values.Count; index++)
            {
                double x = bounds.Left + (double)index / step * bounds.Width;
                double y = bounds.Bottom - (values[index] - min) / (max - min) * bounds.Height;
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
