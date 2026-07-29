namespace KeyboardScreen.Core;

public interface IStockSnapshotSource
{
    Task<StockSnapshot> ReadAsync(StockSettings settings, CancellationToken cancellationToken = default);
}
