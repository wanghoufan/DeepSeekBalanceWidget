using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DeepSeekBalanceWidget.Services;

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
            IBalanceProvider provider = config.UseMockData
                ? new MockBalanceService("sequence")
                : new DeepSeekApiClient(configService.GetApiKey() ?? string.Empty);

            desktop.MainWindow = new MainWindow(configService, config, provider);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
