using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Wintor.App.Services;
using Wintor.App.ViewModels;
using Wintor.Core;
using Wintor.Engine.MonoTorrent;

namespace Wintor.App;

public partial class App : Application
{
    /// <summary>The main application window (for dialogs, pickers and interop).</summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>The native window handle, needed by file and folder pickers.</summary>
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = ConfigureServices();
        Window = new MainWindow();
        Window.Activate();
    }

    /// <summary>Called from any thread when the user launches Wintor again while it is running.</summary>
    public static void BringToFront()
    {
        Window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (Window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter { State: Microsoft.UI.Windowing.OverlappedPresenterState.Minimized } presenter)
            {
                presenter.Restore();
            }

            Window.Activate();
        });
    }

    private static ServiceProvider ConfigureServices()
    {
        // Packaged apps get a private folder that Windows removes on uninstall.
        string dataDirectory = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        AppSettings settings = AppSettings.Load(dataDirectory);

        return new ServiceCollection()
            .AddSingleton(settings)
            .AddSingleton(new EngineOptions { DataDirectory = Path.Combine(dataDirectory, "engine") })
            .AddSingleton<ITorrentEngine, MonoTorrentEngine>()
            .AddSingleton<TorrentsViewModel>()
            .AddTransient<SettingsViewModel>()
            .BuildServiceProvider();
    }
}
