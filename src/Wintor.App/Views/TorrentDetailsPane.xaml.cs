using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wintor.App.ViewModels;

namespace Wintor.App.Views;

/// <summary>The pane under the torrent table: details of the selected torrent, so nobody has to drill in.</summary>
public sealed partial class TorrentDetailsPane : UserControl
{
    public TorrentDetailsPane()
    {
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TorrentDetailsViewModel.HasTorrent))
            {
                UpdateViews();
            }
        };
    }

    public TorrentDetailsViewModel ViewModel { get; } = App.Services.GetRequiredService<TorrentDetailsViewModel>();

    // Through the table's view model, so a failure shows in the page's error bar like any other action.
    private async void PlayFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index } && ViewModel.Id is { } id
            && await App.Services.GetRequiredService<TorrentsViewModel>().PlayAsync(id, index) is { } link)
        {
            await NoPlayerDialog.ShowAsync(XamlRoot, link);
        }
    }

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => UpdateViews();

    /// <summary>Shows the chosen tab's view, or none while nothing is selected.</summary>
    private void UpdateViews()
    {
        string tab = Tabs.SelectedItem?.Tag as string ?? "General";
        GeneralView.Visibility = Show(tab == "General");
        FilesView.Visibility = Show(tab == "Files");
        PeersView.Visibility = Show(tab == "Peers");
        TrackersView.Visibility = Show(tab == "Trackers");
        SpeedView.Visibility = Show(tab == "Speed");

        Visibility Show(bool visible) => visible && ViewModel.HasTorrent ? Visibility.Visible : Visibility.Collapsed;
    }
}
