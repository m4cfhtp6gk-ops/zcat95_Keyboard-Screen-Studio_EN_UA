namespace KeyboardScreen.Core;

public interface IAiQuotaSnapshotSource
{
    Task<AiQuotaSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
