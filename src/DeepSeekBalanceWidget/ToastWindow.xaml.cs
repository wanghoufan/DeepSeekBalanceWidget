using System;
using System.Windows;
using System.Windows.Threading;

namespace DeepSeekBalanceWidget;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;

    public ToastWindow(string title, string body)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Close();
        _timer.Start();

        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            Left = Math.Clamp(Left, wa.Left, wa.Right - Width);
            Top = Math.Clamp(Top, wa.Top, wa.Bottom - Height);
        };
    }
}
