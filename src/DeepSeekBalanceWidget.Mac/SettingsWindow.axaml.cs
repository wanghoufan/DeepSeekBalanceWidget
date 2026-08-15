using Avalonia.Controls;
using Avalonia.Interactivity;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget;

public partial class SettingsWindow : Window
{
    private readonly MacConfigService _configService;
    private readonly AppConfig _config;
    private bool _clearKey;

    public SettingsWindow(MacConfigService configService, AppConfig config)
    {
        InitializeComponent();
        _configService = configService;
        _config = config;

        ApiKeyBox.Text = configService.GetApiKey() ?? string.Empty;
        IntervalBox.Text = config.RefreshIntervalSeconds.ToString();
        ThresholdBox.Text = config.LowBalanceThreshold.ToString("0.##");
        ChangePercentBox.Text = config.AbnormalChangePercent.ToString("0.##");
        CurrencyBox.SelectedIndex = config.SelectedCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        TopmostCheck.IsChecked = config.IsAlwaysOnTop;
        EnableCodexCheck.IsChecked = config.EnableCodexMonitoring;
        ShowPeakCheck.IsChecked = config.ShowPeakIndicator;
        MockCheck.IsChecked = config.UseMockData;
        AutoStartCheck.IsChecked = MacAutoStartService.IsEnabled();
    }

    private void ClearKey_Click(object? sender, RoutedEventArgs e)
    {
        ApiKeyBox.Text = string.Empty;
        _clearKey = true;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalBox.Text, out int interval) || interval is < 5 or > 3600)
        {
            ShowError("刷新间隔需在 5–3600 秒之间。");
            return;
        }
        if (!decimal.TryParse(ThresholdBox.Text, out decimal threshold) || threshold < 0)
        {
            ShowError("低余额阈值不能为负数。");
            return;
        }
        if (!decimal.TryParse(ChangePercentBox.Text, out decimal percentage) || percentage is < 0 or > 100)
        {
            ShowError("异常下降百分比需在 0–100 之间。");
            return;
        }

        try
        {
            if (_clearKey)
                _configService.SetApiKey(_config, null);
            else if (!string.IsNullOrWhiteSpace(ApiKeyBox.Text))
                _configService.SetApiKey(_config, ApiKeyBox.Text);

            _config.RefreshIntervalSeconds = interval;
            _config.LowBalanceThreshold = threshold;
            _config.AbnormalChangePercent = percentage;
            _config.SelectedCurrency = CurrencyBox.SelectedIndex == 1 ? "USD" : "CNY";
            _config.IsAlwaysOnTop = TopmostCheck.IsChecked == true;
            _config.EnableCodexMonitoring = EnableCodexCheck.IsChecked == true;
            _config.ShowPeakIndicator = ShowPeakCheck.IsChecked == true;
            _config.UseMockData = MockCheck.IsChecked == true;
            _config.AutoStart = AutoStartCheck.IsChecked == true;
            MacAutoStartService.Set(_config.AutoStart);
            _configService.Save(_config);
            Close(true);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void ShowError(string message) => ErrorText.Text = message;
}
