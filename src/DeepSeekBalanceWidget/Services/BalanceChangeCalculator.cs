namespace DeepSeekBalanceWidget.Services;

public static class BalanceChangeCalculator
{
    public static decimal? Change(decimal? previous, decimal current)
        => previous.HasValue ? current - previous.Value : null;

    public static decimal? Percent(decimal? previous, decimal current)
    {
        if (!previous.HasValue || previous.Value == 0) return null;
        return (previous.Value - current) / previous.Value * 100m;
    }
}
