using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Wintor.App.Services;

/// <summary>Small helpers that talk to Windows: Explorer, the clipboard, dialogs.</summary>
public static class Shell
{
    /// <summary>Opens a folder in File Explorer; a missing folder opens its nearest existing parent.</summary>
    public static void OpenFolder(string path)
    {
        string? folder = path;
        while (folder is not null && !Directory.Exists(folder))
        {
            folder = Path.GetDirectoryName(folder);
        }

        if (folder is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Explorer not available (restricted environments): nothing else to do.
        }
    }

    public static async Task<string?> PickFolderAsync()
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(App.Window.AppWindow.Id)
        {
            SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.Downloads,
        };
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    public static async Task<IReadOnlyList<string>> PickTorrentFilesAsync()
    {
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(App.Window.AppWindow.Id)
        {
            SuggestedStartLocation = Microsoft.Windows.Storage.Pickers.PickerLocationId.Downloads,
        };
        picker.FileTypeFilter.Add(".torrent");
        return (await picker.PickMultipleFilesAsync())?.Select(f => f.Path).ToList() ?? [];
    }

    public static void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
