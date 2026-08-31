using System;

namespace DeepSeekBalanceWidget.Models;

public sealed record AlertState(
    decimal? LastSuccessfulBalance,
    DateTimeOffset? LastSuccessfulRefreshUtc,
    bool InLowBalanceState,
    DateTimeOffset? LastLowBalanceAlertUtc,
    DateTimeOffset? LastAbnormalAlertUtc)
{
    public bool HasBaseline => LastSuccessfulBalance.HasValue;
    public bool IsFirstRefreshOfSession { get; init; } = false;
}
