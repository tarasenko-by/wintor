using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Wintor.App.ViewModels;

/// <summary>
/// Column widths of the torrent table, shared by the header row and every data row so they line up.
/// Narrow windows hide the less important columns (width 0) until the name has room. When WinUI's TableView
/// ships in a stable Windows App SDK, it can replace the header + ListView pair and take over these widths.
/// </summary>
public partial class TableColumns : ObservableObject
{
    /// <summary>Space between cells, as in the XAML grids.</summary>
    public const double Spacing = 8;

    private const double MinNameWidth = 220;

    // Fixed widths, in the order columns are given up when space runs out (pairs go together).
    private static readonly (string[] Columns, double Width)[] DropOrder =
    [
        (["Seeds", "Peers"], 48),
        (["TimeLeft"], 64),
        (["Up"], 80),
        (["Status"], 104),
        (["Size"], 76),
        (["Queue"], 32),
    ];

    public static TableColumns Current { get; } = new();

    [ObservableProperty]
    public partial GridLength Queue { get; set; } = new(32);

    [ObservableProperty]
    public partial GridLength Name { get; set; } = new(1, GridUnitType.Star);

    [ObservableProperty]
    public partial GridLength Size { get; set; } = new(76);

    [ObservableProperty]
    public partial GridLength Progress { get; set; } = new(128);

    [ObservableProperty]
    public partial GridLength Status { get; set; } = new(104);

    [ObservableProperty]
    public partial GridLength Down { get; set; } = new(80);

    [ObservableProperty]
    public partial GridLength Up { get; set; } = new(80);

    [ObservableProperty]
    public partial GridLength TimeLeft { get; set; } = new(64);

    [ObservableProperty]
    public partial GridLength Seeds { get; set; } = new(48);

    [ObservableProperty]
    public partial GridLength Peers { get; set; } = new(48);

    /// <summary>Shows as many columns as fit in <paramref name="width"/> (the row's content width in DIPs).</summary>
    public void Fit(double width)
    {
        // Progress and download speed always stay: they are what a torrent list is for.
        double needed = MinNameWidth + 128 + 80 + 2 * Spacing;
        var shown = new HashSet<string>();
        foreach (var (columns, columnWidth) in DropOrder.Reverse())
        {
            double cost = columns.Length * (columnWidth + Spacing);
            if (needed + cost > width)
            {
                break;
            }

            needed += cost;
            shown.UnionWith(columns);
        }

        GridLength Width(string column, double value) => shown.Contains(column) ? new(value) : new(0);
        Queue = Width(nameof(Queue), 32);
        Size = Width(nameof(Size), 76);
        Status = Width(nameof(Status), 104);
        Up = Width(nameof(Up), 80);
        TimeLeft = Width(nameof(TimeLeft), 64);
        Seeds = Width(nameof(Seeds), 48);
        Peers = Width(nameof(Peers), 48);
    }
}
