using CommunityToolkit.Mvvm.ComponentModel;

namespace Wintor.App.ViewModels;

/// <summary>A label in the side pane, with how many torrents carry it.</summary>
public partial class LabelItem(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial int Count { get; set; }
}
