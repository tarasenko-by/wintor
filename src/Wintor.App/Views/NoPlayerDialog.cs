using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Wintor.App.Services;

namespace Wintor.App.Views;

/// <summary>
/// "Play" on a file still downloading, but no installed player opens links: say which players do, and offer the
/// link for one that has "Open network stream".
/// </summary>
public static class NoPlayerDialog
{
    public static async Task ShowAsync(XamlRoot root, Uri link)
    {
        var url = new TextBox { Text = link.AbsoluteUri, IsReadOnly = true, Header = Strings.Get("NoPlayer_LinkHeader") };
        AutomationProperties.SetAutomationId(url, "StreamLinkBox");
        var dialog = Dialogs.Prepare(new ContentDialog
        {
            Title = Strings.Get("NoPlayer_Title"),
            Content = new StackPanel
            {
                Spacing = 12,
                MaxWidth = 460,
                Children =
                {
                    new TextBlock { Text = Strings.Get("NoPlayer_Text"), TextWrapping = TextWrapping.Wrap },
                    url,
                },
            },
            PrimaryButtonText = Strings.Get("NoPlayer_Copy"),
            CloseButtonText = Strings.Get("WhatsNew_Close"),
            DefaultButton = ContentDialogButton.Primary,
        }, root);
        AutomationProperties.SetAutomationId(dialog, "NoPlayerDialog");
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
        {
            Shell.CopyText(link.AbsoluteUri);
        }
    }
}
