using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.Views;

/// <summary>"What's new": every release, newest first, from Settings → About and once after an update.</summary>
public static class WhatsNewDialog
{
    public static Version CurrentVersion
    {
        get
        {
            PackageVersion v = Package.Current.Id.Version;
            return new Version(v.Major, v.Minor, v.Build);
        }
    }

    public static async Task ShowAsync(XamlRoot root)
    {
        var releases = new StackPanel { Spacing = 24, MaxWidth = 460 };
        foreach (Release release in WhatsNew.Releases)
        {
            releases.Children.Add(Section(release));
        }

        var dialog = Dialogs.Prepare(new ContentDialog
        {
            Title = Strings.Get("WhatsNew_Title"),
            Content = releases,
            CloseButtonText = Strings.Get("WhatsNew_Close"),
            DefaultButton = ContentDialogButton.Close,
        }, root);
        AutomationProperties.SetAutomationId(dialog, "WhatsNewDialog");
        await dialog.ShowQueuedAsync();
    }

    private static StackPanel Section(Release release)
    {
        var heading = new TextBlock
        {
            Text = Strings.Format("WhatsNew_Version", release.VersionText),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        };
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level2);

        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                heading,
                new TextBlock
                {
                    Text = release.Date.ToDateTime(TimeOnly.MinValue).ToString("D", CultureInfo.CurrentUICulture),
                    Style = (Style)Application.Current.Resources["SecondaryCaptionStyle"],
                },
            },
        });

        foreach (string key in release.Keys)
        {
            var bullet = new TextBlock { Text = "•", Margin = new Thickness(0, 0, 8, 0) };
            // Narrator reads the list as sentences; the bullet is decoration.
            AutomationProperties.SetAccessibilityView(bullet, AccessibilityView.Raw);
            var text = new TextBlock { Text = Strings.Get(key), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            Grid.SetColumn(text, 1);
            section.Children.Add(new Grid
            {
                ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition() },
                Children = { bullet, text },
            });
        }

        return section;
    }
}
