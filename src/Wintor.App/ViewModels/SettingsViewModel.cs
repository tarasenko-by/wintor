using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;
using Windows.Globalization;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string StartupTaskId = "WintorStartup";

    // "" follows Windows; the rest are the folders under Strings/.
    private static readonly string[] LanguageTags =
        ["", "en-US", "ru-RU", "de-DE", "fr-FR", "es-ES", "it-IT", "pt-BR", "pl-PL", "uk-UA", "tr-TR", "zh-Hans", "ja-JP"];

    private readonly AppSettings _settings;
    private readonly ITorrentEngine _engine;
    private readonly int _startLanguageIndex;
    private bool _loading = true;

    public SettingsViewModel(AppSettings settings, ITorrentEngine engine)
    {
        _settings = settings;
        _engine = engine;
        DownloadDirectory = settings.DownloadDirectory;
        ThemeIndex = settings.Theme switch { ElementTheme.Light => 1, ElementTheme.Dark => 2, _ => 0 };
        BackdropIndex = (int)settings.Backdrop;
        MaxDownloadKiB = settings.MaxDownloadKiB;
        MaxUploadKiB = settings.MaxUploadKiB;
        ListenPort = settings.ListenPort;
        CloseToTray = settings.CloseToTray;
        NotifyWhenComplete = settings.NotifyWhenComplete;
        LanguageNames = LanguageTags.Select(t => t.Length == 0 ? Strings.Get("LanguageSystem") : NativeName(t)).ToList();
        _startLanguageIndex = Math.Max(0, Array.IndexOf(LanguageTags, ApplicationLanguages.PrimaryLanguageOverride ?? ""));
        LanguageIndex = _startLanguageIndex;
        _loading = false;
    }

    /// <summary>"As in Windows", then each language in its own name (Deutsch, 日本語…) so anyone can find theirs.</summary>
    public IReadOnlyList<string> LanguageNames { get; }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    /// <summary>Another language was picked; it applies after a restart.</summary>
    [ObservableProperty]
    public partial bool LanguageNeedsRestart { get; set; }

    [ObservableProperty]
    public partial string DownloadDirectory { get; set; }

    /// <summary>0 as in Windows, 1 light, 2 dark.</summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    /// <summary>Index of <see cref="WindowBackdrop"/>.</summary>
    [ObservableProperty]
    public partial int BackdropIndex { get; set; }

    [ObservableProperty]
    public partial double MaxDownloadKiB { get; set; }

    [ObservableProperty]
    public partial double MaxUploadKiB { get; set; }

    [ObservableProperty]
    public partial double ListenPort { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    [ObservableProperty]
    public partial bool NotifyWhenComplete { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool CanChangeStartup { get; set; } = true;

    /// <summary>Why "start with Windows" cannot be changed here (turned off in Windows settings or by policy).</summary>
    [ObservableProperty]
    public partial string? StartupNote { get; set; }

    /// <summary>The project's founder, shown with "and Wintor contributors" so later contributors are credited too.</summary>
    public const string Author = "Siarhei Tarasenka";

    public string Version
    {
        get
        {
            PackageVersion v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>"Version 0.2.0 · © 2026 … and Wintor contributors" under the About card.</summary>
    public string AboutDescription => Strings.Format("About_Description", Version, Author);

    public async Task LoadStartupAsync()
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync(StartupTaskId);
            ShowStartup(task.State);
        }
        catch (Exception)
        {
            CanChangeStartup = false;
        }
    }

    partial void OnDownloadDirectoryChanged(string value) => Save(s => s.DownloadDirectory = value);

    partial void OnLanguageIndexChanged(int value)
    {
        if (_loading || value < 0 || value >= LanguageTags.Length)
        {
            return;
        }

        // Windows keeps this per app and loads our strings in that language from the next start.
        ApplicationLanguages.PrimaryLanguageOverride = LanguageTags[value];
        LanguageNeedsRestart = value != _startLanguageIndex;
    }

    private static string NativeName(string tag)
    {
        var culture = new CultureInfo(tag);
        // "English", not "English (United States)": one variant each, except Brazilian Portuguese and Simplified Chinese.
        string name = tag is "pt-BR" or "zh-Hans" ? culture.NativeName : culture.Parent.NativeName;
        return char.ToUpper(name[0], culture) + name[1..];
    }

    partial void OnThemeIndexChanged(int value)
    {
        ElementTheme theme = value switch { 1 => ElementTheme.Light, 2 => ElementTheme.Dark, _ => ElementTheme.Default };
        Save(s => s.Theme = theme);
        if (!_loading)
        {
            App.Window.Theme = theme;
        }
    }

    partial void OnBackdropIndexChanged(int value)
    {
        Save(s => s.Backdrop = (WindowBackdrop)Math.Clamp(value, 0, (int)WindowBackdrop.AcrylicThin));
        if (!_loading)
        {
            Appearance.Apply();
        }
    }

    partial void OnMaxDownloadKiBChanged(double value) => SaveEngine(s => s.MaxDownloadKiB = Whole(value));

    partial void OnMaxUploadKiBChanged(double value) => SaveEngine(s => s.MaxUploadKiB = Whole(value));

    partial void OnListenPortChanged(double value)
    {
        int port = double.IsNaN(value) ? AppSettings.DefaultListenPort : (int)Math.Clamp(value, 1024, 65535);
        SaveEngine(s => s.ListenPort = port);
    }

    partial void OnCloseToTrayChanged(bool value) => Save(s => s.CloseToTray = value);

    partial void OnNotifyWhenCompleteChanged(bool value) => Save(s => s.NotifyWhenComplete = value);

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (!_loading)
        {
            _ = SetStartupAsync(value);
        }
    }

    // NumberBox gives NaN for an empty box: read as "unlimited".
    private static int Whole(double value) => double.IsNaN(value) ? 0 : (int)Math.Clamp(value, 0, 10_000_000);

    private void Save(Action<AppSettings> change)
    {
        if (_loading)
        {
            return;
        }

        change(_settings);
        _settings.Save();
    }

    private void SaveEngine(Action<AppSettings> change)
    {
        if (_loading)
        {
            return;
        }

        Save(change);
        _ = _engine.ApplyOptionsAsync(_settings.ToEngineOptions());
    }

    private async Task SetStartupAsync(bool on)
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync(StartupTaskId);
            if (on)
            {
                ShowStartup(await task.RequestEnableAsync());
            }
            else
            {
                task.Disable();
                ShowStartup(task.State);
            }
        }
        catch (Exception)
        {
            CanChangeStartup = false;
        }
    }

    private void ShowStartup(StartupTaskState state)
    {
        _loading = true;
        StartWithWindows = state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        _loading = false;
        CanChangeStartup = state is not (StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy);
        StartupNote = state switch
        {
            StartupTaskState.DisabledByUser => Strings.Get("Settings_StartupDisabledByUser"),
            StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy => Strings.Get("Settings_StartupByPolicy"),
            _ => null,
        };
    }
}
