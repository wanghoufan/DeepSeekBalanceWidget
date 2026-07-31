using System;
using DeepSeekBalanceWidget.Models;

namespace DeepSeekBalanceWidget.Services;

public sealed record AlertDecision(
    bool ShowLowBalance,
    bool ShowAbnormalDrop,
    AlertState NewState);

public static class AlertEvaluator
{
    public static AlertDecision Evaluate(AlertState state, ParsedBalance current, AppConfig cfg)
    {
        var now = DateTimeOffset.UtcNow;
        var baseState = state with
        {
            LastSuccessfulBalance = current.Total,
            LastSuccessfulRefreshUtc = now,
            IsFirstRefreshOfSession = false
        };

        if (!state.HasBaseline)
            return new AlertDecision(false, false, baseState);

        if (state.IsFirstRefreshOfSession)
            return new AlertDecision(false, false, baseState);

        bool showLow = false;
        bool showAbnormal = false;
        var prev = state.LastSuccessfulBalance!.Value;

        bool inLow = current.Total < cfg.LowBalanceThreshold;
        if (inLow)
        {
            bool entering = !state.InLowBalanceState;
            bool cooldownElapsed =
                state.LastLowBalanceAlertUtc is null
                || now - state.LastLowBalanceAlertUtc.Value >= TimeSpan.FromSeconds(cfg.LowBalanceCooldownSeconds);
            if (entering || cooldownElapsed) showLow = true;
        }

        if (prev > 0 && current.Total < prev)
        {
            var pct = (prev - current.Total) / prev * 100m;
            if (pct >= cfg.AbnormalChangePercent)
            {
                bool cooldownElapsed =
                    state.LastAbnormalAlertUtc is null
                    || now - state.LastAbnormalAlertUtc.Value >= TimeSpan.FromSeconds(cfg.AbnormalAlertCooldownSeconds);
                if (cooldownElapsed) showAbnormal = true;
            }
        }

        var newState = baseState with
        {
            InLowBalanceState = inLow,
            LastLowBalanceAlertUtc = showLow ? now : state.LastLowBalanceAlertUtc,
            LastAbnormalAlertUtc = showAbnormal ? now : state.LastAbnormalAlertUtc
        };

        return new AlertDecision(showLow, showAbnormal, newState);
    }
}
