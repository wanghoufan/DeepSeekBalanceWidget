using Avalonia;
using Avalonia.Styling;

namespace DeepSeekBalanceWidget.Services;

/// <summary>Applies Avalonia's application-level theme variant.</summary>
public static class ThemeService
{
    public static string CurrentMode { get; private set; } = "Dark";

    public static string Normalize(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "dark" => "Dark",
        "system" => "System",
        _ => "Light"
    };

    public static void Apply(string? mode)
    {
        string normalized = Normalize(mode);
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = normalized switch
            {
                "Dark" => ThemeVariant.Dark,
                "System" => ThemeVariant.Default,
                _ => ThemeVariant.Light
            };
        }

        CurrentMode = normalized;
    }
}
