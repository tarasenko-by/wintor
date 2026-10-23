using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string StartupTaskId = "WintorStartup";

    private readonly AppSettings _settings;
    private readonly ITorrentEngine _engine;
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
        _loading = false;
    }

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

    public string Version
    {
        get
        {
            PackageVersion v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

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
