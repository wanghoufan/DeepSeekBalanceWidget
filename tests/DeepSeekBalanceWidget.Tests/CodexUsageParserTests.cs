using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget.Tests;

public class CodexUsageParserTests
{
    [Fact]
    public async Task GetUsageAsync_WhenLiveProbeEnabled_ReturnsAuthenticatedUsage()
    {
        if (Environment.GetEnvironmentVariable("RUN_CODEX_LIVE_TEST") != "1")
            return;

        var result = await new CodexAppServerClient(TimeSpan.FromSeconds(30))
            .GetUsageAsync(CancellationToken.None);

        Assert.True(result.IsAvailable, result.Error);
        Assert.NotEmpty(result.Windows);
    }

    [Fact]
    public void Parse_ReadsReturnedWindowsAndConvertsToRemainingPercent()
    {
        const string json = """
        {
          "id": 2,
          "result": {
            "rateLimits": {
              "planType": "plus",
              "primary": {
                "usedPercent": 49,
                "windowDurationMins": 10080,
                "resetsAt": 1786164619
              },
              "secondary": null
            }
          }
        }
        """;

        var result = CodexUsageParser.Parse(json);

        Assert.True(result.IsAvailable);
        Assert.Equal("plus", result.PlanType);
        var window = Assert.Single(result.Windows);
        Assert.Equal(51, window.RemainingPercent);
        Assert.Equal(10080, window.DurationMinutes);
        Assert.NotNull(window.ResetsAt);
    }

    [Fact]
    public void Parse_ClampsBackendPercentBeforeCalculatingRemaining()
    {
        const string json = """
        {
          "result": {
            "rateLimits": {
              "primary": { "usedPercent": 130 },
              "secondary": { "usedPercent": -5 }
            }
          }
        }
        """;

        var result = CodexUsageParser.Parse(json);

        Assert.Equal(0, result.Windows[0].RemainingPercent);
        Assert.Equal(100, result.Windows[1].RemainingPercent);
    }

    [Fact]
    public void Parse_ErrorResponseIsUnavailable()
    {
        var result = CodexUsageParser.Parse(
            """{"id":2,"error":{"message":"Not logged in"}}""");

        Assert.False(result.IsAvailable);
        Assert.Equal("Not logged in", result.Error);
    }

    [Fact]
    public void Parse_MissingWindowsIsUnavailable()
    {
        var result = CodexUsageParser.Parse(
            """{"id":2,"result":{"rateLimits":{"planType":"plus"}}}""");

        Assert.False(result.IsAvailable);
        Assert.Equal("未返回 Codex 用量窗口", result.Error);
    }

    [Theory]
    [InlineData(null, "Codex")]
    [InlineData("", "Codex")]
    [InlineData("plus", "Codex Plus")]
    public void FormatPlan_HandlesMissingAndKnownPlan(string? planType, string expected)
    {
        Assert.Equal(expected, CodexUsageFormatter.FormatPlan(planType));
    }

    [Fact]
    public void FormatReset_HandlesMissingResetTime()
    {
        var window = new CodexUsageWindow(25, 75, 300, null);

        Assert.Equal("重置时间未知", CodexUsageFormatter.FormatReset(window));
    }

    [Theory]
    [InlineData(6, 18, 30, "6 天 18 小时")]
    [InlineData(0, 8, 25, "8 小时 25 分钟")]
    [InlineData(0, 0, 12, "12 分钟")]
    public void FormatCountdown_ShowsRemainingDaysAndHours(
        int days, int hours, int minutes, string expected)
    {
        var now = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.FromHours(8));

        string result = CodexUsageFormatter.FormatCountdown(
            now.AddDays(days).AddHours(hours).AddMinutes(minutes), now);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatCountdown_WhenResetPassed_ShowsImminentReset()
    {
        var now = new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.Equal("即将重置", CodexUsageFormatter.FormatCountdown(now.AddSeconds(-1), now));
        Assert.Equal("--", CodexUsageFormatter.FormatCountdown(null, now));
    }

    [Theory]
    [InlineData(300, "5 小时")]
    [InlineData(10080, "每周")]
    [InlineData(120, "2 小时")]
    [InlineData(45, "45 分钟")]
    public void FormatDuration_UsesReturnedWindowLength(int minutes, string expected)
    {
        Assert.Equal(expected, CodexUsageFormatter.FormatDuration(minutes));
    }
}
