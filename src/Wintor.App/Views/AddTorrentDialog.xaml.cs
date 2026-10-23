using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.Views;

public sealed record AddDialogFile(int Index, string Path, long Size)
{
    public string SizeText => Formatting.Bytes(Size);
}

/// <summary>
/// Confirms a torrent before it is added: the magnet link (or the .torrent's name and size), the folder to save
/// to and, for .torrent files, which files to download.
/// </summary>
public sealed partial class AddTorrentDialog : ContentDialog
{
    private readonly List<AddDialogFile> _files = [];
    private bool _syncingSelection;

    private AddTorrentDialog(string folder)
    {
        InitializeComponent();
        Folder = folder;
        FolderText.Text = folder;
        ToolTipService.SetToolTip(FolderText, folder);
    }

    public string Folder { get; private set; }

    public string MagnetLink => MagnetBox.Text.Trim();

    /// <summary>Indexes of the files the user unticked.</summary>
    public IReadOnlySet<int> SkippedFiles =>
        _files.Where(f => !FilesList.SelectedItems.Contains(f)).Select(f => f.Index).ToHashSet();

    public static AddTorrentDialog ForMagnet(string folder, string? magnetLink)
    {
        var dialog = new AddTorrentDialog(folder) { Title = Strings.Get("AddDialog_MagnetTitle") };
        dialog.MagnetBox.Visibility = Visibility.Visible;
        dialog.MagnetBox.Text = magnetLink ?? "";
        dialog.IsPrimaryButtonEnabled = TorrentInput.IsMagnetLink(magnetLink);
        dialog.Loaded += (_, _) => dialog.MagnetBox.Focus(FocusState.Programmatic);
        return dialog;
    }

    public static AddTorrentDialog ForTorrentFile(string folder, TorrentMetadata metadata)
    {
        var dialog = new AddTorrentDialog(folder) { Title = Strings.Get("AddDialog_FileTitle") };
        dialog.TorrentHeader.Visibility = Visibility.Visible;
        dialog.TorrentName.Text = metadata.Name;
        ToolTipService.SetToolTip(dialog.TorrentName, metadata.Name);
        dialog.TorrentSize.Text = Strings.Format(
            metadata.Files.Count == 1 ? "AddDialog_OneFile" : "AddDialog_Files",
            metadata.Files.Count,
            Formatting.Bytes(metadata.TotalSize));

        // A single file needs no choice.
        if (metadata.Files.Count > 1)
        {
            dialog._files.AddRange(metadata.Files
                .OrderBy(f => f.Path, StringComparer.CurrentCultureIgnoreCase)
                .Select(f => new AddDialogFile(f.Index, f.Path, f.Size)));
            dialog.FilesList.ItemsSource = dialog._files;
            dialog.FilesPanel.Visibility = Visibility.Visible;
            dialog.Loaded += (_, _) =>
            {
                dialog.FilesList.SelectAll();
                dialog.UpdateSelection();
            };
        }

        return dialog;
    }

    private void MagnetBox_TextChanged(object sender, TextChangedEventArgs e) =>
        IsPrimaryButtonEnabled = TorrentInput.IsMagnetLink(MagnetBox.Text);

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        if (await Shell.PickFolderAsync() is { } picked)
        {
            Folder = picked;
            FolderText.Text = picked;
            ToolTipService.SetToolTip(FolderText, picked);
            UpdateSpaceWarning();
        }
    }

    /// <summary>Warns before adding, not halfway through the download, when the files will not fit on the drive.</summary>
    private void UpdateSpaceWarning()
    {
        long needed = FilesList.SelectedItems.OfType<AddDialogFile>().Sum(f => f.Size);
        long? free = null;
        try
        {
            if (Path.GetPathRoot(Path.GetFullPath(Folder)) is { Length: > 0 } root)
            {
                free = new DriveInfo(root).AvailableFreeSpace;
            }
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Unknown drive (e.g. a disconnected network share): nothing to warn about yet.
        }

        if (free is { } available && needed > available)
        {
            SpaceWarning.Text = Strings.Format("AddDialog_NotEnoughSpace", Formatting.Bytes(available), Formatting.Bytes(needed));
            SpaceWarning.Visibility = Visibility.Visible;
        }
        else
        {
            SpaceWarning.Visibility = Visibility.Collapsed;
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        _syncingSelection = true;
        if (SelectAllBox.IsChecked == true)
        {
            FilesList.SelectAll();
        }
        else
        {
            FilesList.DeselectRange(new Microsoft.UI.Xaml.Data.ItemIndexRange(0, (uint)_files.Count));
        }

        _syncingSelection = false;
        UpdateSelection();
    }

    private void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingSelection)
        {
            UpdateSelection();
        }
    }

    private void UpdateSelection()
    {
        int selected = FilesList.SelectedItems.Count;
        long size = FilesList.SelectedItems.OfType<AddDialogFile>().Sum(f => f.Size);
        SelectionSummary.Text = Strings.Format("AddDialog_Selection", selected, _files.Count, Formatting.Bytes(size));
        SelectAllBox.IsChecked = selected == _files.Count ? true : selected == 0 ? false : null;
        IsPrimaryButtonEnabled = selected > 0;
        UpdateSpaceWarning();
    }
}
