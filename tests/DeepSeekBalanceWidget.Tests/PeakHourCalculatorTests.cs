using System;
using System.Collections.Generic;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;
using Xunit;

namespace DeepSeekBalanceWidget.Tests;

public class PeakHourCalculatorTests
{
    private static readonly TimeZoneInfo Beijing =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    /// <summary>把指定北京时间的某小时转为 UTC，模拟"本地时钟就是北京时间"的输入。</summary>
    private static DateTime BeijingHourUtc(int hour)
        => TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 7, 31, hour, 0, 0, DateTimeKind.Unspecified), Beijing);

    [Fact]
    public void Peak_Default_0900_IsPeak() =>
        Assert.True(PeakHourCalculator.IsPeak(BeijingHourUtc(9), null));

    [Fact]
    public void Peak_Default_1159_IsPeak() =>
        Assert.True(PeakHourCalculator.IsPeak(BeijingHourUtc(11), null));

    [Fact]
    public void Peak_Default_1200_NotPeak() =>
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(12), null));

    [Fact]
    public void Peak_Default_1400_IsPeak() =>
        Assert.True(PeakHourCalculator.IsPeak(BeijingHourUtc(14), null));

    [Fact]
    public void Peak_Default_1800_NotPeak() =>
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(18), null));

    [Fact]
    public void Peak_Default_0800_NotPeak() =>
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(8), null));

    [Fact]
    public void Peak_CustomRanges_Respected()
    {
        var ranges = new List<PeakRange> { new(10, 11) };
        Assert.True(PeakHourCalculator.IsPeak(BeijingHourUtc(10), ranges));
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(11), ranges));
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(9), ranges));
    }

    [Fact]
    public void Peak_TimeZoneIndependent_UtcInput()
    {
        // 同一绝对时刻：北京 9:00 == UTC 01:00
        var beijing9 = new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(beijing9, Beijing);
        Assert.Equal(1, utc.Hour); // 北京 UTC+8
        Assert.True(PeakHourCalculator.IsPeak(utc, null));
    }

    [Fact]
    public void Peak_EmptyRanges_FallsBackToDefault()
    {
        Assert.True(PeakHourCalculator.IsPeak(BeijingHourUtc(9), new List<PeakRange>()));
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(13), new List<PeakRange>()));
    }

    [Fact]
    public void Peak_InvalidRanges_FallsBackToDefault()
    {
        var invalid = new List<PeakRange> { new(12, 9) }; // Start > End，逆序非法
        Assert.True(PeakHourCalculator.IsPeak(BeijingHourUtc(9), invalid)); // 回退默认：9 点应为峰
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(13), invalid)); // 13 点应为非峰
    }

    [Fact]
    public void Peak_EndHour24_SupportsMidnightBoundary()
    {
        var ranges = new List<PeakRange> { new(20, 24) }; // 20:00~24:00
        Assert.True(PeakHourCalculator.IsPeak(BeijingHourUtc(23), ranges));
        Assert.False(PeakHourCalculator.IsPeak(BeijingHourUtc(0), ranges)); // 次日 0 点非峰
    }

    [Fact]
    public void NextBoundary_InPeak_ReturnsRangeEnd()
    {
        var at10 = BeijingHourUtc(10); // 高峰内（9-12），距 12:00 边界 120 分钟
        Assert.Equal(120, PeakHourCalculator.MinutesUntilNextBoundary(at10, null));
    }

    [Fact]
    public void NextBoundary_NonPeak_ReturnsNextRangeStart()
    {
        var at13 = BeijingHourUtc(13); // 非高峰（12-14 之间），距 14:00 边界 60 分钟
        Assert.Equal(60, PeakHourCalculator.MinutesUntilNextBoundary(at13, null));
    }
}
