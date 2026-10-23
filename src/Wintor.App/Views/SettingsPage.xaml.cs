using CommunityToolkit.WinUI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Wintor.App.Services;
using Wintor.App.ViewModels;
using Wintor.Core;

namespace Wintor.App.Views;

public sealed partial class SettingsPage : Page, ISearchablePage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();

    public string SearchPlaceholder => Strings.Get("Search_Settings");

    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    /// <summary>
    /// Shows the cards whose title or description contain every word of the query, with their section headers;
    /// an empty query shows everything. The page is one column of headers followed by their cards.
    /// </summary>
    public void ApplySearch(string text)
    {
        string query = text.Trim();
        bool any = false;
        TextBlock? header = null;
        bool headerHasMatch = false;

        foreach (UIElement child in SettingsPanel.Children)
        {
            switch (child)
            {
                case TextBlock block when block.Style == (Style)Application.Current.Resources["SectionHeaderStyle"]:
                    CloseSection();
                    header = block;
                    headerHasMatch = false;
                    break;
                case SettingsCard or SettingsExpander:
                    bool match = query.Length == 0 || Matches((Control)child, query);
                    child.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                    headerHasMatch |= match;
                    any |= match;
                    break;
            }
        }

        CloseSection();
        SettingsNothingFound.Visibility = query.Length > 0 && !any ? Visibility.Visible : Visibility.Collapsed;

        void CloseSection()
        {
            if (header is not null)
            {
                header.Visibility = query.Length == 0 || headerHasMatch ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadStartupAsync();
    }

    /// <summary>A card matches on its header and description; an expander also on its inner cards (License, …).</summary>
    private static bool Matches(Control card, string query)
    {
        IEnumerable<string?> texts = card switch
        {
            SettingsExpander expander => new[] { expander.Header as string, expander.Description as string }
                .Concat(expander.Items.OfType<SettingsCard>().SelectMany(c => new[] { c.Header as string, c.Description as string })),
            SettingsCard settingsCard => [settingsCard.Header as string, settingsCard.Description as string],
            _ => [],
        };
        return TorrentQuery.MatchesSearch(string.Join(" ", texts.Where(t => t is not null)), query);
    }

    private void RestartForLanguage_Click(object sender, RoutedEventArgs e) => App.Window.Restart();

    public Uri GitHubUri { get; } = new(SettingsViewModel.GitHubUrl);

    public Uri StoreUri { get; } = new($"ms-windows-store://pdp/?ProductId={SettingsViewModel.StoreProductId}");

    private void WhatsNew_Click(object sender, RoutedEventArgs e) => _ = WhatsNewDialog.ShowAsync(XamlRoot);

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        if (await Shell.PickFolderAsync() is { } folder)
        {
            ViewModel.DownloadDirectory = folder;
        }
    }
}
