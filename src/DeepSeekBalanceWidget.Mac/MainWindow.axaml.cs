using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget;

public partial class MainWindow : Window
{
    private readonly MacConfigService _configService;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _codexTimer;
    private readonly DispatcherTimer _peakTimer;
    private readonly ICodexAccountsUsageProvider _codexProvider = new MacCodexUsageProvider();
    private readonly CancellationTokenSource _cancellation = new();
    private IBalanceProvider _provider;
    private MacMenuBarBalance? _menuBarBalance;
    private ParsedBalance? _latestBalance;
    private string _menuBarBalanceText = "¥ --";
    private string _menuBarBalanceTooltip = "DeepSeek 余额监控：正在读取余额";
    private string _menuBarCodexText = "--";
    private string _menuBarCodexTooltip = "ChatGPT Plus：正在读取额度";
    private string _menuBarPeakText = "谷";
    private decimal? _previousBalance;
    private bool _refreshing;
    private bool _codexRefreshing;

    public MainWindow(MacConfigService configService, AppConfig config, IBalanceProvider provider)
    {
        InitializeComponent();
        _configService = configService;
        _config = config;
        _provider = provider;
        Topmost = _config.IsAlwaysOnTop;

        _refreshTimer = new DispatcherTimer { Interval = RefreshInterval() };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _codexTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _codexTimer.Tick += async (_, _) => await RefreshCodexAsync();
        _peakTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _peakTimer.Tick += (_, _) => RefreshPeakStatus();

        Opened += OnOpened;
        Closing += OnClosing;
        PointerPressed += Window_PointerPressed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        RestorePosition();
        _menuBarBalance = MacMenuBarBalance.Create();
        RefreshMenuBar();
        RefreshPeakStatus();
        _refreshTimer.Start();
        _peakTimer.Start();
        if (_config.EnableCodexMonitoring) _codexTimer.Start();
        _ = RefreshAsync();
        if (_config.EnableCodexMonitoring) _ = RefreshCodexAsync();
    }

    private void RestorePosition()
    {
        if (_config.WindowLeft is double left && _config.WindowTop is double top)
            Position = new PixelPoint((int)Math.Round(left), (int)Math.Round(top));
    }

