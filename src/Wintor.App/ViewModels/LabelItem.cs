using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Wintor.App.Services;

namespace Wintor.App.ViewModels;

/// <summary>A label in the side pane, with its color and how many torrents carry it.</summary>
public partial class LabelItem(string name, string color) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Brush))]
    public partial string Color { get; set; } = color;

    [ObservableProperty]
    public partial int Count { get; set; }

    public Brush Brush => LabelColors.BrushOf(Color);
}
