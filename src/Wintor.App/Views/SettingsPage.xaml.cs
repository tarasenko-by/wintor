using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Wintor.App.Services;
using Wintor.App.ViewModels;

namespace Wintor.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();

    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadStartupAsync();
    }

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        if (await Shell.PickFolderAsync() is { } folder)
        {
            ViewModel.DownloadDirectory = folder;
        }
    }
}
