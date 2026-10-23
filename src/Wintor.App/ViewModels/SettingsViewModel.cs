using CommunityToolkit.Mvvm.ComponentModel;
using Wintor.App.Services;

namespace Wintor.App.ViewModels;

public partial class SettingsViewModel(AppSettings settings) : ObservableObject
{
    [ObservableProperty]
    public partial string DownloadDirectory { get; set; } = settings.DownloadDirectory;

    public string Version
    {
        get
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    partial void OnDownloadDirectoryChanged(string value)
    {
        settings.DownloadDirectory = value;
        settings.Save();
    }
}
