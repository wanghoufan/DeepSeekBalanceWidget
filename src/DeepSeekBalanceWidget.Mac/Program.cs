using Avalonia;
using System.Diagnostics;

namespace DeepSeekBalanceWidget;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var processes = Process.GetProcessesByName("DeepSeekBalanceWidget");
        try
        {
            if (processes.Any(process => process.Id != Environment.ProcessId))
            {
                Process.Start(
                    "osascript",
                    "-e \"tell application \\\"DeepSeek Balance Widget\\\" to activate\"");
                return 0;
            }
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
