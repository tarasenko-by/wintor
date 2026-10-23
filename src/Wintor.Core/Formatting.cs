using System.Globalization;

namespace Wintor.Core;

public static class Formatting
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Formats a byte count using binary multiples, e.g. 1536 → "1.5 KB".</summary>
    public static string Bytes(long bytes, IFormatProvider? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 1024)
        {
            return string.Create(culture, $"{Math.Max(bytes, 0)} B");
        }

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Three significant digits: 1.23 GB, 12.3 GB, 123 GB.
        string format = value < 10 ? "0.##" : value < 100 ? "0.#" : "0";
        return value.ToString(format, culture) + " " + Units[unit];
    }

    public static string Rate(long bytesPerSecond, IFormatProvider? culture = null) =>
        Bytes(bytesPerSecond, culture) + "/s";

    /// <summary>Compact duration: "45s", "12m", "3h 05m", "2d 4h".</summary>
    public static string Duration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m";
        }

        return $"{Math.Max((int)duration.TotalSeconds, 0)}s";
    }
}
