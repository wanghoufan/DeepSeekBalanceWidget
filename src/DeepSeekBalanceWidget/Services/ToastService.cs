using System.Windows;

namespace DeepSeekBalanceWidget.Services;

public static class ToastService
{
    public static void Show(Window owner, string title, string body)
    {
        var toast = new ToastWindow(title, body)
        {
            Owner = owner,
            Left = owner.Left + owner.Width + 8,
            Top = owner.Top
        };
        toast.Show();
    }
}
