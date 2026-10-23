using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Wintor.App.Services;

namespace Wintor.App.ViewModels;

/// <param name="Key">Also the name of the width property below and the "Column_…" string.</param>
/// <param name="Locked">Always shown: a torrent table without it makes no sense.</param>
public sealed record ColumnInfo(string Key, double DefaultWidth, double MinWidth, bool Locked);

/// <summary>
/// Column widths of the torrent table, shared by the header row and every data row so they line up.
/// The user picks which columns show and drags their widths (saved in <see cref="AppSettings"/>); the name takes
/// what is left. A window too narrow for the chosen columns hides optional ones for the moment, right to left.
/// When WinUI's TableView ships in a stable Windows App SDK, it can replace the header + ListView pair.
/// </summary>
public partial class TableColumns : ObservableObject
{

    /// <summary>The column-picker button at the end of the header row (and the empty cell under it).</summary>
    public const double PickerWidth = 32;

    public const double MinNameWidth = 200;

    /// <summary>Every column but the name, left to right.</summary>
    public static IReadOnlyList<ColumnInfo> All { get; } =
    [
        // Widths include the cell's own 6 px padding on each side (the grids have no column spacing).
        new(nameof(Queue), 44, 36, false),
        new(nameof(Size), 84, 64, false),
        new(nameof(Progress), 136, 88, true),
        new(nameof(Status), 112, 72, true),
        new(nameof(Down), 88, 64, false),
        new(nameof(Up), 88, 64, false),
        new(nameof(TimeLeft), 80, 56, false),
        new(nameof(Seeds), 60, 48, false),
        new(nameof(Peers), 60, 48, false),
    ];

    // Optional columns a narrow window gives up first.
    private static readonly string[] NarrowDropOrder = [nameof(Peers), nameof(Seeds), nameof(TimeLeft), nameof(Up), nameof(Size), nameof(Queue), nameof(Down)];

    private AppSettings? _settings;
    private double _lastWidth;

    public static TableColumns Current { get; } = new();

    [ObservableProperty]
    public partial GridLength Queue { get; set; } = new(44);

    [ObservableProperty]
    public partial GridLength Name { get; set; } = new(1, GridUnitType.Star);

    [ObservableProperty]
    public partial GridLength Size { get; set; } = new(84);

    [ObservableProperty]
    public partial GridLength Progress { get; set; } = new(136);

    [ObservableProperty]
    public partial GridLength Status { get; set; } = new(112);

    [ObservableProperty]
    public partial GridLength Down { get; set; } = new(88);

    [ObservableProperty]
    public partial GridLength Up { get; set; } = new(88);

    [ObservableProperty]
    public partial GridLength TimeLeft { get; set; } = new(80);

    [ObservableProperty]
    public partial GridLength Seeds { get; set; } = new(60);

    [ObservableProperty]
    public partial GridLength Peers { get; set; } = new(60);

    public void Attach(AppSettings settings) => _settings = settings;

    public static ColumnInfo Info(string key) => All.First(c => c.Key == key);

    /// <summary>Chosen by the user (locked columns always are).</summary>
    public bool IsChosen(string key) => Info(key).Locked || _settings?.HiddenColumns.Contains(key) != true;

    /// <summary>On screen right now: chosen and not squeezed out by a narrow window.</summary>
    public bool IsShownNow(string key) => key switch
    {
        nameof(Name) => true,
        nameof(Queue) => Queue.Value > 0,
        nameof(Size) => Size.Value > 0,
        nameof(Progress) => Progress.Value > 0,
        nameof(Status) => Status.Value > 0,
        nameof(Down) => Down.Value > 0,
        nameof(Up) => Up.Value > 0,
        nameof(TimeLeft) => TimeLeft.Value > 0,
        nameof(Seeds) => Seeds.Value > 0,
        nameof(Peers) => Peers.Value > 0,
        _ => false,
    };

    public double WidthOf(string key) =>
        _settings?.ColumnWidths.TryGetValue(key, out double width) == true ? width : Info(key).DefaultWidth;

