using System;
using System.Collections.Generic;
using System.Linq;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// 高峰时段判断（参考 DeepSeek 官方峰谷计价策略，2026-08-23 更新）。
/// 工作日：北京时间 9:00~12:00、14:00~18:00 为高峰；其余时段及周末全天均为非高峰（低谷）。
/// 统一以北京时间（UTC+8）判断；输入时间（Kind=Utc 或 Local）内部转换到北京时间。
/// </summary>
public static class PeakHourCalculator
{
    /// <summary>官方默认区间（半开 [Start, End)，支持 End=24 表达次日 0 点）。周末全天非高峰。</summary>
    public static readonly IReadOnlyList<PeakRange> DefaultRanges = new[]
    {
        new PeakRange(9, 12, WeekdaysOnly: true),
        new PeakRange(14, 18, WeekdaysOnly: true)
    };

    private static readonly TimeZoneInfo BeijingTz =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    /// <summary>把给定时间转换到北京时间（输入 Kind=Utc 则按 UTC 转换，Local/Unspecified 按本地转换）。</summary>
    public static DateTime ToBeijing(DateTime localNow)
        => TimeZoneInfo.ConvertTime(localNow, BeijingTz);

    /// <summary>判断给定时间是否处于高峰（北京时间视角）。区间为空/非法时回退官方默认。</summary>
    public static bool IsPeak(DateTime localNow, IReadOnlyList<PeakRange>? ranges)
    {
        var list = Normalize(ranges);
        var bj = ToBeijing(localNow);
        return list.Any(r => r.Contains(bj.Hour, bj.DayOfWeek));
    }

    /// <summary>距下一个高峰状态边界的分钟数（1~1440），供一次性定时器精确对齐。</summary>
    public static int MinutesUntilNextBoundary(DateTime localNow, IReadOnlyList<PeakRange>? ranges)
    {
        var list = Normalize(ranges);
        var bj = ToBeijing(localNow);
        bool cur = IsPeakAt(bj, list);
        for (int m = 1; m <= 24 * 60; m++)
        {
            if (IsPeakAt(bj.AddMinutes(m), list) != cur) return m;
        }
        return 24 * 60;
    }

    private static bool IsPeakAt(DateTime bj, IReadOnlyList<PeakRange> ranges)
        => ranges.Any(r => r.Contains(bj.Hour, bj.DayOfWeek));

    /// <summary>校验区间列表：非空、每段 Start 0-23、End 1-24、Start&lt;End。非法返回官方默认。</summary>
    private static IReadOnlyList<PeakRange> Normalize(IReadOnlyList<PeakRange>? ranges)
    {
        if (ranges is not { Count: > 0 }) return DefaultRanges;
        foreach (var r in ranges)
        {
            if (r.StartHour < 0 || r.StartHour > 23) return DefaultRanges;
            if (r.EndHour < 1 || r.EndHour > 24) return DefaultRanges;
            if (r.StartHour >= r.EndHour) return DefaultRanges; // 不支持跨午夜
        }
        return ranges;
    }
}
