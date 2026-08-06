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
    [InlineData(300, "5 小时")]
    [InlineData(10080, "每周")]
    [InlineData(120, "2 小时")]
    [InlineData(45, "45 分钟")]
    public void FormatDuration_UsesReturnedWindowLength(int minutes, string expected)
    {
        Assert.Equal(expected, CodexUsageFormatter.FormatDuration(minutes));
    }
}
