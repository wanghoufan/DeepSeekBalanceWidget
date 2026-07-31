namespace DeepSeekBalanceWidget.Models;

/// <summary>高峰时段区间，半开区间 [StartHour, EndHour)。EndHour 支持 24（表达次日 0 点），不支持跨午夜（Start &lt; End）。</summary>
public sealed record PeakRange(int StartHour, int EndHour)
{
    /// <summary>判断给定小时是否落在本区间内。</summary>
    public bool Contains(int hour) => hour >= StartHour && hour < EndHour;
}
