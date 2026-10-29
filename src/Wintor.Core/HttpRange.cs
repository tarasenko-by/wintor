namespace Wintor.Core;

/// <summary>
/// The part of a file a media player asks for in an HTTP <c>Range</c> header. Players ask for the end of a file
/// (where MP4 and MKV keep their index) and jump around while seeking, so the stream link must answer these.
/// </summary>
/// <param name="Start">First byte, inclusive.</param>
/// <param name="End">Last byte, inclusive.</param>
public readonly record struct HttpRange(long Start, long End)
{
    public long Length => End - Start + 1;

    /// <summary>
    /// Reads <c>bytes=a-b</c>, <c>bytes=a-</c> and <c>bytes=-n</c> (the last n bytes). Only the first range of a
    /// list is served; players do not ask for several. Null header: the whole file.
    /// </summary>
    /// <returns>The range clamped to the file, or null when it does not overlap the file (HTTP 416).</returns>
    public static HttpRange? Parse(string? header, long length)
    {
        if (length <= 0)
        {
            return null;
        }

        var whole = new HttpRange(0, length - 1);
        if (string.IsNullOrWhiteSpace(header))
        {
            return whole;
        }

        header = header.Trim();
        if (!header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return whole; // an unknown unit is ignored, as the RFC says
        }

        string first = header["bytes=".Length..].Split(',')[0].Trim();
        int dash = first.IndexOf('-');
        if (dash < 0)
        {
            return whole;
        }

        string from = first[..dash].Trim(), to = first[(dash + 1)..].Trim();
        if (from.Length == 0)
        {
            // Suffix: the last n bytes.
            return long.TryParse(to, out long suffix) && suffix > 0
                ? new HttpRange(Math.Max(0, length - suffix), length - 1)
                : null;
        }

        if (!long.TryParse(from, out long start) || start < 0 || start >= length)
        {
            return null;
        }

        long end = to.Length > 0 && long.TryParse(to, out long last) ? last : length - 1;
        return end < start ? null : new HttpRange(start, Math.Min(end, length - 1));
    }
}
