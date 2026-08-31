namespace DeepSeekBalanceWidget.Models;

public sealed record CodexUsageWindow(
    int UsedPercent,
    int RemainingPercent,
    int? DurationMinutes,
    DateTimeOffset? ResetsAt);

public sealed record CodexUsageSnapshot(
    bool IsAvailable,
    string? PlanType,
    IReadOnlyList<CodexUsageWindow> Windows,
    string? Error)
{
    public static CodexUsageSnapshot Unavailable(string error)
        => new(false, null, Array.Empty<CodexUsageWindow>(), error);
}
