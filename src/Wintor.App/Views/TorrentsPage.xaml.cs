using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Wintor.App.Services;
using Wintor.App.ViewModels;
using Wintor.Core;

namespace Wintor.App.Views;

public sealed partial class TorrentsPage : Page
{
    public TorrentsViewModel ViewModel { get; } = App.Services.GetRequiredService<TorrentsViewModel>();

    public TorrentsPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    private async void AddMagnet_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox
        {
            Header = Strings.Get("MagnetDialog_Header"),
            PlaceholderText = "magnet:?xt=urn:btih:…",
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
        };

        // Most people copy the link right before adding it — prefill from the clipboard.
        try
        {
            DataPackageView clipboard = Clipboard.GetContent();
            if (clipboard.Contains(StandardDataFormats.Text))
            {
                string text = await clipboard.GetTextAsync();
                if (TorrentInput.IsMagnetLink(text))
                {
                    input.Text = text.Trim();
                }
            }
        }
        catch (Exception)
        {
            // The clipboard can be locked by another app; prefilling is only a convenience.
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Get("MagnetDialog_Title"),
            Content = input,
            PrimaryButtonText = Strings.Get("MagnetDialog_Add"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = TorrentInput.IsMagnetLink(input.Text),
        };
        input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = TorrentInput.IsMagnetLink(input.Text);

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.AddMagnetAsync(input.Text);
        }
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add(".torrent");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);

        IReadOnlyList<StorageFile> files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
        {
            await ViewModel.AddTorrentFilesAsync(files.Select(f => f.Path));
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTorrent is not { } torrent)
        {
            return;
        }

        var deleteFiles = new CheckBox { Content = Strings.Get("RemoveDialog_DeleteFiles") };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Get("RemoveDialog_Title"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = torrent.Name, TextWrapping = TextWrapping.Wrap },
                    deleteFiles,
                },
            },
            PrimaryButtonText = Strings.Get("RemoveDialog_Remove"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RemoveAsync(torrent, deleteFiles.IsChecked == true);
        }
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems) || e.DataView.Contains(StandardDataFormats.Text))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Strings.Get("Drop_Caption");
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            await ViewModel.AddTorrentFilesAsync(items.Select(i => i.Path).Where(TorrentInput.IsTorrentFile));
        }
        else if (e.DataView.Contains(StandardDataFormats.Text))
        {
            string text = await e.DataView.GetTextAsync();
            if (TorrentInput.IsMagnetLink(text))
            {
                await ViewModel.AddMagnetAsync(text);
            }
        }
    }
}
