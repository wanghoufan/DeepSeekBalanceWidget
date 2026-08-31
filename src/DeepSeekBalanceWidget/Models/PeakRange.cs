namespace DeepSeekBalanceWidget.Models;

/// <summary>
/// 高峰时段区间，半开区间 [StartHour, EndHour)。
/// EndHour 支持 24（表达次日 0 点），不支持跨午夜（Start &lt; End）。
/// WeekdaysOnly=true 时仅工作日（周一至周五）生效，周末全天视为非高峰。
/// </summary>
public sealed record PeakRange(int StartHour, int EndHour, bool WeekdaysOnly = false)
{
    /// <summary>判断给定小时和星期是否落在本区间内。</summary>
    public bool Contains(int hour, DayOfWeek day)
    {
        if (WeekdaysOnly && (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday))
            return false;
        return hour >= StartHour && hour < EndHour;
    }
}
