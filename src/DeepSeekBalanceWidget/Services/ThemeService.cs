using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace DeepSeekBalanceWidget.Services;

/// <summary>
/// Owns the application-level theme dictionary shared by the settings window and widget.
/// </summary>
public static class ThemeService
{
    private const string ThemeMarkerKey = "ThemeModeMarker";
    private const string LightSource = "/DeepSeekBalanceWidget;component/Themes/Light.xaml";
    private const string DarkSource = "/DeepSeekBalanceWidget;component/Themes/Dark.xaml";

    /// <summary>The effective theme currently loaded into application resources.</summary>
    public static string CurrentMode { get; private set; } = "Light";

    /// <summary>Raised after the application theme dictionary has been replaced.</summary>
    public static event EventHandler? OnThemeChanged;

    public static string Normalize(string? mode) =>
        mode?.Trim().ToLowerInvariant() switch
        {
            "dark" => "Dark",
            "system" => "System",
            _ => "Light"
        };

    public static void Apply(string? mode)
    {
        var app = Application.Current;
        if (app is null) return;

        string requested = Normalize(mode);
        string normalized = requested == "System" && IsSystemDarkMode() ? "Dark" :
            requested == "System" ? "Light" : requested;
        var dictionaries = app.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(d => d.Contains(ThemeMarkerKey));
        var replacement = new ResourceDictionary
        {
            Source = new Uri(normalized == "Dark" ? DarkSource : LightSource, UriKind.Relative)
        };

        if (current is not null)
        {
            int index = dictionaries.IndexOf(current);
            dictionaries[index] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }

        CurrentMode = normalized;
        OnThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            // If the registry is unavailable (for example in a restricted test host),
            // keep the safe light fallback while preserving the System preference.
            return false;
        }
    }
}
