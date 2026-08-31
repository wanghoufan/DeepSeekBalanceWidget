namespace DeepSeekBalanceWidget.Services;

public enum ConsumptionAlertLevel
{
    Normal,
    Warning,
    Critical
}

public sealed record ConsumptionRateResult(
    ConsumptionAlertLevel Level,
    int FiveMinuteConsumption,
    int OneMinuteConsumption);

public sealed class CodexConsumptionRateTracker
{
    private static readonly TimeSpan FiveMinuteWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OneMinuteWindow = TimeSpan.FromSeconds(75);
    private readonly Dictionary<string, List<UsageSample>> _samplesByAccount = new();

    public ConsumptionRateResult Observe(
        string accountId,
        int remainingPercent,
        DateTimeOffset observedAt)
    {
        if (!_samplesByAccount.TryGetValue(accountId, out var samples))
        {
            samples = new List<UsageSample>();
            _samplesByAccount[accountId] = samples;
        }

        if (samples.Count > 0 && remainingPercent > samples[^1].RemainingPercent)
            samples.Clear();

        samples.RemoveAll(sample => observedAt - sample.ObservedAt > FiveMinuteWindow);
        samples.Add(new UsageSample(observedAt, remainingPercent));

        int fiveMinuteConsumption = Math.Max(0, samples[0].RemainingPercent - remainingPercent);
        UsageSample? oneMinuteBaseline = samples
            .Where(sample => sample.ObservedAt < observedAt
                && observedAt - sample.ObservedAt <= OneMinuteWindow)
            .FirstOrDefault();
        int oneMinuteConsumption = oneMinuteBaseline is null
            ? 0
            : Math.Max(0, oneMinuteBaseline.RemainingPercent - remainingPercent);

        ConsumptionAlertLevel level = fiveMinuteConsumption >= 5 || oneMinuteConsumption >= 3
            ? ConsumptionAlertLevel.Critical
            : fiveMinuteConsumption >= 2
                ? ConsumptionAlertLevel.Warning
                : ConsumptionAlertLevel.Normal;

        return new ConsumptionRateResult(level, fiveMinuteConsumption, oneMinuteConsumption);
    }

    private sealed record UsageSample(DateTimeOffset ObservedAt, int RemainingPercent);
}
