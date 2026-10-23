namespace Wintor.Core;

public static class TorrentInput
{
    /// <summary>Quick check that the text looks like a BitTorrent magnet link (the engine does the full parse).</summary>
    public static bool IsMagnetLink(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        return trimmed.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)
            && (trimmed.Contains("xt=urn:btih:", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("xt=urn:btmh:", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsTorrentFile(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
}
