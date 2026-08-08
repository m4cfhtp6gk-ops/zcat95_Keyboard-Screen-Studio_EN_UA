namespace KeyboardScreen.Core;

public enum AiQuotaSourceKind
{
    Tokscale
}

public enum AiUsageDataKind
{
    SubscriptionRemaining,
    ModelTokens,
    ModelCost
}

public sealed class AiQuotaSettings
{
    public AiQuotaSourceKind SourceKind { get; set; } = AiQuotaSourceKind.Tokscale;

    public AiUsageDataKind DataKind { get; set; } = AiUsageDataKind.SubscriptionRemaining;

    public string SelectedItemKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal ProgressTarget { get; set; }
}
