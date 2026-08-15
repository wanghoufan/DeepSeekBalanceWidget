using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public static class CodexUsageFormatter
{
    public static string FormatPlan(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType)) return "Codex";
        return "Codex " + char.ToUpperInvariant(planType[0]) + planType[1..];
    }

    public static string FormatWindow(CodexUsageWindow window)
        => $"{FormatDuration(window.DurationMinutes)}剩余 {window.RemainingPercent}%";

    public static string FormatReset(CodexUsageWindow window)
        => window.ResetsAt is null
            ? "重置时间未知"
            : window.ResetsAt.Value.ToLocalTime().ToString("MM-dd HH:mm") + " 重置";

    public static string FormatCountdown(DateTimeOffset? resetsAt, DateTimeOffset now)
    {
        if (resetsAt is null) return "--";
        TimeSpan remaining = resetsAt.Value - now;
        if (remaining <= TimeSpan.Zero) return "即将重置";
        if (remaining.Days > 0) return $"{remaining.Days} 天 {remaining.Hours} 小时";
        if (remaining.Hours > 0) return $"{remaining.Hours} 小时 {remaining.Minutes} 分钟";
        return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} 分钟";
    }

    public static string FormatDuration(int? minutes) => minutes switch
    {
        300 => "5 小时",
        10080 => "每周",
        > 0 when minutes.Value % 60 == 0 => $"{minutes.Value / 60} 小时",
        > 0 => $"{minutes.Value} 分钟",
        _ => "当前窗口"
    };
}
