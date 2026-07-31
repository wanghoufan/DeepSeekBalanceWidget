using System;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;
using Xunit;

namespace DeepSeekBalanceWidget.Tests;

public class AlertEvaluatorTests
{
    private static readonly AppConfig Cfg = new();

    private static ParsedBalance Bal(decimal total) => new("CNY", total, 0, total, true);

    [Fact]
    public void FirstRefresh_EstablishesBaseline_NoAlerts()
    {
        var state = new AlertState(null, null, false, null, null);
        var d = AlertEvaluator.Evaluate(state, Bal(110m), Cfg);
        Assert.False(d.ShowLowBalance);
        Assert.False(d.ShowAbnormalDrop);
        Assert.True(d.NewState.HasBaseline);
        Assert.Equal(110m, d.NewState.LastSuccessfulBalance);
    }

    [Fact]
    public void RestartFirstRefresh_DoesNotReplay()
    {
        var persisted = new AlertState(100m, DateTimeOffset.UtcNow.AddHours(-1), false, null, null);
        var state = persisted with { IsFirstRefreshOfSession = true };
        var d = AlertEvaluator.Evaluate(state, Bal(70m), Cfg); // 大降但不应告警
        Assert.False(d.ShowAbnormalDrop);
        Assert.False(d.ShowLowBalance);
    }

    [Fact]
    public void Rise_NoAbnormalAlert()
    {
        var state = new AlertState(100m, DateTimeOffset.UtcNow, false, null, null);
        var d = AlertEvaluator.Evaluate(state, Bal(110m), Cfg);
        Assert.False(d.ShowAbnormalDrop);
        Assert.True(d.ShowLowBalance == false);
    }

    [Fact]
    public void Drop_OverThreshold_Triggers()
    {
        var state = new AlertState(100m, DateTimeOffset.UtcNow, false, null, null);
        var d = AlertEvaluator.Evaluate(state, Bal(80m), Cfg); // 下降 20%
        Assert.True(d.ShowAbnormalDrop);
    }

    [Fact]
    public void Drop_UnderThreshold_NoAlert()
    {
        var state = new AlertState(100m, DateTimeOffset.UtcNow, false, null, null);
        var d = AlertEvaluator.Evaluate(state, Bal(95m), Cfg); // 下降 5% < 10%
        Assert.False(d.ShowAbnormalDrop);
    }

    [Fact]
    public void LowBalance_EntersState_AlertsOnce()
    {
        var state = new AlertState(100m, DateTimeOffset.UtcNow, false, null, null);
        var d = AlertEvaluator.Evaluate(state, Bal(8m), Cfg); // < 10 阈值
        Assert.True(d.ShowLowBalance);
        Assert.True(d.NewState.InLowBalanceState);
    }

    [Fact]
    public void LowBalance_Cooldown_SuppressesRepeat()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new AlertState(8m, now, true, now, null); // 刚告警过
        var d = AlertEvaluator.Evaluate(state, Bal(7m), Cfg);
        Assert.False(d.ShowLowBalance); // 冷却期内
    }

    [Fact]
    public void LowBalance_Recovery_ResetsState()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new AlertState(8m, now, true, now, null);
        var d = AlertEvaluator.Evaluate(state, Bal(50m), Cfg); // 恢复
        Assert.False(d.ShowLowBalance);
        Assert.False(d.NewState.InLowBalanceState);
    }

    [Fact]
    public void ZeroBaseline_NoPercent()
    {
        var pct = BalanceChangeCalculator.Percent(0m, 5m);
        Assert.Null(pct);
    }

    [Fact]
    public void ChangeCalculator_UpAndDown()
    {
        Assert.Equal(10m, BalanceChangeCalculator.Change(100m, 110m));
        Assert.Equal(-10m, BalanceChangeCalculator.Change(100m, 90m));
        Assert.Null(BalanceChangeCalculator.Change(null, 50m));
    }
}
