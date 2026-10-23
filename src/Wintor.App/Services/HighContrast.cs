using Microsoft.UI;
using Microsoft.UI.System;

namespace Wintor.App.Services;

/// <summary>Windows high-contrast themes: no backdrop and system caption-button colors while one is on.</summary>
public static class HighContrast
{
    private static ThemeSettings? _settings; // kept alive for its Changed event

    /// <summary>Raised when a high-contrast theme is turned on or off.</summary>
    public static event EventHandler? Changed;

    public static bool IsOn
    {
        get
        {
            try
            {
                return _settings?.HighContrast == true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Once, with the main window, before anything asks <see cref="IsOn"/>.</summary>
    public static void Initialize(WindowId window)
    {
        try
        {
            _settings = ThemeSettings.CreateForWindowId(window);
            _settings.Changed += (_, _) => Changed?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Without these APIs the app simply keeps its own colors.
        }
    }
}
