using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Wintor.App.Services;

/// <summary>
/// Windows notifications: "download finished" with an "Open folder" button, and the one-time hint that Wintor keeps
/// running in the notification area. Does nothing where notifications are not available.
/// </summary>
public sealed class NotificationService
{
    public const string ActionOpenFolder = "openFolder";
    public const string ActionShow = "show";

    /// <summary>A notification or its button was clicked: (action, argument). Comes on a background thread.</summary>
    public event Action<string, string?>? Invoked;

    public bool IsAvailable { get; private set; }

    /// <summary>Once at startup, before the window is created.</summary>
    public void Register()
    {
        try
        {
            if (!AppNotificationManager.IsSupported())
            {
                return;
            }

            AppNotificationManager.Default.NotificationInvoked += (_, args) => Raise(args.Arguments);
            AppNotificationManager.Default.Register();
            IsAvailable = true;
        }
        catch (Exception)
        {
            IsAvailable = false; // e.g. an unpackaged run without the COM activator
        }
    }

    public void Unregister()
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch (Exception)
        {
            // Shutting down anyway.
        }
    }

    /// <summary>Handles a launch that came from clicking a notification while Wintor was closed.</summary>
    public void HandleActivation(AppNotificationActivatedEventArgs args) => Raise(args.Arguments);

    public void ShowCompleted(string torrentName, string folder) => Show(new AppNotificationBuilder()
        .AddArgument("action", ActionShow)
        .AddText(Strings.Get("Notification_Completed"))
        .AddText(torrentName)
        .AddButton(new AppNotificationButton(Strings.Get("Notification_OpenFolder"))
            .AddArgument("action", ActionOpenFolder)
            .AddArgument("path", folder)));

    public void ShowStillRunning() => Show(new AppNotificationBuilder()
        .AddArgument("action", ActionShow)
        .AddText(Strings.Get("Notification_StillRunningTitle"))
        .AddText(Strings.Get("Notification_StillRunning")));

    private void Show(AppNotificationBuilder builder)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception)
        {
            // Notifications are a convenience; turned off by the user or by policy is fine.
        }
    }

    private void Raise(IDictionary<string, string> arguments)
    {
        if (arguments.TryGetValue("action", out string? action))
        {
            arguments.TryGetValue("path", out string? path);
            Invoked?.Invoke(action, path);
        }
    }
}
