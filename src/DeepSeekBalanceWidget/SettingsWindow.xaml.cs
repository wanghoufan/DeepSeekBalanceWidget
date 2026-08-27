using System.Collections.Generic;
using System.Windows;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppConfig _cfg;
    private readonly List<string> _agentOrder = new();

    public SettingsWindow(ConfigService configService, AppConfig cfg)
    {
        InitializeComponent();
        _configService = configService;
        _cfg = cfg;

        _agentOrder.AddRange(cfg.AgentOrder is { Count: > 0 }
            ? cfg.AgentOrder
            : new List<string> { "deepseek", "chatgpt", "workbuddy" });
        RefreshAgentOrderList();

        ApiKeyBox.Password = configService.GetApiKey() ?? "";
        IntervalBox.Text = cfg.RefreshIntervalSeconds.ToString();
        ThresholdBox.Text = cfg.LowBalanceThreshold.ToString("0.##");
        ChangePercentBox.Text = cfg.AbnormalChangePercent.ToString("0.##");
        CurrencyBox.SelectedIndex = cfg.SelectedCurrency.Equals("USD", System.StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        TopmostCheck.IsChecked = cfg.IsAlwaysOnTop;
        EdgeAutoHideCheck.IsChecked = cfg.EnableEdgeAutoHide;
        MiniModeCheck.IsChecked = cfg.UseMiniMode;
        EnableCodexCheck.IsChecked = cfg.EnableCodexMonitoring;
        CodexFontSizeBox.Text = System.Math.Clamp(cfg.CodexFontSize, 10, 24).ToString("0.#");
        CodexFontStyleBox.SelectedIndex = cfg.CodexFontStyle switch
        {
            "Regular" => 1,
            "Bold" => 2,
            _ => 0
        };
        MockCheck.IsChecked = cfg.UseMockData;
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled();

        // 高峰区间（配置不足两段时回退官方默认）
        var ranges = cfg.PeakHourRanges.Count >= 2
            ? cfg.PeakHourRanges
            : new List<PeakRange> { new(9, 12), new(14, 18) };
        Peak1StartBox.Text = ranges[0].StartHour.ToString();
        Peak1EndBox.Text = ranges[0].EndHour.ToString();
        Peak2StartBox.Text = ranges[1].StartHour.ToString();
        Peak2EndBox.Text = ranges[1].EndHour.ToString();
        ShowPeakCheck.IsChecked = cfg.ShowPeakIndicator;
        CornerBox.SelectedIndex = cfg.DefaultCorner switch
        {
            "BottomRight" => 1,
            "BottomLeft" => 2,
            _ => 0
        };
    }

    private void ClearKey_Click(object sender, RoutedEventArgs e)
    {
        ApiKeyBox.Password = "";
        _configService.SetApiKey(_cfg, null);
    }

    private static string AgentDisplayName(string kind) => kind switch
    {
        "deepseek" => "DeepSeek 余额",
        "chatgpt" => "ChatGPT 额度",
        "workbuddy" => "WorkBuddy",
        "te" => "tE 积分",
        _ => kind
    };

    private void RefreshAgentOrderList()
    {
        AgentOrderBox.ItemsSource = _agentOrder.Select(AgentDisplayName).ToList();
    }

    private void AgentOrderUp_Click(object sender, RoutedEventArgs e)
    {
        int index = AgentOrderBox.SelectedIndex;
        if (index <= 0) return;
        (_agentOrder[index - 1], _agentOrder[index]) = (_agentOrder[index], _agentOrder[index - 1]);
        RefreshAgentOrderList();
        AgentOrderBox.SelectedIndex = index - 1;
    }

    private void AgentOrderDown_Click(object sender, RoutedEventArgs e)
    {
        int index = AgentOrderBox.SelectedIndex;
        if (index < 0 || index >= _agentOrder.Count - 1) return;
        (_agentOrder[index + 1], _agentOrder[index]) = (_agentOrder[index], _agentOrder[index + 1]);
        RefreshAgentOrderList();
        AgentOrderBox.SelectedIndex = index + 1;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalBox.Text, out int interval) || interval < 5 || interval > 3600)
        { System.Windows.MessageBox.Show("刷新间隔需在 5-3600 之间"); return; }
        if (!decimal.TryParse(ThresholdBox.Text, out decimal threshold) || threshold < 0)
        { System.Windows.MessageBox.Show("阈值不能为负"); return; }
        if (!decimal.TryParse(ChangePercentBox.Text, out decimal pct) || pct < 0 || pct > 100)
        { System.Windows.MessageBox.Show("异常百分比需在 0-100 之间"); return; }
        if (!double.TryParse(CodexFontSizeBox.Text, out double codexFontSize) ||
            codexFontSize < 10 || codexFontSize > 24)
        { System.Windows.MessageBox.Show("额度文字大小需在 10-24 之间"); return; }

        // 高峰区间校验：整数小时，Start 0-23、End 1-24（半开区间支持 24），Start < End
        if (!TryParseHour(Peak1StartBox.Text, out int p1s) || p1s < 0 || p1s > 23 ||
            !TryParseHour(Peak1EndBox.Text, out int p1e) || p1e < 1 || p1e > 24 || p1s >= p1e ||
            !TryParseHour(Peak2StartBox.Text, out int p2s) || p2s < 0 || p2s > 23 ||
            !TryParseHour(Peak2EndBox.Text, out int p2e) || p2e < 1 || p2e > 24 || p2s >= p2e)
        {
            System.Windows.MessageBox.Show("高峰时段需为整数小时，且每段开始须小于结束（如 9-12、14-18）");
            return;
        }

        // 仅当输入框有内容时才更新 Key；留空保留原 Key（显式清除请用「清除 Key」按钮），
        // 避免"空框保存"静默抹掉已存 Key 导致每次都要重新输入。
        if (!string.IsNullOrEmpty(ApiKeyBox.Password))
            _configService.SetApiKey(_cfg, ApiKeyBox.Password);

        _cfg.RefreshIntervalSeconds = interval;
        _cfg.LowBalanceThreshold = threshold;
        _cfg.AbnormalChangePercent = pct;
        _cfg.SelectedCurrency = (CurrencyBox.SelectedIndex == 1) ? "USD" : "CNY";
        _cfg.IsAlwaysOnTop = TopmostCheck.IsChecked == true;
        _cfg.EnableEdgeAutoHide = EdgeAutoHideCheck.IsChecked == true;
        _cfg.UseMiniMode = MiniModeCheck.IsChecked == true;
        _cfg.EnableCodexMonitoring = EnableCodexCheck.IsChecked == true;
        _cfg.CodexFontSize = codexFontSize;
        _cfg.CodexFontStyle = (CodexFontStyleBox.SelectedItem as System.Windows.Controls.ComboBoxItem)
            ?.Tag?.ToString() ?? "DeepSeek";
        _cfg.UseMockData = MockCheck.IsChecked == true;
        AutoStartService.Set(AutoStartCheck.IsChecked == true);

        _cfg.PeakHourRanges = new List<PeakRange> { new(p1s, p1e), new(p2s, p2e) };
        _cfg.ShowPeakIndicator = ShowPeakCheck.IsChecked == true;
        _cfg.DefaultCorner = (CornerBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "Remember";
        _cfg.AgentOrder = new List<string>(_agentOrder);

        _configService.Save(_cfg);
        DialogResult = true;
        Close();
    }

    private static bool TryParseHour(string s, out int h) => int.TryParse(s, out h);
}
