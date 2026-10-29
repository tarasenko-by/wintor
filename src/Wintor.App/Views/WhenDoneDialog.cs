using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.Views;

/// <summary>
/// The last minute before a "When done" action: a countdown the user can cancel or skip. The PC may be unattended,
/// so running out of time means go ahead.
/// </summary>
public static class WhenDoneDialog
{
    private const int Seconds = 60;

    /// <summary>True to take the action now (the time ran out, or "… now"); false when the user cancelled.</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot root, WhenDoneRule rule)
    {
        string action = rule.Action.ToString();
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var bar = new ProgressBar { Maximum = Seconds, Value = Seconds };
        AutomationProperties.SetAccessibilityView(bar, AccessibilityView.Raw); // the text says the same
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);

        var dialog = Dialogs.Prepare(new ContentDialog
        {
            Title = Strings.Get(rule.Condition == WhenDoneCondition.AllFinished ? "WhenDone_Title_All" : "WhenDone_Title_Downloads"),
            Content = new StackPanel { Spacing = 16, MinWidth = 320, Children = { text, bar } },
            PrimaryButtonText = Strings.Get("WhenDone_Now_" + action),
            CloseButtonText = Strings.Get("Cancel"),
            // Enter must not shut the PC down by accident; the countdown does it anyway if nobody is there.
            DefaultButton = ContentDialogButton.Close,
        }, root);
        AutomationProperties.SetAutomationId(dialog, "WhenDoneDialog");

        int left = Seconds;
        bool expired = false;
        void Show()
        {
            text.Text = Strings.Format("WhenDone_Countdown_" + action, left);
            bar.Value = left;
        }

        Show();
        DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            left--;
            Show();
            if (left == 0)
            {
                expired = true;
                timer.Stop();
                dialog.Hide();
            }
        };
        dialog.Opened += (_, _) =>
        {
            timer.Start();
            // Narrator reads the countdown once when it opens, not every second.
            FrameworkElementAutomationPeer.FromElement(text)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        };
        dialog.Closed += (_, _) => timer.Stop();

        ContentDialogResult result = await dialog.ShowQueuedAsync();
        return expired || result == ContentDialogResult.Primary;
    }
}
