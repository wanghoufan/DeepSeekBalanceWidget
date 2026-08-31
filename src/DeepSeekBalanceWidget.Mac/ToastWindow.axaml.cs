using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using DeepSeekBalanceWidget.Models;
using DeepSeekBalanceWidget.Services;

namespace DeepSeekBalanceWidget;

public enum ToastAlertStyle
{
    Notice,
    Alarm
}

/// <summary>
/// macOS 通知/警报窗口。通知 8 秒后消失；警报持续播放声音并常驻，
/// 或在限时模式下至少显示/播放 10 秒后自动消失。
/// </summary>
public partial class ToastWindow : Window
{
    private const double MinAlarmSeconds = 10;
    private const double NoticeSeconds = 8;
    private const double Margin = 16;
    private const double Gap = 8;
    private static readonly List<ToastWindow> Active = new();

    private readonly ToastAlertStyle _style;
    private readonly bool _sound;
    private readonly bool _persistent;
    private readonly DispatcherTimer _autoCloseTimer;
    private bool _dismissed;
    private bool _closing;
    private DateTime _shownUtc = DateTime.UtcNow;

    public ToastWindow(
        string title,
        string body,
        ToastAlertStyle style,
        bool soundEnabled,
        string? alertMode,
        string? alertPosition,
        string? alertSoundStyle)
    {
        InitializeComponent();
        _style = style;
        _sound = soundEnabled && style == ToastAlertStyle.Alarm;
        _persistent = style == ToastAlertStyle.Alarm
                      && string.Equals(alertMode, "Continuous", StringComparison.OrdinalIgnoreCase);

        TitleText.Text = title;
        BodyText.Text = body;

        bool isRecovery = title.Contains("已恢复", StringComparison.Ordinal);
        TitleText.Foreground = isRecovery
            ? Brush.Parse("#6DDC6D")
            : Brush.Parse("#FFB04D");

        if (style == ToastAlertStyle.Alarm)
        {
            ToastBorder.BorderBrush = Brush.Parse("#FFB04D");
            ToastBorder.BorderThickness = new Thickness(1.5);
            DismissBtn.IsVisible = true;
        }

        _autoCloseTimer = new DispatcherTimer();
        if (style == ToastAlertStyle.Notice)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(NoticeSeconds);
            _autoCloseTimer.Tick += (_, _) => FadeOutAndClose();
            _autoCloseTimer.Start();
        }
        else if (!_persistent)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(MinAlarmSeconds);
            _autoCloseTimer.Tick += (_, _) => FadeOutAndClose();
            _autoCloseTimer.Start();
        }

        // Keep the configured position with the window so that newly opened
        // toasts can use it as the stack anchor.
        PositionHint = alertPosition;
        SoundStyle = string.IsNullOrWhiteSpace(alertSoundStyle) ? "Standard" : alertSoundStyle;

        Opened += Window_Opened;
        Closed += Window_Closed;
        PointerPressed += Window_PointerPressed;
    }

    private string? PositionHint { get; }
    private string SoundStyle { get; }

    private void Window_Opened(object? sender, EventArgs e)
    {
        _shownUtc = DateTime.UtcNow;
        lock (Active)
        {
            Active.Add(this);
            RepositionAll();
        }

        if (_sound) MacAlarmSound.Play(SoundStyle);
        Opacity = 0;
        _ = FadeAsync(0, 1);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        lock (Active)
        {
            Active.Remove(this);
            RepositionAll();
        }
        StopAlarmSoundIfLast();
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_style == ToastAlertStyle.Alarm && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            Dismiss();
    }

    private void DismissBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Dismiss();

    private void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        double elapsed = (DateTime.UtcNow - _shownUtc).TotalSeconds;
        if (_style == ToastAlertStyle.Alarm && !_persistent && elapsed < MinAlarmSeconds)
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(MinAlarmSeconds - elapsed);
            _autoCloseTimer.Start();
            return;
        }
        FadeOutAndClose();
    }

    private void FadeOutAndClose()
    {
        if (_closing) return;
        _closing = true;
        _autoCloseTimer.Stop();
        _ = FadeOutThenCloseAsync();
    }

    private async Task FadeOutThenCloseAsync()
    {
        await FadeAsync(Opacity, 0);
        if (!_closing) return;
        Close();
    }

    private async Task FadeAsync(double from, double to)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.25),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(Visual.OpacityProperty, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(Visual.OpacityProperty, to) }
                }
            }
        };
        await animation.RunAsync(this);
    }

    private void StopAlarmSoundIfLast()
    {
        bool alarmActive;
        lock (Active) alarmActive = Active.Any(window => window._sound);
        if (!alarmActive) MacAlarmSound.Stop();
    }

    /// <summary>使用主屏幕工作区，在设置的锚点处堆叠所有活动通知窗口。</summary>
    private static void RepositionAll()
    {
        if (Active.Count == 0) return;
        var screen = Active[0].Screens?.Primary;
        if (screen is null) return;

        PixelRect area = screen.WorkingArea;
        double scale = Active[0].RenderScaling > 0 ? Active[0].RenderScaling : 1;
        var heights = Active.Select(window =>
        {
            double height = window.Bounds.Height > 0 ? window.Bounds.Height : 90;
            return height * scale;
        }).ToArray();
        double width = (Active[0].Bounds.Width > 0 ? Active[0].Bounds.Width : 260) * scale;
        double totalHeight = heights.Sum() + Gap * scale * Math.Max(0, Active.Count - 1);

        string position = Active[0].PositionHint ?? "TopRight";
        double top = position switch
        {
            "RightCenter" => area.Y + Math.Max(0, (area.Height - totalHeight) / 2),
            "BottomRight" => area.Bottom - Margin * scale - totalHeight,
            _ => area.Y + Margin * scale
        };
        int left = area.Right - (int)Math.Round(width) - (int)Math.Round(Margin * scale);
        double cursor = top;
        for (int i = 0; i < Active.Count; i++)
        {
            Active[i].Position = new PixelPoint(left, (int)Math.Round(cursor));
            cursor += heights[i] + Gap * scale;
        }
    }
}
