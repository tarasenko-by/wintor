using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Wintor.App.Services;

/// <summary>
/// The colors a label can have, as categories in Outlook. These are the user's data, not UI chrome, so they are not
/// theme resources: mid-tone (Tailwind 500) colors read on both the light and the dark palette. High contrast shows
/// labels in the system text color instead (the label's name still tells them apart).
/// </summary>
public static class LabelColors
{
    public static IReadOnlyList<(string Key, Color Color)> All { get; } =
    [
        ("Red", Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)),
        ("Orange", Color.FromArgb(0xFF, 0xF9, 0x73, 0x16)),
        ("Yellow", Color.FromArgb(0xFF, 0xEA, 0xB3, 0x08)),
        ("Green", Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E)),
        ("Teal", Color.FromArgb(0xFF, 0x14, 0xB8, 0xA6)),
        ("Blue", Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)),
        ("Purple", Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6)),
        ("Pink", Color.FromArgb(0xFF, 0xEC, 0x48, 0x99)),
    ];

    /// <summary>The color a new label gets: the first one fewest labels use, so labels start out different.</summary>
    public static string Next(IEnumerable<string> used)
    {
        var counts = used.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
        return All.OrderBy(c => counts.GetValueOrDefault(c.Key)).First().Key;
    }

    public static Brush BrushOf(string? key)
    {
        if (HighContrast.IsOn)
        {
            return (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"];
        }

        Color color = All.FirstOrDefault(c => c.Key == key).Color;
        return new SolidColorBrush(color.A == 0 ? All[5].Color : color);
    }
}
