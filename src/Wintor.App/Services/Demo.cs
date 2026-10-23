using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace Wintor.App.Services;

/// <summary>
/// "wintor --demo" (optionally "--dark" or "--light"): sample torrents for screenshots. It runs beside the real Wintor
/// as its own instance, in English, with fresh settings in a folder of its own, so the user's torrents, labels and
/// preferences are never shown or changed.
/// </summary>
public static class Demo
{
    public static bool IsOn { get; private set; }

    private static ElementTheme _theme = ElementTheme.Default;

    /// <summary>Reads the launch arguments; call first thing in Main, before the single-instance check.</summary>
    public static void Detect()
    {
        AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activation.Kind != ExtendedActivationKind.Launch || activation.Data is not ILaunchActivatedEventArgs launch)
        {
            return;
        }

        string[] args = launch.Arguments?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        IsOn = args.Contains("--demo");
        _theme = args.Contains("--dark") ? ElementTheme.Dark : args.Contains("--light") ? ElementTheme.Light : ElementTheme.Default;
    }

    /// <summary>
    /// Fresh settings for every demo run: three labels, a column layout that shows time left, English, and the real
    /// Wintor's window size.
    /// </summary>
    public static AppSettings CreateSettings(string dataDirectory, DemoEngine engine)
    {
        string folder = Path.Combine(dataDirectory, "demo");
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        AppSettings settings = AppSettings.Load(folder);
        // The window and column widths as the user set them in the real Wintor: screenshots come out like that.
        AppSettings real = AppSettings.Load(dataDirectory);
        settings.Window = real.Window;
        settings.ColumnWidths = new Dictionary<string, double>(real.ColumnWidths);
        settings.Theme = _theme;
        settings.Language = "en-US";
        settings.DownloadDirectory = @"C:\Downloads";
        settings.HiddenColumns = ["Seeds", "Peers"];
        foreach ((string name, string color) in DemoEngine.Labels)
        {
            settings.Labels.Add(name);
            settings.LabelColors[name] = color;
        }

        foreach ((string id, string label) in engine.TorrentLabels)
        {
            settings.TorrentLabels[id] = label;
        }

        settings.Queue = engine.Order.ToList();
        settings.Save();
        return settings;
    }
}
