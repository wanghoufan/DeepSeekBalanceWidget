using System;

namespace DeepSeekBalanceWidget.Models;

public sealed class AppConfig
{
    public int ConfigVersion { get; set; } = 1;
    public string? ApiKeyEncrypted { get; set; }
    public string SelectedCurrency { get; set; } = "CNY";
    public int RefreshIntervalSeconds { get; set; } = 30;
    public decimal LowBalanceThreshold { get; set; } = 10m;
    public decimal AbnormalChangePercent { get; set; } = 10m;
    public int LowBalanceCooldownSeconds { get; set; } = 1800;
    public int AbnormalAlertCooldownSeconds { get; set; } = 600;
    public bool ShowToastNotifications { get; set; } = true;
    public bool IsAlwaysOnTop { get; set; } = true;
    public bool EnableEdgeAutoHide { get; set; }
    public bool UseMockData { get; set; }
    public bool UseMiniMode { get; set; }
    public bool EnableCodexMonitoring { get; set; } = true;
    public double CodexFontSize { get; set; } = 14;
    public string CodexFontStyle { get; set; } = "DeepSeek"; // DeepSeek / Regular / Bold
    /// <summary>胶囊区块渲染顺序：deepseek / chatgpt / workbuddy / te（未来），可在设置中调整。</summary>
    public List<string> AgentOrder { get; set; } = new() { "deepseek", "chatgpt", "workbuddy" };
    public string DefaultCorner { get; set; } = "Remember"; // Remember / BottomRight / BottomLeft
    public bool ShowPeakIndicator { get; set; } = true;
    public List<PeakRange> PeakHourRanges { get; set; } = new()
    {
        new PeakRange(9, 12),
        new PeakRange(14, 18)
    };
    public bool AutoStart { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public decimal? LastSuccessfulBalance { get; set; }
    public DateTimeOffset? LastSuccessfulRefreshUtc { get; set; }
    public bool InLowBalanceState { get; set; }
}
