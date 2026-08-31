using System.Text.Json;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public static class CodexUsageParser
{
    public static CodexUsageSnapshot Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                string message = error.TryGetProperty("message", out var errorMessage)
                    ? errorMessage.GetString() ?? "Codex 返回错误"
                    : "Codex 返回错误";
                return CodexUsageSnapshot.Unavailable(message);
            }

            if (!root.TryGetProperty("result", out var result)
                || !result.TryGetProperty("rateLimits", out var limits))
            {
                return CodexUsageSnapshot.Unavailable("未返回 Codex 用量");
            }

            var windows = new List<CodexUsageWindow>();
            AddWindow(limits, "primary", windows);
            AddWindow(limits, "secondary", windows);

            if (windows.Count == 0)
                return CodexUsageSnapshot.Unavailable("未返回 Codex 用量窗口");

            string? planType = limits.TryGetProperty("planType", out var plan)
                ? plan.GetString()
                : null;
            return new CodexUsageSnapshot(true, planType, windows, null);
        }
        catch (JsonException)
        {
            return CodexUsageSnapshot.Unavailable("Codex 用量数据格式错误");
        }
    }

    private static void AddWindow(
        JsonElement limits,
        string propertyName,
        ICollection<CodexUsageWindow> windows)
    {
        if (!limits.TryGetProperty(propertyName, out var window)
            || window.ValueKind != JsonValueKind.Object
            || !window.TryGetProperty("usedPercent", out var usedElement)
            || !usedElement.TryGetInt32(out int usedPercent))
        {
            return;
        }

        int? duration = window.TryGetProperty("windowDurationMins", out var durationElement)
                        && durationElement.TryGetInt32(out int durationMinutes)
            ? durationMinutes
            : null;

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("resetsAt", out var resetsElement)
            && resetsElement.TryGetInt64(out long unixSeconds))
        {
            try { resetsAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds); }
            catch (ArgumentOutOfRangeException) { }
        }

        int normalizedUsed = Math.Clamp(usedPercent, 0, 100);
        windows.Add(new CodexUsageWindow(
            normalizedUsed,
            100 - normalizedUsed,
            duration,
            resetsAt));
    }
}
