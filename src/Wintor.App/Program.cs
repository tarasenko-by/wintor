using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Wintor.App;

/// <summary>
/// Custom entry point that keeps Wintor single-instance: two engines sharing one state file
/// would overwrite each other's torrent list. A second launch (a .torrent double-click, a magnet
/// link, the Start menu) hands its activation to the running instance and exits.
/// </summary>
public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        AppInstance mainInstance = AppInstance.FindOrRegisterForKey("main");
        if (!mainInstance.IsCurrent)
        {
            AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            // Redirect off the STA thread so the call cannot deadlock on it.
            Task.Run(() => mainInstance.RedirectActivationToAsync(activation).AsTask()).Wait();
            return 0;
        }

        // Comes on a background thread; App marshals it to the UI thread.
        mainInstance.Activated += (_, activation) => App.OnRedirected(activation);

        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        return 0;
    }
}
