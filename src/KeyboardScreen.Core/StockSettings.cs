namespace KeyboardScreen.Core;

public enum StockSourceKind
{
    Tencent,
    Yahoo
}

public sealed class StockSettings
{
    public StockSourceKind SourceKind { get; set; } = StockSourceKind.Tencent;

    public bool RedForGain { get; set; } = true;

    public List<StockItemSettings> Items { get; set; } =
    [
        new(),
        new(),
        new(),
        new(),
        new()
    ];
}

public sealed class StockItemSettings
{
    public bool Enabled { get; set; } = true;

    public string Symbol { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;
}
