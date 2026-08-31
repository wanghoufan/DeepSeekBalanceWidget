using System;

namespace DeepSeekBalanceWidget.Models;

/// <summary>
/// OpenRouter credits snapshot. OpenRouter exposes account-level credits rather
/// than rolling quota windows, so this model intentionally contains totals only.
/// </summary>
public sealed record OpenRouterUsageSnapshot(
    bool IsAvailable,
    string? Error,
    decimal TotalCreditsUsd,
    decimal TotalUsageUsd)
{
    public decimal RemainingCreditsUsd
        => Math.Max(0m, TotalCreditsUsd - TotalUsageUsd);

    public decimal RemainingPercent
        => TotalCreditsUsd <= 0m
            ? 0m
            : Math.Clamp(RemainingCreditsUsd / TotalCreditsUsd * 100m, 0m, 100m);

    // Short aliases keep the model convenient for callers that do not need the
    // currency suffix; the canonical properties above make the unit explicit.
    public decimal TotalCredits => TotalCreditsUsd;
    public decimal TotalUsage => TotalUsageUsd;
    public decimal RemainingCredits => RemainingCreditsUsd;

    public static OpenRouterUsageSnapshot Unavailable(string error)
        => new(false, error, 0m, 0m);
}