    private async void Refresh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshAsync();
        if (_config.EnableCodexMonitoring) await RefreshCodexAsync();
    }

    private async void Settings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_configService, _config);
        bool saved = await dialog.ShowDialog<bool>(this);
        if (!saved) return;

        Topmost = _config.IsAlwaysOnTop;
        _refreshTimer.Interval = RefreshInterval();
        if (_provider is DeepSeekApiClient)
            _provider = new DeepSeekApiClient(_configService.GetApiKey() ?? string.Empty);
        _codexTimer.IsEnabled = _config.EnableCodexMonitoring;
        RefreshPeakStatus();
        await RefreshAsync();
        if (_config.EnableCodexMonitoring) await RefreshCodexAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            ErrorText.Text = string.Empty;
            BalanceParseResult parsed = BalanceParser.Parse(await _provider.GetBalanceJsonAsync(_cancellation.Token));
            if (!parsed.Success) throw new InvalidOperationException(parsed.Error);
            var selected = CurrencySelector.Select(parsed.Balances, _config.SelectedCurrency);
            if (!selected.Found || selected.Balance is null)
                throw new InvalidOperationException($"未找到 {_config.SelectedCurrency} 余额");

            var balance = selected.Balance;
            decimal? change = BalanceChangeCalculator.Change(_previousBalance, balance.Total);
            _previousBalance = balance.Total;
            _latestBalance = balance;
            BalanceText.Text = Symbol(balance.Currency) + balance.Total.ToString("0.00");
            ChangeText.Text = change is null ? "首次刷新" : $"变动 {(change >= 0 ? "+" : string.Empty)}{change:0.00}";
            BreakdownText.Text = $"充值 {balance.ToppedUp:0.00} · 赠送 {balance.Granted:0.00}";
            StatusDot.Foreground = balance.IsAvailable ? Brush.Parse("#4CC94C") : Brush.Parse("#E86656");
            StatusText.Text = balance.IsAvailable ? "账户正常" : "账户不可用";
            RefreshTimeText.Text = "上次刷新：" + DateTime.Now.ToString("HH:mm:ss");
            UpdateMenuBar(
                Symbol(balance.Currency) + balance.Total.ToString("0.00"),
                $"DeepSeek 余额：{balance.Total:0.00} {balance.Currency}\n上次更新：{DateTime.Now:HH:mm:ss}");

            _config.LastSuccessfulBalance = balance.Total;
            _config.LastSuccessfulRefreshUtc = DateTimeOffset.UtcNow;
            _configService.Save(_config);
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex) { ShowError(ex.IsAuthFailure ? "认证失败：请在设置中检查 API Key" : ex.Message); }
        catch (Exception ex) { ShowError("刷新失败：" + ex.Message); }
        finally { _refreshing = false; }
    }

    private async Task RefreshCodexAsync()
    {
        if (_codexRefreshing) return;
        _codexRefreshing = true;
        try
        {
            var accounts = await _codexProvider.GetUsagesAsync(_cancellation.Token);
            CodexText.Text = accounts.Count == 0
                ? "未找到可用的 CC Switch 账号"
                : string.Join(Environment.NewLine, accounts.Take(2).Select(FormatAccount));
            _menuBarCodexText = FormatMenuBarCodex(accounts);
            _menuBarCodexTooltip = string.Join(
                Environment.NewLine,
                accounts.Take(2).Select(FormatAccount));
            RefreshMenuBar();
        }
        catch (OperationCanceledException) { }
        catch
        {
            CodexText.Text = "暂时无法读取 ChatGPT Plus 用量";
            _menuBarCodexText = "--";
            _menuBarCodexTooltip = "ChatGPT Plus：暂时无法读取额度";
            RefreshMenuBar();
        }
        finally { _codexRefreshing = false; }
    }

    private static string FormatAccount(CodexAccountUsageSnapshot account)
    {
        if (!account.Usage.IsAvailable || account.Usage.Windows.Count == 0)
            return account.Email + "：" + (account.RefreshError ?? account.Usage.Error ?? "暂不可用");
        return account.Email + "：" + string.Join(
            Environment.NewLine,
            account.Usage.Windows.Select(window =>
                CodexUsageFormatter.FormatWindowRow(window, DateTimeOffset.Now)));
    }

    private static string FormatMenuBarCodex(IReadOnlyList<CodexAccountUsageSnapshot> accounts)
    {
        var windows = accounts
            .Where(account => account.Usage.IsAvailable)
            .SelectMany(account => account.Usage.Windows)
            .Take(2)
            .ToArray();
        return windows.Length == 0
            ? "--"
            : string.Join("/", windows.Select(window => window.RemainingPercent)) + "%";
    }

    private void RefreshPeakStatus()
    {
        bool isPeak = PeakHourCalculator.IsPeak(DateTime.Now, _config.PeakHourRanges);
        PeakText.IsVisible = _config.ShowPeakIndicator;
        PeakText.Text = isPeak ? "● 预计高峰时段" : "● 预计非高峰时段";
        PeakText.Foreground = Brush.Parse(isPeak ? "#F27D72" : "#78D79A");
        _menuBarPeakText = isPeak ? "峰" : "谷";
        RefreshMenuBar();
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Exit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Hide();

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason is not WindowCloseReason.ApplicationShutdown
            and not WindowCloseReason.OSShutdown)
        {
            e.Cancel = true;
            SaveWindowPosition();
            Hide();
            return;
        }

        _refreshTimer.Stop();
        _codexTimer.Stop();
        _peakTimer.Stop();
        _cancellation.Cancel();
        SaveWindowPosition();
        _cancellation.Dispose();
        _menuBarBalance?.Dispose();
        _menuBarBalance = null;
        if (_codexProvider is IDisposable disposable) disposable.Dispose();
    }

    private void SaveWindowPosition()
    {
        _config.WindowLeft = Position.X;
        _config.WindowTop = Position.Y;
        _configService.Save(_config);
    }

    private void ShowError(string message)
    {
        StatusDot.Foreground = Brush.Parse("#E86656");
        StatusText.Text = "需要注意";
        ErrorText.Text = message;
        if (_latestBalance is { } balance)
        {
            UpdateMenuBar(
                Symbol(balance.Currency) + balance.Total.ToString("0.00") + " !",
                $"DeepSeek 余额：{balance.Total:0.00} {balance.Currency}\n刷新失败：{message}");
        }
        else
        {
            UpdateMenuBar("¥ --", "DeepSeek 余额读取失败：" + message);
        }
    }

    private void UpdateMenuBar(string title, string tooltip)
    {
        _menuBarBalanceText = title;
        _menuBarBalanceTooltip = tooltip;
        RefreshMenuBar();
    }

    private void RefreshMenuBar()
    {
        var titleParts = new List<string> { _menuBarBalanceText };
        if (_config.ShowPeakIndicator) titleParts.Add(_menuBarPeakText);
        titleParts.Add(_menuBarCodexText);
        _menuBarBalance?.Update(
            string.Join(" · ", titleParts),
            _menuBarBalanceTooltip + Environment.NewLine +
            $"DeepSeek 时段：{(_menuBarPeakText == "峰" ? "预计高峰时段" : "预计非高峰时段")}（北京时间）" +
            Environment.NewLine + _menuBarCodexTooltip);
    }

    private TimeSpan RefreshInterval() => TimeSpan.FromSeconds(Math.Clamp(_config.RefreshIntervalSeconds, 5, 3600));
    private static string Symbol(string currency) => currency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? "$" : "¥";
}
