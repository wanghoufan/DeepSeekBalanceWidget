namespace DeepSeekBalanceWidget.Models;

public sealed record CodexAccountUsageSnapshot(
    string AccountId,
    string Email,
    string MiniLabel,
    CodexUsageSnapshot Usage,
    DateTimeOffset? UpdatedAt,
    bool IsStale,
    string? RefreshError = null);

