using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Wintor.App.Services;

/// <summary>
/// The window background chosen in Settings, applied live: the solid palette (default) or Mica / Acrylic behind
/// the title bar and the navigation pane, with a translucent layer under the pages. A backdrop is only used while
/// Windows' own transparency effects are on, outside high contrast and where the material is supported.
/// <para>
/// WinUI does not read a Color resource again once a brush exists, so the palette's layer brushes themselves get the
/// new colors: everything on screen follows at once, without recreating anything.
/// </para>
/// </summary>
public static class Appearance
{
    private static readonly (string Brush, string Translucent)[] LayerKeys =
    [
        ("NavigationViewContentBackground", "ShellLayerTranslucentColor"),
        ("ContentLayerBrush", "ContentLayerTranslucentColor"),
    ];

    private static UISettings? _ui; // kept alive for its events
    private static AppSettings? _settings;
    private static Window? _window;
    private static Panel? _root;
    private static List<(SolidColorBrush Brush, Color Solid, Color Translucent)>? _layers;
    private static bool _layersTranslucent;

    /// <summary>On the UI thread, before the window is shown.</summary>
    public static void Attach(Window window, Panel root, AppSettings settings)
    {
        _window = window;
        _root = root;
        _settings = settings;

        DispatcherQueue queue = DispatcherQueue.GetForCurrentThread();
        try
        {
            _ui = new UISettings();
            // Comes on a background thread.
            _ui.AdvancedEffectsEnabledChanged += (_, _) => queue.TryEnqueue(Apply);
        }
        catch (Exception)
        {
            // Without UISettings the solid palette stays.
        }

        HighContrast.Changed += (_, _) => queue.TryEnqueue(Apply);
        Apply();
    }

    /// <summary>After a change in Settings.</summary>
    public static void Apply()
    {
        if (_window is null || _root is null)
        {
            return;
        }

        WindowBackdrop backdrop = Effective;
        ApplyLayers(backdrop != WindowBackdrop.None);

        if (backdrop == WindowBackdrop.None)
        {
            _window.SystemBackdrop = null;
            _root.ClearValue(Panel.BackgroundProperty); // back to WindowBackgroundStyle
            return;
        }

        bool same = (backdrop, _window.SystemBackdrop) switch
        {
            (WindowBackdrop.Mica, MicaBackdrop { Kind: MicaKind.Base }) => true,
            (WindowBackdrop.MicaAlt, MicaBackdrop { Kind: MicaKind.BaseAlt }) => true,
            (WindowBackdrop.Acrylic, AcrylicBackdrop { Kind: DesktopAcrylicKind.Base }) => true,
            (WindowBackdrop.AcrylicThin, AcrylicBackdrop { Kind: DesktopAcrylicKind.Thin }) => true,
            _ => false,
        };
        if (!same)
        {
            _window.SystemBackdrop = backdrop switch
            {
                WindowBackdrop.Mica => new MicaBackdrop { Kind = MicaKind.Base },
                WindowBackdrop.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                WindowBackdrop.AcrylicThin => new AcrylicBackdrop(DesktopAcrylicKind.Thin),
                _ => new AcrylicBackdrop(DesktopAcrylicKind.Base),
            };
        }

        _root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    /// <summary>The material chosen in Settings, or None where it cannot be shown now.</summary>
    private static WindowBackdrop Effective
    {
        get
        {
            WindowBackdrop choice = _settings?.Backdrop ?? WindowBackdrop.None;
            if (choice == WindowBackdrop.None || HighContrast.IsOn)
            {
                return WindowBackdrop.None;
            }

            try
            {
                bool supported = choice is WindowBackdrop.Mica or WindowBackdrop.MicaAlt
                    ? MicaController.IsSupported()
                    : DesktopAcrylicController.IsSupported();
                return _ui?.AdvancedEffectsEnabled == true && supported ? choice : WindowBackdrop.None;
            }
            catch (Exception)
            {
                return WindowBackdrop.None;
            }
        }
    }

    private static void ApplyLayers(bool translucent)
    {
        _layers ??= FindLayers();
        if (translucent == _layersTranslucent)
        {
            return;
        }

        _layersTranslucent = translucent;
        foreach (var (brush, solid, see) in _layers)
        {
            brush.Color = translucent ? see : solid;
        }
    }

    private static List<(SolidColorBrush, Color, Color)> FindLayers()
    {
        var found = new List<(SolidColorBrush, Color, Color)>();
        ResourceDictionary? palette = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.EndsWith("Palette.xaml", StringComparison.OrdinalIgnoreCase) == true);
        if (palette is null)
        {
            return found;
        }

        foreach (string theme in new[] { "Light", "Dark" })
        {
            if (!palette.ThemeDictionaries.TryGetValue(theme, out object? value) || value is not ResourceDictionary dictionary)
            {
                continue;
            }

            foreach (var (brushKey, colorKey) in LayerKeys)
            {
                if (dictionary.TryGetValue(brushKey, out object? brush) && brush is SolidColorBrush solid
                    && dictionary.TryGetValue(colorKey, out object? color) && color is Color see)
                {
                    found.Add((solid, solid.Color, see));
                }
            }
        }

        return found;
    }
}
