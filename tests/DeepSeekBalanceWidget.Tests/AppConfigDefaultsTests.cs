using DeepSeekBalanceWidget.Models;
using System.Text.Json;

namespace DeepSeekBalanceWidget.Tests;

public class AppConfigDefaultsTests
{
    [Fact]
    public void NewConfig_EnablesCodexWithReadableDeepSeekStyle()
    {
        var config = new AppConfig();

        Assert.True(config.EnableCodexMonitoring);
        Assert.Equal(14, config.CodexFontSize);
        Assert.Equal("DeepSeek", config.CodexFontStyle);
    }

    [Fact]
    public void NewConfig_UsesWeekdayOnlyPeakRanges()
    {
        var config = new AppConfig();

        Assert.Equal(2, config.PeakHourRanges.Count);
        Assert.All(config.PeakHourRanges, range => Assert.True(range.WeekdaysOnly));
    }

    [Fact]
    public void LegacyConfigWithoutCodexFields_UsesCodexDefaults()
    {
        var config = JsonSerializer.Deserialize<AppConfig>("""{"selectedCurrency":"USD"}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(config);
        Assert.True(config.EnableCodexMonitoring);
        Assert.Equal(14, config.CodexFontSize);
        Assert.Equal("DeepSeek", config.CodexFontStyle);
    }
}
