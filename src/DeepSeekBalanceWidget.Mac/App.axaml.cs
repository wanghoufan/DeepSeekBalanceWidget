using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DeepSeekBalanceWidget.Services;
using System.Diagnostics;

namespace DeepSeekBalanceWidget;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // This is a menu-bar monitor: closing its window must not terminate
            // polling or remove the live balance from the macOS status bar.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var configService = new MacConfigService();
            var config = configService.Load();
            ThemeService.Apply(config.ThemeMode);
            IBalanceProvider provider = config.UseMockData
                ? new MockBalanceService("sequence")
                : new DeepSeekApiClient(configService.GetApiKey() ?? string.Empty);

            desktop.MainWindow = new MainWindow(configService, config, provider);

            // A hidden window is still owned by the running application. When
            // macOS activates the app from the Dock, restore that window
            // explicitly instead of letting it flash and remain hidden.
            if (Application.Current?.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
            {
                Debug.WriteLine($"[DockLifecycle] Activatable lifetime registered: {activatable.GetType().FullName}");
                Console.Error.WriteLine($"[DockLifecycle] Activatable lifetime registered: {activatable.GetType().FullName}");
                activatable.Activated += (_, e) =>
                {
                    Debug.WriteLine($"[DockLifecycle] Activated entry kind={e.Kind} visible={desktop.MainWindow?.IsVisible}");
                    Console.Error.WriteLine($"[DockLifecycle] Activated entry kind={e.Kind} visible={desktop.MainWindow?.IsVisible}");
                    if (desktop.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.IsRestoringFromDock = true;
                        try
                        {
                            // BringToFront only activates the native window. If
                            // edge auto-hide moved it off-screen, restore the
                            // saved visible dock position before activation.
                            mainWindow.RestoreAndActivate();
                        }
                        finally
                        {
                            mainWindow.IsRestoringFromDock = false;
                        }
                    }
                    Debug.WriteLine($"[DockLifecycle] Activated exit visible={desktop.MainWindow?.IsVisible}");
                    Console.Error.WriteLine($"[DockLifecycle] Activated exit visible={desktop.MainWindow?.IsVisible}");
                };
            }
            else
            {
                Debug.WriteLine($"[DockLifecycle] Activatable lifetime unavailable; application lifetime={ApplicationLifetime?.GetType().FullName ?? "null"}");
                Console.Error.WriteLine($"[DockLifecycle] Activatable lifetime unavailable; application lifetime={ApplicationLifetime?.GetType().FullName ?? "null"}");
            }

        }

        base.OnFrameworkInitializationCompleted();
    }
}
