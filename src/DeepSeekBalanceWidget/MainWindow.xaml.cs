using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using System.Windows.Threading;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace DeepSeekBalanceWidget;

public partial class MainWindow : Window
{
    private const double EdgeDetectionThreshold = 16;
    private const double EdgeRevealThickness = 12;

    private readonly ConfigService _configService;
    private readonly AppConfig _cfg;
    private readonly CancellationTokenSource _cts = new();
    private IBalanceProvider _provider;
    private readonly ICodexUsageProvider _codexUsageProvider;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _codexTimer;
    private readonly DispatcherTimer _savePosTimer;
    private readonly DispatcherTimer _peakTimer;
    private readonly DispatcherTimer _autoHideTimer;
    private AlertState _alertState;
    private bool _isRefreshing;
    private bool _isAuthPaused;
    private bool _isExiting;
    private bool _isMini;
    private bool _isPeak;
    private bool _isDragging;
    private bool _isEdgeHidden;
    private bool _isSettingsOpen;
    private bool _isChangingDockPosition;
    private DockEdge _dockEdge;

    public event Action? RequestExit;
    public event Action<string, bool?>? TrayStatusChanged;

    public MainWindow(ConfigService configService, AppConfig cfg, IBalanceProvider provider)
    {
        InitializeComponent();
        _configService = configService;
        _cfg = cfg;
        _provider = provider;
        _codexUsageProvider = new CodexAppServerClient();

        _savePosTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _savePosTimer.Tick += (_, _) => { _savePosTimer.Stop(); SavePosition(); };

        _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _autoHideTimer.Tick += AutoHideTimer_Tick;

        Topmost = _cfg.IsAlwaysOnTop;
        UpdatePinButton();
        ApplySavedPosition();
        ApplyMiniMode(_cfg.UseMiniMode);
        ApplyCodexAppearance();
        ApplyCodexVisibility();
        Loaded += (_, _) => EvaluateEdgeAutoHide();

        _alertState = new AlertState(
            _cfg.LastSuccessfulBalance,
            _cfg.LastSuccessfulRefreshUtc,
            _cfg.InLowBalanceState,
            null, null)
        { IsFirstRefreshOfSession = true };

        _timer = new DispatcherTimer
        { Interval = TimeSpan.FromSeconds(Math.Clamp(_cfg.RefreshIntervalSeconds, 5, 3600)) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _codexTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _codexTimer.Tick += async (_, _) => await RefreshCodexUsageAsync();
        if (_cfg.EnableCodexMonitoring) _codexTimer.Start();

        _peakTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _peakTimer.Tick += (_, _) => RefreshPeakStatus();
        _peakTimer.Start();

        RefreshPeakStatus();
        _ = RefreshAsync();
        if (_cfg.EnableCodexMonitoring) _ = RefreshCodexUsageAsync();
    }

    private void ApplySavedPosition()
    {
        bool hasSaved = _cfg.WindowLeft is double && _cfg.WindowTop is double;
        if (hasSaved)
        {
            Left = _cfg.WindowLeft!.Value;
            Top = _cfg.WindowTop!.Value;
        }
        else
        {
            ApplyDefaultCorner();
        }
        if (IsOffScreen()) ApplyDefaultCorner();
    }

    private void ApplyDefaultCorner()
    {
        var wa = SystemParameters.WorkArea;
        switch (_cfg.DefaultCorner)
        {
            case "BottomLeft":
                Left = wa.Left + 20;
                Top = wa.Bottom - Height - 20;
                break;
            case "BottomRight":
                Left = wa.Right - Width - 20;
                Top = wa.Bottom - Height - 20;
                break;
            default: // Remember：无历史坐标时回退右下角
                Left = wa.Right - Width - 20;
                Top = wa.Bottom - Height - 20;
                break;
        }
    }

    private bool IsOffScreen()
    {
        var wa = SystemParameters.WorkArea;
        return (Left + Width < wa.Left) || (Left > wa.Right)
            || (Top + Height < wa.Top) || (Top > wa.Bottom);
    }

    private void ClampToWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, wa.Left, Math.Max(wa.Left, wa.Right - Width));
        Top = Math.Clamp(Top, wa.Top, Math.Max(wa.Top, wa.Bottom - Height));
    }

    public void ResetPosition()
    {
        DisableCurrentDock();
        ApplyDefaultCorner();
        SavePosition();
    }

    private void SavePosition()
    {
        if (double.IsNaN(Left) || double.IsNaN(Top)) return;
        var position = _dockEdge == DockEdge.None
            ? new Point(Left, Top)
            : GetDockPosition(hidden: false);
        _cfg.WindowLeft = position.X;
        _cfg.WindowTop = position.Y;
        _configService.Save(_cfg);
    }

    private void ApplyMiniMode(bool mini)
    {
        _isMini = mini;
        Card.Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
        MiniCard.Visibility = mini ? Visibility.Visible : Visibility.Collapsed;
        Width = mini ? GetMiniModeWidth() : 236;
        if (IsLoaded)
        {
            if (_dockEdge != DockEdge.None)
                SetDockPosition(_isEdgeHidden);
            else
                ClampToWorkArea(); // 尺寸变化后只做边界 Clamp，不强制回角落
        }
    }

    private double GetMiniModeWidth()
    {
        if (!_cfg.EnableCodexMonitoring) return 156;
        double extra = Math.Max(0, Math.Clamp(_cfg.CodexFontSize, 10, 24) - 14) * 6;
        return 205 + extra;
    }

    private void MiniBtn_Click(object sender, RoutedEventArgs e)
    {
        ApplyMiniMode(!_isMini);
        _cfg.UseMiniMode = _isMini;
        _configService.Save(_cfg);
    }

    private void MiniCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        e.Handled = true;

        if (e.ClickCount >= 2)
        {
            ApplyMiniMode(false);
            _cfg.UseMiniMode = false;
            _configService.Save(_cfg);
            return;
        }

        DragWindow();
    }

    private void UpdatePinButton()
    {
        // 置顶按钮高亮表示当前置顶
        PinBtn.Foreground = Topmost
            ? new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7))
            : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
        PinBtn.ToolTip = Topmost ? "取消置顶" : "始终置顶";
    }

    private void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _cfg.IsAlwaysOnTop = Topmost;
        _configService.Save(_cfg);
        UpdatePinButton();
    }

    private void TrayBtn_Click(object sender, RoutedEventArgs e)
    {
        SavePosition();
        Hide(); // 收进托盘：隐藏常驻
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(this,
            "确定退出 DeepSeek 余额监控吗？", "退出确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes) ExitApp();
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        if (_isChangingDockPosition) return;
        _savePosTimer.Stop();
        _savePosTimer.Start();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        DragWindow();
    }

    private void DragWindow()
    {
        _autoHideTimer.Stop();
        _isDragging = true;
        _isEdgeHidden = false;
        _dockEdge = DockEdge.None;
        try { DragMove(); } catch (InvalidOperationException) { }
        finally { _isDragging = false; }

        EvaluateEdgeAutoHide();
        SavePosition();
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _autoHideTimer.Stop();
        if (_isEdgeHidden) SetDockPosition(hidden: false);
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_cfg.EnableEdgeAutoHide && _dockEdge != DockEdge.None && !_isDragging)
            _autoHideTimer.Start();
    }

    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        if (IsMouseOver)
        {
            _autoHideTimer.Stop();
            return;
        }
        if (_isDragging || _isSettingsOpen || ContextMenu?.IsOpen == true) return;

        _autoHideTimer.Stop();
        if (_cfg.EnableEdgeAutoHide && _dockEdge != DockEdge.None)
            SetDockPosition(hidden: true);
    }

    private void EvaluateEdgeAutoHide()
    {
        if (!_cfg.EnableEdgeAutoHide || _isDragging || !IsLoaded)
        {
            if (!_cfg.EnableEdgeAutoHide) DisableCurrentDock();
            return;
        }

        var window = CurrentWindowRect();
        var edge = EdgeAutoHideCalculator.Detect(
            window, SystemParameters.WorkArea, EdgeDetectionThreshold);
        if (edge == DockEdge.None) return;

        _dockEdge = edge;
        SetDockPosition(hidden: true);
        SavePosition();
    }

    private Rect CurrentWindowRect()
    {
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : Height;
        return new Rect(Left, Top, width, height);
    }

    private Point GetDockPosition(bool hidden)
    {
        var window = CurrentWindowRect();
        return hidden
            ? EdgeAutoHideCalculator.HiddenPosition(
                _dockEdge, window, SystemParameters.WorkArea, EdgeRevealThickness)
            : EdgeAutoHideCalculator.VisiblePosition(
                _dockEdge, window, SystemParameters.WorkArea);
    }

    private void SetDockPosition(bool hidden)
    {
        if (_dockEdge == DockEdge.None) return;
        var position = GetDockPosition(hidden);
        _isChangingDockPosition = true;
        try
        {
            Left = position.X;
            Top = position.Y;
            _isEdgeHidden = hidden;
        }
        finally { _isChangingDockPosition = false; }
    }

    private void DisableCurrentDock()
    {
        _autoHideTimer.Stop();
        if (_dockEdge != DockEdge.None && _isEdgeHidden)
            SetDockPosition(hidden: false);
        _isEdgeHidden = false;
        _dockEdge = DockEdge.None;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void RefreshMenu_Click(object sender, RoutedEventArgs e) => _ = RefreshNowAsync();
    private void SettingsMenu_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void ResetPositionMenu_Click(object sender, RoutedEventArgs e) => ResetPosition();
    private void ExitMenu_Click(object sender, RoutedEventArgs e) => ExitApp();

    public void RefreshNow() => _ = RefreshNowAsync();

    private async Task RefreshNowAsync()
    {
        _isAuthPaused = false;
        _timer.Start();
        var codexRefresh = _cfg.EnableCodexMonitoring
            ? RefreshCodexUsageAsync()
            : Task.CompletedTask;
        await Task.WhenAll(RefreshAsync(), codexRefresh);
    }

    public void OpenSettings()
    {
        if (_isEdgeHidden) SetDockPosition(hidden: false);
        var dlg = new SettingsWindow(_configService, _cfg) { Owner = this };
        _isSettingsOpen = true;
        bool saved;
        try { saved = dlg.ShowDialog() == true; }
        finally { _isSettingsOpen = false; }
        if (saved)
        {
            Topmost = _cfg.IsAlwaysOnTop;
            UpdatePinButton();
            ApplyMiniMode(_cfg.UseMiniMode);
            ApplyCodexAppearance();
            ApplyCodexMonitoring();
            _timer.Interval = TimeSpan.FromSeconds(Math.Clamp(_cfg.RefreshIntervalSeconds, 5, 3600));
            if (_provider is DeepSeekApiClient) RebuildProvider();
            _isAuthPaused = false;
            _timer.Start();
            RefreshPeakStatus(); // 设置变更后立即刷新高峰状态
            _ = RefreshAsync();
            EvaluateEdgeAutoHide();
        }
        else if (_cfg.EnableEdgeAutoHide && _dockEdge != DockEdge.None)
            _autoHideTimer.Start();
    }

    public void RestoreAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        if (_isEdgeHidden) SetDockPosition(hidden: false);
        Activate();
    }

    private void RebuildProvider()
    {
        _provider = new DeepSeekApiClient(_configService.GetApiKey() ?? string.Empty);
    }

    public void ExitApp()
    {
        if (_isExiting) return;
        _isExiting = true;
        SavePosition();
        RequestExit?.Invoke();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            SavePosition();
            Hide();
        }
        base.OnClosing(e);
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing || _isAuthPaused) return;
        _isRefreshing = true;
        try
        {
            var json = await _provider.GetBalanceJsonAsync(_cts.Token);
            var parsed = BalanceParser.Parse(json);
            if (!parsed.Success)
            {
                ShowError(parsed.Error!);
                RaiseTrayError();
                return;
            }
            var selection = CurrencySelector.Select(parsed.Balances, _cfg.SelectedCurrency);
            if (!selection.Found)
            {
                ShowUnavailableCurrency(selection.SelectedCurrency);
                RaiseTrayError();
                return;
            }
            var bal = selection.Balance!;

            decimal? prev = _cfg.LastSuccessfulBalance;
            decimal? change = BalanceChangeCalculator.Change(prev, bal.Total);
            decimal? pct = BalanceChangeCalculator.Percent(prev, bal.Total);

            ApplyBalance(bal, parsed.IsConsistent, change, pct);
            RaiseTrayStatus(bal, change);

            var decision = AlertEvaluator.Evaluate(_alertState, bal, _cfg);
            _alertState = decision.NewState;

            _cfg.LastSuccessfulBalance = decision.NewState.LastSuccessfulBalance;
            _cfg.LastSuccessfulRefreshUtc = decision.NewState.LastSuccessfulRefreshUtc;
            _cfg.InLowBalanceState = decision.NewState.InLowBalanceState;
            _configService.Save(_cfg);

            if (decision.ShowLowBalance)
                ToastService.Show(this, "低余额提醒", $"余额 {bal.Total:0.00} {bal.Currency} 低于阈值 {_cfg.LowBalanceThreshold:0.00}");
            if (decision.ShowAbnormalDrop)
                ToastService.Show(this, "余额异常下降", $"单次下降 {Math.Abs(pct ?? 0):0.0}%");
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex)
        {
            if (ex.IsAuthFailure)
            {
                _isAuthPaused = true;
                _timer.Stop();
                ShowError("认证失败：请检查 API Key");
            }
            else ShowError("刷新失败：" + ex.Message);
            RaiseTrayError();
        }
        catch (Exception ex)
        {
            ShowError("刷新失败：" + ex.Message);
            RaiseTrayError();
        }
        finally { _isRefreshing = false; }
    }

    private bool _isCodexRefreshing;

    private async Task RefreshCodexUsageAsync()
    {
        if (!_cfg.EnableCodexMonitoring || _isCodexRefreshing) return;
        _isCodexRefreshing = true;
        try
        {
            var usage = await _codexUsageProvider.GetUsageAsync(_cts.Token);
            ApplyCodexUsage(usage);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            ApplyCodexUsage(CodexUsageSnapshot.Unavailable("Codex 用量读取失败"));
        }
        finally { _isCodexRefreshing = false; }
    }

    private void ApplyCodexMonitoring()
    {
        ApplyCodexVisibility();
        ApplyMiniMode(_isMini);
        if (_cfg.EnableCodexMonitoring)
        {
            _codexTimer.Start();
            _ = RefreshCodexUsageAsync();
        }
        else
        {
            _codexTimer.Stop();
        }
    }

    private void ApplyCodexVisibility()
    {
        var visibility = _cfg.EnableCodexMonitoring ? Visibility.Visible : Visibility.Collapsed;
        CodexPanel.Visibility = visibility;
        MiniCodexText.Visibility = visibility;
    }

    private void ApplyCodexAppearance()
    {
        double size = Math.Clamp(_cfg.CodexFontSize, 10, 24);
        FontWeight weight = _cfg.CodexFontStyle switch
        {
            "Regular" => FontWeights.Normal,
            "Bold" => FontWeights.Bold,
            _ => FontWeights.SemiBold
        };

        CodexUsageText.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        CodexResetText.FontFamily = CodexUsageText.FontFamily;
        MiniCodexText.FontFamily = CodexUsageText.FontFamily;
        CodexUsageText.FontSize = size;
        CodexResetText.FontSize = Math.Max(10, size - 2);
        MiniCodexText.FontSize = Math.Max(11, size - 1);
        CodexUsageText.FontWeight = weight;
        CodexResetText.FontWeight = weight;
        MiniCodexText.FontWeight = weight;
    }

    private void ApplyCodexUsage(CodexUsageSnapshot usage)
    {
        if (!usage.IsAvailable || usage.Windows.Count == 0)
        {
            CodexUsageText.Text = "ChatGPT Plus · 暂不可用";
            CodexUsageText.Foreground = new SolidColorBrush(Colors.Orange);
            CodexResetText.Text = usage.Error ?? "未返回 Codex 用量窗口";
            MiniCodexText.Text = "C --";
            MiniCodexText.ToolTip = CodexResetText.Text;
            return;
        }

        CodexUsageText.Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xDF, 0xFF));
        CodexUsageText.Text = CodexUsageFormatter.FormatPlan(usage.PlanType)
            + " · "
            + string.Join(" · ", usage.Windows.Select(CodexUsageFormatter.FormatWindow));
        CodexResetText.Text = string.Join(" · ", usage.Windows.Select(CodexUsageFormatter.FormatReset));

        int remaining = usage.Windows.Min(window => window.RemainingPercent);
        MiniCodexText.Text = $"C {remaining}%";
        MiniCodexText.ToolTip = CodexUsageText.Text + Environment.NewLine + CodexResetText.Text;
    }

    private void RefreshPeakStatus()
    {
        _isPeak = PeakHourCalculator.IsPeak(DateTime.Now, _cfg.PeakHourRanges);
        UpdatePeakUi();
        // 计算下一边界，安排更精确的一次性定时器（60s 兜底 + 边界对齐）
        int mins = PeakHourCalculator.MinutesUntilNextBoundary(DateTime.Now, _cfg.PeakHourRanges);
        _peakTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, mins));
    }

    private void UpdatePeakUi()
    {
        if (!_cfg.ShowPeakIndicator)
        {
            PeakText.Visibility = Visibility.Collapsed;
            MiniPeakDot.Visibility = Visibility.Collapsed;
            return;
        }
        PeakText.Visibility = Visibility.Visible;
        MiniPeakDot.Visibility = Visibility.Visible;

        // 高峰信息是次级参考状态：只用小圆点和文字，不与余额抢视觉焦点
        var peakBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0x7D, 0x72));
        var normalBrush = new SolidColorBrush(Color.FromRgb(0x78, 0xD7, 0x9A));
        var labelBrush = new SolidColorBrush(Color.FromRgb(0xAE, 0xB8, 0xC4));
        PeakText.Background = System.Windows.Media.Brushes.Transparent;
        PeakDot.Foreground = _isPeak ? peakBrush : normalBrush;
        PeakLabel.Text = _isPeak ? "高峰时段" : "非高峰时段";
        PeakLabel.Foreground = labelBrush;
        MiniPeakDot.Foreground = _isPeak ? peakBrush : normalBrush;
        MiniPeakDot.ToolTip = _isPeak ? "预计高峰时段" : "预计非高峰时段";
    }

    private void RaiseTrayStatus(ParsedBalance bal, decimal? change)
    {
        string chg = change.HasValue
            ? $"变动 {(change.Value >= 0 ? "+" : "")}{change.Value:0.00}"
            : "首次刷新";
        string peak = _cfg.ShowPeakIndicator ? (_isPeak ? "预计高峰" : "预计非高峰") : "";
        string status = $"余额 {Symbol(bal.Currency)}{bal.Total:0.00} | {chg} | {peak}".TrimEnd(' ', '|');
        TrayStatusChanged?.Invoke(status, _isPeak);
    }

    private void RaiseTrayError()
    {
        string last = _cfg.LastSuccessfulRefreshUtc.HasValue
            ? _cfg.LastSuccessfulRefreshUtc.Value.ToLocalTime().ToString("HH:mm")
            : "从未";
        string status = $"余额未知（最后成功 {last}）";
        TrayStatusChanged?.Invoke(status, _isPeak);
    }

    private void ApplyBalance(ParsedBalance bal, bool consistent, decimal? change, decimal? pct)
    {
        var normalDot = new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x4C));
        var dangerDot = new SolidColorBrush(Color.FromRgb(0xE8, 0x66, 0x56));
        StatusDot.Foreground = bal.IsAvailable ? normalDot : dangerDot;
        StatusLabel.Text = bal.IsAvailable ? "正常" : "账户不可用";
        StatusLabel.Foreground = bal.IsAvailable
            ? new SolidColorBrush(Color.FromRgb(0xE8, 0xF4, 0xFF))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));

        if (!bal.IsAvailable)
        {
            var danger = new LinearGradientBrush(
                Color.FromArgb(0xF0, 0x3B, 0x1F, 0x22),
                Color.FromArgb(0xF0, 0x24, 0x12, 0x16), 45);
            Card.Background = danger;
            MiniCard.Background = danger;
        }

        string sym = Symbol(bal.Currency);
        string balance = sym + bal.Total.ToString("0.00");
        BalanceText.Text = balance;
        MiniBalanceText.Text = balance;

        if (change is null)
        {
            ChangeText.Text = "首次刷新";
            MiniChangeText.Text = "首次";
        }
        else
        {
            string sign = change.Value >= 0 ? "+" : "";
            string txt = sign + change.Value.ToString("0.00")
                         + (pct.HasValue ? "（" + pct.Value.ToString("0.0") + "%）" : "");
            ChangeText.Text = txt;
            MiniChangeText.Text = sign + change.Value.ToString("0.00");
            ChangeText.Foreground = new SolidColorBrush(change.Value >= 0
                ? Color.FromRgb(0x4C, 0xC9, 0x4C) : Color.FromRgb(0xE8, 0x66, 0x56));
            MiniChangeText.Foreground = ChangeText.Foreground;
        }

        string breakdown = "充值 " + sym + bal.ToppedUp.ToString("0.00");
        if (bal.Granted > 0)
            breakdown += "  ·  赠送 " + sym + bal.Granted.ToString("0.00");
        if (!consistent)
            breakdown += "（待核对）";
        BreakdownText.Text = breakdown;
        RefreshTimeText.Text = "上次刷新 " + DateTime.Now.ToString("HH:mm:ss");
    }

    private void ShowError(string msg)
    {
        StatusLabel.Text = msg;
        StatusLabel.Foreground = new SolidColorBrush(Colors.Orange);
    }

    private void ShowUnavailableCurrency(string currency)
    {
        StatusLabel.Text = "未返回 " + currency + " 余额";
        StatusLabel.Foreground = new SolidColorBrush(Colors.Orange);
    }

    private static string Symbol(string currency) => currency.ToUpperInvariant() switch
    {
        "CNY" => "¥",
        "USD" => "$",
        _ => currency + " "
    };
}
