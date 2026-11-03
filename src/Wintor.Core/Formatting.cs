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

    /// <summary>
    /// A 0–1 fraction as "62.8%". Rounds down to a tenth, so "100%" only ever means all of it: 0.9996 with a few
    /// megabytes still missing reads "99.9%", not a finished-looking "100%".
    /// </summary>
    public static string Percent(double fraction, IFormatProvider? culture = null)
    {
        // The small epsilon keeps 0.628 (627.99999… thousandths in binary) at 62.8 instead of 62.7.
        double tenths = Math.Floor(Math.Clamp(fraction, 0, 1) * 1000 + 1e-6) / 10;
        return tenths.ToString("0.#", culture ?? CultureInfo.CurrentCulture) + "%";
    }

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
