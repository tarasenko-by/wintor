using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Wintor.App.Services;
using Wintor.App.ViewModels;
using Wintor.Core;
using Wintor.Engine.MonoTorrent;

namespace Wintor.App;

public partial class App : Application
{
    /// <summary>The main application window (for dialogs, pickers and interop).</summary>
    public static MainWindow Window { get; private set; } = null!;

    /// <summary>The native window handle, needed by file and folder pickers.</summary>
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Services = ConfigureServices();

        var notifications = Services.GetRequiredService<NotificationService>();
        notifications.Register();
        notifications.Invoked += (action, path) => Window.DispatcherQueue.TryEnqueue(() => OnNotification(action, path));

        AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (IsQuitRequest(activation))
        {
            Exit(); // nothing was running
            return;
        }

        var settings = Services.GetRequiredService<AppSettings>();
        bool startHidden = activation.Kind == ExtendedActivationKind.StartupTask && settings.CloseToTray;

        Window = new MainWindow();
        _ = Services.GetRequiredService<TorrentsViewModel>().InitializeAsync();
        if (!startHidden)
        {
            Window.Activate();
        }

        Route(activation);
    }

    /// <summary>A second launch redirected here (any thread).</summary>
    public static void OnRedirected(AppActivationArguments activation) =>
        Window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (IsQuitRequest(activation))
            {
                Window.Exit();
                return;
            }

            Window.ShowAndActivate();
            Route(activation);
        });

    /// <summary>"wintor --quit" in a terminal (scripts, tests): quit gracefully, saving the torrent state.</summary>
    private static bool IsQuitRequest(AppActivationArguments activation) =>
        activation.Kind == ExtendedActivationKind.Launch
        && activation.Data is ILaunchActivatedEventArgs launch
        && launch.Arguments?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("--quit") == true;

    private static void Route(AppActivationArguments activation)
    {
        var torrents = Services.GetRequiredService<TorrentsViewModel>();
        switch (activation.Kind)
        {
            case ExtendedActivationKind.File when activation.Data is IFileActivatedEventArgs file:
                foreach (IStorageItem item in file.Files)
                {
                    if (TorrentInput.IsTorrentFile(item.Path))
                    {
                        torrents.RequestAdd(AddRequest.File(item.Path));
                    }
                }

                Window.ShowTorrents();
                break;
            case ExtendedActivationKind.Protocol when activation.Data is IProtocolActivatedEventArgs protocol:
                string link = protocol.Uri.OriginalString;
                if (TorrentInput.IsMagnetLink(link))
                {
                    torrents.RequestAdd(AddRequest.Magnet(link));
                }

                Window.ShowTorrents();
                break;
            case ExtendedActivationKind.AppNotification when activation.Data is AppNotificationActivatedEventArgs notification:
                Services.GetRequiredService<NotificationService>().HandleActivation(notification);
                break;
        }
    }

    private static void OnNotification(string action, string? path)
    {
        if (action == NotificationService.ActionOpenFolder && path is not null)
        {
            Shell.OpenFolder(path);
            return;
        }

        Window.ShowAndActivate();
    }

    private static ServiceProvider ConfigureServices()
    {
        // Packaged apps get a private folder that Windows removes on uninstall.
        string dataDirectory = ApplicationData.Current.LocalFolder.Path;
        AppSettings settings = AppSettings.Load(dataDirectory);

        return new ServiceCollection()
            .AddSingleton(settings)
            .AddSingleton<ITorrentEngine>(_ => new MonoTorrentEngine(settings.ToEngineOptions()))
            .AddSingleton<NotificationService>()
            .AddSingleton<TorrentsViewModel>()
            .AddTransient<TorrentDetailsViewModel>()
            .AddTransient<SettingsViewModel>()
            .BuildServiceProvider();
    }
}
