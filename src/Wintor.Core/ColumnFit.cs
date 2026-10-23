namespace Wintor.Core;

/// <summary>Table column layout math, independent of any UI framework.</summary>
public static class ColumnFit
{
    /// <summary>
    /// Shrinks columns to fit <paramref name="available"/>, each in proportion to its width, none below its minimum:
    /// a wide column gives up more room than a narrow one. Columns that already fit keep their widths. When even the
    /// minimums do not fit, every column gets its minimum (the caller scrolls). Widths are whole pixels.
    /// </summary>
    public static double[] Shrink(IReadOnlyList<(double Width, double Min)> columns, double available)
    {
        double[] widths = columns.Select(c => Math.Max(c.Width, c.Min)).ToArray();
        if (widths.Sum() <= available)
        {
            return widths;
        }

        if (columns.Sum(c => c.Min) >= available)
        {
            return columns.Select(c => c.Min).ToArray();
        }

        // Scale the free columns; any that would drop below its minimum is pinned there and the rest scale again.
        var pinned = new bool[widths.Length];
        while (true)
        {
            double pinnedSum = 0, freeSum = 0;
            for (int i = 0; i < widths.Length; i++)
            {
                if (pinned[i])
                {
                    pinnedSum += columns[i].Min;
                }
                else
                {
                    freeSum += Math.Max(columns[i].Width, columns[i].Min);
                }
            }

            double scale = (available - pinnedSum) / freeSum;
            bool pinnedMore = false;
            for (int i = 0; i < widths.Length; i++)
            {
                if (!pinned[i] && Math.Max(columns[i].Width, columns[i].Min) * scale < columns[i].Min)
                {
                    pinned[i] = true;
                    pinnedMore = true;
                }
            }

            if (pinnedMore)
            {
                continue;
            }

            for (int i = 0; i < widths.Length; i++)
            {
                // Floor so the total never exceeds the room; the star column absorbs the few pixels left.
                widths[i] = pinned[i] ? columns[i].Min : Math.Floor(Math.Max(columns[i].Width, columns[i].Min) * scale);
            }

            return widths;
        }
    }
}
