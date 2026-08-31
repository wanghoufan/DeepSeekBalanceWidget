using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget.Tests;

public sealed class CodexConsumptionRateTrackerTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 9, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Observe_TwoPercentInFiveMinutes_ReturnsWarning()
    {
        var tracker = new CodexConsumptionRateTracker();

        tracker.Observe("account", 80, Start);
        ConsumptionRateResult result = tracker.Observe("account", 78, Start.AddMinutes(4));

        Assert.Equal(ConsumptionAlertLevel.Warning, result.Level);
        Assert.Equal(2, result.FiveMinuteConsumption);
    }

    [Fact]
    public void Observe_FivePercentInFiveMinutes_ReturnsCritical()
    {
        var tracker = new CodexConsumptionRateTracker();

        tracker.Observe("account", 80, Start);
        ConsumptionRateResult result = tracker.Observe("account", 75, Start.AddMinutes(5));

        Assert.Equal(ConsumptionAlertLevel.Critical, result.Level);
        Assert.Equal(5, result.FiveMinuteConsumption);
    }

    [Fact]
    public void Observe_ThreePercentInOneMinute_ReturnsCritical()
    {
        var tracker = new CodexConsumptionRateTracker();

        tracker.Observe("account", 80, Start);
        ConsumptionRateResult result = tracker.Observe("account", 77, Start.AddMinutes(1));

        Assert.Equal(ConsumptionAlertLevel.Critical, result.Level);
        Assert.Equal(3, result.OneMinuteConsumption);
    }

    [Fact]
    public void Observe_AfterFiveQuietMinutes_ReturnsNormal()
    {
        var tracker = new CodexConsumptionRateTracker();

        tracker.Observe("account", 80, Start);
        tracker.Observe("account", 77, Start.AddMinutes(1));
        ConsumptionRateResult result = tracker.Observe("account", 77, Start.AddMinutes(6).AddSeconds(1));

        Assert.Equal(ConsumptionAlertLevel.Normal, result.Level);
        Assert.Equal(0, result.FiveMinuteConsumption);
    }

    [Fact]
    public void Observe_WhenQuotaIncreases_ResetsHistory()
    {
        var tracker = new CodexConsumptionRateTracker();

        tracker.Observe("account", 20, Start);
        ConsumptionRateResult result = tracker.Observe("account", 100, Start.AddMinutes(1));

        Assert.Equal(ConsumptionAlertLevel.Normal, result.Level);
        Assert.Equal(0, result.FiveMinuteConsumption);
    }

    [Fact]
    public void Observe_TracksAccountsIndependently()
    {
        var tracker = new CodexConsumptionRateTracker();

        tracker.Observe("first", 80, Start);
        tracker.Observe("second", 60, Start);
        tracker.Observe("first", 75, Start.AddMinutes(1));
        ConsumptionRateResult second = tracker.Observe("second", 60, Start.AddMinutes(1));

        Assert.Equal(ConsumptionAlertLevel.Normal, second.Level);
    }
}
