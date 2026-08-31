using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DeepSeekBalanceWidget.Services;

public sealed class TrayIconService : IDisposable
{
    private const int MaxTooltip = 63;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _statusItem;
    private Icon? _peakIcon;
    private Icon? _normalIcon;

    public TrayIconService(MainWindow mainWindow, CancellationTokenSource cts)
    {
        _peakIcon = CreateCircleIcon(Color.FromArgb(0xE8, 0x66, 0x56));   // 高峰红
        _normalIcon = CreateCircleIcon(Color.FromArgb(0x4C, 0xC9, 0x4C)); // 非高峰绿

        _icon = new NotifyIcon
        {
            Icon = _normalIcon,
            Text = "DeepSeek 余额",
            Visible = true
        };
        _icon.DoubleClick += (_, _) => mainWindow.RestoreAndActivate();

        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem("余额：--") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("显示窗口", null, (_, _) => mainWindow.RestoreAndActivate());
        menu.Items.Add("立即刷新", null, (_, _) => mainWindow.RefreshNow());
        menu.Items.Add("设置", null, (_, _) => mainWindow.OpenSettings());
        menu.Items.Add("恢复默认位置", null, (_, _) => mainWindow.ResetPosition());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => mainWindow.ExitApp());
        _icon.ContextMenuStrip = menu;
    }

    /// <summary>
    /// 更新托盘 tooltip 与图标。isPeak：null=中性（默认图标，关闭高峰指示时用），true=高峰红，false=非高峰绿。
    /// tooltip 同时显示文本状态（双重呈现，兼顾色觉障碍）。
    /// </summary>
    public void UpdateStatus(string status, bool? isPeak)
    {
        _icon.Text = Truncate(status, MaxTooltip);
        _statusItem.Text = status;
        _icon.Icon = isPeak switch
        {
            true => _peakIcon,
            false => _normalIcon,
            _ => System.Drawing.SystemIcons.Application
        };
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max);

    private static Icon CreateCircleIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.White, 1f);
            g.FillEllipse(brush, 1, 1, 14, 14);
            g.DrawEllipse(pen, 1, 1, 14, 14);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _peakIcon?.Dispose();
        _normalIcon?.Dispose();
        _peakIcon = null;
        _normalIcon = null;
    }
}