    public void SetChosen(string key, bool chosen)
    {
        if (_settings is null || Info(key).Locked)
        {
            return;
        }

        _settings.HiddenColumns.Remove(key);
        if (!chosen)
        {
            _settings.HiddenColumns.Add(key);
        }

        Fit(_lastWidth);

        // A column the user just turned on must show up: give back room from widened columns, widest first.
        while (chosen && !IsShownNow(key))
        {
            ColumnInfo? widened = All
                .Where(c => c.Key != key && IsChosen(c.Key) && WidthOf(c.Key) > c.DefaultWidth)
                .OrderByDescending(c => WidthOf(c.Key) - c.DefaultWidth)
                .FirstOrDefault();
            if (widened is null)
            {
                break; // the window really is too narrow; it shows once it is wider
            }

            _settings.ColumnWidths.Remove(widened.Key);
            Fit(_lastWidth);
        }

        _settings.Save();
    }

    /// <summary>All columns left to right, the name included, as keys.</summary>
    public static IReadOnlyList<string> Order { get; } =
        [nameof(Queue), nameof(Name), nameof(Size), nameof(Progress), nameof(Status), nameof(Down), nameof(Up), nameof(TimeLeft), nameof(Seeds), nameof(Peers)];

    /// <summary>The column shown right after <paramref name="key"/>, or null for the last one.</summary>
    public string? NextShown(string key) => Order.Skip(Order.ToList().IndexOf(key) + 1).FirstOrDefault(IsShownNow);

    /// <summary>
    /// Width now, the name included (it gets what the other columns leave). Captured when a drag starts.
    /// </summary>
    public double CurrentWidth(string key) => key == nameof(Name)
        ? Math.Max(MinNameWidth, _lastWidth - (NeededNow() - MinNameWidth))
        : WidthOf(key);

    /// <summary>
    /// Drags the divider between two neighbours by <paramref name="delta"/> from where the drag started: the left one
    /// grows as much as the right one shrinks, so the divider follows the pointer and no other column moves. Both
    /// stop at their minimum. Call <see cref="SaveWidths"/> when the drag ends.
    /// </summary>
    public void ResizeBetween(string left, string right, double leftStart, double rightStart, double delta)
    {
        if (_settings is null)
        {
            return;
        }

        double leftMin = left == nameof(Name) ? MinNameWidth : Info(left).MinWidth;
        double rightMin = right == nameof(Name) ? MinNameWidth : Info(right).MinWidth;
        double d = Math.Round(Math.Clamp(delta, leftMin - leftStart, rightStart - rightMin));

        // The name has no width of its own: it takes up whatever its neighbour gives or takes.
        if (left != nameof(Name))
        {
            _settings.ColumnWidths[left] = leftStart + d;
        }

        if (right != nameof(Name))
        {
            _settings.ColumnWidths[right] = rightStart - d;
        }

        Fit(_lastWidth);
    }

    /// <summary>Width the columns on screen need, with the name at its minimum.</summary>
    private double NeededNow() => MinNameWidth + PickerWidth + All.Where(c => IsShownNow(c.Key)).Sum(c => WidthOf(c.Key));

    public void ResetWidth(string key)
    {
        if (_settings?.ColumnWidths.Remove(key) == true)
        {
            _settings.Save();
            Fit(_lastWidth);
        }
    }

    public void SaveWidths() => _settings?.Save();

    /// <summary>Back to the default columns and widths.</summary>
    public void Reset()
    {
        if (_settings is null)
        {
            return;
        }

        _settings.HiddenColumns = [nameof(TimeLeft), nameof(Seeds), nameof(Peers)];
        _settings.ColumnWidths.Clear();
        _settings.Save();
        Fit(_lastWidth);
    }

    /// <summary>Lays the chosen columns out in <paramref name="width"/> (the row's content width in DIPs).</summary>
    public void Fit(double width)
    {
        _lastWidth = width;
        var shown = All.Where(c => IsChosen(c.Key)).Select(c => c.Key).ToHashSet();

        // Exact: the columns, the name and the picker add up to the row's width, nothing in between.
        double Needed() => MinNameWidth + PickerWidth + shown.Sum(WidthOf);

        if (width > 0)
        {
            foreach (string key in NarrowDropOrder)
            {
                if (Needed() <= width)
                {
                    break;
                }

                shown.Remove(key);
            }
        }

        GridLength Length(string key) => shown.Contains(key) ? new(WidthOf(key)) : new(0);
        Queue = Length(nameof(Queue));
        Size = Length(nameof(Size));
        Progress = Length(nameof(Progress));
        Status = Length(nameof(Status));
        Down = Length(nameof(Down));
        Up = Length(nameof(Up));
        TimeLeft = Length(nameof(TimeLeft));
        Seeds = Length(nameof(Seeds));
        Peers = Length(nameof(Peers));
    }
}
