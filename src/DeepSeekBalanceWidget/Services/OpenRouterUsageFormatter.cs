using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>Display helpers for the account-level OpenRouter credits model.</summary>
public static class OpenRouterUsageFormatter
{
    public static string ProviderLabel => OpenRouterUsageProvider.ProviderName;

    public static string FormatRemaining(OpenRouterUsageSnapshot snapshot)
        => snapshot.IsAvailable
            ? $"${snapshot.RemainingCreditsUsd:0.##} / ${snapshot.TotalCreditsUsd:0.##}"
            : "--";

    public static string FormatUsage(OpenRouterUsageSnapshot snapshot)
        => snapshot.IsAvailable ? $"${snapshot.TotalUsageUsd:0.##}" : "--";

    public static string FormatRemainingPercent(OpenRouterUsageSnapshot snapshot)
        => snapshot.IsAvailable ? $"{snapshot.RemainingPercent:0.#}%" : "--";

    public static string FormatStatus(OpenRouterUsageSnapshot snapshot)
        => snapshot.IsAvailable ? FormatRemaining(snapshot) : snapshot.Error ?? "暂不可用";
}
